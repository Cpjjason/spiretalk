using BaseLib.Config;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using SpireTalk.Store;
using SpireTalk.Yumina;

namespace SpireTalk;

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
/// Mod settings, rendered by BaseLib inside the vanilla mod-settings screen.
/// Labels live in localization/&lt;lang&gt;/settings_ui.json under the SPIRETALK- prefix.
/// </summary>
public sealed class SpireTalkConfig : SimpleModConfig
{
    // ── Account ──────────────────────────────────────────────────────────

    [ConfigSection("Account")]
    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string ServerUrl { get; set; } = "https://yumina.io";

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 200)]
    public static string ApiToken { get; set; } = "";

    [ConfigButton("TestConnectionBtn", Color = "#4a9d7a")]
    public static void TestConnection(ModConfig config)
    {
        _ = TestConnectionAsync();
    }

    // ── Conversation ─────────────────────────────────────────────────────

    [ConfigSection("Conversation")]
    public static bool EnableAncients { get; set; } = true;

    public static bool EnableMerchant { get; set; } = true;

    public static bool FreeTextInput { get; set; } = true;

    public static bool RememberAcrossRuns { get; set; } = true;

    public static ReplyLanguage Language { get; set; } = ReplyLanguage.FollowGame;

    [ConfigTextInput(TextInputPreset.Anything, MaxLength = 120)]
    public static string Model { get; set; } = "";

    // ── Data ─────────────────────────────────────────────────────────────

    [ConfigSection("Data")]
    public static bool DebugLogging { get; set; } = false;

    [ConfigButton("ResetMemoriesBtn", Color = "#b03f3f")]
    public static void ResetMemories(ModConfig config)
    {
        TalkStore.Instance.ResetAll();
        ShowPopup(Loc.Ui("ResetDone"), Loc.Ui("ResetDoneBody"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Push the current URL/token into the shared client. Called on load and on every change.</summary>
    public static void ApplyToClient()
    {
        MainFile.Client.Configure(ServerUrl, ApiToken);
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
            ShowPopup(Loc.Ui("TestOk"), string.Format(Loc.Ui("TestOkBody"), me.Name, credits, manifest.Worlds.Count));
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
