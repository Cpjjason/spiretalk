using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SpireTalk.Npc;
using SpireTalk.Store;

namespace SpireTalk.Backend;

/// <summary>
/// The mod's small service on feedback.yumina.io (a Cloudflare worker shared with our other game
/// mod; not yumina.io's own servers): is there a newer release, one-click update, the feedback the
/// player writes us, and anonymous per-visit counts. github.com is not reachable everywhere, so the
/// release manifest and the zip come through the worker, which fetches them from GitHub.
///
/// Updating: the zip's size and sha256 are checked against the manifest, mods/SpireTalk is unpacked
/// beside the installed one, and a small PowerShell job waits for the game to close and copies it
/// over (the game holds SpireTalk.pck open, so it can't be replaced while running). The next start
/// runs the new version.
/// </summary>
public static class ModService
{
    public const string Repo = "Cpjjason/spiretalk";
    public const string ReleasesPage = "https://github.com/" + Repo + "/releases/latest";
    public static string Relay = "https://feedback.yumina.io";

    private static readonly HttpClient Http = MakeClient(15);

    private static HttpClient MakeClient(int seconds)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(seconds) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SpireTalk/" + MainFile.Version);
        return http;
    }

    /// <summary>Raised when the update state moves (found, downloading, staged, failed).</summary>
    public static event Action? Changed;

    /// <summary>Listeners touch nodes, so they always run on the main thread, a frame later.</summary>
    private static void Notify()
    {
        Godot.Callable.From(() =>
        {
            try { Changed?.Invoke(); }
            catch (Exception ex) { MainFile.Logger.Warn($"[Update] listener failed: {ex.Message}", 1); }
        }).CallDeferred();
    }

    public static void Start()
    {
        _ = CheckAsync();
    }

    // ── Is there a newer release ────────────────────────────────────────

    public static string? Latest { get; private set; }
    public static string? Notes { get; private set; }
    private static JsonObject? _asset;

    public static bool Newer => Latest != null && Compare(Latest, MainFile.Version) > 0;

    public enum UpdateState { Idle, Working, Staged, Failed }

    public static UpdateState State { get; private set; } = UpdateState.Idle;
    public static float Progress { get; private set; }
    public static string? Error { get; private set; }

    /// <summary>The folder the running SpireTalk.dll was loaded from.</summary>
    public static string ModDir => Path.GetDirectoryName(typeof(MainFile).Assembly.Location) ?? "";

    /// <summary>A Workshop copy is Steam's to update; one-click only replaces a copy in the game's mods folder.</summary>
    public static bool CanUpdate => Newer && _asset != null && !ModDir.Replace('\\', '/').Contains("/workshop/", StringComparison.OrdinalIgnoreCase);

    private static async Task CheckAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4));
            var text = await Http.GetStringAsync(Relay + "/v1/spiretalk/release");
            var m = JsonNode.Parse(text);
            Latest = m?["version"]?.GetValue<string>();
            Notes = m?["notes"]?.GetValue<string>();
            _asset = (m?["assets"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(a => a["kind"]?.GetValue<string>() == "main");
            if (_asset != null && _asset["tag"] == null) _asset["tag"] = m?["tag"]?.GetValue<string>() ?? "v" + Latest;
            MainFile.Logger.Info($"[Update] latest {Latest ?? "?"} (running {MainFile.Version})", 1);
            Notify();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Update] check skipped: {ex.Message}", 1);
        }
    }

    public static int Compare(string a, string b)
    {
        int[] Parts(string v) => v.TrimStart('v', 'V').Split('.', '-').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var x = Parts(a);
        var y = Parts(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var d = (i < x.Length ? x[i] : 0) - (i < y.Length ? y[i] : 0);
            if (d != 0) return d;
        }
        return 0;
    }

    // ── One-click update ─────────────────────────────────────────────────

    public static async void Install()
    {
        if (!CanUpdate || State == UpdateState.Working || _asset == null) return;
        State = UpdateState.Working;
        Progress = 0;
        Error = null;
        Notify();
        var name = _asset["name"]!.GetValue<string>();
        var tmp = Path.Combine(Path.GetTempPath(), "spiretalk-" + name);
        var staging = ModDir + ".update";
        try
        {
            long size = 0;
            try { size = _asset["size"]?.GetValue<long>() ?? 0; } catch { /* unknown */ }
            await Fetch($"{Relay}/v1/spiretalk/download/{_asset["tag"]!.GetValue<string>()}/{name}", tmp, size);
            var want = _asset["sha256"]?.GetValue<string>() ?? "";
            if (!string.Equals(await Task.Run(() => Sha256(tmp)), want, StringComparison.OrdinalIgnoreCase)) throw new Exception(Loc.Ui("UpdBroken"));
            var files = await Task.Run(() => Unpack(tmp, staging));
            if (files == 0) throw new Exception(Loc.Ui("UpdBroken"));
            ApplyAfterExit(staging, ModDir);
            State = UpdateState.Staged;
            MainFile.Logger.Info($"[Update] {Latest} staged in {staging} ({files} files); applied when the game closes", 1);
        }
        catch (Exception ex)
        {
            State = UpdateState.Failed;
            Error = ex.Message.Length > 80 ? ex.Message[..80] + "…" : ex.Message;
            MainFile.Logger.Warn($"[Update] failed: {ex}", 1);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* temp */ }
        }
        Notify();
    }

    private static async Task Fetch(string url, string to, long size)
    {
        using var http = MakeClient(600);
        using var res = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();
        var total = size > 0 ? size : res.Content.Headers.ContentLength ?? 0;
        await using var src = await res.Content.ReadAsStreamAsync();
        await using var dst = File.Create(to);
        var buf = new byte[81920];
        long got = 0;
        int n;
        var lastNotify = DateTime.UtcNow;
        while ((n = await src.ReadAsync(buf)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n));
            got += n;
            if (total > 0 && DateTime.UtcNow - lastNotify > TimeSpan.FromMilliseconds(200))
            {
                lastNotify = DateTime.UtcNow;
                Progress = Math.Min(1f, (float)got / total);
                Notify();
            }
        }
        if (size > 0 && got != size) throw new Exception(Loc.Ui("UpdBroken"));
    }

    private static string Sha256(string path)
    {
        using var f = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
    }

    /// <summary>The zip's mods/SpireTalk/* into the staging folder; BaseLib and the rest of the zip stay out.</summary>
    private static int Unpack(string zip, string staging)
    {
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        Directory.CreateDirectory(staging);
        var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
        var n = 0;
        using var archive = ZipFile.OpenRead(zip);
        foreach (var e in archive.Entries)
        {
            var path = e.FullName.Replace('\\', '/');
            const string prefix = "mods/SpireTalk/";
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || path.EndsWith('/')) continue;
            var target = Path.GetFullPath(Path.Combine(staging, path[prefix.Length..]));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue; // no ../ out of the folder
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            e.ExtractToFile(target, true);
            n++;
        }
        return n;
    }

    /// <summary>A hidden PowerShell job: wait for this game process to end, copy the staged files over, clean up.</summary>
    private static void ApplyAfterExit(string staging, string modDir)
    {
        var script = Path.Combine(Path.GetTempPath(), "spiretalk-apply-update.ps1");
        File.WriteAllText(script, """
            param([int]$GamePid, [string]$From, [string]$To)
            try { Wait-Process -Id $GamePid -ErrorAction SilentlyContinue } catch {}
            Start-Sleep -Seconds 2
            for ($i = 0; $i -lt 30; $i++) {
              try { Copy-Item -Path (Join-Path $From '*') -Destination $To -Recurse -Force -ErrorAction Stop; Remove-Item -LiteralPath $From -Recurse -Force; break }
              catch { Start-Sleep -Seconds 2 }
            }
            """, new UTF8Encoding(true));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden", "-File", script,
                     "-GamePid", Environment.ProcessId.ToString(), "-From", staging, "-To", modDir })
        {
            psi.ArgumentList.Add(a);
        }
        Process.Start(psi);
    }

    // ── Feedback ─────────────────────────────────────────────────────────

    /// <summary>What the player wrote, and (if they ticked it) their most recent conversation. Never a key or a token.</summary>
    public static async Task SendFeedbackAsync(string message, bool attachConversation)
    {
        var body = new JsonObject
        {
            ["client"] = YuminaAccount.ClientId,
            ["message"] = message,
            ["version"] = MainFile.Version,
            ["language"] = Loc.ReplyLanguageCode(),
            ["source"] = SpireTalkConfig.UseAi ? SpireTalkConfig.Source.ToString() : "Script",
            ["model"] = SpireTalkConfig.UseAi ? OpenAiClient.Model : "",
        };
        if (YuminaAccount.Linked && YuminaAccount.Current is { } me) body["account"] = me.Username;
        if (attachConversation && LastConversation() is { } convo) body["conversation"] = convo;
        using var res = await Http.PostAsync(Relay + "/v1/feedback", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        if (res.IsSuccessStatusCode) return;
        string? why = null;
        try { why = JsonNode.Parse(await res.Content.ReadAsStringAsync())?["error"]?["message"]?.GetValue<string>(); } catch { /* not JSON */ }
        throw new BackendException(why ?? "HTTP " + (int)res.StatusCode, (int)res.StatusCode);
    }

    /// <summary>The visit most recently talked in: who, when, and its lines.</summary>
    private static JsonObject? LastConversation()
    {
        NpcDefinition? npc = null;
        TalkStore.NpcRecord? last = null;
        foreach (var n in NpcCatalog.All)
        {
            var r = TalkStore.Instance.Get(n.Key);
            if (r.History.Count == 0) continue;
            if (last == null || string.CompareOrdinal(r.LastTalkUtc, last.LastTalkUtc) > 0) { last = r; npc = n; }
        }
        if (last == null || npc == null) return null;
        var visit = last.History[^1].Visit;
        var lines = new JsonArray();
        foreach (var h in last.History.Where(h => visit == null || h.Visit == visit).TakeLast(40))
        {
            lines.Add(new JsonObject { ["who"] = h.Role == "player" ? "player" : "npc", ["text"] = h.Text });
        }
        return new JsonObject { ["with"] = Loc.NpcName(npc), ["when"] = last.LastTalkUtc ?? "", ["lines"] = lines };
    }

    // ── Anonymous counts: one per visit ─────────────────────────────────

    private static VisitContext? _visit;
    private static int _answered, _errors;
    private static string? _err;
    private static bool _scripted;

    /// <summary>A reply (or a failure) on this visit. A new visit sends the previous one's count.</summary>
    public static void Count(VisitContext ctx, bool scripted, Exception? error)
    {
        if (!ReferenceEquals(ctx, _visit))
        {
            FlushVisit();
            _visit = ctx;
            _answered = _errors = 0;
            _err = null;
        }
        _scripted = scripted;
        if (error == null)
        {
            _answered++;
            return;
        }
        if (error is OperationCanceledException) return; // left the room, or stopped waiting
        _errors++;
        _err ??= error is BackendException be ? $"{be.StatusCode?.ToString() ?? "-"}:{be.Code ?? "-"}" : error.GetType().Name;
    }

    /// <summary>The visit is over (room left, panel closed): send its count, if any.</summary>
    public static void FlushVisit()
    {
        var ctx = _visit;
        _visit = null;
        if (ctx == null || !SpireTalkConfig.SendStats) return;
#if SPIRETALK_DEV
        if (Dev.TestBridge.ShouldInstall) return; // test runs count nothing
#endif
        try
        {
            if (string.IsNullOrEmpty(SpireTalkConfig.InstallId))
            {
                SpireTalkConfig.InstallId = Guid.NewGuid().ToString("N")[..16];
                SpireTalkConfig.Instance?.SaveDebounced(500);
            }
            var body = new JsonObject
            {
                ["client"] = YuminaAccount.ClientId,
                ["iid"] = SpireTalkConfig.InstallId,
                ["v"] = MainFile.Version,
                ["lang"] = Loc.ReplyLanguageCode(),
                ["src"] = _scripted ? "Script" : SpireTalkConfig.Source.ToString(),
                // A model name typed for a custom endpoint or a local Ollama can say something about the player; leave it out.
                ["model"] = _scripted || SpireTalkConfig.Source is ApiSource.Custom or ApiSource.Ollama ? "" : OpenAiClient.Model,
                ["npc"] = ctx.NpcKey,
                ["name"] = ctx.Npc.Name,
                ["via"] = ctx.Kind.ToString().ToLowerInvariant(),
                ["n"] = ctx.Talks,
                ["used"] = ConversationRules.LinesPerVisit - ctx.LinesLeft,
                ["max"] = ConversationRules.LinesPerVisit,
                ["answered"] = _answered,
                ["errors"] = _errors,
                ["err"] = _err,
                ["fallback"] = _scripted,
                ["aff"] = ctx.Affection,
            };
            var text = body.ToJsonString();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var res = await Http.PostAsync(Relay + "/v1/event", new StringContent(text, Encoding.UTF8, "application/json"));
                }
                catch { /* a count that doesn't arrive is not worth bothering anyone about */ }
            });
        }
        catch (Exception ex)
        {
            MainFile.Logger.Info($"[Stats] skipped: {ex.Message}", 1);
        }
    }
}
