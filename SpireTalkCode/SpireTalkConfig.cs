using System.Reflection;
using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SpireTalk.Backend;
using SpireTalk.Store;
using SpireTalk.Ui;
using SpireTalk.Yumina;

namespace SpireTalk;

public enum BackendMode
{
    OwnKey,
    Yumina,
    Offline,
}

public enum ReplyLanguage
{
    FollowGame,
    English,
    ChineseSimplified,
    ChineseTraditional,
    Japanese,
    Korean,
}

/// <summary>
/// Mod settings. BaseLib owns persistence (mod_configs/SpireTalk.cfg) and the themed controls;
/// the screen the player actually sees is <see cref="SpireTalkScreen"/>, opened from the mushie
/// on the main menu or from the one button this class puts on the vanilla mod list.
/// Labels live in localization/&lt;lang&gt;/settings_ui.json under the SPIRETALK- prefix.
/// </summary>
public sealed class SpireTalkConfig : SimpleModConfig
{
    // A field, not a property: BaseLib persists every public static property of this class.
    public static SpireTalkConfig? Instance;

    public SpireTalkConfig()
    {
        Instance = this;
    }

    // ── Mode ─────────────────────────────────────────────────────────────

    public static BackendMode Mode { get; set; } = BackendMode.OwnKey;

    // ── Own key (OpenRouter / OpenAI / Ollama / any OpenAI-compatible) ──

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string OwnBaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 300)]
    public static string OwnKey { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string OwnModel { get; set; } = "google/gemini-2.5-flash-lite";

    public static bool OwnJsonMode { get; set; } = true;

    // ── Yumina account ───────────────────────────────────────────────────

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string ServerUrl { get; set; } = "https://yumina.io";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string ApiToken { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string YuminaModel { get; set; } = "";

    // ── Conversation ─────────────────────────────────────────────────────

    public static bool EnableAncients { get; set; } = true;

    public static bool EnableMerchant { get; set; } = true;

    public static bool FreeTextInput { get; set; } = true;

    public static bool RememberAcrossRuns { get; set; } = true;

    public static bool UseAncientAffection { get; set; } = true;

    public static ReplyLanguage Language { get; set; } = ReplyLanguage.FollowGame;

    // ── Data ─────────────────────────────────────────────────────────────

    public static bool DebugLogging { get; set; } = false;

    // ── Vanilla mod list: one button that opens our screen ───────────────

    public override void SetupConfigUI(Control optionContainer)
    {
        // Deliberately not calling base: the auto-generated rows are replaced by SpireTalkScreen.
        try
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 18);
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 24);
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddChild(box);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 20);
            row.AddChild(GameArt.Icon(GameArt.MushieSitPath, 72));
            var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            text.AddChild(GameArt.Label("[b]" + Loc.Ui("ScreenTitle") + "[/b]", 32, GameArt.Cream));
            text.AddChild(GameArt.Label(Loc.Ui("ModListBlurb"), 24, GameArt.Muted));
            row.AddChild(text);
            box.AddChild(row);

            var open = MakeButton(Loc.Ui("OpenSettingsBtn"), () => SpireTalkScreen.Open());
            open.CustomMinimumSize = new Vector2(420f, open.CustomMinimumSize.Y);
            var center = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
            center.AddChild(open);
            box.AddChild(center);

            optionContainer.AddChild(margin);
            SimpleModConfig.SetupFocusNeighbors(optionContainer);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Config] mod-list page failed: {ex.Message}", 1);
        }
    }

    // ── Control factories for the custom screen (BaseLib's themed widgets, bound to our props) ──

    private static PropertyInfo Prop(string name) =>
        typeof(SpireTalkConfig).GetProperty(name, BindingFlags.Public | BindingFlags.Static)
        ?? throw new ArgumentException("no config property " + name);

    internal NConfigLineEdit MakeLineEdit(string property, bool secret = false, float width = 560f)
    {
        var edit = CreateRawLineEditControl(Prop(property));
        edit.Secret = secret;
        edit.CustomMinimumSize = new Vector2(width, 56f);
        edit.AddThemeFontSizeOverride("font_size", 22);
        return edit;
    }

    internal NConfigTickbox MakeTickbox(string property)
    {
        var tick = CreateRawTickboxControl(Prop(property));
        // BaseLib reserves a dropdown-wide 324px slot for its tickbox rows; the visuals are
        // centred in it, so a 64px slot shows exactly the box.
        tick.CustomMinimumSize = new Vector2(64f, 64f);
        tick.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        tick.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return tick;
    }

    internal NDropdownPositioner MakeDropdown(string property) => CreateRawDropdownControl(Prop(property));

    internal NConfigButton MakeButton(string label, Action onPressed, string? color = null)
    {
        var button = CreateRawButtonControl(label, onPressed);
        button.CustomMinimumSize = new Vector2(220f, button.CustomMinimumSize.Y);
        if (!string.IsNullOrEmpty(color)) button.SetColor(Color.FromHtml(color));
        return button;
    }

    // ── Actions used by the screen ───────────────────────────────────────

    /// <summary>Push the current URL/token into the shared client. Called on load and on every change.</summary>
    public static void ApplyToClient()
    {
        MainFile.Client.Configure(Mode == BackendMode.Offline ? MockYumina.Marker : ServerUrl, ApiToken);
    }

    public static void SetMode(BackendMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        Instance?.Changed();
        Instance?.SaveDebounced();
        ApplyToClient();
    }

    public static void Logout()
    {
        ApiToken = "";
        Instance?.Changed();
        Instance?.SaveDebounced();
        ApplyToClient();
        AccountPanel.Refresh();
    }

    public static void ResetMemories()
    {
        TalkStore.Instance.ResetAll();
    }

    /// <summary>Probe the Yumina server with the current token. Returns (ok, one-line status).</summary>
    public static async Task<(bool ok, string text)> TestConnectionAsync()
    {
        ApplyToClient();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var me = await MainFile.Client.WhoAmIAsync(cts.Token);
            var manifest = await MainFile.Client.GetManifestAsync(cts.Token);
            MainFile.Manifest = manifest;
            var credits = me.Credits.HasValue ? me.Credits.Value.ToString("N0") : "?";
            AccountPanel.Refresh();
            return (true, string.Format(Loc.Ui("TestOkBody"), me.Name, credits, manifest.Worlds.Count));
        }
        catch (Exception ex)
        {
            return (false, Loc.DescribeError(ex));
        }
    }

    /// <summary>Probe the player's own endpoint. Returns (ok, one-line status).</summary>
    public static async Task<(bool ok, string text)> TestOwnKeyAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var probe = await MainFile.Direct.ProbeAsync(cts.Token);
            return (true, string.Format(Loc.Ui("TestOwnOkBody"), OwnBaseUrl, OwnModel, probe));
        }
        catch (Exception ex)
        {
            return (false, Loc.DescribeError(ex));
        }
    }

    public static void ShowPopup(string title, string body)
    {
        try
        {
            var popup = NErrorPopup.Create(title, body, false);
            if (popup != null && NModalContainer.Instance != null)
            {
                NModalContainer.Instance.Add(popup, true);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Config] popup failed: {ex.Message}", 1);
        }
    }
}
