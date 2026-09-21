using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace SpireTalk.Yumina;

/// <summary>
/// Device-code login: the mod asks the server for a short code, opens the browser on the
/// approval page, and polls until the player clicks "Allow". No password ever enters the game.
/// </summary>
public static class DeviceLogin
{
    private static CancellationTokenSource? _current;

    public static bool InProgress => _current != null && !_current.IsCancellationRequested;

    public static async Task StartAsync()
    {
        _current?.Cancel();
        var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        _current = cts;
        SpireTalkConfig.ApplyToClient();
        var client = MainFile.Client;

        NErrorPopup? popup = null;
        try
        {
            var start = await client.StartDeviceLoginAsync("Slay the Spire 2 · SpireTalk", cts.Token);
            var url = start.VerificationUrl.Contains('?')
                ? start.VerificationUrl + "&code=" + Uri.EscapeDataString(start.UserCode)
                : start.VerificationUrl + "?code=" + Uri.EscapeDataString(start.UserCode);

            try
            {
                DisplayServer.ClipboardSet(url);
            }
            catch { /* clipboard is optional */ }
            try
            {
                OS.ShellOpen(url);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"[Login] could not open browser: {ex.Message}", 1);
            }

            popup = ShowPopup(Loc.Ui("LoginTitle"), string.Format(Loc.Ui("LoginBody"), start.UserCode, url));

            var interval = TimeSpan.FromSeconds(Math.Max(2, start.Interval));
            while (!cts.IsCancellationRequested)
            {
                await Task.Delay(interval, cts.Token);
                var token = await client.PollDeviceLoginAsync(start.DeviceCode, cts.Token);
                if (token != null)
                {
                    SpireTalkConfig.ApiToken = token;
                    SpireTalkConfig.ApplyToClient();
                    ClosePopup(popup);
                    popup = null;
                    var me = await client.WhoAmIAsync(cts.Token);
                    AccountPanel.Refresh();
                    ShowPopup(Loc.Ui("LoginDone"), string.Format(Loc.Ui("LoginDoneBody"), me.Name));
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            ClosePopup(popup);
            if (_current == cts)
            {
                ShowPopup(Loc.Ui("LoginFailed"), Loc.Ui("ErrTimeout"));
            }
        }
        catch (Exception ex)
        {
            ClosePopup(popup);
            ShowPopup(Loc.Ui("LoginFailed"), Loc.DescribeError(ex));
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

    private static NErrorPopup? ShowPopup(string title, string body)
    {
        try
        {
            var popup = NErrorPopup.Create(title, body, false);
            if (popup != null && NModalContainer.Instance != null)
            {
                NModalContainer.Instance.Add(popup, true);
            }
            return popup;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Login] popup failed: {ex.Message}", 1);
            return null;
        }
    }

    private static void ClosePopup(NErrorPopup? popup)
    {
        try
        {
            if (popup != null && GodotObject.IsInstanceValid(popup))
            {
                popup.QueueFree();
            }
        }
        catch { /* ignore */ }
    }
}
