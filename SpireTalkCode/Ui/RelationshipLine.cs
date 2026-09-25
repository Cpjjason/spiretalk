using SpireTalk.Npc;

namespace SpireTalk.Ui;

/// <summary>
/// The small line under "that is all for now": the relationship as five hearts and the lines left
/// as dots, in place of raw numbers. BBCode images, so it sits in the game's own option text.
/// </summary>
public static class RelationshipLine
{
    private const string Folder = "res://SpireTalk/images/ui/";
    private const int HeartSize = 17;
    private const int PipSize = 11;

    public static string Build(int affection, NpcKind kind, ConversationRules.Tier tier, int linesLeft, bool showLines, string extra = "")
    {
        var max = kind == NpcKind.Merchant ? ConversationRules.MaxMerchantAffection : ConversationRules.MaxAncientAffection;
        var filled = affection <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(affection * 5.0 / max), 1, 5);
        var tierName = Loc.EventText("tier." + ConversationRules.TierName(tier));
        var tierText = tier == ConversationRules.Tier.Hostile ? $"[red]{tierName}[/red]" : tierName;

        var hearts = string.Concat(Enumerable.Range(0, 5).Select(i => Img(i < filled ? "heart_full" : "heart_empty", HeartSize)));
        var line = $"{tierText}  {hearts}";
        if (showLines)
        {
            var pips = string.Concat(Enumerable.Range(0, ConversationRules.LinesPerVisit).Select(i => Img(i < linesLeft ? "pip_full" : "pip_empty", PipSize)));
            line += $"    {pips}";
        }
        if (!string.IsNullOrWhiteSpace(extra)) line += "    " + extra.Trim();
        return line;
    }

    // Native-size pictures, like the game's own sprite-font icons: a sized [img=WxH] tag makes the
    // option label shrink its whole text to fit.
    private static string Img(string name, int size) => $"[img]{Folder}{name}_s.png[/img]";
}
