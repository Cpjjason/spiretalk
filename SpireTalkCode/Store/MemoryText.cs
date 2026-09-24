using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SpireTalk.Store;

/// <summary>
/// Turns conversation logs into what the model reads. Earlier visits become short notes in the
/// third person ("the climber said …"); the NPC's own old wording is never sent back, because
/// models copy whatever sits in their context. Only the visit in progress goes in verbatim.
/// </summary>
public static class MemoryText
{
    private const int QuotesPerVisit = 3;
    private const int QuoteMaxChars = 60;
    private const int NotesInPrompt = 12;

    private static readonly Regex Directive = new(@"^\s*\[(affection_delta|choices|action|affection|rumor|rumor_sentiment)\s*:", RegexOptions.IgnoreCase);

    /// <summary>A visit summed up as one note, or null when the climber said nothing worth keeping.</summary>
    public static string? FromVisit(IReadOnlyList<TalkStore.HistoryEntry> lines)
    {
        var said = lines
            .Where(l => l.Role == "player" && !IsStageNote(l.Text))
            .Select(l => Clip(l.Text.Trim()))
            .Where(t => t.Length > 0)
            .Distinct()
            .TakeLast(QuotesPerVisit)
            .ToList();
        if (said.Count == 0) return null;
        return string.Format(Loc.Ui("MemoryNoteSaid"), string.Join(Loc.Ui("MemoryQuoteJoin"), said.Select(s => string.Format(Loc.Ui("MemoryQuote"), s))));
    }

    /// <summary>The NPC's reply without the directive lines stored after it.</summary>
    public static string CleanNpcText(string text)
    {
        var kept = text.Split('\n').Where(l => !Directive.IsMatch(l));
        return string.Join("\n", kept).Trim();
    }

    /// <summary>Bracketed lines are the mod talking to the model (openers, road notes), not the climber.</summary>
    public static bool IsStageNote(string text) => text.TrimStart().StartsWith('[');

    private static string Clip(string text)
    {
        text = Regex.Replace(text, @"\s+", " ");
        return text.Length <= QuoteMaxChars ? text : text[..QuoteMaxChars] + "…";
    }

    /// <summary>
    /// The history the model gets: one block of memory notes, then this visit's exchange as it
    /// happened (with the directive lines, so the model keeps writing them).
    /// </summary>
    public static JsonArray BuildHistory(string npcKey, string? runKey, bool includeCurrentVisit = true)
    {
        var store = TalkStore.Instance;
        var history = new JsonArray();
        var block = PromptBlock(store.MemoriesFor(npcKey, runKey), runKey);
        if (block != null) history.Add(new JsonObject { ["role"] = "user", ["content"] = block });
        if (includeCurrentVisit)
        {
            foreach (var h in store.CurrentVisitLines(npcKey))
            {
                history.Add(new JsonObject { ["role"] = h.Role == "npc" ? "assistant" : "user", ["content"] = h.Text });
            }
        }
        return history;
    }

    private static string? PromptBlock(List<TalkStore.MemoryNote> notes, string? runKey)
    {
        var chosen = notes.Where(n => n.Source == "player")
            .Concat(notes.Where(n => n.Source != "player").TakeLast(NotesInPrompt))
            .ToList();
        if (chosen.Count == 0) return null;
        var sb = new StringBuilder();
        sb.Append("[WHAT YOU REMEMBER ABOUT THIS CLIMBER from earlier meetings, oldest first. ")
          .Append("These are notes, not your words: never quote or restate them, and do not repeat what you told them before. ")
          .Append("Bring one up only when it fits.");
        foreach (var n in chosen)
        {
            var when = n.Source == "player" ? "fact" : n.Run != null && n.Run == runKey ? "this run" : "an earlier run";
            sb.Append("\n- (").Append(when).Append(") ").Append(n.Text.Trim());
        }
        sb.Append(']');
        return sb.ToString();
    }
}
