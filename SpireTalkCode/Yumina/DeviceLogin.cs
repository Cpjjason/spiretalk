using Godot;

namespace SpireTalk.Yumina;

/// <summary>
/// Device-code login: the mod asks the server for a short code, opens the browser on the
/// approval page, and polls until the player clicks "Allow". No password ever enters the game.
/// Progress is reported to whoever started it (the settings screen shows it inline).
/// </summary>
public static class DeviceLogin
{
    public enum Phase
    {
        Waiting,   // browser opened, code shown, polling
        Done,      // token stored
        Failed,    // error or timeout; text carries the reason
        Cancelled,
    }

    public sealed record Update(Phase Phase, string Text, string UserCode = "", string Url = "");

    private static CancellationTokenSource? _current;
    private static string _lastUrl = "";

    public static bool InProgress => _current != null && !_current.IsCancellationRequested;

    /// <summary>Re-open the approval page from the last attempt (in case the first browser tab was lost).</summary>
    public static void ReopenBrowser()
    {
        if (string.IsNullOrEmpty(_lastUrl)) return;
        try { OS.ShellOpen(_lastUrl); } catch { /* best effort */ }
    }

    public static async Task StartAsync(Action<Update> report)
    {
        _current?.Cancel();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        _current = cts;
        SpireTalkConfig.ApplyToClient();
        var client = MainFile.Client;

        try
        {
            var start = await client.StartDeviceLoginAsync("Slay the Spire 2 · SpireTalk", cts.Token);
            var url = start.VerificationUrl.Contains('?')
                ? start.VerificationUrl + "&code=" + Uri.EscapeDataString(start.UserCode)
                : start.VerificationUrl + "?code=" + Uri.EscapeDataString(start.UserCode);
            _lastUrl = url;

            try { DisplayServer.ClipboardSet(url); } catch { /* clipboard is optional */ }
            try { OS.ShellOpen(url); }
            catch (Exception ex) { MainFile.Logger.Warn($"[Login] could not open browser: {ex.Message}", 1); }

            report(new Update(Phase.Waiting, string.Format(Loc.Ui("LoginWaiting"), start.UserCode), start.UserCode, url));

            var interval = TimeSpan.FromSeconds(Math.Max(2, start.Interval));
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(interval, cts.Token);
                var token = await client.PollDeviceLoginAsync(start.DeviceCode, cts.Token);
                if (token != null)
                {
                    SpireTalkConfig.ApiToken = token;
                    SpireTalkConfig.Instance?.Changed();
                    SpireTalkConfig.Instance?.SaveDebounced();
                    SpireTalkConfig.ApplyToClient();
                    var me = await client.WhoAmIAsync(cts.Token);
                    AccountPanel.Refresh();
                    report(new Update(Phase.Done, string.Format(Loc.Ui("LoginDoneBody"), me.Name)));
                    return;
                }
            }
            report(new Update(Phase.Cancelled, ""));
        }
        catch (OperationCanceledException)
        {
            report(_current == cts
                ? new Update(Phase.Failed, Loc.Ui("ErrTimeout"))
                : new Update(Phase.Cancelled, ""));
        }
        catch (Exception ex)
        {
            report(new Update(Phase.Failed, Loc.DescribeError(ex)));
        }
        finally
        {
            if (_current == cts) _current = null;
        }
    }

    public static void Cancel()
    {
        _current?.Cancel();
        _current = null;
    }
}
