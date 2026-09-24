using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace SpireTalk.Backend;

/// <summary>Where the replies come from. The settings page picks one; the client only reads the resulting URL/key/model.</summary>
public enum ApiSource
{
    OpenRouter,
    Ollama,
    Custom,
}

/// <summary>
/// The sources the settings page offers, their fixed endpoints, and the models each lists.
/// OpenRouter gets a short hand-picked list (its catalogue has hundreds); a local Ollama and a
/// custom endpoint list whatever they actually serve.
/// </summary>
public static class ApiSources
{
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

    public static ApiSource Infer(string? baseUrl)
    {
        var url = (baseUrl ?? "").Trim();
        if (url.Length == 0 || url.Contains("openrouter.ai", StringComparison.OrdinalIgnoreCase)) return ApiSource.OpenRouter;
        if (url.Contains(":11434", StringComparison.Ordinal)) return ApiSource.Ollama;
        return ApiSource.Custom;
    }

    public static string UrlFor(ApiSource source) => source switch
    {
        ApiSource.OpenRouter => OpenRouterUrl,
        ApiSource.Ollama => OllamaUrl,
        _ => (SpireTalkConfig.CustomBaseUrl ?? "").Trim(),
    };

    /// <summary>
    /// Models the source serves, as (id, label). OpenRouter answers from the list above; Ollama
    /// and custom endpoints are asked. Empty when they cannot be reached.
    /// </summary>
    public static async Task<List<(string Id, string Label)>> ListModelsAsync(ApiSource source, CancellationToken ct)
    {
        if (source == ApiSource.OpenRouter) return OpenRouterModels.ToList();
        var result = new List<(string, string)>();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
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
                    if (!string.IsNullOrEmpty(id)) result.Add((id, id));
                }
                result.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
                // A dropdown is for picking, not browsing; past this the "other" entry is the way in.
                if (result.Count > 30) result.RemoveRange(30, result.Count - 30);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            MainFile.Logger.Info($"[Config] model list for {source} unavailable: {ex.Message}");
        }
        return result;
    }
}
