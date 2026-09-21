using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SpireTalk.Yumina;

/// <summary>Result of one conversational turn against a Yumina session.</summary>
public sealed record TurnResult(
    string Text,
    IReadOnlyList<string> Choices,
    JsonObject? Variables,
    string? ModelUsed,
    int? CreditsBalance,
    string? RawContent = null);

/// <summary>Who the token belongs to, as reported by the server.</summary>
public sealed record WhoAmI(string UserId, string Name, int? Credits, string? Plan);

/// <summary>One selectable model from the account's list.</summary>
public sealed record ModelInfo(string Id, string Name, string? MinPlan, double? AvgCost);

/// <summary>A pending device-code login.</summary>
public sealed record DeviceStart(string DeviceCode, string UserCode, string VerificationUrl, int ExpiresIn, int Interval);

/// <summary>Server-published mapping of game NPCs to Yumina worlds.</summary>
public sealed record IntegrationManifest(
    IReadOnlyDictionary<string, string> Worlds,
    string? DefaultModel,
    int Version);

public sealed class YuminaException : Exception
{
    public int? StatusCode { get; }
    public string? Code { get; }

    public YuminaException(string message, int? statusCode = null, string? code = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

/// <summary>
/// Thin HTTP client for the Yumina session API. One instance per mod; the base URL and
/// token come from the mod settings and can change at runtime (see <see cref="Configure"/>).
/// </summary>
public sealed class YuminaClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private string _baseUrl = "https://yumina.io";
    private string _token = "";

    public YuminaClient()
    {
        _http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(120),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SpireTalk/0.1 (Slay the Spire 2 mod)");
    }

    public string BaseUrl => _baseUrl;
    public bool HasToken => IsMock || !string.IsNullOrWhiteSpace(_token);
    public bool IsMock { get; private set; }

    public void Configure(string baseUrl, string token)
    {
        var trimmed = (baseUrl ?? "").Trim().TrimEnd('/');
        IsMock = MockYumina.IsEnabled(trimmed);
        if (IsMock)
        {
            _baseUrl = MockYumina.Marker;
            _token = (token ?? "").Trim();
            return;
        }
        if (trimmed.Length == 0) trimmed = "https://yumina.io";
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }
        _baseUrl = trimmed;
        _token = (token ?? "").Trim();
    }

    // ── Auth / discovery ──────────────────────────────────────────────────

    public async Task<WhoAmI> WhoAmIAsync(CancellationToken ct)
    {
        if (IsMock) return await MockYumina.WhoAmIAsync();
        using var res = await SendAsync(HttpMethod.Get, "/api/integrations/sts2/me", null, ct);
        var json = await ReadJsonAsync(res, ct);
        var data = json["data"] as JsonObject ?? json;
        return new WhoAmI(
            data["userId"]?.GetValue<string>() ?? "",
            data["name"]?.GetValue<string>() ?? "",
            data["credits"]?.GetValue<int?>(),
            data["plan"]?.GetValue<string>());
    }

