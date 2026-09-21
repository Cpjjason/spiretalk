using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SpireTalk.Backend;
using SpireTalk.Yumina;

namespace SpireTalk;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireTalk";
    public const string Version = "0.2.0";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    /// <summary>Shared Yumina HTTP client; URL/token are pushed in from the settings.</summary>
    public static YuminaClient Client { get; } = new();

    /// <summary>Own-key backend (OpenRouter / OpenAI / Ollama / compatible).</summary>
    public static DirectBackend Direct { get; } = new();

    private static readonly YuminaBackend YuminaBackendInstance = new(Client);
    private static readonly MockBackend MockBackendInstance = new();

    /// <summary>The backend the settings currently point at.</summary>
    public static INpcBackend Backend => SpireTalkConfig.Mode switch
    {
        BackendMode.Yumina => YuminaBackendInstance,
        BackendMode.Offline => MockBackendInstance,
        _ => Direct,
    };

    /// <summary>NPC → world mapping published by the server. Null until fetched.</summary>
    public static IntegrationManifest? Manifest { get; set; }

    private static DateTime _manifestFetchedUtc = DateTime.MinValue;
    private static Task<IntegrationManifest?>? _manifestInFlight;

    /// <summary>
    /// Last-known-good mapping shipped with the mod. The server manifest wins when reachable, so
    /// worlds can be re-pointed without shipping a new mod build.
    /// </summary>
    private static readonly Dictionary<string, string> BundledWorlds = new(StringComparer.OrdinalIgnoreCase)
    {
        // Filled in once the official worlds are published on yumina.io.
    };

    public static void Initialize()
    {
        var config = new SpireTalkConfig();
        ModConfigRegistry.Register(ModId, config);
        config.ConfigChanged += (_, _) =>
        {
            SpireTalkConfig.ApplyToClient();
            AccountPanel.UpdateVisibility();
        };
        config.OnConfigReloaded += SpireTalkConfig.ApplyToClient;
        SpireTalkConfig.ApplyToClient();

        var harmony = new Harmony("io.yumina.spiretalk");
        harmony.PatchAll(typeof(MainFile).Assembly);

        Logger.Info($"[Init] SpireTalk {Version} ready. mode={SpireTalkConfig.Mode} backend={Backend.DisplayName} configured={Backend.IsConfigured}", 1);
    }

    /// <summary>Resolve which Yumina world an NPC talks through: server manifest first, bundled fallback second.</summary>
    public static async Task<string?> ResolveWorldIdAsync(string npcKey, CancellationToken ct)
    {
        var manifest = await GetManifestCachedAsync(ct);
        if (manifest != null && manifest.Worlds.TryGetValue(npcKey, out var id) && !string.IsNullOrEmpty(id))
        {
            return id;
        }
        return BundledWorlds.TryGetValue(npcKey, out var bundled) ? bundled : null;
    }

    private static async Task<IntegrationManifest?> GetManifestCachedAsync(CancellationToken ct)
    {
        if (Manifest != null && DateTime.UtcNow - _manifestFetchedUtc < TimeSpan.FromMinutes(30))
        {
            return Manifest;
        }
        var inflight = _manifestInFlight;
        if (inflight == null)
        {
            inflight = FetchManifestAsync();
            _manifestInFlight = inflight;
        }
        try
        {
            var done = await Task.WhenAny(inflight, Task.Delay(Timeout.Infinite, ct));
            if (done != inflight) ct.ThrowIfCancellationRequested();
            return await inflight;
        }
        finally
        {
            if (ReferenceEquals(_manifestInFlight, inflight)) _manifestInFlight = null;
        }
    }

    private static async Task<IntegrationManifest?> FetchManifestAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var manifest = await Client.GetManifestAsync(cts.Token);
            Manifest = manifest;
            _manifestFetchedUtc = DateTime.UtcNow;
            if (SpireTalkConfig.DebugLogging)
            {
                Logger.Info($"[Manifest] {manifest.Worlds.Count} world(s), defaultModel={manifest.DefaultModel ?? "-"}", 1);
            }
            return manifest;
        }
        catch (Exception ex)
        {
            Logger.Warn($"[Manifest] fetch failed, using bundled mapping: {ex.Message}", 1);
            return Manifest;
        }
    }
}
