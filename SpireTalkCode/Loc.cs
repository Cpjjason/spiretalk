using System.Text;
using MegaCrit.Sts2.Core.Localization;
using SpireTalk.Yumina;

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

    public static string EventText(string key)
    {
        var loc = Event(key);
        return loc.Exists() ? loc.GetFormattedText() : key;
    }

    /// <summary>UI string from settings_ui.json: SPIRETALK-MSG.&lt;key&gt;</summary>
    public static string Ui(string key)
    {
        var loc = LocString.GetIfExists(UiTable, "SPIRETALK-MSG." + key);
        return loc?.GetFormattedText() ?? key;
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

    /// <summary>Human-readable language name the AI should answer in.</summary>
    public static string ReplyLanguageName()
    {
        var pick = SpireTalkConfig.Language;
        if (pick == ReplyLanguage.FollowGame)
        {
            var code = LocManager.Instance?.Language ?? "eng";
            return code switch
            {
                "zhs" => "Simplified Chinese (简体中文)",
                "zht" => "Traditional Chinese (繁體中文)",
                "jpn" => "Japanese (日本語)",
                "kor" => "Korean (한국어)",
                "fra" => "French",
                "deu" => "German",
                "spa" => "Spanish",
                "ita" => "Italian",
                "por" => "Portuguese",
                "rus" => "Russian",
                "pol" => "Polish",
                "ukr" => "Ukrainian",
                "tur" => "Turkish",
                "vie" => "Vietnamese",
                "tha" => "Thai",
                "ind" => "Indonesian",
                _ => "English",
            };
        }
        return pick switch
        {
            ReplyLanguage.ChineseSimplified => "Simplified Chinese (简体中文)",
            ReplyLanguage.ChineseTraditional => "Traditional Chinese (繁體中文)",
            ReplyLanguage.Japanese => "Japanese (日本語)",
            ReplyLanguage.Korean => "Korean (한국어)",
            _ => "English",
        };
    }

    public static string DescribeError(Exception ex)
    {
        if (ex is YuminaException ye)
        {
            var key = ye.Code switch
            {
                "NO_TOKEN" => "ErrNoToken",
                "UNAUTHORIZED" => "ErrUnauthorized",
                "NO_CREDITS" => "ErrNoCredits",
                "RATE_LIMITED" => "ErrRateLimited",
                "NETWORK" => "ErrNetwork",
                "TIMEOUT" => "ErrTimeout",
                "NOT_FOUND" => "ErrNotFound",
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