    public async Task<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct)
    {
        if (IsMock) return new[] { new ModelInfo("mock-model", "Mock model", null, 0) };
        using var res = await SendAsync(HttpMethod.Get, "/api/models", null, ct);
        var json = await ReadJsonAsync(res, ct);
        var list = new List<ModelInfo>();
        // "curated" is the platform's short list; it comes back as [] for private-provider (BYOK)
        // accounts, so fall back to the full list whenever it is empty rather than only when absent.
        var data = json["data"] as JsonObject;
        var arr = data?["curated"] as JsonArray;
        if (arr == null || arr.Count == 0) arr = data?["all"] as JsonArray;
        if (arr == null) return list;
        foreach (var n in arr)
        {
            if (n is not JsonObject m) continue;
            var id = m["id"]?.GetValue<string>();
            if (string.IsNullOrEmpty(id)) continue;
            double? cost = null;
            if (m["avgCostMushies"] is JsonValue cv && cv.TryGetValue<double>(out var c)) cost = c;
            list.Add(new ModelInfo(id, m["name"]?.GetValue<string>() ?? id, m["minPlan"]?.GetValue<string>(), cost));
        }
        return list;
    }

    // ── Device-code login ─────────────────────────────────────────────────

    public async Task<DeviceStart> StartDeviceLoginAsync(string name, CancellationToken ct)
    {
        if (IsMock) throw new YuminaException("Offline mode has no login", null, "OFFLINE");
        var body = new JsonObject { ["name"] = name };
        using var res = await SendAsync(HttpMethod.Post, "/api/game-tokens/device", body, ct, requireToken: false);
        var json = await ReadJsonAsync(res, ct);
        var d = json["data"] as JsonObject ?? json;
        return new DeviceStart(
            d["deviceCode"]?.GetValue<string>() ?? "",
            d["userCode"]?.GetValue<string>() ?? "",
            d["verificationUrl"]?.GetValue<string>() ?? _baseUrl + "/link",
            d["expiresIn"]?.GetValue<int?>() ?? 600,
            d["interval"]?.GetValue<int?>() ?? 3);
    }

    /// <summary>Returns the game token once approved, null while pending; throws when denied/expired.</summary>
    public async Task<string?> PollDeviceLoginAsync(string deviceCode, CancellationToken ct)
    {
        using var res = await SendRawAsync(HttpMethod.Get, "/api/game-tokens/device/poll?deviceCode=" + Uri.EscapeDataString(deviceCode), null, ct, requireToken: false);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new YuminaException("This login request expired. Start again.", 404, "EXPIRED");
        }
        await EnsureSuccessAsync(res, ct);
        var json = await ReadJsonAsync(res, ct);
        var d = json["data"] as JsonObject ?? json;
        var status = d["status"]?.GetValue<string>() ?? "pending";
        return status switch
        {
            "approved" => d["token"]?.GetValue<string>(),
            "denied" => throw new YuminaException("You declined the login in the browser.", 403, "DENIED"),
            _ => null,
        };
    }

    public async Task<IntegrationManifest> GetManifestAsync(CancellationToken ct)
    {
        if (IsMock) return await MockYumina.ManifestAsync();
        using var res = await SendAsync(HttpMethod.Get, "/api/integrations/sts2/manifest", null, ct, requireToken: false);
        var json = await ReadJsonAsync(res, ct);
        var data = json["data"] as JsonObject ?? json;
        var worlds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (data["worlds"] is JsonObject w)
        {
            foreach (var kv in w)
            {
                if (kv.Value is JsonValue v && v.TryGetValue<string>(out var id) && !string.IsNullOrEmpty(id))
                    worlds[kv.Key] = id;
            }
        }
        return new IntegrationManifest(worlds, data["defaultModel"]?.GetValue<string>(), data["version"]?.GetValue<int?>() ?? 0);
    }

    // ── Sessions ──────────────────────────────────────────────────────────

    public async Task<string> CreateSessionAsync(string worldId, string name, CancellationToken ct)
    {
        if (IsMock) return "mock-session-" + Guid.NewGuid().ToString("N")[..8];
        var body = new JsonObject { ["worldId"] = worldId, ["name"] = name };
        using var res = await SendAsync(HttpMethod.Post, "/api/sessions", body, ct);
        var json = await ReadJsonAsync(res, ct);
        var id = (json["data"] as JsonObject)?["id"]?.GetValue<string>();
        if (string.IsNullOrEmpty(id)) throw new YuminaException("Session create returned no id");
        return id;
    }

    public async Task<bool> SessionExistsAsync(string sessionId, CancellationToken ct)
    {
        if (IsMock) return sessionId.StartsWith("mock-session-", StringComparison.Ordinal);
        using var res = await SendRawAsync(HttpMethod.Get, $"/api/sessions/{sessionId}", null, ct);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound) return false;
        await EnsureSuccessAsync(res, ct);
        return true;
    }

    public async Task PatchStateAsync(string sessionId, JsonObject variables, CancellationToken ct)
    {
        if (IsMock) return;
        // The route merges `state.variables` into the session; bare keys are ignored.
        var body = new JsonObject { ["state"] = new JsonObject { ["variables"] = variables } };
        using var res = await SendAsync(HttpMethod.Patch, $"/api/sessions/{sessionId}/state", body, ct);
        await EnsureSuccessAsync(res, ct);
    }

    /// <summary>
    /// Send one player line and wait for the full AI reply. The endpoint streams SSE; we
    /// consume it to the terminal "done" event and hand back the parsed turn.
    /// </summary>
    public async Task<TurnResult> SendMessageAsync(string sessionId, string content, string? model, CancellationToken ct)
    {
        if (IsMock) return await MockYumina.TurnAsync(content, ct);
        var body = new JsonObject
        {
            ["content"] = content,
            ["overrides"] = new JsonObject { ["streaming"] = true },
        };
        if (!string.IsNullOrWhiteSpace(model)) body["model"] = model;

        using var req = BuildRequest(HttpMethod.Post, $"/api/sessions/{sessionId}/messages", body, requireToken: true);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!res.IsSuccessStatusCode)
        {
            await EnsureSuccessAsync(res, ct); // throws with server message
        }

        var mediaType = res.Content.Headers.ContentType?.MediaType ?? "";
        if (!mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
        {
            // Non-streaming JSON answer (older servers / streaming disabled server-side).
            var json = await ReadJsonAsync(res, ct);
            return ParseDone(json["data"] as JsonObject ?? json, fallbackText: "");
        }

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var textSoFar = new StringBuilder();
        string? eventName = null;
        var data = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null) break; // stream closed without "done"

            if (line.Length == 0)
            {
                if (eventName != null)
                {
                    var payload = data.ToString();
                    switch (eventName)
                    {
                        case "text":
                            textSoFar.Append(ExtractText(payload));
                            break;
                        case "done":
                            return ParseDone(SafeParse(payload), textSoFar.ToString());
                        case "error":
                            throw ErrorFromPayload(payload);
                    }
                }
                eventName = null;
                data.Clear();
                continue;
            }

            if (line.StartsWith(':')) continue; // comment / keepalive
            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].Trim();
            }
            else if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                if (data.Length > 0) data.Append('\n');
                data.Append(line[5..].TrimStart());
            }
        }

        if (textSoFar.Length > 0)
        {
            // Connection dropped after content arrived; treat as a plain reply with no state.
            return new TurnResult(textSoFar.ToString(), Array.Empty<string>(), null, null, null);
        }
        throw new YuminaException("The stream ended before a reply arrived");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string ExtractText(string payload)
    {
        var node = SafeParse(payload);
        if (node == null) return "";
        // {"text":"..."} or {"content":"..."} or a bare string
        return node["text"]?.GetValue<string>() ?? node["content"]?.GetValue<string>() ?? "";
    }

    private static TurnResult ParseDone(JsonObject? done, string fallbackText)
    {
        var text = done?["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text)) text = fallbackText;

        JsonObject? variables = null;
        if (done?["state"] is JsonObject state)
        {
            variables = state["variables"] as JsonObject ?? state;
        }

        var choices = new List<string>();
        void AddChoices(JsonNode? n)
        {
            if (n is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    var s = item?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(s)) choices.Add(s.Trim());
                }
            }
        }
        if (variables != null) AddChoices(variables["choices"]);
        if (choices.Count == 0) AddChoices(done?["choices"]);

        int? credits = null;
        if (done?["credits"] is JsonObject cr)
        {
            credits = cr["balance"]?.GetValue<int?>();
        }

        return new TurnResult(text ?? "", choices, variables, done?["model"]?.GetValue<string>(), credits, done?["rawContent"]?.GetValue<string>());
    }

    private static YuminaException ErrorFromPayload(string payload)
    {
        var node = SafeParse(payload);
        var msg = node?["error"]?.GetValue<string>() ?? node?["message"]?.GetValue<string>() ?? payload;
        var code = node?["code"]?.GetValue<string>();
        return new YuminaException(msg, null, code);
    }

    private static JsonObject? SafeParse(string payload)
    {
        try
        {
            return JsonNode.Parse(payload) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, JsonObject? body, bool requireToken)
    {
        if (requireToken && !HasToken)
        {
            throw new YuminaException("No Yumina token configured", 401, "NO_TOKEN");
        }
        var req = new HttpRequestMessage(method, _baseUrl + path);
        if (HasToken) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        if (body != null)
        {
            req.Content = new StringContent(body.ToJsonString(JsonOpts), Encoding.UTF8, "application/json");
        }
        return req;
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, JsonObject? body, CancellationToken ct, bool requireToken = true)
    {
        using var req = BuildRequest(method, path, body, requireToken);
        try
        {
            return await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new YuminaException($"Cannot reach {_baseUrl}: {ex.Message}", null, "NETWORK", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new YuminaException("The request timed out", null, "TIMEOUT", ex);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonObject? body, CancellationToken ct, bool requireToken = true)
    {
        var res = await SendRawAsync(method, path, body, ct, requireToken);
        await EnsureSuccessAsync(res, ct);
        return res;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode) return;
        var status = (int)res.StatusCode;
        string message = res.ReasonPhrase ?? status.ToString();
        string? code = null;
        try
        {
            var text = await res.Content.ReadAsStringAsync(ct);
            var node = SafeParse(text);
            if (node != null)
            {
                message = node["error"]?.GetValue<string>() ?? node["message"]?.GetValue<string>() ?? message;
                code = node["code"]?.GetValue<string>();
            }
        }
        catch
        {
            // keep the reason phrase
        }
        res.Dispose();
        throw new YuminaException(message, status, code ?? status switch
        {
            401 => "UNAUTHORIZED",
            402 => "NO_CREDITS",
            403 => "FORBIDDEN",
            404 => "NOT_FOUND",
            429 => "RATE_LIMITED",
            _ => null,
        });
    }

    private static async Task<JsonObject> ReadJsonAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var text = await res.Content.ReadAsStringAsync(ct);
        return SafeParse(text) ?? throw new YuminaException("Server returned something that is not JSON");
    }
}
