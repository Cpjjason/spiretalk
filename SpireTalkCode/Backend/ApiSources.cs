using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace SpireTalk.Backend;

/// <summary>Where the replies come from. The settings page picks one; the client only reads the resulting URL/key/model.</summary>
public enum ApiSource
{
    OpenRouter,
    OpenAI,
    Anthropic,
    Google,
    DeepSeek,
    XAI,
    Mistral,
    Groq,
    Moonshot,
    SiliconFlow,
    DashScope,
    Zhipu,
    Ollama,
    Custom,
}

/// <summary>
/// The sources the settings page offers — the same providers Yumina's own "private API" card lists,
/// plus the ones players in China reach without a proxy — their fixed endpoints, and the models each
/// lists. Every one of them speaks the OpenAI chat-completions API, so the client needs nothing per
/// provider beyond the address. OpenRouter gets a short hand-picked list (its catalogue has
/// hundreds); the others are asked what they serve once a key is in.
/// </summary>
public static class ApiSources
{
    public sealed record Preset(ApiSource Source, string Url, string? KeysPage, string KeysSite, bool CanList, (string Id, string Label)[] Models);

    public const string OpenRouterUrl = "https://openrouter.ai/api/v1";
    public const string OllamaUrl = "http://localhost:11434/v1";
    public const string OpenRouterKeysPage = "https://openrouter.ai/keys";

    /// <summary>Checked against openrouter.ai/api/v1/models on 2026-09-22. First entry is the default.</summary>
    public static readonly (string Id, string Label)[] OpenRouterModels =
    {
        ("deepseek/deepseek-v4.1-flash", "DeepSeek V4.1 Flash"),
        ("google/gemini-3.8-flash", "Gemini 3.8 Flash"),
        ("qwen/qwen3.8-flash", "Qwen 3.8 Flash"),
        ("z-ai/glm-5.3-flash", "GLM 5.3 Flash"),
        ("openai/gpt-6-luna", "GPT-6 Luna"),
        ("moonshotai/kimi-k3", "Kimi K3"),
        ("anthropic/claude-sonnet-5", "Claude Sonnet 5"),
    };

    private static readonly (string, string)[] None = Array.Empty<(string, string)>();

    public static readonly Preset[] Presets =
    {
        new(ApiSource.OpenRouter, OpenRouterUrl, OpenRouterKeysPage, "OpenRouter", false, OpenRouterModels),
        new(ApiSource.OpenAI, "https://api.openai.com/v1", "https://platform.openai.com/api-keys", "OpenAI", true, None),
        // Anthropic's OpenAI-compatible endpoint; its model list needs Anthropic's own headers, so it is fixed here.
        new(ApiSource.Anthropic, "https://api.anthropic.com/v1", "https://console.anthropic.com/settings/keys", "Anthropic", false, new[]
        {
            ("claude-haiku-4-5", "Claude Haiku 4.5"),
            ("claude-sonnet-5", "Claude Sonnet 5"),
            ("claude-opus-5-5", "Claude Opus 5.5"),
        }),
        new(ApiSource.Google, "https://generativelanguage.googleapis.com/v1beta/openai", "https://aistudio.google.com/apikey", "Google AI Studio", true, None),
        new(ApiSource.DeepSeek, "https://api.deepseek.com/v1", "https://platform.deepseek.com/api_keys", "DeepSeek", true, None),
        new(ApiSource.XAI, "https://api.x.ai/v1", "https://console.x.ai", "xAI", true, None),
        new(ApiSource.Mistral, "https://api.mistral.ai/v1", "https://console.mistral.ai/api-keys", "Mistral", true, None),
        new(ApiSource.Groq, "https://api.groq.com/openai/v1", "https://console.groq.com/keys", "Groq", true, None),
        new(ApiSource.Moonshot, "https://api.moonshot.cn/v1", "https://platform.moonshot.cn/console/api-keys", "Moonshot", true, None),
        new(ApiSource.SiliconFlow, "https://api.siliconflow.cn/v1", "https://cloud.siliconflow.cn/account/ak", "SiliconFlow", true, None),
        new(ApiSource.DashScope, "https://dashscope.aliyuncs.com/compatible-mode/v1", "https://bailian.console.aliyun.com/?apiKey=1", "DashScope", true, None),
        new(ApiSource.Zhipu, "https://open.bigmodel.cn/api/paas/v4", "https://open.bigmodel.cn/usercenter/apikeys", "BigModel", true, None),
    };

