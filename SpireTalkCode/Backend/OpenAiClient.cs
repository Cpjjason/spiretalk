using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpireTalk.Backend;

/// <summary>
/// The one network call the mod makes: chat completions against any OpenAI-compatible endpoint
/// with the player's own key — OpenRouter, OpenAI, a local Ollama (http://localhost:11434/v1, key
/// optional), or anything that speaks the same dialect — or Yumina's AI gateway, where the bearer
/// is the signed-in account's short-lived token (<see cref="YuminaAccount"/>).
/// </summary>
public sealed class OpenAiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public OpenAiClient()
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(120),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SpireTalk/" + MainFile.Version + " (Slay the Spire 2 mod)");
    }

    private static bool OnYumina => SpireTalkConfig.Source == ApiSource.Yumina;

    public bool IsConfigured
    {
        get
        {
            if (OnYumina) return YuminaAccount.Linked;
            var url = BaseUrl;
            if (string.IsNullOrWhiteSpace(url)) return false;
            // Local servers usually need no key; hosted ones do.
            var local = url.Contains("localhost", StringComparison.OrdinalIgnoreCase) || url.Contains("127.0.0.1");
            return local || !string.IsNullOrWhiteSpace(SpireTalkConfig.OwnKey);
        }
    }

    public static string BaseUrl
    {
        get
        {
            if (OnYumina) return YuminaAccount.ApiBase;
            var raw = (SpireTalkConfig.OwnBaseUrl ?? "").Trim().TrimEnd('/');
            if (raw.Length == 0) raw = "https://openrouter.ai/api/v1";
            if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                raw = "https://" + raw;
            return raw;
        }
    }

    /// <summary>The model a line goes to: the player's pick, unless Yumina could not take it a moment ago (see Bench).</summary>
    public static string Model
    {
        get
        {
            var m = Picked;
            return m == _benched && DateTime.UtcNow < _benchedUntil && Fallback is { } f ? f : m;
        }
    }

    private static string Picked => OnYumina
        ? (string.IsNullOrWhiteSpace(SpireTalkConfig.YuminaModel) ? YuminaAccount.Current?.DefaultModel ?? "" : SpireTalkConfig.YuminaModel.Trim())
        : string.IsNullOrWhiteSpace(SpireTalkConfig.OwnModel) ? "google/gemini-2.5-flash-lite" : SpireTalkConfig.OwnModel.Trim();

    // Yumina's free line is shared by every player on the site, and now and then a model there is
    // down for a while. When the pick still fails after its retry, lines go to the account's default
    // model for ten minutes (and the player is told once). Own keys never switch: that's their bill.
    private static string? _benched;
    private static DateTime _benchedUntil;
    private static string? _fellBack;

    private static string? Fallback
    {
        get
        {
            if (!OnYumina) return null;
            var d = YuminaAccount.Current?.DefaultModel;
            return string.IsNullOrWhiteSpace(d) ? null : d;
        }
    }

    private static bool Bench(BackendException ex) =>
        (ex.StatusCode is >= 500 || ex.Code is "TIMEOUT" or "EMPTY") && Fallback is { } f && Model != f;

    /// <summary>The model whose failure moved this line to the default one, once; null when nothing moved.</summary>
    public static string? TakeFallBack() => Interlocked.Exchange(ref _fellBack, null);

    private static bool LooksLikeOllama => BaseUrl.Contains(":11434", StringComparison.Ordinal) || BaseUrl.Contains("ollama", StringComparison.OrdinalIgnoreCase);
    private static bool LooksLikeOpenRouter => BaseUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase);
    // Gemma on the same endpoint does not take reasoning_effort.
    private static bool LooksLikeGoogleGemini => BaseUrl.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase)
        && Model.Contains("gemini", StringComparison.OrdinalIgnoreCase);

    /// <summary>One completion for the messages the engine assembled; see <see cref="CompleteOnceAsync"/> and Bench.</summary>
    public async Task<string> CompleteAsync(JsonArray messages, int maxTokens, double temperature, CancellationToken ct)
    {
        // Signed in, but the account (and so its default model) has not loaded yet: load it first.
        if (OnYumina && Model.Length == 0) await YuminaAccount.RefreshAsync();
        try
        {
            return await CompleteOnceAsync(messages, maxTokens, temperature, ct);
        }
        catch (BackendException ex) when (!ct.IsCancellationRequested && Bench(ex))
        {
            var from = Model;
            _benched = from;
            _benchedUntil = DateTime.UtcNow.AddMinutes(10);
            Interlocked.Exchange(ref _fellBack, from);
            MainFile.Logger.Warn($"[LLM] {from} failed ({ex.StatusCode?.ToString() ?? ex.Code}) after its retry; lines go to {Model} for 10 minutes", 1);
            return await CompleteOnceAsync(messages, maxTokens, temperature, ct);
        }
    }

    /// <summary>One completion; one retry on the failures that usually clear themselves.</summary>
    private async Task<string> CompleteOnceAsync(JsonArray messages, int maxTokens, double temperature, CancellationToken ct)
    {
        // An NPC line needs no chain of thought, and the reply only shows once it is complete, so
        // thinking is pure waiting: Qwen on OpenRouter spends 10-20 s on it even at "low". Ask every
        // source that has a switch to skip it; models that refuse (Gemini, GLM: "Reasoning is
        // mandatory") answer 400, and are asked again the old way and remembered for the session.
        var thinkingKey = BaseUrl + "|" + Model;
        var skipThinking = !ThinkingRequired.ContainsKey(thinkingKey);
        var body = BuildBody(messages, maxTokens, temperature, skipThinking);

        try
        {
            return await PostAsync(body, ct);
        }
        catch (BackendException ex) when (!ct.IsCancellationRequested && skipThinking && ex.StatusCode is 400 or 422 && SkipsThinking(body))
        {
            MainFile.Logger.Info($"[LLM] {Model} will not skip thinking ({ex.Message}); asking with light thinking instead", 1);
            ThinkingRequired[thinkingKey] = true;
            return await CompleteOnceAsync(messages, maxTokens, temperature, ct);
        }
        catch (BackendException ex) when (!ct.IsCancellationRequested && IsTransient(ex))
        {
            MainFile.Logger.Warn($"[LLM] {ex.Code ?? "?"} {ex.Message} — retrying once", 1);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return await PostAsync(body, ct);
        }
    }

    /// <summary>Endpoint|model pairs that rejected "no thinking" this session.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> ThinkingRequired = new();

    private static readonly string[] EnableThinkingHosts = { "dashscope.aliyuncs.com", "api.siliconflow.cn" };
    private static readonly string[] ThinkingTypeHosts = { "open.bigmodel.cn", "api.deepseek.com", "api.moonshot.cn" };

    private static bool HostIs(params string[] hosts) => hosts.Any(h => BaseUrl.Contains(h, StringComparison.OrdinalIgnoreCase));

    private static bool SkipsThinking(JsonObject body) =>
        body["reasoning"]?["effort"]?.GetValue<string>() == "none"
        || body.ContainsKey("enable_thinking")
        || body.ContainsKey("thinking")
        || body["reasoning_effort"]?.GetValue<string>() == "none";

    private static JsonObject BuildBody(JsonArray messages, int maxTokens, double temperature, bool skipThinking)
    {
        var body = new JsonObject
        {
            // The same messages may be sent twice (the thinking fallback); a node can only have one parent.
            ["model"] = Model,
            ["messages"] = messages.DeepClone(),
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
        };
        if (LooksLikeOllama)
        {
            // Ollama ignores `think` on its OpenAI-compatible route, so thinking models (qwen3,
            // deepseek-r1) burn the whole budget on reasoning and return nothing. Its native
            // /api/chat honours it; same messages, different envelope.
            body.Remove("max_tokens");
            body.Remove("temperature");
            body["stream"] = false;
            body["think"] = false;
            body["options"] = new JsonObject
            {
                ["num_predict"] = maxTokens,
                ["temperature"] = temperature,
                // Size the context to the prompt: Ollama's server default is either too small for our
                // prompts (silently truncated) or 32k (a KV cache of several GB that pushes a 27B model
                // out of VRAM on a 24 GB card and makes every call crawl).
                ["num_ctx"] = OllamaContextFor(messages, maxTokens),
            };
        }
        else if (LooksLikeOpenRouter)
        {
            // Keep the tokens for the reply; models that must think, think lightly.
            body["reasoning"] = new JsonObject { ["exclude"] = true, ["effort"] = skipThinking ? "none" : "low" };
        }
        else if (LooksLikeGoogleGemini)
        {
            // Google's own endpoint: Gemini 3 and 2.5 Pro always think, at the model's default depth
            // unless told otherwise, and the thinking counts against max_tokens, so the reply can come
            // back empty. Think lightly, and leave room for it on top of the reply's own budget.
            body["reasoning_effort"] = "low";
            body["max_tokens"] = maxTokens + 2048;
        }
        else if (skipThinking && HostIs(EnableThinkingHosts))
        {
            body["enable_thinking"] = false; // Qwen 3 on Bailian / SiliconFlow
        }
        else if (skipThinking && HostIs(ThinkingTypeHosts))
        {
            body["thinking"] = new JsonObject { ["type"] = "disabled" }; // GLM, DeepSeek, Kimi
        }
        else if (skipThinking && HostIs("api.openai.com") && LooksLikeOpenAiReasoner)
        {
            body["reasoning_effort"] = "none";
        }
        return body;
    }

    private static bool LooksLikeOpenAiReasoner
    {
        get
        {
            var m = Model.ToLowerInvariant();
            return m.StartsWith("gpt-5") || m.StartsWith("gpt-6") || (m.Length > 1 && m[0] == 'o' && char.IsDigit(m[1]));
        }
    }

    /// <summary>Prompt characters over 2 is a safe token over-estimate for CJK and English alike.</summary>
    private static int OllamaContextFor(JsonArray messages, int maxTokens)
    {
        long chars = 0;
        foreach (var m in messages)
        {
            chars += m?["content"]?.ToString().Length ?? 0;
        }
        var needed = (int)(chars / 2 + maxTokens + 512);
        var ctx = 8192;
        while (ctx < needed && ctx < 65536) ctx *= 2;
        return ctx;
    }

    private static bool IsTransient(BackendException ex) =>
        ex.Code is "RATE_LIMITED" or "NETWORK" or "EMPTY" || ex.StatusCode is >= 500 and < 600;

    /// <summary>Connectivity probe for the settings page: lists models.</summary>
    public async Task<string> ProbeAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/models");
        await AddAuth(req);
        try
        {
            using var res = await _http.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                var json = await res.Content.ReadAsStringAsync(ct);
                var count = (JsonNode.Parse(json)?["data"] as JsonArray)?.Count ?? 0;
                return count > 0 ? $"{count} models" : "ok";
            }
            return $"HTTP {(int)res.StatusCode}";
        }
        catch (HttpRequestException ex)
        {
            throw new BackendException($"Cannot reach {BaseUrl}: {ex.Message}", null, "NETWORK", ex);
        }
    }

    /// <summary>Ollama's native chat endpoint lives beside its OpenAI-compatible /v1.</summary>
    private static string OllamaChatUrl
    {
        get
        {
            var root = BaseUrl;
            if (root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) root = root[..^3];
            return root + "/api/chat";
        }
    }

    private async Task<string> PostAsync(JsonObject body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, LooksLikeOllama ? OllamaChatUrl : BaseUrl + "/chat/completions");
        await AddAuth(req);
        req.Content = new StringContent(body.ToJsonString(JsonOpts), Encoding.UTF8, "application/json");
        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new BackendException($"Cannot reach {BaseUrl}: {ex.Message}", null, "NETWORK", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new BackendException("The model took too long to answer", null, "TIMEOUT", ex);
        }
        using (res)
        {
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                var msg = TryReadError(text) ?? res.ReasonPhrase ?? ((int)res.StatusCode).ToString();
                var code = (int)res.StatusCode switch
                {
                    401 or 403 => "UNAUTHORIZED",
                    402 => "NO_CREDITS",
                    429 => "RATE_LIMITED",
                    _ => null,
                };
                if (OnYumina && (int)res.StatusCode is 401 or 402)
                {
                    // Our own words (top up / sign in again), not the gateway's English.
                    var noCredits = (int)res.StatusCode == 402;
                    throw new BackendException(Loc.Ui(noCredits ? "YmNoCredits" : "YmRelink"), (int)res.StatusCode, noCredits ? "YUMINA_NO_CREDITS" : "YUMINA_RELINK");
                }
                throw new BackendException(msg, (int)res.StatusCode, code);
            }
            var node = JsonNode.Parse(text);
            if (OnYumina && node?["yumina"]?["balance"] is JsonValue left && left.TryGetValue<double>(out var balance))
            {
                YuminaAccount.NoteBalance(balance);
            }
            var content = LooksLikeOllama
                ? node?["message"]?["content"]?.GetValue<string>()
                : node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new BackendException("The model returned an empty reply", null, "EMPTY");
            }
            return content;
        }
    }

    private static async Task AddAuth(HttpRequestMessage req)
    {
        if (OnYumina)
        {
            // Our own endpoint: the account's token, no OpenRouter attribution headers.
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await YuminaAccount.AccessTokenAsync());
            return;
        }
        var key = (SpireTalkConfig.OwnKey ?? "").Trim();
        if (key.Length > 0) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        // OpenRouter attribution headers are harmless elsewhere.
        req.Headers.TryAddWithoutValidation("HTTP-Referer", "https://github.com/lovetimo0421/yumina-oss");
        req.Headers.TryAddWithoutValidation("X-Title", "SpireTalk (Slay the Spire 2)");
    }

    private static string? TryReadError(string text)
    {
        try
        {
            var n = JsonNode.Parse(text);
            return n?["error"]?["message"]?.GetValue<string>() ?? n?["error"]?.GetValue<string>() ?? n?["message"]?.GetValue<string>();
        }
        catch
        {
            return null;
        }
    }
}
