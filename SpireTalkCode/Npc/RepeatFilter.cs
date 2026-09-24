using System.Text;

namespace SpireTalk.Npc;

/// <summary>
/// Cuts what the NPC already said. Any run of characters in the new reply that also appears in an
/// earlier line (this visit and the last couple) is marked; a fragment — the text between
/// punctuation or pause marks — that is mostly marked is dropped, along with a one- or
/// two-letter fragment left hanging next to it. So a catchphrase lands once instead of closing
/// every reply. A reply is never emptied: if everything repeats, it is kept as it came.
/// </summary>
public static class RepeatFilter
{
    // A repeat is a shared run of at least 5 CJK characters, or 22 letters of Latin text.
    private const int MinCjkRun = 5;
    private const int MinLatinRun = 22;
    private const double DropWhenCovered = 0.6;

    public static List<string> Apply(IReadOnlyList<string> lines, IReadOnlyList<string> earlier)
    {
        var said = earlier.Select(Normalize).Where(s => s.Length > 0).ToList();
        if (said.Count == 0 || lines.Count == 0) return lines.ToList();

        var result = new List<string>();
        foreach (var line in lines)
        {
            var kept = StripLine(line, said);
            if (kept.Length > 0) result.Add(kept);
        }
        return result.Count > 0 ? result : lines.ToList();
    }

    private sealed class Fragment
    {
        public string Text = "";
        public string Tail = "";
        public string Norm = "";
        public int Start;
        public bool Drop;
    }

    private static string StripLine(string line, List<string> said)
    {
        var fragments = Split(line);
        var norm = new StringBuilder();
        foreach (var f in fragments)
        {
            f.Norm = Normalize(f.Text);
            f.Start = norm.Length;
            norm.Append(f.Norm);
        }
        var covered = Cover(norm.ToString(), said);

        foreach (var f in fragments)
        {
            if (f.Norm.Length == 0) continue;
            var hits = 0;
            for (var i = 0; i < f.Norm.Length; i++) if (covered[f.Start + i]) hits++;
            f.Drop = hits >= DropWhenCovered * f.Norm.Length;
        }
        for (var i = 0; i < fragments.Count; i++)
        {
            var f = fragments[i];
            if (f.Drop || f.Norm.Length == 0 || f.Norm.Length > (f.Norm.Any(IsCjk) ? 2 : 10)) continue;
            var prev = Neighbour(fragments, i, -1);
            var next = Neighbour(fragments, i, +1);
            if ((prev?.Drop ?? false) || (next?.Drop ?? false)) f.Drop = true;
        }

        var sb = new StringBuilder();
        var any = false;
        foreach (var f in fragments)
        {
            if (f.Drop) continue;
            sb.Append(f.Text).Append(f.Tail);
            if (f.Norm.Length > 0) any = true;
        }
        return any ? sb.ToString().Trim() : "";
    }

    private static Fragment? Neighbour(List<Fragment> fragments, int i, int step)
    {
        for (var j = i + step; j >= 0 && j < fragments.Count; j += step)
        {
            if (fragments[j].Norm.Length > 0) return fragments[j];
        }
        return null;
    }

    private static bool[] Cover(string text, List<string> said)
    {
        var covered = new bool[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var min = IsCjk(text[i]) ? MinCjkRun : MinLatinRun;
            if (i + min > text.Length) continue;
            var window = text.Substring(i, min);
            if (said.Any(s => s.Contains(window, StringComparison.Ordinal)))
            {
                for (var k = i; k < i + min; k++) covered[k] = true;
            }
        }
        return covered;
    }

    /// <summary>Splits after sentence/clause punctuation and pause runs ("..", "…"), keeping the separators as tails.</summary>
    private static List<Fragment> Split(string line)
    {
        var parts = new List<Fragment>();
        var text = new StringBuilder();
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];
            if (IsBreak(c) || (c == '.' && i + 1 < line.Length && line[i + 1] == '.') || (c == '.' && i + 1 < line.Length && char.IsWhiteSpace(line[i + 1])))
            {
                var tail = new StringBuilder();
                while (i < line.Length && (IsBreak(line[i]) || line[i] == '.' || char.IsWhiteSpace(line[i])))
                {
                    tail.Append(line[i]);
                    i++;
                }
                parts.Add(new Fragment { Text = text.ToString(), Tail = tail.ToString() });
                text.Clear();
                continue;
            }
            text.Append(c);
            i++;
        }
        if (text.Length > 0) parts.Add(new Fragment { Text = text.ToString() });
        return parts;
    }

    private static bool IsBreak(char c) => "。！？!?，,、；;：:…\n—".Contains(c);

    private static bool IsCjk(char c) => c >= 0x2E80;

    /// <summary>Letters and digits only, lower-cased: "..去.. 杀.. 死... 建筑师" → "去杀死建筑师".</summary>
    private static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
