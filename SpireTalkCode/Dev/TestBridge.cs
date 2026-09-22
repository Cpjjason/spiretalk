#if SPIRETALK_DEV
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Runs;

namespace SpireTalk.Dev;

/// <summary>
/// Developer-only automation bridge. Enabled ONLY when the file
/// %APPDATA%/SlayTheSpire2/spiretalk-bridge/enabled exists; players never see it.
///
/// An outside process drops cmd.json into that folder; the bridge executes it on the main
/// thread and writes res.json. Input is synthesised through Godot's own Input singleton, so
/// the game needs neither focus nor a real mouse, and screenshots come from the viewport
/// texture, so the window may sit anywhere (even behind another game).
///
/// ops: shot{path,scale} · click{x,y} (window px) · clicktext{text} · clickname{name} ·
///      move{x,y} · key{key} · type{text} · console{cmd} · tree{path} · state · wait{ms}
/// </summary>
public sealed partial class TestBridge : Node
{
    private static readonly string Dir = Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "spiretalk-bridge");
    private static readonly string CmdPath = Path.Combine(Dir, "cmd.json");
    private static readonly string ResPath = Path.Combine(Dir, "res.json");

    private readonly Queue<Action> _steps = new();
    private int _frameWait;
    private double _poll;

    public static bool ShouldInstall => File.Exists(Path.Combine(Dir, "enabled"));

    public static void Install()
    {
        try
        {
            if (!ShouldInstall) return;
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            if (tree == null) return;
            var node = new TestBridge { Name = "SpireTalkTestBridge", ProcessMode = ProcessModeEnum.Always };
            tree.Root.CallDeferred(Node.MethodName.AddChild, node);
            MainFile.Logger.Info("[Bridge] test bridge armed at " + Dir, 1);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn("[Bridge] install failed: " + ex.Message, 1);
        }
    }

    public override void _Process(double delta)
    {
        if (_frameWait > 0)
        {
            _frameWait--;
            return;
        }
        if (_steps.Count > 0)
        {
            var step = _steps.Dequeue();
            try
            {
                step();
            }
            catch (Exception ex)
            {
                WriteResult(false, ex.GetType().Name + ": " + ex.Message);
                _steps.Clear();
            }
            return;
        }
        _poll += delta;
        if (_poll < 0.1) return;
        _poll = 0;
        if (!File.Exists(CmdPath)) return;

        JsonObject? cmd;
        try
        {
            var text = File.ReadAllText(CmdPath);
            cmd = JsonNode.Parse(text) as JsonObject;
            File.Delete(CmdPath);
        }
        catch
        {
            return; // writer may still be mid-write; try next tick
        }
        if (cmd == null) return;
        try
        {
            Dispatch(cmd);
        }
        catch (Exception ex)
        {
            WriteResult(false, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void Dispatch(JsonObject cmd)
    {
        var op = cmd["op"]?.GetValue<string>() ?? "";
        switch (op)
        {
            case "shot":
                Screenshot(cmd["path"]?.GetValue<string>() ?? Path.Combine(Dir, "shot.png"), cmd["scale"]?.GetValue<double?>() ?? 1.0);
                break;
            case "click":
                ClickAt(TextureToWindow(new Vector2(cmd["x"]!.GetValue<float>(), cmd["y"]!.GetValue<float>())), cmd["right"]?.GetValue<bool?>() ?? false);
                break;
            case "move":
                Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(cmd["x"]!.GetValue<float>(), cmd["y"]!.GetValue<float>()), GlobalPosition = new Vector2(cmd["x"]!.GetValue<float>(), cmd["y"]!.GetValue<float>()) });
                WriteResult(true, "moved");
                break;
            case "scroll":
            {
                // Mouse wheel at a texture-space point; n < 0 scrolls down (WheelDown), n > 0 up.
                var at = TextureToWindow(new Vector2(cmd["x"]?.GetValue<float?>() ?? 1280f, cmd["y"]?.GetValue<float?>() ?? 720f));
                var n = cmd["n"]?.GetValue<int?>() ?? -3;
                var button = n < 0 ? MouseButton.WheelDown : MouseButton.WheelUp;
                for (var i = 0; i < Math.Abs(n); i++)
                {
                    Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = true, Factor = 1f });
                    Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = false, Factor = 1f });
                }
                WriteResult(true, $"scrolled {n}");
                break;
            }
            case "clicktext":
            case "clickname":
            {
                var needle = cmd[op == "clicktext" ? "text" : "name"]?.GetValue<string>() ?? "";
                var index = cmd["index"]?.GetValue<int?>() ?? 0;
                var target = FindControl(needle, op == "clickname", index);
                if (target == null)
                {
                    WriteResult(false, "no visible control matching '" + needle + "'");
                    break;
                }
                Activate(target);
                break;
            }
            case "key":
                PressKey(cmd["key"]?.GetValue<string>() ?? "Enter");
                break;
            case "type":
                TypeText(cmd["text"]?.GetValue<string>() ?? "");
                break;
            case "console":
                RunConsole(cmd["cmd"]?.GetValue<string>() ?? "");
                break;
            case "tree":
                DumpTree(cmd["path"]?.GetValue<string>() ?? Path.Combine(Dir, "tree.txt"), cmd["all"]?.GetValue<bool?>() ?? false);
                break;
            case "state":
                WriteResult(true, State());
                break;
            case "ls":
            {
                // List a res:// (pck) directory, one level; used to find the game's own UI art.
                var dir = cmd["path"]?.GetValue<string>() ?? "res://";
                var da = DirAccess.Open(dir);
                if (da == null) { WriteResult(false, "cannot open " + dir); break; }
                var sb = new StringBuilder();
                foreach (var d in da.GetDirectories()) sb.Append(d).AppendLine("/");
                foreach (var f in da.GetFiles()) sb.AppendLine(f);
                WriteResult(true, sb.ToString());
                break;
            }
            case "tex":
            {
                // Save a texture resource as PNG so it can be looked at outside the game.
                var res = cmd["path"]?.GetValue<string>() ?? "";
                var outPath = cmd["out"]?.GetValue<string>() ?? Path.Combine(Dir, "tex.png");
                var t = ResourceLoader.Load<Texture2D>(res);
                if (t == null) { WriteResult(false, "not a texture: " + res); break; }
                var img = t.GetImage();
                if (img.IsCompressed()) img.Decompress();
                img.SavePng(outPath);
                WriteResult(true, $"{t.GetWidth()}x{t.GetHeight()} {outPath}");
                break;
            }
            case "inspect":
            {
                // Dump every descendant of a node with the visual facts needed to imitate it:
                // type, rect, texture/stylebox/font resource paths, text.
                var needle = cmd["name"]?.GetValue<string>() ?? "";
                var target = FindNode(GetTree().Root, needle);
                if (target == null) { WriteResult(false, "no node named " + needle); break; }
                var sb = new StringBuilder();
                Inspect(target, 0, sb);
                var outPath = cmd["path"]?.GetValue<string>() ?? Path.Combine(Dir, "inspect.txt");
                File.WriteAllText(outPath, sb.ToString());
                WriteResult(true, outPath);
                break;
            }
            case "find":
            {
                var needle = cmd["name"]?.GetValue<string>() ?? "";
                var sb = new StringBuilder();
                var n = 0;
                void Visit(Node node, int depth)
                {
                    if (n > 200) return;
                    var type = node.GetType().Name;
                    if (node.Name.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase) || type.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(type).Append(" \"").Append(node.Name).Append("\" ").Append(node.GetPath()).AppendLine();
                        n++;
                    }
                    foreach (var c in node.GetChildren()) Visit(c, depth + 1);
                }
                Visit(GetTree().Root, 0);
                WriteResult(true, sb.Length == 0 ? "no match" : sb.ToString());
                break;
            }
            case "patches":
            {
                var sb = new StringBuilder();
                foreach (var m in HarmonyLib.Harmony.GetAllPatchedMethods())
                {
                    var info = HarmonyLib.Harmony.GetPatchInfo(m);
                    var owners = string.Join(",", info.Owners);
                    var all = cmd["all"]?.GetValue<bool?>() ?? false;
                    if (all || owners.Contains("spiretalk", StringComparison.OrdinalIgnoreCase))
                        sb.Append(m.DeclaringType?.Name).Append('.').Append(m.Name).Append(" [").Append(owners).AppendLine("]");
                }
                WriteResult(true, sb.Length == 0 ? "no spiretalk patches" : sb.ToString());
                break;
            }
            case "affection":
            {
                var key = cmd["key"]?.GetValue<string>() ?? "NEOW";
                var value = cmd["value"]?.GetValue<int?>() ?? 0;
                var (current, _) = Affection.AffectionService.Read(key);
                var kind = key == "MERCHANT" ? Npc.NpcKind.Merchant : Npc.NpcKind.Ancient;
                var after = Affection.AffectionService.Apply(key, kind, value - current);
                WriteResult(true, $"{key}: {current} -> {after} (source={(Affection.AffectionService.UsingAncientAffection ? "AA" : "own")})");
                break;
            }
            case "relics":
            {
                var player = Merchant.MerchantTalk.ResolveLocalPlayer();
                if (player == null) { WriteResult(false, "no player"); break; }
                var lines = player.Relics.Select(r =>
                {
                    string title, desc;
                    try { title = r.Title.GetFormattedText(); } catch (Exception ex) { title = "title? " + ex.Message; }
                    try { desc = r.DynamicDescription.GetFormattedText(); } catch (Exception ex) { desc = "desc? " + ex.Message; }
                    var extra = r is Keepsakes.KeepsakeRelic k ? $" [level {k.Level}, engravings {k.Engravings}]" : "";
                    return $"{r.Id.Entry}: {title} — {desc}{extra}";
                });
                WriteResult(true, string.Join(" | ", lines));
                break;
            }
            case "quit":
                WriteResult(true, "quitting");
                _steps.Enqueue(() => GetTree().Quit());
                break;
            case "wait":
            {
                var frames = Math.Max(1, (int)((cmd["ms"]?.GetValue<int?>() ?? 500) / 16.0));
                _frameWait = frames;
                _steps.Enqueue(() => WriteResult(true, "waited"));
                break;
            }
            default:
                WriteResult(false, "unknown op " + op);
                break;
        }
    }

    /// <summary>Screenshot pixel → window pixel (the texture is the 16:9 canvas; the window may be letterboxed).</summary>
    private Vector2 TextureToWindow(Vector2 tex)
    {
        var vp = GetViewport();
        var texSize = vp.GetTexture().GetSize();
        var canvasSize = vp.GetVisibleRect().Size;
        var canvas = new Vector2(tex.X * canvasSize.X / Math.Max(1, texSize.X), tex.Y * canvasSize.Y / Math.Max(1, texSize.Y));
        return vp.GetScreenTransform() * canvas;
    }

    /// <summary>
    /// Press a control the way the game's own AutoSlay harness does: NClickableControl.ForceClick()
    /// bypasses hover/focus, Godot buttons get their pressed signal, anything else a synthesised click.
    /// </summary>
    private void Activate(Control target)
    {
        var clickable = FindClickableAncestor(target);
        if (clickable != null)
        {
            var force = clickable.GetType().GetMethod("ForceClick", Type.EmptyTypes);
            if (force != null)
            {
                force.Invoke(clickable, null);
                WriteResult(true, $"ForceClick {clickable.GetType().Name} '{clickable.Name}'");
                return;
            }
        }
        if (clickable is BaseButton bb)
        {
            bb.EmitSignal(BaseButton.SignalName.Pressed);
            WriteResult(true, $"pressed {bb.GetType().Name} '{bb.Name}'");
            return;
        }
        var rect = target.GetGlobalRect();
        var centre = rect.Position + rect.Size / 2f;
        var window = target.GetViewport().GetScreenTransform() * centre;
        ClickAt(window, false, $"{target.GetType().Name} '{target.Name}' at {window.X:0},{window.Y:0}");
    }

    private static Control? FindClickableAncestor(Control c)
    {
        Node? n = c;
        for (var i = 0; i < 6 && n != null; i++, n = n.GetParent())
        {
            if (n is Control ctl)
            {
                var name = ctl.GetType().Name;
                if (ctl is BaseButton || name.Contains("Button", StringComparison.Ordinal) || name.Contains("Clickable", StringComparison.Ordinal))
                    return ctl;
            }
        }
        return null;
    }

    // ── ops ──────────────────────────────────────────────────────────────

    private void Screenshot(string path, double scale)
    {
        var img = GetViewport().GetTexture().GetImage();
        if (scale > 0 && Math.Abs(scale - 1.0) > 0.01)
        {
            img.Resize((int)(img.GetWidth() * scale), (int)(img.GetHeight() * scale), Image.Interpolation.Lanczos);
        }
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var err = img.SavePng(path);
        WriteResult(err == Error.Ok, $"{err} {img.GetWidth()}x{img.GetHeight()} {path}");
    }

    private void ClickAt(Vector2 p, bool right, string? note = null)
    {
        var button = right ? MouseButton.Right : MouseButton.Left;
        _steps.Enqueue(() => { Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p }); _frameWait = 3; });
        _steps.Enqueue(() => { Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = p, GlobalPosition = p }); _frameWait = 3; });
        _steps.Enqueue(() => { Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = p, GlobalPosition = p }); _frameWait = 2; });
        _steps.Enqueue(() => WriteResult(true, note ?? $"clicked {p.X:0},{p.Y:0}"));
    }

    private void PressKey(string name)
    {
        if (!Enum.TryParse<Key>(name, true, out var key))
        {
            WriteResult(false, "unknown key " + name);
            return;
        }
        _steps.Enqueue(() => { Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }); _frameWait = 2; });
        _steps.Enqueue(() => { Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }); _frameWait = 2; });
        _steps.Enqueue(() => WriteResult(true, "key " + key));
    }

    private void TypeText(string text)
    {
        foreach (var ch in text)
        {
            var c = ch;
            _steps.Enqueue(() =>
            {
                Input.ParseInputEvent(new InputEventKey { Unicode = c, Pressed = true, Keycode = Key.None });
                Input.ParseInputEvent(new InputEventKey { Unicode = c, Pressed = false, Keycode = Key.None });
                _frameWait = 1;
            });
        }
        _steps.Enqueue(() => WriteResult(true, "typed " + text.Length + " chars"));
    }

    private void RunConsole(string command)
    {
        var console = NDevConsole.Instance;
        var field = typeof(NDevConsole).GetField("_devConsole", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("_devConsole field missing");
        var dev = field.GetValue(console) ?? throw new InvalidOperationException("console not created");
        var method = dev.GetType().GetMethod("ProcessCommand", new[] { typeof(string) })
            ?? throw new InvalidOperationException("ProcessCommand missing");
        var result = method.Invoke(dev, new object[] { command });
        var success = result?.GetType().GetField("success")?.GetValue(result) as bool? ?? true;
        var message = result?.GetType().GetField("message")?.GetValue(result)?.ToString() ?? result?.ToString() ?? "";
        WriteResult(success, message);
    }

    private static Node? FindNode(Node root, string name)
    {
        if (root.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
        foreach (var c in root.GetChildren())
        {
            var hit = FindNode(c, name);
            if (hit != null) return hit;
        }
        return null;
    }

    private static string ResName(Resource? r) => r == null ? "-" : (string.IsNullOrEmpty(r.ResourcePath) ? r.GetType().Name + "(inline)" : r.ResourcePath);

    private static void Inspect(Node node, int depth, StringBuilder sb)
    {
        if (depth > 12) return;
        sb.Append(' ', depth * 2).Append(node.GetType().Name).Append(" \"").Append(node.Name).Append('"');
        if (node is Control c)
        {
            sb.Append($" pos={c.GlobalPosition.X:0},{c.GlobalPosition.Y:0} size={c.Size.X:0}x{c.Size.Y:0} vis={c.IsVisibleInTree()}");
            if (c.Theme != null) sb.Append(" theme=").Append(ResName(c.Theme));
            switch (c)
            {
                case TextureRect tr: sb.Append(" tex=").Append(ResName(tr.Texture)); break;
                case NinePatchRect np: sb.Append(" ninepatch=").Append(ResName(np.Texture)).Append($" margins={np.PatchMarginLeft},{np.PatchMarginTop},{np.PatchMarginRight},{np.PatchMarginBottom}"); break;
                case TextureButton tb: sb.Append(" normal=").Append(ResName(tb.TextureNormal)).Append(" hover=").Append(ResName(tb.TextureHover)); break;
                case Label l: sb.Append(" text=\"").Append(l.Text.Replace("\n", "\\n")).Append('"').Append(" font=").Append(ResName(l.GetThemeFont("font"))).Append(" size=").Append(l.GetThemeFontSize("font_size")); break;
                case RichTextLabel rl: sb.Append(" text=\"").Append(rl.Text.Replace("\n", "\\n")).Append('"').Append(" font=").Append(ResName(rl.GetThemeFont("normal_font"))).Append(" size=").Append(rl.GetThemeFontSize("normal_font_size")); break;
                case Button b: sb.Append(" text=\"").Append(b.Text).Append('"').Append(" normal=").Append(ResName(b.GetThemeStylebox("normal"))); break;
                case PanelContainer pc: sb.Append(" panel=").Append(ResName(pc.GetThemeStylebox("panel"))); break;
                case Panel pn: sb.Append(" panel=").Append(ResName(pn.GetThemeStylebox("panel"))); break;
            }
            if (c.Modulate != Colors.White) sb.Append($" modulate={c.Modulate.ToHtml()}");
        }
        else if (node is Sprite2D sp)
        {
            sb.Append(" sprite=").Append(ResName(sp.Texture)).Append($" pos={sp.GlobalPosition.X:0},{sp.GlobalPosition.Y:0}");
        }
        sb.AppendLine();
        foreach (var child in node.GetChildren()) Inspect(child, depth + 1, sb);
    }

    private void DumpTree(string path, bool all)
    {
        var sb = new StringBuilder();
        var root = GetTree().Root;
        Walk(root, 0, sb, all, 0);
        File.WriteAllText(path, sb.ToString());
        WriteResult(true, $"{sb.Length} chars → {path}");
    }

    private static int Walk(Node node, int depth, StringBuilder sb, bool all, int count)
    {
        if (count > 4000) return count;
        if (node is Control c)
        {
            var visible = c.IsVisibleInTree();
            if (visible || all)
            {
                var rect = c.GetGlobalRect();
                var text = TextOf(c);
                if (!string.IsNullOrEmpty(text) || c is BaseButton || c.GetType().Name.StartsWith("NButton") || c.GetType().Name.Contains("Clickable") || all)
                {
                    sb.Append(new string(' ', Math.Min(depth, 20)))
                      .Append(c.GetType().Name).Append(' ').Append('"').Append(c.Name).Append('"')
                      .Append(visible ? "" : " [hidden]")
                      .Append(" @").Append((int)rect.Position.X).Append(',').Append((int)rect.Position.Y)
                      .Append(' ').Append((int)rect.Size.X).Append('x').Append((int)rect.Size.Y);
                    if (!string.IsNullOrEmpty(text)) sb.Append(" text=").Append(JsonSerializer.Serialize(text.Length > 80 ? text[..80] : text));
                    sb.AppendLine();
                    count++;
                }
            }
            if (!visible && !all) return count;
        }
        foreach (var child in node.GetChildren())
        {
            count = Walk(child, depth + 1, sb, all, count);
        }
        return count;
    }

    private static string? TextOf(Control c)
    {
        try
        {
            var v = c.Get("text");
            if (v.VariantType == Variant.Type.String)
            {
                var s = v.AsString();
                return string.IsNullOrWhiteSpace(s) ? null : s.Replace("\n", " ").Trim();
            }
        }
        catch { /* no text property */ }
        return null;
    }

    private Control? FindControl(string needle, bool byName, int index)
    {
        var matches = new List<Control>();
        void Visit(Node n)
        {
            if (n is Control c && c.IsVisibleInTree())
            {
                var hay = byName ? c.Name.ToString() : TextOf(c) ?? "";
                if (!string.IsNullOrEmpty(hay) && hay.Contains(needle, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(c);
                }
            }
            if (n is Control cc && !cc.IsVisibleInTree()) return;
            foreach (var child in n.GetChildren()) Visit(child);
        }
        Visit(GetTree().Root);
        return index < matches.Count ? matches[index] : null;
    }

    private string State()
    {
        var sb = new StringBuilder();
        var size = DisplayServer.WindowGetSize();
        sb.Append("window=").Append(size.X).Append('x').Append(size.Y);
        try
        {
            sb.Append(" run=").Append(RunManager.Instance.IsInProgress);
            var state = RunManager.Instance.DebugOnlyGetState();
            if (state != null)
            {
                sb.Append(" room=").Append(state.CurrentRoom?.GetType().Name ?? "-");
                sb.Append(" act=").Append(state.CurrentActIndex + 1);
            }
        }
        catch (Exception ex)
        {
            sb.Append(" run=? (").Append(ex.Message).Append(')');
        }
        sb.Append(" backend=").Append(MainFile.Backend.DisplayName).Append(" configured=").Append(MainFile.Backend.IsConfigured);
        return sb.ToString();
    }

    private static void WriteResult(bool ok, string info)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var obj = new JsonObject { ["ok"] = ok, ["info"] = info, ["at"] = DateTime.UtcNow.ToString("o") };
            File.WriteAllText(ResPath, obj.ToJsonString());
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn("[Bridge] result write failed: " + ex.Message, 1);
        }
    }
}
#endif