    public static Preset? PresetOf(ApiSource source) => Presets.FirstOrDefault(p => p.Source == source);

    public static ApiSource Infer(string? baseUrl)
    {
        var url = (baseUrl ?? "").Trim().TrimEnd('/');
        if (url.Length == 0) return ApiSource.OpenRouter;
        if (url.Contains(":11434", StringComparison.Ordinal)) return ApiSource.Ollama;
        foreach (var p in Presets)
        {
            if (url.StartsWith(p.Url, StringComparison.OrdinalIgnoreCase)) return p.Source;
        }
        return ApiSource.Custom;
    }

    public static string UrlFor(ApiSource source) => source switch
    {
        ApiSource.Ollama => OllamaUrl,
        ApiSource.Custom => (SpireTalkConfig.CustomBaseUrl ?? "").Trim(),
        _ => PresetOf(source)?.Url ?? OpenRouterUrl,
    };

    /// <summary>Whether the page should ask this source for its models right now.</summary>
    public static bool ShouldFetch(ApiSource source) => source switch
    {
        ApiSource.Ollama => true,
        ApiSource.Custom => !string.IsNullOrWhiteSpace(SpireTalkConfig.CustomBaseUrl),
        _ => PresetOf(source)?.CanList == true && !string.IsNullOrWhiteSpace(SpireTalkConfig.OwnKey),
    };

    /// <summary>The fixed list for sources that have one (OpenRouter, Anthropic); null for sources that are asked.</summary>
    public static List<(string Id, string Label)>? FixedModels(ApiSource source)
    {
        var p = PresetOf(source);
        return p != null && !p.CanList && p.Models.Length > 0 ? p.Models.ToList() : null;
    }

    /// <summary>
    /// Models the source serves, as (id, label). Fixed-list sources answer from their list; Ollama
    /// and the rest are asked. Empty when they cannot be reached.
    /// </summary>
    public static async Task<List<(string Id, string Label)>> ListModelsAsync(ApiSource source, CancellationToken ct)
    {
        var fixedList = FixedModels(source);
        if (fixedList != null) return fixedList;
        var result = new List<(string, string)>();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            if (source == ApiSource.Ollama)
            {
                var json = await http.GetStringAsync(OllamaUrl[..^3] + "/api/tags", ct);
                foreach (var m in JsonNode.Parse(json)?["models"] as JsonArray ?? new JsonArray())
                {
                    var name = m?["name"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(name)) continue;
                    var size = m?["details"]?["parameter_size"]?.GetValue<string>();
                    result.Add((name, string.IsNullOrEmpty(size) ? name : $"{name}  ·  {size}"));
                }
            }
            else
            {
                var url = UrlFor(source).TrimEnd('/');
                if (url.Length == 0) return result;
                using var req = new HttpRequestMessage(HttpMethod.Get, url + "/models");
                var key = (SpireTalkConfig.OwnKey ?? "").Trim();
                if (key.Length > 0) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                using var res = await http.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return result;
                var json = await res.Content.ReadAsStringAsync(ct);
                foreach (var m in JsonNode.Parse(json)?["data"] as JsonArray ?? new JsonArray())
                {
                    var id = m?["id"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(id)) continue;
                    if (id.StartsWith("models/", StringComparison.Ordinal)) id = id["models/".Length..]; // Google
                    if (!LooksLikeChatModel(id)) continue;
                    result.Add((id, id));
                }
                result.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
                // A dropdown is for picking, not browsing; past this the "other" entry is the way in.
                if (result.Count > 40) result.RemoveRange(40, result.Count - 40);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            MainFile.Logger.Info($"[Config] model list for {source} unavailable: {ex.Message}");
        }
        return result;
    }

    /// <summary>Providers list their embedding, speech and image models too; those cannot hold a conversation.</summary>
    private static bool LooksLikeChatModel(string id)
    {
        var s = id.ToLowerInvariant();
        string[] skip = { "embed", "whisper", "tts", "dall-e", "image", "moderation", "rerank", "audio", "transcribe", "realtime", "speech", "bge-", "flux", "stable-diffusion", "kolors", "cogview", "cogvideo" };
        return !skip.Any(s.Contains);
    }
}
