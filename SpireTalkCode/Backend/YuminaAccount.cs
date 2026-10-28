using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Godot;
using HttpClient = System.Net.Http.HttpClient;
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;

namespace SpireTalk.Backend;

/// <summary>
/// "Use Yumina": the player's Yumina account instead of a key of their own. Yumina is an OAuth 2.1
/// authorization server and this mod is a registered public client, signing in the way desktop apps
/// do (RFC 8252): authorization code + PKCE, the browser hands the code back to a loopback port on
/// this machine. The game never sees a password. What it keeps is a refresh token (config, rotated
/// on every use); access tokens live in memory for minutes and are scoped to the AI gateway
/// (/api/ai/v1, OpenAI-compatible), whose replies are billed in the account's mushies.
///
/// Everything here is started from the main thread and awaits there (Godot's synchronization
/// context), so callbacks may touch nodes and the config directly.
/// </summary>
public static class YuminaAccount
{
    public const string ClientId = "spiretalk";
    private const string Scopes = "openid profile offline_access ai:chat wallet:read";
    // The loopback redirect URIs registered for this client; the first free port is used.
    private static readonly int[] Ports = { 47623, 47624, 47625, 47626, 47627 };

    public sealed class Me
    {
        public string Name = "", Username = "", Plan = "", DefaultModel = "";
        public string? PlansUrl, SiteUrl;
        public double Balance;
        public List<Model> Models = new();
    }

    /// <summary>A model as yumina.io's Play picker lists it (the gateway's /models keeps its order).</summary>
    public sealed class Model
    {
        public string Id = "", Name = "";
        public double AvgCost;
        public bool Available = true;
    }

    public static Me? Current { get; private set; }
    /// <summary>The sign-in page while the browser is out; null otherwise.</summary>
    public static string? PendingUrl { get; private set; }
    public static string? LastError { get; private set; }
    public static bool Linked => !string.IsNullOrWhiteSpace(SpireTalkConfig.YuminaToken);

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static int _loginGen;
    private static string? _access;
    private static DateTime _accessUntil;
    private static readonly SemaphoreSlim Refreshing = new(1, 1);

    public static string Origin => (string.IsNullOrWhiteSpace(SpireTalkConfig.YuminaSite) ? "https://yumina.io" : SpireTalkConfig.YuminaSite).Trim().TrimEnd('/');
    public static string ApiBase => Origin + "/api/ai/v1";
    private static string AuthBase => Origin + "/api/auth/oauth2";
    public static string SiteUrl => Current?.SiteUrl ?? Origin + "/?utm_source=mod&utm_medium=slay-the-spire-2&utm_campaign=spiretalk";
    // Top-up opens the subscription plans (a plan refills every month); packs are a tab away.
    public static string PlansUrl => Current?.PlansUrl ?? Origin + "/app/plans?tab=subscription";

    /// <summary>Raised whenever the account's state moves (signed in/out, balance, models, errors).</summary>
    public static event Action? Changed;

    /// <summary>Listeners touch nodes, so they always run on the main thread, a frame later.</summary>
    private static void Notify()
    {
        Godot.Callable.From(() =>
        {
            try { Changed?.Invoke(); }
            catch (Exception ex) { MainFile.Logger.Warn($"[Yumina] listener failed: {ex.Message}", 1); }
        }).CallDeferred();
    }

    /// <summary>Opens a yumina.io page in the player's browser. Dev builds only note it (tests drive the page themselves).</summary>
    public static void Open(string url)
    {
        LastOpened = url;
#if SPIRETALK_DEV
        if (Dev.TestBridge.ShouldInstall) { MainFile.Logger.Info("[Yumina] open (dev, not opened): " + url, 1); return; }
#endif
        try { OS.ShellOpen(url); } catch { /* best effort */ }
    }

    public static string? LastOpened { get; private set; }

    private static string B64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string RandomToken(int bytes) => B64Url(RandomNumberGenerator.GetBytes(bytes));

    private static void Save() => SpireTalkConfig.Instance?.SaveDebounced(200);

    // ── Signing in ───────────────────────────────────────────────────────

