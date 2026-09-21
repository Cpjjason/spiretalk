using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpireTalk.Backend;

/// <summary>
/// The one network call the mod makes: chat completions against any OpenAI-compatible endpoint
/// with the player's own key — OpenRouter (default), OpenAI, a local Ollama
/// (http://localhost:11434/v1, key optional), or anything that speaks the same dialect.
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

    public bool IsConfigured
    {
        get
        {
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
            var raw = (SpireTalkConfig.OwnBaseUrl ?? "").Trim().TrimEnd('/');
            if (raw.Length == 0) raw = "https://openrouter.ai/api/v1";
            if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                raw = "https://" + raw;
            return raw;
        }
    }

    public static string Model => string.IsNullOrWhiteSpace(SpireTalkConfig.OwnModel) ? "google/gemini-2.5-flash-lite" : SpireTalkConfig.OwnModel.Trim();

    private static bool LooksLikeOllama => BaseUrl.Contains(":11434", StringComparison.Ordinal) || BaseUrl.Contains("ollama", StringComparison.OrdinalIgnoreCase);
    private static bool LooksLikeOpenRouter => BaseUrl.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase);

    /// <summary>One completion for the messages the engine assembled; one retry on the failures that usually clear themselves.</summary>
    public async Task<string> CompleteAsync(JsonArray messages, int maxTokens, double temperature, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
        };
        if (LooksLikeOllama)
        {
            // Ollama's thinking models spend the whole budget on reasoning and return no
            // content unless thinking is switched off. Other servers reject unknown fields.
            body["think"] = false;
        }
        else if (LooksLikeOpenRouter)
        {
            // Same problem with reasoning models behind OpenRouter: keep the tokens for the reply.
            body["reasoning"] = new JsonObject { ["exclude"] = true, ["effort"] = "low" };
        }

        try
        {
            return await PostAsync(body, ct);
        }
        catch (BackendException ex) when (!ct.IsCancellationRequested && IsTransient(ex))
        {
            MainFile.Logger.Warn($"[LLM] {ex.Code ?? "?"} {ex.Message} — retrying once", 1);
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
            return await PostAsync(body, ct);
        }
    }

    private static bool IsTransient(BackendException ex) =>
        ex.Code is "RATE_LIMITED" or "NETWORK" or "EMPTY" || ex.StatusCode is >= 500 and < 600;

    /// <summary>Connectivity probe for the settings page: lists models.</summary>
    public async Task<string> ProbeAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + "/models");
        AddAuth(req);
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

    private async Task<string> PostAsync(JsonObject body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/chat/completions");
        AddAuth(req);
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
                throw new BackendException(msg, (int)res.StatusCode, code);
            }
            var node = JsonNode.Parse(text);
            var content = node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new BackendException("The model returned an empty reply", null, "EMPTY");
            }
            return content;
        }
    }

    private static void AddAuth(HttpRequestMessage req)
    {
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
