using System.Text;
using MegaCrit.Sts2.Core.Localization;
using SpireTalk.Backend;

namespace SpireTalk;

/// <summary>
/// Localization helpers. Static strings live in the mod's localization JSON (merged into the
/// vanilla "ancients" / "settings_ui" tables). AI text is injected through a single
/// "{text}" entry so it can flow through the game's own LocString rendering.
/// </summary>
public static class Loc
{
    public const string AncientsTable = "ancients";
    public const string UiTable = "settings_ui";
    public const string DynamicKey = "SPIRETALK.dynamic";

    /// <summary>A LocString that renders arbitrary runtime text.</summary>
    public static LocString Dynamic(string text)
    {
        var loc = new LocString(AncientsTable, DynamicKey);
        loc.Add("text", Sanitize(text));
        return loc;
    }

    /// <summary>In-event string from ancients.json: SPIRETALK.&lt;key&gt;</summary>
    public static LocString Event(string key)
    {
        return new LocString(AncientsTable, "SPIRETALK." + key);
    }

    /// <summary>
    /// Plain string for code that formats with string.Format. Uses the RAW table text: the game's
    /// SmartFormat would otherwise expand "{0}" into its variables dictionary.
    /// </summary>
    public static string EventText(string key)
    {
        var loc = Event(key);
        if (!loc.Exists()) return key;
        var raw = loc.GetRawText();
        return raw.Contains('{') ? raw : loc.GetFormattedText();
    }

    /// <summary>UI string from settings_ui.json: SPIRETALK-MSG.&lt;key&gt; (raw text, see EventText).</summary>
    public static string Ui(string key)
    {
        var loc = LocString.GetIfExists(UiTable, "SPIRETALK-MSG." + key);
        if (loc == null) return key;
        var raw = loc.GetRawText();
        return raw.Contains('{') ? raw : loc.GetFormattedText();
    }

    /// <summary>
    /// Strip characters that would be interpreted by SmartFormat ({}) or BBCode ([]) when the
    /// text is rendered by the game's rich text labels.
    /// </summary>
    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            sb.Append(ch switch
            {
                '{' => '｛',
                '}' => '｝',
                '[' => '［',
                ']' => '］',
                _ => ch,
            });
        }
        return sb.ToString().Trim();
    }

    /// <summary>
    /// The name the player sees: the game's own localized title for an Ancient (ancients table,
    /// "NEOW.title" → 涅奥), our SPIRETALK.name.&lt;KEY&gt; entry otherwise, then the roster's English name.
    /// </summary>
    public static string NpcName(SpireTalk.Npc.NpcDefinition npc)
    {
        var key = npc.Key.ToUpperInvariant();
        if (npc.Kind == SpireTalk.Npc.NpcKind.Ancient)
        {
            var vanilla = LocString.GetIfExists(AncientsTable, key + ".title");
            if (vanilla != null) return vanilla.GetFormattedText();
        }
        var own = LocString.GetIfExists(AncientsTable, "SPIRETALK.name." + key);
        return own != null ? own.GetFormattedText() : npc.Name;
    }

    /// <summary>
    /// Language the AI should answer in. Only English and Simplified Chinese are supported;
    /// following the game means Chinese for zhs and English for everything else.
    /// </summary>
    public static string ReplyLanguageName()
    {
        var pick = SpireTalkConfig.Language;
        if (pick == ReplyLanguage.FollowGame)
        {
            pick = LocManager.Instance?.Language == "zhs" ? ReplyLanguage.ChineseSimplified : ReplyLanguage.English;
        }
        return pick == ReplyLanguage.ChineseSimplified ? "Simplified Chinese (简体中文)" : "English";
    }

    public static string DescribeError(Exception ex)
    {
        if (ex is BackendException ye)
        {
            var key = ye.Code switch
            {
                "UNAUTHORIZED" => "ErrUnauthorized",
                "NO_CREDITS" => "ErrNoCredits",
                "RATE_LIMITED" => "ErrRateLimited",
                "NETWORK" => "ErrNetwork",
                "TIMEOUT" => "ErrTimeout",
                "NOT_FOUND" => "ErrNotFound",
                "NOT_CONFIGURED" => "ErrNotConfigured",
                _ => null,
            };
            if (key != null)
            {
                var text = Ui(key);
                return text == key ? ye.Message : text;
            }
            return ye.Message;
        }
        if (ex is OperationCanceledException) return Ui("ErrTimeout");
        return ex.Message;
    }
}