    /// <summary>Opens the sign-in page and waits on a loopback port for the browser to come back with a code.</summary>
    public static async void StartLogin()
    {
        var gen = ++_loginGen;
        LastError = null;
        PendingUrl = null;
        TcpListener? listener = null;
        var port = 0;
        foreach (var p in Ports)
        {
            try { listener = new TcpListener(IPAddress.Loopback, p); listener.Start(); port = p; break; }
            catch { listener = null; }
        }
        if (listener == null)
        {
            LastError = string.Format(Loc.Ui("YmNoPort"), Ports[0], Ports[^1]);
            Notify();
            return;
        }
        try
        {
            string verifier = RandomToken(48), state = RandomToken(16);
            var challenge = B64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
            var redirect = $"http://127.0.0.1:{port}/callback";
            var url = AuthBase + "/authorize?response_type=code"
                + "&client_id=" + Uri.EscapeDataString(ClientId)
                + "&redirect_uri=" + Uri.EscapeDataString(redirect)
                + "&scope=" + Uri.EscapeDataString(Scopes)
                + "&code_challenge=" + challenge + "&code_challenge_method=S256"
                + "&state=" + state
                + "&resource=" + Uri.EscapeDataString(ApiBase)
                + "&prompt=consent";
            var expires = DateTime.UtcNow.AddMinutes(10);
            PendingUrl = url;
            Open(url);
            Notify();

            string? code = null, error = null;
            Task<TcpClient>? accept = null; // one pending accept at a time, or a connection lands on an abandoned one
            while (gen == _loginGen && DateTime.UtcNow < expires && code == null && error == null)
            {
                accept ??= listener.AcceptTcpClientAsync();
                if (await Task.WhenAny(accept, Task.Delay(1000)) != accept) continue;
                using var client = await accept;
                accept = null;
                var (query, ok) = await Callback(client, state);
                if (!ok) continue; // a stray request (favicon, another tab): keep listening
                code = query.GetValueOrDefault("code");
                if (code == null) error = query.GetValueOrDefault("error") ?? "no_code";
            }
            if (gen != _loginGen) return;
            if (code == null)
            {
                PendingUrl = null;
                LastError = Loc.Ui(error == "access_denied" ? "YmDenied" : "YmExpired");
                Notify();
                return;
            }
            var tokens = await Token(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = redirect,
                ["client_id"] = ClientId, ["code_verifier"] = verifier, ["resource"] = ApiBase,
            });
            if (gen != _loginGen) return;
            PendingUrl = null;
            Keep(tokens);
            SpireTalkConfig.YuminaModel = "";
            Save();
            MainFile.Logger.Info("[Yumina] signed in", 1);
            Notify();
            Refresh();
        }
        catch (Exception ex)
        {
            if (gen != _loginGen) return;
            PendingUrl = null;
            LastError = string.Format(Loc.Ui("YmNetwork"), ex.Message);
            MainFile.Logger.Warn($"[Yumina] sign-in failed: {ex.Message}", 1);
            Notify();
        }
        finally
        {
            try { listener.Stop(); } catch { /* already stopped */ }
        }
    }

    /// <summary>Reads the browser's GET /callback?…, answers with a page that says to go back to the game.</summary>
    private static async Task<(Dictionary<string, string> Query, bool Ok)> Callback(TcpClient client, string state)
    {
        var q = new Dictionary<string, string>();
        var stream = client.GetStream();
        var buf = new byte[8192];
        int n;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            n = await stream.ReadAsync(buf, cts.Token);
        }
        catch { return (q, false); }
        if (n <= 0) return (q, false); // Chrome's speculative preconnect: opened, never used, closed
        var head = Encoding.ASCII.GetString(buf, 0, n);
        var m = Regex.Match(head, @"^GET /callback\?(\S*) HTTP");
        var ok = false;
        if (m.Success)
        {
            foreach (var pair in m.Groups[1].Value.Split('&'))
            {
                var i = pair.IndexOf('=');
                if (i > 0) q[Uri.UnescapeDataString(pair[..i])] = Uri.UnescapeDataString(pair[(i + 1)..].Replace('+', ' '));
            }
            ok = q.TryGetValue("state", out var s) && s == state;
        }
        var title = ok && q.ContainsKey("code") ? Loc.Ui("YmBackTitle") : Loc.Ui("YmBackFail");
        var html = "<!doctype html><meta charset=utf-8><title>SpireTalk · Yumina</title><body style=\"margin:0;height:100vh;display:flex;align-items:center;justify-content:center;background:#1b1714;color:#f0e6cf;font:20px system-ui,sans-serif\"><div style=\"text-align:center\"><div style=\"font-size:34px;margin-bottom:14px\">"
            + WebUtility.HtmlEncode(title) + "</div><div style=\"opacity:.7\">" + WebUtility.HtmlEncode(Loc.Ui("YmBackSub")) + "</div></div>";
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {(m.Success ? "200 OK" : "404 Not Found")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        // The page is a courtesy: a browser that hangs up early must not cost the sign-in.
        try
        {
            await stream.WriteAsync(header);
            await stream.WriteAsync(body);
        }
        catch (Exception ex) { MainFile.Logger.Info("[Yumina] callback page not delivered: " + ex.Message, 1); }
        return (q, ok);
    }

    public static void CancelLogin()
    {
        _loginGen++;
        PendingUrl = null;
        Notify();
    }

    private static async Task<JsonNode> Token(Dictionary<string, string> form)
    {
        using var res = await Http.PostAsync(AuthBase + "/token", new FormUrlEncodedContent(form));
        var text = await res.Content.ReadAsStringAsync();
        JsonNode? node = null;
        try { node = JsonNode.Parse(text); } catch { /* not JSON */ }
        if (!res.IsSuccessStatusCode || node?["access_token"] == null)
        {
            throw new BackendException(Str(node?["error_description"]) ?? Str(node?["error"]) ?? res.ReasonPhrase ?? "token", (int)res.StatusCode, Str(node?["error"]));
        }
        return node;
    }

    private static string? Str(JsonNode? n)
    {
        try { return n?.GetValue<string>(); } catch { return n?.ToJsonString(); }
    }

    private static double Num(JsonNode? n)
    {
        try { return n?.GetValue<double>() ?? 0; } catch { return 0; }
    }

    /// <summary>A fresh token pair: the access token in memory, the (rotated) refresh token in the config.</summary>
    private static void Keep(JsonNode tokens)
    {
        _access = Str(tokens["access_token"]);
        var life = Num(tokens["expires_in"]);
        _accessUntil = DateTime.UtcNow.AddSeconds((life > 0 ? life : 600) - 30);
        var refresh = Str(tokens["refresh_token"]);
        if (string.IsNullOrEmpty(refresh)) return;
        SpireTalkConfig.YuminaToken = refresh;
        Save();
    }

    /// <summary>A valid access token, refreshing it (and rotating the refresh token) when it has run out.</summary>
    public static async Task<string> AccessTokenAsync()
    {
        if (_access != null && DateTime.UtcNow < _accessUntil) return _access;
        await Refreshing.WaitAsync();
        try
        {
            if (_access != null && DateTime.UtcNow < _accessUntil) return _access;
            var refresh = (SpireTalkConfig.YuminaToken ?? "").Trim();
            if (refresh.Length == 0) throw new BackendException(Loc.Ui("YmRelink"), 401, "YUMINA_RELINK");
            JsonNode tokens;
            try
            {
                tokens = await Token(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token", ["refresh_token"] = refresh, ["client_id"] = ClientId, ["resource"] = ApiBase,
                });
            }
            catch (BackendException ex) when (ex.StatusCode is 400 or 401)
            {
                // Revoked on the site, or expired: sign in again.
                SpireTalkConfig.YuminaToken = "";
                Current = null;
                Save();
                Notify();
                throw new BackendException(Loc.Ui("YmRelink"), 401, "YUMINA_RELINK");
            }
            // Keep the rotated refresh token right away so a parallel caller doesn't reuse the spent one.
            SpireTalkConfig.YuminaToken = Str(tokens["refresh_token"]) ?? refresh;
            Keep(tokens);
            return _access!;
        }
        finally
        {
            Refreshing.Release();
        }
    }

    public static void Logout()
    {
        var refresh = SpireTalkConfig.YuminaToken;
        SpireTalkConfig.YuminaToken = "";
        SpireTalkConfig.YuminaModel = "";
        Save();
        _access = null;
        Current = null;
        Notify();
        if (string.IsNullOrWhiteSpace(refresh)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var _ = await Http.PostAsync(AuthBase + "/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = refresh.Trim(), ["token_type_hint"] = "refresh_token", ["client_id"] = ClientId,
                }));
            }
            catch { /* the token dies on its own */ }
        });
    }

    // ── The account ──────────────────────────────────────────────────────

    private static async Task<JsonNode> Get(string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, ApiBase + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync());
        using var res = await Http.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        JsonNode? node = null;
        try { node = JsonNode.Parse(text); } catch { /* not JSON */ }
        if (res.IsSuccessStatusCode) return node ?? new JsonObject();
        if ((int)res.StatusCode == 401) _access = null;
        throw new BackendException(Str(node?["error"]?["message"]) ?? res.ReasonPhrase ?? "error", (int)res.StatusCode, Str(node?["error"]?["code"]));
    }

    public static void Refresh() => _ = RefreshAsync();

    /// <summary>Name, balance, default model and the model list; quietly does nothing when not linked.</summary>
    public static async Task RefreshAsync()
    {
        if (!Linked) return;
        try
        {
            var n = await Get("/me");
            var me = new Me
            {
                Name = Str(n["name"]) ?? "",
                Username = Str(n["username"]) ?? "",
                Plan = Str(n["plan"]) ?? "",
                Balance = Num(n["balance"]),
                PlansUrl = Str(n["plansUrl"]),
                SiteUrl = Str(n["siteUrl"]),
                DefaultModel = Str(n["defaultModel"]) ?? "",
            };
            var models = await Get("/models");
            foreach (var m in models["data"] as JsonArray ?? new JsonArray())
            {
                var id = Str(m?["id"]);
                if (string.IsNullOrEmpty(id)) continue;
                bool available;
                try { available = m?["available"]?.GetValue<bool>() ?? true; } catch { available = true; }
                me.Models.Add(new Model { Id = id, Name = Str(m?["name"]) ?? id, AvgCost = Num(m?["avg_cost_mushies"]), Available = available });
            }
            Current = me;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = Linked ? string.Format(Loc.Ui("YmNetwork"), ex.Message) : null;
            MainFile.Logger.Warn($"[Yumina] account refresh failed: {ex.Message}", 1);
        }
        Notify();
    }

    /// <summary>After a reply, the balance the gateway reported (yumina.balance on the completion).</summary>
    public static void NoteBalance(double balance)
    {
        if (Current == null) return;
        Current.Balance = balance;
        Notify();
    }
}
