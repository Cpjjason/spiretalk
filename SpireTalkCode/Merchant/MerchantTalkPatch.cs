using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Ancients;
using SpireTalk.Store;
using SpireTalk.Yumina;

namespace SpireTalk.Merchant;

/// <summary>
/// The Merchant has no dialogue flow in vanilla — just one-liners that fly in on events. We keep
/// those and add one AI line when the shop opens: the merchant greets this particular climber,
/// remembering earlier visits. Fire-and-forget; if Yumina is slow or down nothing changes.
/// </summary>
[HarmonyPatch(typeof(NMerchantDialogue), nameof(NMerchantDialogue.Initialize))]
internal static class MerchantGreetingPatch
{
    private const string NpcKey = "MERCHANT";

    private static readonly MethodInfo? ShowRandomMethod = AccessTools.Method(typeof(NMerchantDialogue), "ShowRandom");
    private static readonly PropertyInfo? RunStateProperty = typeof(RunManager).GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic);

    [HarmonyPostfix]
    private static void GreetWithYumina(NMerchantDialogue __instance, MerchantDialogueSet dialogueSet)
    {
        if (!SpireTalkConfig.EnableMerchant || ShowRandomMethod == null) return;
        _ = GreetAsync(__instance);
    }

    private static async Task GreetAsync(NMerchantDialogue node)
    {
        try
        {
            var owner = ResolveLocalPlayer();
            if (owner == null || owner.RunState.Players.Count > 1) return;

            var client = MainFile.Client;
            SpireTalkConfig.ApplyToClient();
            if (!client.HasToken) return;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var worldId = await MainFile.ResolveWorldIdAsync(NpcKey, cts.Token);
            if (worldId == null) return;

            var record = TalkStore.Instance.Get(NpcKey);
            var sessionId = await EnsureSessionAsync(client, worldId, record, cts.Token);
            await client.PatchStateAsync(sessionId, GameContext.CollectForMerchant(owner, record), cts.Token);

            record.Talks++;
            record.LastTalkUtc = DateTime.UtcNow.ToString("o");
            TalkStore.Instance.Save();

            var character = owner.Character.Title.GetFormattedText();
            var opener = $"[{character} walks into your shop{(record.Talks > 1 ? $" again (visit #{record.Talks})" : " for the first time")}. Greet them in one or two short sentences. Reply in {Loc.ReplyLanguageName()}.]";
            var model = SpireTalkConfig.Model?.Trim();
            if (string.IsNullOrEmpty(model)) model = MainFile.Manifest?.DefaultModel;
            var result = await client.SendMessageAsync(sessionId, opener, model, cts.Token);

            if (result.Variables?["affection"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<int>(out var aff))
            {
                record.Affection = aff;
            }
            record.LastLine = result.Text.Length > 300 ? result.Text[..300] : result.Text;
            TalkStore.Instance.Save();

            var line = FirstSentences(result.Text, 160);
            if (string.IsNullOrWhiteSpace(line)) return;
            if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) return;

            ShowRandomMethod!.Invoke(node, new object[] { new List<LocString> { Loc.Dynamic(line) } });
            if (SpireTalkConfig.DebugLogging)
            {
                MainFile.Logger.Info($"[Merchant] greeting shown: {line}", 1);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Merchant] greeting skipped: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }

    private static async Task<string> EnsureSessionAsync(YuminaClient client, string worldId, TalkStore.NpcRecord record, CancellationToken ct)
    {
        if (SpireTalkConfig.RememberAcrossRuns && !string.IsNullOrEmpty(record.SessionId))
        {
            try
            {
                if (await client.SessionExistsAsync(record.SessionId, ct)) return record.SessionId;
            }
            catch (YuminaException ex) when (ex.StatusCode is 403 or 404)
            {
                // create a new one below
            }
        }
        var id = await client.CreateSessionAsync(worldId, "STS2 · Merchant", ct);
        record.SessionId = id;
        TalkStore.Instance.Save();
        return id;
    }

    private static Player? ResolveLocalPlayer()
    {
        try
        {
            if (RunStateProperty?.GetValue(RunManager.Instance) is RunState run)
            {
                return LocalContext.GetMe(run);
            }
        }
        catch
        {
            // no run in progress
        }
        return null;
    }

    private static string FirstSentences(string text, int maxChars)
    {
        var lines = AncientConversation.SplitIntoLines(text);
        if (lines.Count == 0) return "";
        var first = lines[0];
        if (first.Length > maxChars) first = first[..maxChars].TrimEnd() + "…";
        return first;
    }
}
