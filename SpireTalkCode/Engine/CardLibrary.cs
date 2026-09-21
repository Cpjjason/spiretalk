using System.Reflection;
using System.Text.Json.Nodes;

namespace SpireTalk.Engine;

/// <summary>
/// The NPC cards the engine plays. Nine ship inside the DLL (Cards/*.json, compiled from
/// SpireTalk/npcs.json by tools/build-cards.mjs); anyone can drop their own Yumina card into
/// <c>%APPDATA%\SlayTheSpire2\spiretalk\cards\</c> and it replaces the shipped one for the NPC it
/// names in <c>spiretalk.npc</c> (or, failing that, in its <c>id</c>: <c>sts2-neow</c>).
/// That folder is the whole "persona market": a card is a file, share it however you like.
/// </summary>
public static class CardLibrary
{
    public sealed record Card(string NpcKey, string Json, string Name, string Source);

    private static readonly object Gate = new();
    private static Dictionary<string, Card>? _cards;

    public static string UserCardsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SlayTheSpire2", "spiretalk", "cards");

    public static Card? For(string npcKey)
    {
        lock (Gate)
        {
            _cards ??= Load();
            return _cards.TryGetValue(npcKey, out var card) ? card : null;
        }
    }

    public static IReadOnlyCollection<Card> All
    {
        get
        {
            lock (Gate) return (_cards ??= Load()).Values;
        }
    }

    /// <summary>Forget the cache so a card dropped into the folder is picked up without a restart.</summary>
    public static void Reload()
    {
        lock (Gate) _cards = null;
    }

    private static Dictionary<string, Card> Load()
    {
        var cards = new Dictionary<string, Card>(StringComparer.OrdinalIgnoreCase);
        var asm = Assembly.GetExecutingAssembly();
        foreach (var resource in asm.GetManifestResourceNames())
        {
            if (!resource.StartsWith("SpireTalk.Cards.", StringComparison.Ordinal) || !resource.EndsWith(".json", StringComparison.Ordinal)) continue;
            using var stream = asm.GetManifestResourceStream(resource);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            Add(cards, reader.ReadToEnd(), "built-in");
        }

        try
        {
            if (Directory.Exists(UserCardsDir))
            {
                foreach (var file in Directory.EnumerateFiles(UserCardsDir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    Add(cards, File.ReadAllText(file), Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Cards] user cards folder unreadable: {ex.Message}", 1);
        }

        MainFile.Logger.Info($"[Cards] {cards.Count} card(s): " + string.Join(", ", cards.Values.Select(c => $"{c.NpcKey}<{c.Source}>")), 1);
        return cards;
    }

    private static void Add(Dictionary<string, Card> cards, string json, string source)
    {
        try
        {
            var node = JsonNode.Parse(json)?.AsObject();
            if (node == null) return;
            var key = node["spiretalk"]?["npc"]?.GetValue<string>();
            if (string.IsNullOrEmpty(key))
            {
                var id = node["id"]?.GetValue<string>() ?? "";
                if (id.StartsWith("sts2-", StringComparison.OrdinalIgnoreCase)) key = id[5..].ToUpperInvariant();
            }
            if (string.IsNullOrEmpty(key))
            {
                MainFile.Logger.Warn($"[Cards] {source}: no spiretalk.npc and no sts2-* id, skipped", 1);
                return;
            }
            if (node["entries"] is not JsonArray || node["variables"] is not JsonArray)
            {
                MainFile.Logger.Warn($"[Cards] {source}: not a Yumina card (entries/variables missing), skipped", 1);
                return;
            }
            var name = node["name"]?.GetValue<string>() ?? key;
            if (cards.TryGetValue(key, out var previous) && previous.Source != "built-in")
            {
                MainFile.Logger.Warn($"[Cards] {source} also plays {key}; keeping {previous.Source}", 1);
                return;
            }
            cards[key] = new Card(key.ToUpperInvariant(), json, name, source);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Cards] {source}: {ex.Message}", 1);
        }
    }
}
