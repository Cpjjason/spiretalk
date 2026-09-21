using System.Diagnostics;
using System.Reflection;
using Jint;
using Jint.Native;

namespace SpireTalk.Engine;

/// <summary>
/// Hosts the Yumina engine (packages/engine, bundled to Engine/yumina-runtime.js) inside the game via Jint.
/// Everything that touches Jint types lives here so MainFile can register the assembly resolver before the
/// JIT ever needs Jint.dll — see <see cref="MainFile.ResolveBundledAssembly"/>.
/// </summary>
public sealed class YuminaRuntime
{
    private static YuminaRuntime? _instance;
    public static YuminaRuntime Instance => _instance ??= Load();

    private readonly Jint.Engine _js;
    private readonly JsValue _api;

    private YuminaRuntime(Jint.Engine js, JsValue api)
    {
        _js = js;
        _api = api;
    }

    private static YuminaRuntime Load()
    {
        var sw = Stopwatch.StartNew();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SpireTalk.Engine.yumina-runtime.js")
            ?? throw new InvalidOperationException("yumina-runtime.js is not embedded in SpireTalk.dll");
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        var js = new Jint.Engine(o => o.Strict(false).LimitRecursion(512));
        js.Execute(source, "yumina-runtime.js");
        js.Execute("function __toJson(v){ return JSON.stringify(v); } function __fromJson(s){ return JSON.parse(s); }");
        var api = js.GetValue("Yumina");
        MainFile.Logger.Info($"[Engine] Yumina runtime loaded in {sw.ElapsedMilliseconds} ms ({source.Length / 1024} KB)", 1);
        return new YuminaRuntime(js, api);
    }

    /// <summary>Parse a JSON document into a live JS value (a card, a state snapshot).</summary>
    public JsValue FromJson(string json) => _js.Invoke("__fromJson", json);

    public string ToJson(JsValue value) => _js.Invoke("__toJson", value).AsString();

    public JsValue Call(string fn, params JsValue[] args)
    {
        var f = _api.AsObject().Get(fn);
        return _js.Invoke(f, _api, args);
    }

    /// <summary>Self-check used by the in-game load test: build a prompt and parse a directive line.</summary>
    public string SelfTest(string cardJson)
    {
        var sw = Stopwatch.StartNew();
        var world = FromJson(cardJson);
        var state = Call("createState", world);
        var vars = FromJson("""{"language":"zh","hp":60,"max_hp":80,"gold":99,"affection":12,"lines_left":3}""");
        state = Call("setVars", world, state, vars);
        var msgs = Call("buildMessages", world, state, FromJson("[]"), "hello");
        var built = sw.ElapsedMilliseconds;
        var r = Call("parseAndApply", world, state, "...hello... [affection: add 2]");
        var json = ToJson(r);
        return $"prompt {built} ms, parse+apply {sw.ElapsedMilliseconds - built} ms, messages={ToJson(msgs).Length} chars, result={json[..Math.Min(160, json.Length)]}";
    }
}
