using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace SpireTalk.Npc;

public sealed class NpcDefinition
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("kind")] public string KindName { get; set; } = "ancient";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("zh")] public string ZhName { get; set; } = "";
    [JsonPropertyName("epithet")] public string Epithet { get; set; } = "";
    [JsonPropertyName("persona")] public string Persona { get; set; } = "";
    [JsonPropertyName("moves_you")] public string MovesYou { get; set; } = "";
    [JsonPropertyName("greeting")] public string Greeting { get; set; } = "";
    /// <summary>Deal templates this NPC may propose (see DealService.ParseType).</summary>
    [JsonPropertyName("deals")] public List<string> Deals { get; set; } = new();

    [JsonIgnore] public NpcKind Kind => KindName == "merchant" ? NpcKind.Merchant : NpcKind.Ancient;
    /// <summary>Localized, player-facing name (see <see cref="Loc.NpcName"/>). <see cref="Name"/> is for prompts.</summary>
    [JsonIgnore] public string DisplayName => Loc.NpcName(this);
}

/// <summary>
/// The NPC roster shipped inside the pck (res://SpireTalk/npcs.json). The direct-LLM backend
/// builds its prompt from this; the Yumina worlds are generated from the same file, so both
/// backends speak with one voice.
/// </summary>
public static class NpcCatalog
{
    private sealed class File
    {
        [JsonPropertyName("shared_rules")] public string SharedRules { get; set; } = "";
        [JsonPropertyName("affection_rules")] public string AffectionRules { get; set; } = "";
        [JsonPropertyName("npcs")] public List<NpcDefinition> Npcs { get; set; } = new();
    }

    private static File? _file;
    private static readonly Dictionary<string, NpcDefinition> ByKey = new(StringComparer.OrdinalIgnoreCase);

    public static string SharedRules => Load().SharedRules;
    public static string AffectionRules => Load().AffectionRules;

    public static NpcDefinition? Get(string key)
    {
        Load();
        return ByKey.TryGetValue(key, out var def) ? def : null;
    }

    public static IEnumerable<NpcDefinition> All => Load().Npcs;

    private static File Load()
    {
        if (_file != null) return _file;
        try
        {
            using var fa = Godot.FileAccess.Open("res://SpireTalk/npcs.json", Godot.FileAccess.ModeFlags.Read);
            var text = fa?.GetAsText() ?? "";
            _file = JsonSerializer.Deserialize<File>(text) ?? new File();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[Catalog] npcs.json failed to load: {ex.Message}", 1);
            _file = new File();
        }
        ByKey.Clear();
        foreach (var n in _file.Npcs) ByKey[n.Key] = n;
        return _file;
    }
}
