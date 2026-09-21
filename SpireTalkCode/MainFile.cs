using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SpireTalk.Backend;
using SpireTalk.Engine;

namespace SpireTalk;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireTalk";
    public const string Version = "0.3.0";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    /// <summary>The Yumina engine in-process plus the player's own OpenAI-compatible key.</summary>
    public static EngineBackend Backend { get; } = new();

    public static void Initialize()
    {
        // The game loads only SpireTalk.dll (default AssemblyLoadContext, LoadFromAssemblyPath), so its
        // dependencies next to it are invisible to the runtime until we point at them. Registered before
        // anything that references Jint gets JIT-compiled.
        AppDomain.CurrentDomain.AssemblyResolve += ResolveBundledAssembly;

        var config = new SpireTalkConfig();
        ModConfigRegistry.Register(ModId, config);

        var harmony = new Harmony("io.yumina.spiretalk");
        harmony.PatchAll(typeof(MainFile).Assembly);

#if SPIRETALK_DEV
        // Developer automation bridge — dev builds only (dotnet build -p:SpireTalkDev=true), and
        // even then only when the flag file exists (see Dev/TestBridge.cs).
        Dev.TestBridge.Install();
#endif

        Logger.Info($"[Init] SpireTalk {Version} ready. backend={Backend.DisplayName} configured={Backend.IsConfigured} cards={CardLibrary.All.Count}", 1);
        // Load the engine now rather than on the first conversation: 300 ms nobody notices at the menu.
        WarmEngine();
    }

    private static readonly string[] BundledAssemblies = { "Jint", "Acornima" };

    /// <summary>Resolve Jint and its parser from the mod folder (the game never probes there).</summary>
    public static System.Reflection.Assembly? ResolveBundledAssembly(object? sender, ResolveEventArgs args)
    {
        var name = new System.Reflection.AssemblyName(args.Name).Name;
        if (name == null || Array.IndexOf(BundledAssemblies, name) < 0) return null;
        var dir = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        if (dir == null) return null;
        var path = Path.Combine(dir, name + ".dll");
        if (!File.Exists(path))
        {
            Logger.Error($"[Engine] {name}.dll missing next to SpireTalk.dll ({dir})");
            return null;
        }
        return System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(MainFile).Assembly)!.LoadFromAssemblyPath(path);
    }

    private static void WarmEngine()
    {
        try
        {
            _ = YuminaRuntime.Instance;
        }
        catch (Exception ex)
        {
            Logger.Error($"[Engine] failed to load the Yumina runtime: {ex}");
        }
    }
}
