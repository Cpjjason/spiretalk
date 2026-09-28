using System.Reflection;
using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SpireTalk.Backend;
using SpireTalk.Store;
using SpireTalk.Ui;

namespace SpireTalk;

/// <summary>How hard the NPCs are to please. One knob; the rules, rewards and deals all read it.</summary>
public enum Temperament
{
    Lenient,
    Normal,
    Strict,
}

public enum ReplyLanguage
{
    FollowGame,
    English,
    ChineseSimplified,
}

/// <summary>
/// Mod settings. BaseLib owns persistence (mod_configs/SpireTalk.cfg) and the themed controls;
/// the screen the player actually sees is <see cref="SpireTalkScreen"/>, opened from the mushie
/// on the main menu or from the one button this class puts on the vanilla mod list.
/// Everything runs on the player's own model key; there is no account of any kind.
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

    // ── Own key (OpenRouter / OpenAI / Ollama / any OpenAI-compatible) ──

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string OwnBaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 300)]
    public static string OwnKey { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string OwnModel { get; set; } = ApiSources.OpenRouterModels[0].Id;

    // The three values above are what the client reads. The settings page drives them from a
    // source picker; these remember each source's own key and address so switching back and
    // forth loses nothing.

    public static ApiSource Source { get; set; } = ApiSource.OpenRouter;

    public static bool SourceChosen { get; set; } = false;

    /// <summary>The first-launch welcome has been shown once; after that the guide opens only from the settings sheet.</summary>
    public static bool GuideSeen { get; set; } = false;

    /// <summary>
    /// How NPCs talk: true = the AI writes their lines (once a key works; until then the written
    /// script stands in), false = always the written script, key or not.
    /// </summary>
    public static bool UseAi { get; set; } = true;

    /// <summary>Players from before the mode switch get a dot on the mushie until they open the settings once.</summary>
    public static bool ModeNoticeSeen { get; set; } = false;

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 300)]
    public static string KeyOpenRouter { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 300)]
    public static string KeyCustom { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string CustomBaseUrl { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string ModelOpenRouter { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string ModelOllama { get; set; } = "";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string ModelCustom { get; set; } = "";

    /// <summary>Key and model per provider preset (OpenAI, DeepSeek, …) as JSON: {"DeepSeek":{"k":"…","m":"…"}}.</summary>
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 8000)]
    public static string SourceSlots { get; set; } = "";

    private static (string key, string model) ReadSlot(ApiSource source)
    {
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(SourceSlots) ? "{}" : SourceSlots)?[source.ToString()];
            return (node?["k"]?.GetValue<string>() ?? "", node?["m"]?.GetValue<string>() ?? "");
        }
        catch
        {
            return ("", "");
        }
    }

    private static void WriteSlot(ApiSource source, string key, string model)
    {
        System.Text.Json.Nodes.JsonObject root;
        try
        {
            root = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(SourceSlots) ? "{}" : SourceSlots) as System.Text.Json.Nodes.JsonObject ?? new();
        }
        catch
        {
            root = new();
        }
        root[source.ToString()] = new System.Text.Json.Nodes.JsonObject { ["k"] = key, ["m"] = model };
        SourceSlots = root.ToJsonString();
    }

    // ── Conversation ─────────────────────────────────────────────────────

    public static bool EnableAncients { get; set; } = true;

    public static bool EnableMerchant { get; set; } = true;

    public static bool FreeTextInput { get; set; } = true;

    public static bool RememberAcrossRuns { get; set; } = true;

    public static bool UseAncientAffection { get; set; } = true;

    public static ReplyLanguage Language { get; set; } = ReplyLanguage.FollowGame;

    public static Temperament Temperament { get; set; } = Temperament.Normal;

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
        // Long labels (German "Erinnerungen löschen", Russian "Стереть воспоминания") ran past the
        // hexagon and off the sheet. The game's own label can shrink its font to fit; BaseLib turns
        // that off, so turn it back on and keep the text inside the pointed ends.
        if (button.GetNodeOrNull<MegaCrit.Sts2.addons.mega_text.MegaLabel>("Label") is { } text)
        {
            text.MaxFontSize = 28;
            text.MinFontSize = 18;
            text.AutoSizeEnabled = true;
            text.OffsetLeft = 22f;
            text.OffsetRight = -22f;
        }
        if (!string.IsNullOrEmpty(color)) button.SetColor(Color.FromHtml(color));
        return button;
    }

    // ── Actions used by the screen ───────────────────────────────────────

    /// <summary>Configs from before the source picker only have a URL; work out which source it was once.</summary>
    public static void EnsureSource()
    {
        if (SourceChosen) return;
        Source = ApiSources.Infer(OwnBaseUrl);
        if (Source == ApiSource.Custom) CustomBaseUrl = OwnBaseUrl;
        StashKey();
        SourceChosen = true;
    }

    /// <summary>
    /// Point the client at another source: its address, and the key and model last used with it.
    /// A source never used before gets OpenRouter's default model, or none (the page then picks
    /// the first model the server lists).
    /// </summary>
    public static void SwitchSource(ApiSource next)
    {
        EnsureSource();
        StashKey();
        Source = next;
        OwnBaseUrl = ApiSources.UrlFor(next);
        var slot = ReadSlot(next);
        OwnKey = next switch
        {
            ApiSource.OpenRouter => KeyOpenRouter,
            ApiSource.Custom => KeyCustom,
            ApiSource.Ollama => "",
            _ => slot.key,
        };
        var fixedFirst = ApiSources.FixedModels(next)?.FirstOrDefault().Id ?? "";
        OwnModel = next switch
        {
            ApiSource.OpenRouter => string.IsNullOrWhiteSpace(ModelOpenRouter) ? ApiSources.OpenRouterModels[0].Id : ModelOpenRouter,
            ApiSource.Ollama => ModelOllama,
            ApiSource.Custom => ModelCustom,
            _ => string.IsNullOrWhiteSpace(slot.model) ? fixedFirst : slot.model,
        };
    }

    /// <summary>Copy the live key and model into the current source's slots (the page edits the live ones).</summary>
    public static void StashKey()
    {
        switch (Source)
        {
            case ApiSource.OpenRouter: KeyOpenRouter = OwnKey; ModelOpenRouter = OwnModel; break;
            case ApiSource.Ollama: ModelOllama = OwnModel; break;
            case ApiSource.Custom: KeyCustom = OwnKey; ModelCustom = OwnModel; break;
            default: WriteSlot(Source, OwnKey, OwnModel); break;
        }
        if (Source == ApiSource.Custom) OwnBaseUrl = CustomBaseUrl;
    }

    public static void ResetMemories()
    {
        TalkStore.Instance.ResetAll();
    }

    /// <summary>Probe the player's own endpoint. Returns (ok, one-line status).</summary>
    public static async Task<(bool ok, string text)> TestOwnKeyAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var probe = await MainFile.Backend.Client.ProbeAsync(cts.Token);
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
