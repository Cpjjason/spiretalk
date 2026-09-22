using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SpireTalk.Npc;

namespace SpireTalk.Backend;

/// <summary>
/// Salvage for models that ignore the structured contract and write the proposal fields as
/// loose "choices: [...]" / "affection_delta: +1" / "action: {...}" lines at the end of their
/// prose. Anything recovered is removed from the visible text so the player never sees it.
/// </summary>
public static class LooseFields
{
    // Accepts both `choices: ["a", "b"]` and the bare `[choices: "a", "b", "c"]` small models like to write.
    private static readonly Regex ChoicesRe = new(@"(?im)^\s*[\[\(]?\s*choices\s*[:=]\s*(\[[^\]]*\]|""[^""\r\n]*""(?:\s*,\s*""[^""\r\n]*"")*)\s*[\]\)]?\s*$", RegexOptions.Compiled);
    private static readonly Regex DeltaRe = new(@"(?im)^\s*[\[\(]?\s*affection_delta\s*[:=]\s*([+-]?\d)\s*[\]\)]?\s*$", RegexOptions.Compiled);
    private static readonly Regex ActionRe = new(@"(?im)^\s*[\[\(]?\s*action\s*[:=]\s*(\{[^}]*\})\s*[\]\)]?\s*$", RegexOptions.Compiled);
    private static readonly Regex StageRe = new(@"\[\s*stage direction\s*:\s*([^\]]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RumorRe = new(@"(?im)^\s*[\[\(]?\s*rumor\s*[:=]\s*(?:set\s+)?""([^""\r\n]*)""\s*[\]\)]?\s*$", RegexOptions.Compiled);
    private static readonly Regex RumorMoodRe = new(@"(?im)^\s*[\[\(]?\s*rumor_sentiment\s*[:=]\s*(?:set\s+)?""?([+\-0])""?\s*[\]\)]?\s*$", RegexOptions.Compiled);
    private static readonly Regex LeftoverDirectiveRe = new(@"(?m)^\s*\[(?:affection_delta|choices|action|rumor|rumor_sentiment)\s*:[^\]]*\]\s*$", RegexOptions.Compiled);

    /// <summary>
    /// Returns the display text (cleaned) and fills in any field that the structured path left empty.
    /// <paramref name="raw"/> is the model's full output; <paramref name="clean"/> is what the engine
    /// already stripped of directives (used when the raw text has nothing extra).
    /// </summary>
    public static string Extract(string raw, string clean, ref List<string> choices, ref int delta, ref NpcAction action)
    {
        string? rumor = null;
        string? mood = null;
        return Extract(raw, clean, ref choices, ref delta, ref action, ref rumor, ref mood);
    }

    public static string Extract(string raw, string clean, ref List<string> choices, ref int delta, ref NpcAction action, ref string? rumor, ref string? rumorMood)
    {
        var text = string.IsNullOrWhiteSpace(raw) ? clean : raw;
        if (string.IsNullOrWhiteSpace(text)) return clean;

        if (string.IsNullOrWhiteSpace(rumor))
        {
            var m = RumorRe.Match(text);
            if (m.Success) rumor = m.Groups[1].Value.Trim();
        }
        if (string.IsNullOrWhiteSpace(rumorMood) || rumorMood == "0")
        {
            var m = RumorMoodRe.Match(text);
            if (m.Success) rumorMood = m.Groups[1].Value;
        }

        if (choices.Count == 0)
        {
            var m = ChoicesRe.Match(text);
            if (m.Success)
            {
                try
                {
                    var list = m.Groups[1].Value.Trim();
                    if (!list.StartsWith('[')) list = "[" + list + "]";
                    if (JsonNode.Parse(list) is JsonArray arr)
                    {
                        foreach (var n in arr)
                        {
                            var s = n?.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(s)) choices.Add(s.Trim());
                        }
                    }
                }
                catch
                {
                    // malformed list — ignore
                }
            }
        }
        if (delta == 0)
        {
            var m = DeltaRe.Match(text);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var d)) delta = d;
        }
        if (action.IsNone)
        {
            var m = ActionRe.Match(text);
            if (m.Success)
            {
                try
                {
                    if (JsonNode.Parse(m.Groups[1].Value) is JsonObject o)
                    {
                        var type = o["type"]?.GetValue<string>() ?? "none";
                        var amount = 0;
                        if (o["amount"] is JsonValue av && av.TryGetValue<int>(out var a)) amount = a;
                        action = new NpcAction(type.Trim().ToLowerInvariant(), amount);
                    }
                }
                catch
                {
                    // malformed object — ignore
                }
            }
        }

        // Visible text starts from the engine's cleaned version (proper directives already gone);
        // then drop loose field lines, leftover directive-looking lines, and turn stage
        // directions into parentheses.
        var visible = string.IsNullOrWhiteSpace(clean) ? text : clean;
        visible = ChoicesRe.Replace(visible, "");
        visible = DeltaRe.Replace(visible, "");
        visible = ActionRe.Replace(visible, "");
        visible = RumorRe.Replace(visible, "");
        visible = RumorMoodRe.Replace(visible, "");
        visible = LeftoverDirectiveRe.Replace(visible, "");
        visible = StageRe.Replace(visible, m => "(" + m.Groups[1].Value.Trim() + ")");
        return visible.Trim();
    }
}
