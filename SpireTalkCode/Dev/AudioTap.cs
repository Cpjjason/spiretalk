#if SPIRETALK_DEV
using System.Text;
using System.Text.Json;
using Godot;
using HarmonyLib;

namespace SpireTalk.Dev;

/// <summary>
/// Developer-only: logs every call the game makes into its two FMOD proxy nodes (the GDScript
/// AudioManagerProxy and MusicControllerProxy, both named "Proxy") with the frame it happened on.
/// FMOD mixes outside Godot, so --write-movie records silence; replaying this log through FMOD
/// offline rebuilds the soundtrack frame-accurately. Armed with the test bridge.
/// </summary>
public static class AudioTap
{
    private static StreamWriter? _out;

    public static void Install(Harmony harmony)
    {
        var dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "spiretalk-bridge");
        var path = Path.Combine(dir, $"audio-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        _out = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        var call = AccessTools.Method(typeof(GodotObject), nameof(GodotObject.Call), new[] { typeof(StringName), typeof(Variant[]) });
        harmony.Patch(call, prefix: new HarmonyMethod(typeof(AudioTap), nameof(Prefix)));
        MainFile.Logger.Info("[AudioTap] logging FMOD proxy calls to " + path, 1);
    }

    private static void Prefix(GodotObject __instance, StringName method, Variant[] args)
    {
        if (_out == null || __instance is not Node node || node.Name != "Proxy") return;
        try
        {
            var parent = node.GetParent()?.Name.ToString() ?? "";
            var sb = new StringBuilder();
            sb.Append("{\"f\":").Append(Godot.Engine.GetFramesDrawn())
              .Append(",\"t\":").Append(Time.GetTicksMsec())
              .Append(",\"node\":").Append(JsonSerializer.Serialize(parent))
              .Append(",\"m\":").Append(JsonSerializer.Serialize(method.ToString()))
              .Append(",\"a\":[");
            for (var i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ToJson(args[i]));
            }
            sb.Append("]}");
            _out.WriteLine(sb.ToString());
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn("[AudioTap] " + ex.Message, 1);
        }
    }

    /// <summary>A one-shot the game would have played had a real mouse done what the bridge did.</summary>
    public static void Synthetic(string path)
    {
        _out?.WriteLine("{\"f\":" + Godot.Engine.GetFramesDrawn() + ",\"t\":" + Time.GetTicksMsec() +
                        ",\"node\":\"AudioManager\",\"m\":\"play_one_shot\",\"a\":[" + JsonSerializer.Serialize(path) + ",{},1.0],\"synthetic\":true}");
    }

    private static string ToJson(Variant v) => v.VariantType switch
    {
        Variant.Type.Nil => "null",
        Variant.Type.Bool => v.AsBool() ? "true" : "false",
        Variant.Type.Int => v.AsInt64().ToString(),
        Variant.Type.Float => v.AsDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
        Variant.Type.Dictionary => "{" + string.Join(",", v.AsGodotDictionary().Select(kv => JsonSerializer.Serialize(kv.Key.ToString()) + ":" + ToJson(kv.Value))) + "}",
        _ => JsonSerializer.Serialize(v.ToString()),
    };
}
#endif
