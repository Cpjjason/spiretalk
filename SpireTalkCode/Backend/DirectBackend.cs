using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SpireTalk.Npc;
using SpireTalk.Yumina;

namespace SpireTalk.Backend;

/// <summary>
/// Talks to any OpenAI-compatible chat-completions endpoint with the player's own key:
/// OpenRouter (default), OpenAI, a local Ollama (http://localhost:11434/v1, key optional), or
/// anything else that speaks the same dialect. The mod builds the prompt itself and keeps the
/// conversation history locally, so no account anywhere is needed.
/// </summary>
public sealed class DirectBackend : INpcBackend
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public DirectBackend()
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(120),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SpireTalk/0.2 (Slay the Spire 2 mod)");
    }

    public string DisplayName => "Own key";

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

    private static string BaseUrl
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

    private static bool LooksLikeOllama => BaseUrl.Contains(":11434", StringComparison.Ordinal) || BaseUrl.Contains("ollama", StringComparison.OrdinalIgnoreCase);

    private static string Model => string.IsNullOrWhiteSpace(SpireTalkConfig.OwnModel) ? "google/gemini-2.5-flash-lite" : SpireTalkConfig.OwnModel.Trim();

    public async Task<NpcTurn> ExchangeAsync(VisitContext ctx, string playerLine, bool isOpener, CancellationToken ct)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = PromptBuilder.SystemPrompt(ctx) },
        };
        foreach (var h in ctx.History.TakeLast(PromptBuilder.HistoryLinesToSend))
        {
            messages.Add(new JsonObject { ["role"] = h.Role == "npc" ? "assistant" : "user", ["content"] = h.Text });
        }
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = playerLine });

        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages,
            ["temperature"] = 0.9,
            ["max_tokens"] = 700,
        };
        if (SpireTalkConfig.OwnJsonMode)
        {
            body["response_format"] = new JsonObject { ["type"] = "json_object" };
        }
        if (LooksLikeOllama)
        {
            // Ollama's thinking models spend the whole budget on reasoning and return no
            // content unless thinking is switched off. Other servers reject unknown fields.
            body["think"] = false;
        }

        var raw = await CompleteAsync(body, ct);
        return Parse(raw);
    }

    /// <summary>Connectivity probe for the settings page: lists models or does a 1-token completion.</summary>
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
            throw new YuminaException($"Cannot reach {BaseUrl}: {ex.Message}", null, "NETWORK", ex);
        }
    }

    private async Task<string> CompleteAsync(JsonObject body, CancellationToken ct)
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
            throw new YuminaException($"Cannot reach {BaseUrl}: {ex.Message}", null, "NETWORK", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new YuminaException("The model took too long to answer", null, "TIMEOUT", ex);
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
                throw new YuminaException(msg, (int)res.StatusCode, code);
            }
            var node = JsonNode.Parse(text);
            var content = node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new YuminaException("The model returned an empty reply", null, "EMPTY");
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

    /// <summary>Lenient parse of the JSON contract; falls back to plain text when the model ignored it.</summary>
    internal static NpcTurn Parse(string raw)
    {
        var text = raw.Trim();
        // Strip a stray code fence and any prose around the object.
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        JsonObject? obj = null;
        if (start >= 0 && end > start)
        {
            try
            {
                obj = JsonNode.Parse(text[start..(end + 1)]) as JsonObject;
            }
            catch
            {
                obj = null;
            }
        }
        if (obj == null)
        {
            // Plain prose: treat the whole thing as dialogue.
            var lines = Ancients.AncientConversation.SplitIntoLines(text);
            return new NpcTurn(lines, Array.Empty<string>(), 0, null, NpcAction.None, null, null);
        }

        var outLines = new List<string>();
        if (obj["lines"] is JsonArray la)
        {
            foreach (var n in la)
            {
                var s = n?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(s)) outLines.Add(s.Trim());
            }
        }
        else if (obj["text"]?.GetValue<string>() is string t)
        {
            outLines.AddRange(Ancients.AncientConversation.SplitIntoLines(t));
        }

        var choices = new List<string>();
        if (obj["choices"] is JsonArray ca)
        {
            foreach (var n in ca)
            {
                var s = n?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(s)) choices.Add(s.Trim());
            }
        }

        var delta = 0;
        if (obj["affection_delta"] is JsonValue dv)
        {
            if (dv.TryGetValue<int>(out var i)) delta = i;
            else if (dv.TryGetValue<double>(out var d)) delta = (int)Math.Round(d);
            else if (dv.TryGetValue<string>(out var s) && int.TryParse(s, out var p)) delta = p;
        }

        var action = NpcAction.None;
        if (obj["action"] is JsonObject ao)
        {
            var type = ao["type"]?.GetValue<string>() ?? "none";
            var amount = 0;
            if (ao["amount"] is JsonValue av)
            {
                if (av.TryGetValue<int>(out var i)) amount = i;
                else if (av.TryGetValue<double>(out var d)) amount = (int)Math.Round(d);
            }
            action = new NpcAction(type.Trim().ToLowerInvariant(), amount);
        }
        else if (obj["action"]?.GetValue<string>() is string at)
        {
            action = new NpcAction(at.Trim().ToLowerInvariant(), 0);
        }

        return new NpcTurn(outLines, choices, delta, obj["reason"]?.GetValue<string>(), action, null, null);
    }
}
