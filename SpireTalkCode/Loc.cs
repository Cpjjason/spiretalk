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
    /// <summary>Like <see cref="Dynamic"/> but keeps BBCode; only for markup the mod writes itself, never model text.</summary>
    public static LocString DynamicMarkup(string markup)
    {
        var loc = new LocString(AncientsTable, DynamicKey);
        loc.Add("text", markup.Replace("{", "｛").Replace("}", "｝"));
        return loc;
    }

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

    // The game's own language codes (LocManager._supportedLanguages), as the AI should read them.
    private static readonly Dictionary<string, string> GameLanguageNames = new()
    {
        ["eng"] = "English",
        ["zhs"] = "Simplified Chinese (简体中文)",
        ["zht"] = "Traditional Chinese (繁體中文)",
        ["deu"] = "German (Deutsch)",
        ["esp"] = "Latin American Spanish (español de Latinoamérica)",
        ["spa"] = "Spanish (español de España)",
        ["fra"] = "French (français)",
        ["ind"] = "Indonesian (Bahasa Indonesia)",
        ["ita"] = "Italian (italiano)",
        ["jpn"] = "Japanese (日本語)",
        ["kor"] = "Korean (한국어)",
        ["pol"] = "Polish (polski)",
        ["ptb"] = "Brazilian Portuguese (português do Brasil)",
        ["rus"] = "Russian (русский)",
        ["tha"] = "Thai (ไทย)",
        ["tur"] = "Turkish (Türkçe)",
    };

    /// <summary>
    /// Language the AI should answer in. Following the game covers every language the game ships;
    /// the two explicit picks force English or Simplified Chinese whatever the game is set to.
    /// Only the AI's words follow it: the mod's own interface and the written lines stay English/Chinese.
    /// </summary>
    public static string ReplyLanguageName()
    {
        return SpireTalkConfig.Language switch
        {
            ReplyLanguage.English => "English",
            ReplyLanguage.ChineseSimplified => GameLanguageNames["zhs"],
            _ => GameLanguageNames.GetValueOrDefault(LocManager.Instance?.Language ?? "eng", "English"),
        };
    }

    /// <summary>
    /// Which set of the NPC's in-game lines goes into the prompt as their voice: the game's own
    /// Chinese lines for either Chinese, the English ones for every other reply language.
    /// </summary>
    public static string CanonLanguage() => ReplyLanguageName().Contains("Chinese") ? "Chinese" : "English";

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
