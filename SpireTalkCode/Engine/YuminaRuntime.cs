using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Jint;

namespace SpireTalk.Engine;

/// <summary>
/// Hosts the Yumina engine inside the game. Engine/yumina-runtime.js is packages/engine from the
/// Yumina repo bundled into one file (pnpm build → dist/yumina-runtime.js), embedded in this DLL
/// and run by Jint — a managed JS interpreter, so nothing native ships with the mod.
///
/// Everything crosses the boundary as JSON text: a card (Yumina WorldDefinition), a state snapshot,
/// the chat history, and back come the messages to send to the model and the parsed result.
/// The engine never talks to the network; <see cref="Backend.OpenAiClient"/> does that with the
/// player's own key. Everything that touches Jint types stays in this class so MainFile can
/// register the assembly resolver before the JIT needs Jint.dll.
/// </summary>
public sealed class YuminaRuntime
{
    private static YuminaRuntime? _instance;
    private static readonly object Gate = new();

    public static YuminaRuntime Instance
    {
        get
        {
            lock (Gate) return _instance ??= Load();
        }
    }

    private readonly Jint.Engine _js;
    public string Version { get; }

    private YuminaRuntime(Jint.Engine js, string version)
    {
        _js = js;
        Version = version;
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
        // JSON in, JSON out. Jint marshals strings for free; JS objects would need per-call conversion.
        js.Execute("""
            function __start(worldJson, varsJson) {
              return JSON.stringify(Yumina.startSession({ world: JSON.parse(worldJson), variables: JSON.parse(varsJson) }));
            }
            function __apply(worldJson, stateJson, varsJson) {
              return JSON.stringify(Yumina.applyVariables(JSON.parse(worldJson), JSON.parse(stateJson), JSON.parse(varsJson)));
            }
            function __build(worldJson, stateJson, historyJson, userText, model) {
              return JSON.stringify(Yumina.buildTurnPrompt({ world: JSON.parse(worldJson), state: JSON.parse(stateJson), history: JSON.parse(historyJson), userText: userText, model: model }));
            }
            function __finish(worldJson, stateJson, userText, rawText) {
              return JSON.stringify(Yumina.finishTurn({ world: JSON.parse(worldJson), state: JSON.parse(stateJson), userText: userText, rawText: rawText }));
            }
            """);
        var version = js.Evaluate("Yumina.RUNTIME_VERSION").AsString();
        MainFile.Logger.Info($"[Engine] Yumina runtime {version} loaded in {sw.ElapsedMilliseconds} ms ({source.Length / 1024} KB)", 1);
        return new YuminaRuntime(js, version);
    }

    // Jint engines are not thread-safe; turns run on the thread pool, so every call takes the lock.

    /// <summary>Open a session on a card: initial state (+ host variables) and the greeting.</summary>
    public StartedSession StartSession(string cardJson, JsonObject variables)
    {
        string json;
        lock (Gate) json = _js.Invoke("__start", cardJson, variables.ToJsonString()).AsString();
        var node = JsonNode.Parse(json)!.AsObject();
        return new StartedSession(node["state"]!.ToJsonString(), node["greeting"]?.GetValue<string>());
    }

    /// <summary>Push host-owned values (run facts, relationship numbers) into the state.</summary>
    public string ApplyVariables(string cardJson, string stateJson, JsonObject variables)
    {
        lock (Gate) return _js.Invoke("__apply", cardJson, stateJson, variables.ToJsonString()).AsString();
    }

    /// <summary>Assemble the messages for one turn. The returned state must be handed to <see cref="FinishTurn"/>.</summary>
    public TurnPrompt BuildTurnPrompt(string cardJson, string stateJson, JsonArray history, string userText, string model)
    {
        string json;
        lock (Gate) json = _js.Invoke("__build", cardJson, stateJson, history.ToJsonString(), userText, model).AsString();
        var node = JsonNode.Parse(json)!.AsObject();
        return new TurnPrompt(
            // Detached from the parsed document so the caller can put it straight into a request body.
            node["messages"]!.DeepClone().AsArray(),
            node["state"]!.ToJsonString(),
            node["maxTokens"]?.GetValue<int>() ?? 1024,
            node["temperature"]?.GetValue<double>() ?? 0.8);
    }

    /// <summary>Parse the model's reply, apply its directives and the card's behaviors.</summary>
    public TurnResult FinishTurn(string cardJson, string stateJson, string userText, string rawText)
    {
        string json;
        lock (Gate) json = _js.Invoke("__finish", cardJson, stateJson, userText, rawText).AsString();
        var node = JsonNode.Parse(json)!.AsObject();
        var state = node["state"]!.AsObject();
        return new TurnResult(
            node["cleanText"]?.GetValue<string>() ?? "",
            state.ToJsonString(),
            state["variables"] as JsonObject ?? new JsonObject(),
            node["changes"] as JsonArray ?? new JsonArray(),
            node["droppedEffects"] as JsonArray ?? new JsonArray());
    }

    public sealed record StartedSession(string StateJson, string? Greeting);

    public sealed record TurnPrompt(JsonArray Messages, string StateJson, int MaxTokens, double Temperature);

    public sealed record TurnResult(string CleanText, string StateJson, JsonObject Variables, JsonArray Changes, JsonArray DroppedEffects);
}
