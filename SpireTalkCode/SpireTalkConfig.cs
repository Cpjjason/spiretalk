using BaseLib.Config;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SpireTalk.Backend;
using SpireTalk.Store;
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
/// Mod settings, rendered by BaseLib inside the vanilla mod-settings screen, plus a custom
/// account panel (see <see cref="SetupConfigUI"/>). Labels live in localization/&lt;lang&gt;/settings_ui.json
/// under the SPIRETALK- prefix.
/// </summary>
public sealed class SpireTalkConfig : SimpleModConfig
{
    // ── Mode ─────────────────────────────────────────────────────────────

    [ConfigSection("Mode")]
    public static BackendMode Mode { get; set; } = BackendMode.OwnKey;

    // ── Own key (OpenRouter / OpenAI / Ollama / any OpenAI-compatible) ──

    [ConfigSection("OwnKey")]
    [ConfigVisibleIf(nameof(Mode), BackendMode.OwnKey)]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string OwnBaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    [ConfigVisibleIf(nameof(Mode), BackendMode.OwnKey)]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 300)]
    public static string OwnKey { get; set; } = "";

    [ConfigVisibleIf(nameof(Mode), BackendMode.OwnKey)]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string OwnModel { get; set; } = "google/gemini-2.5-flash-lite";

    [ConfigVisibleIf(nameof(Mode), BackendMode.OwnKey)]
    public static bool OwnJsonMode { get; set; } = true;

    [ConfigVisibleIf(nameof(Mode), BackendMode.OwnKey)]
    [ConfigButton("TestOwnKeyBtn", Color = "#4a9d7a")]
    public static void TestOwnKey(ModConfig config)
    {
        _ = TestOwnKeyAsync();
    }

    // ── Yumina account ───────────────────────────────────────────────────

    [ConfigSection("Yumina")]
    [ConfigVisibleIf(nameof(Mode), BackendMode.Yumina)]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string ServerUrl { get; set; } = "https://yumina.io";

    [ConfigVisibleIf(nameof(Mode), BackendMode.Yumina)]
    [ConfigButton("LoginBtn", Color = "#c9a227")]
    public static void Login(ModConfig config)
    {
        _ = DeviceLogin.StartAsync();
    }

    [ConfigVisibleIf(nameof(Mode), BackendMode.Yumina)]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string ApiToken { get; set; } = "";

    [ConfigVisibleIf(nameof(Mode), BackendMode.Yumina)]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string YuminaModel { get; set; } = "";

    [ConfigVisibleIf(nameof(Mode), BackendMode.Yumina)]
    [ConfigButton("TestConnectionBtn", Color = "#4a9d7a")]
    public static void TestConnection(ModConfig config)
    {
        _ = TestConnectionAsync();
    }

    [ConfigVisibleIf(nameof(Mode), BackendMode.Yumina)]
    [ConfigButton("LogoutBtn", Color = "#b03f3f")]
    public static void Logout(ModConfig config)
    {
        ApiToken = "";
        ApplyToClient();
        AccountPanel.Refresh();
        ShowPopup(Loc.Ui("LoggedOut"), Loc.Ui("LoggedOutBody"));
    }

    // ── Conversation ─────────────────────────────────────────────────────

    [ConfigSection("Conversation")]
    public static bool EnableAncients { get; set; } = true;

    public static bool EnableMerchant { get; set; } = true;

    public static bool FreeTextInput { get; set; } = true;

    public static bool RememberAcrossRuns { get; set; } = true;

    public static bool UseAncientAffection { get; set; } = true;

    public static ReplyLanguage Language { get; set; } = ReplyLanguage.FollowGame;

    // ── Data ─────────────────────────────────────────────────────────────

    [ConfigSection("Data")]
    public static bool DebugLogging { get; set; } = false;

    [ConfigButton("ResetMemoriesBtn", Color = "#b03f3f")]
    public static void ResetMemories(ModConfig config)
    {
        TalkStore.Instance.ResetAll();
        ShowPopup(Loc.Ui("ResetDone"), Loc.Ui("ResetDoneBody"));
    }

    // ── Custom UI: account panel under the Yumina section ────────────────

    public override void SetupConfigUI(Control optionContainer)
    {
        base.SetupConfigUI(optionContainer);
        try
        {
            // Secrets render as dots. BaseLib has no "password" preset, so flip Godot's own flag.
            MaskLineEdit(optionContainer, nameof(OwnKey));
            MaskLineEdit(optionContainer, nameof(ApiToken));

            // The live account block belongs inside the Yumina section so it collapses and hides
            // with it; the bare container is the fallback if BaseLib ever renames its sections.
            var yuminaSection = FindNode(optionContainer, n => n is BaseLib.Config.UI.NConfigCollapsibleSection && n.Name.ToString().EndsWith("Yumina", StringComparison.Ordinal));
            var host = (yuminaSection as BaseLib.Config.UI.NConfigCollapsibleSection)?.ContentContainer ?? optionContainer;
            AccountPanel.Install(this, host);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Config] account panel failed: {ex.Message}", 1);
        }
    }

    private static void MaskLineEdit(Control container, string rowName)
    {
        var row = FindNode(container, n => n is BaseLib.Config.UI.NConfigOptionRow && n.Name == rowName);
        if (row == null) return;
        if (FindNode(row, n => n is LineEdit) is LineEdit edit)
        {
            edit.Secret = true;
        }
    }

    private static Node? FindNode(Node root, Func<Node, bool> match)
    {
        foreach (var child in root.GetChildren())
        {
            if (match(child)) return child;
            var deeper = FindNode(child, match);
            if (deeper != null) return deeper;
        }
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Expose BaseLib's themed button factory to the account panel.</summary>
    internal Control MakeButton(string label, Action onPressed)
    {
        var button = CreateRawButtonControl(label, onPressed);
        button.CustomMinimumSize = new Vector2(220f, button.CustomMinimumSize.Y);
        return button;
    }

    /// <summary>Push the current URL/token into the shared client. Called on load and on every change.</summary>
    public static void ApplyToClient()
    {
        MainFile.Client.Configure(Mode == BackendMode.Offline ? MockYumina.Marker : ServerUrl, ApiToken);
    }

    private static async Task TestConnectionAsync()
    {
        ApplyToClient();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var me = await MainFile.Client.WhoAmIAsync(cts.Token);
            var manifest = await MainFile.Client.GetManifestAsync(cts.Token);
            MainFile.Manifest = manifest;
            var credits = me.Credits.HasValue ? me.Credits.Value.ToString() : "?";
            AccountPanel.Refresh();
            ShowPopup(Loc.Ui("TestOk"), string.Format(Loc.Ui("TestOkBody"), me.Name, credits, manifest.Worlds.Count));
        }
        catch (Exception ex)
        {
            ShowPopup(Loc.Ui("TestFailed"), Loc.DescribeError(ex));
        }
    }

    private static async Task TestOwnKeyAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var probe = await MainFile.Direct.ProbeAsync(cts.Token);
            ShowPopup(Loc.Ui("TestOk"), string.Format(Loc.Ui("TestOwnOkBody"), OwnBaseUrl, OwnModel, probe));
        }
        catch (Exception ex)
        {
            ShowPopup(Loc.Ui("TestFailed"), Loc.DescribeError(ex));
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
