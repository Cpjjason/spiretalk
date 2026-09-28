using System.Text.Json;
using System.Text.Json.Serialization;
using SpireTalk.Npc;

namespace SpireTalk.Backend;

/// <summary>
/// Plays an NPC without any AI: written lines from <c>offline_script.json</c>. The greeting follows
/// the relationship, the climber gets three written replies (a warm one, a plain one, a rude one,
/// shuffled) and the NPC answers the one picked. Affection, deals, keepsakes and small gifts go
/// through the same rules as an AI reply, so a player with no key still gets the whole mod except
/// typing their own words.
/// </summary>
public sealed class ScriptBackend : INpcBackend
{
    public static readonly ScriptBackend Instance = new();

    public string DisplayName => "Offline script";

    public bool IsConfigured => true;

    private readonly Random _rng = new();
    /// <summary>Per NPC: the replies on screen right now, by their text, and the ones used this visit.</summary>
    private readonly Dictionary<string, Dictionary<string, Exchange>> _offered = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<Exchange>> _used = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lastOpener = new(StringComparer.OrdinalIgnoreCase);

    public Task<NpcTurn> ExchangeAsync(VisitContext ctx, string playerLine, bool isOpener, CancellationToken ct)
    {
        var script = Load().GetValueOrDefault(ctx.NpcKey.ToUpperInvariant())
            ?? throw new BackendException(string.Format(Loc.Ui("ErrNoCard"), ctx.NpcKey), null, "NO_SCRIPT");
        var lang = Loc.ReplyLanguageCode();
        var lines = new List<string>();
        var delta = 0;
        var action = NpcAction.None;

        if (isOpener)
        {
            _used[ctx.NpcKey] = new HashSet<Exchange>();
            var pool = script.Open.GetValueOrDefault(ConversationRules.TierName(ctx.Tier)) ?? script.Open.Values.First();
            var greeting = PickFresh(pool.Select(t => t.In(lang)).ToList(), _lastOpener.GetValueOrDefault(ctx.NpcKey));
            _lastOpener[ctx.NpcKey] = greeting;
            lines.AddRange(Bubbles(greeting));
            if (ctx.Variables["deal_offer"]?.ToString() is { } offer && offer != "none")
            {
                lines.Add(Pick(script.Deal).In(lang));
            }
        }
        else
        {
            var picked = _offered.GetValueOrDefault(ctx.NpcKey)?.GetValueOrDefault(playerLine);
            if (picked != null)
            {
                _used.GetValueOrDefault(ctx.NpcKey)?.Add(picked);
                lines.AddRange(Bubbles(picked.Reply.In(lang)));
                delta = picked.Tag switch
                {
                    "warm" => 3,
                    "rude" => -3,
                    _ => 1,
                };
                // A warm word may earn a small favour, as the AI would hand one out: the rules above
                // decide what is allowed at this relationship, the NPC just picks one of those.
                var gifts = ctx.AllowedActions.Where(a => a != "reroll_relics").ToList();
                if (picked.Tag == "warm" && gifts.Count > 0 && (ctx.LinesLeft == 0 || _rng.NextDouble() < 0.4))
                {
                    action = new NpcAction(gifts[_rng.Next(gifts.Count)], 0);
                    lines.Add(Pick(script.Gift).In(lang));
                }
            }
            else
            {
                // Not one of the written replies (shouldn't happen: there is no typing offline).
                lines.AddRange(Bubbles(Pick(script.Exchanges.Where(e => e.Tag == "plain").ToList()).Reply.In(lang)));
            }
        }

        var choices = new List<string>();
        var onScreen = new Dictionary<string, Exchange>();
        var used = _used.GetValueOrDefault(ctx.NpcKey) ?? new HashSet<Exchange>();
        foreach (var tag in new[] { "warm", "plain", "rude" })
        {
            var pool = script.Exchanges.Where(e => e.Tag == tag && !used.Contains(e)).ToList();
            if (pool.Count == 0) pool = script.Exchanges.Where(e => e.Tag == tag).ToList();
            if (pool.Count == 0) continue;
            var ex = Pick(pool);
            var text = ex.Choice.In(lang);
            if (onScreen.TryAdd(text, ex)) choices.Add(text);
        }
        _offered[ctx.NpcKey] = onScreen;
        Shuffle(choices);

        var tidy = lines.Select(l => EngineBackend.TidyDots(l, ctx.NpcKey)).Where(l => l.Length > 0).ToList();
        return Task.FromResult(new NpcTurn(tidy, choices, delta, null, action, "script"));
    }

    private static IEnumerable<string> Bubbles(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private T Pick<T>(IReadOnlyList<T> pool) => pool[_rng.Next(pool.Count)];

    private string PickFresh(List<string> pool, string? last)
    {
        var fresh = pool.Where(p => p != last).ToList();
        return Pick(fresh.Count > 0 ? fresh : pool);
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ── Data ────────────────────────────────────────────────────────────

    /// <summary>One written line: "en" and "zh" (Simplified), plus the other game languages under their
    /// game codes ("jpn", "deu"…). A language without the line falls back to English.</summary>
    public sealed class Text
    {
        [JsonPropertyName("en")] public string En { get; set; } = "";
        [JsonPropertyName("zh")] public string Zh { get; set; } = "";
        [JsonExtensionData] public Dictionary<string, JsonElement>? Other { get; set; }

        public string In(string lang)
        {
            if (lang == "zhs") return Zh.Length > 0 ? Zh : En;
            if (lang != "eng" && Other != null && Other.TryGetValue(lang, out var v) && v.ValueKind == JsonValueKind.String
                && v.GetString() is { Length: > 0 } s) return s;
            return En;
        }
    }

    public sealed class Exchange
    {
        [JsonPropertyName("tag")] public string Tag { get; set; } = "plain";
        [JsonPropertyName("choice")] public Text Choice { get; set; } = new();
        [JsonPropertyName("reply")] public Text Reply { get; set; } = new();
    }

    public sealed class NpcScript
    {
        [JsonPropertyName("open")] public Dictionary<string, List<Text>> Open { get; set; } = new();
        [JsonPropertyName("exchanges")] public List<Exchange> Exchanges { get; set; } = new();
        [JsonPropertyName("deal")] public List<Text> Deal { get; set; } = new();
        [JsonPropertyName("gift")] public List<Text> Gift { get; set; } = new();
    }

    private static Dictionary<string, NpcScript>? _scripts;

    private static Dictionary<string, NpcScript> Load()
    {
        if (_scripts != null) return _scripts;
        try
        {
            using var fa = Godot.FileAccess.Open("res://SpireTalk/offline_script.json", Godot.FileAccess.ModeFlags.Read);
            _scripts = JsonSerializer.Deserialize<Dictionary<string, NpcScript>>(fa?.GetAsText() ?? "{}") ?? new();
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[Script] offline_script.json failed to load: {ex.Message}", 1);
            _scripts = new();
        }
        return _scripts;
    }
}

/// <summary>Which backend plays the NPCs: the player's AI when a key is set, the written script otherwise.</summary>
public static class Backends
{
    /// <summary>The player wants the AI and a key is in: lines are written by the model.</summary>
    public static bool AiReady => SpireTalkConfig.UseAi && MainFile.Backend.IsConfigured;

    public static bool Scripted => !AiReady;

    public static INpcBackend Active => Scripted ? ScriptBackend.Instance : MainFile.Backend;
}
