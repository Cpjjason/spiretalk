using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Runs;
using SpireTalk.Affection;
using SpireTalk.Ancients;
using SpireTalk.Backend;
using SpireTalk.Deals;
using SpireTalk.Engine;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;
using SpireTalk.Ui;

namespace SpireTalk.Epilogue;

/// <summary>
/// The run is over; the NPC who knew the climber best says a few words about it on the game-over
/// screen — the deals, where they fell or how they won. One call per run, written to the
/// profile store so it can be read again. Also the moment open deals are judged (death breaks them).
/// </summary>
[HarmonyPatch(typeof(NGameOverScreen), "_Ready")]
internal static class EpilogueService
{
    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NGameOverScreen), "_runState");

    // A finalizer, not a postfix: other mods' patches inside _Ready can throw (a null death-quote
    // key has been seen), and the deals and epilogue must still run. The exception is passed on.
    private static Exception? Finalizer(Exception? __exception, NGameOverScreen __instance)
    {
        Run(__instance);
        return __exception;
    }

    private static void Run(NGameOverScreen __instance)
    {
        try
        {
            if (RunStateField?.GetValue(__instance) is not RunState runState) return;
            var player = runState.Players.FirstOrDefault();
            if (player == null || runState.Players.Count > 1) return;
            var win = SafeWin(runState);

            DealService.OnRunEnded(runState, win);

            var runKey = RewardExecutor.RunKey(player);
            var run = TalkStore.Instance.GetRun(runKey);
            if (run.EpilogueDone || !Backend.Backends.AiReady) return;

            var npc = PickNarrator(runKey);
            if (npc == null) return;
            run.EpilogueDone = true;
            TalkStore.Instance.Save();

            var panel = EpiloguePanel.Attach(__instance, npc);
            _ = WriteAsync(npc, player, runState, win, runKey, panel);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Epilogue] skipped: {ex.GetType().Name}: {ex.Message}", 1);
        }
    }

    private static bool SafeWin(RunState runState)
    {
        try
        {
            return runState.CurrentRoom?.IsVictoryRoom ?? false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The NPC the climber talked to this run with the highest affection; ties go to the most recent talk.</summary>
    private static NpcDefinition? PickNarrator(string runKey)
    {
        NpcDefinition? best = null;
        var bestScore = int.MinValue;
        foreach (var npc in NpcCatalog.All)
        {
            var record = TalkStore.Instance.Get(npc.Key);
            if (!record.History.Any(h => h.Run == runKey && h.Role == "npc")) continue;
            var (affection, married) = AffectionService.Read(npc.Key);
            var score = (married ? 1000 : 0) + affection;
            if (score > bestScore)
            {
                bestScore = score;
                best = npc;
            }
        }
        return best;
    }

    private static async Task WriteAsync(NpcDefinition npc, Player player, RunState runState, bool win, string runKey, EpiloguePanel? panel)
    {
        try
        {
            var card = CardLibrary.For(npc.Key);
            if (card == null) return;
            var runtime = YuminaRuntime.Instance;

            var vars = GameContext.CollectForMerchant(player);
            vars["npc"] = npc.Key;
            var (affection, married) = AffectionService.Read(npc.Key);
            vars["affection"] = affection;
            vars["affection_tier"] = ConversationRules.TierName(ConversationRules.TierOf(affection, married));
            vars["married"] = married;
            vars["talks"] = TalkStore.Instance.Get(npc.Key).Talks;
            vars["lines_left"] = 0;
            vars["allowed_actions"] = "none";
            vars["deals"] = DealsOfRun(npc.Key, runKey);
            vars["deal_offer"] = "none";

            var state = runtime.StartSession(card.Json, vars).StateJson;
            TalkStore.Instance.SummarizeForViewing(npc.Key);
            var history = MemoryText.BuildHistory(npc.Key, runKey, includeCurrentVisit: false);
            var request = BuildRequest(player, runState, win);
            var prompt = runtime.BuildTurnPrompt(card.Json, state, history, request, OpenAiClient.Model);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var raw = await MainFile.Backend.Client.CompleteAsync(prompt.Messages, Math.Max(400, prompt.MaxTokens), prompt.Temperature, cts.Token);
            var result = runtime.FinishTurn(card.Json, prompt.StateJson, request, raw);

            var choices = new List<string>();
            var delta = 0;
            var action = NpcAction.None;
            var text = LooseFields.Extract(raw, result.CleanText, ref choices, ref delta, ref action).Trim();
            text = string.Join("\n", text.Split('\n').Select(l => Backend.EngineBackend.TidyDots(l, npc.Key)).Where(l => l.Length > 0));
            if (string.IsNullOrWhiteSpace(text)) return;

            TalkStore.Instance.AddEpilogue(new TalkStore.EpilogueRecord
            {
                Npc = npc.Key,
                Run = runKey,
                Win = win,
                Floor = SafeFloor(runState),
                Text = text,
                Utc = DateTime.UtcNow.ToString("o"),
            });
            TalkStore.Instance.Save();
            MainFile.Logger.Info($"[Epilogue] {npc.Key}: {text.Replace('\n', ' ')}", 1);
            panel?.Fill(text);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Epilogue] failed: {ex.GetType().Name}: {ex.Message}", 1);
            panel?.Dismiss();
        }
    }

    private static string DealsOfRun(string npcKey, string runKey)
    {
        var lines = TalkStore.Instance.Deals.Where(d => d.Run == runKey).Select(d =>
        {
            var who = d.Npc == npcKey ? "you" : NpcCatalog.Get(d.Npc)?.Name ?? d.Npc;
            var type = DealService.ParseType(d.Type) ?? DealService.DealType.ReachNextAct;
            return $"- promised {who} to {DealService.PromptText(type, d.Param, d.TargetAct)}: {d.Status}";
        }).ToList();
        return lines.Count == 0 ? "none" : string.Join("\n", lines);
    }

    private static string BuildRequest(Player player, RunState runState, bool win)
    {
        var sb = new StringBuilder();
        var character = SafeString(() => player.Character.Title.GetFormattedText(), "The climber");
        var floor = SafeFloor(runState);
        var act = SafeInt(() => runState.CurrentActIndex + 1, 1);
        sb.Append("[EPILOGUE. The climb is over: ");
        sb.Append(win ? $"{character} reached the top and won." : $"{character} died on floor {floor}, in act {act}.");
        sb.Append(" Speak as yourself, looking back on this climber's whole run: 3 or 4 short sentences in your own voice (as short as your usual lines), one paragraph, in ");
        sb.Append(Loc.ReplyLanguageName());
        sb.Append(". Draw on what you remember - what they said to you, the deals they kept or broke. ");
        sb.Append("This is a farewell, not a visit: no choices, no offers, no directive lines at all - plain prose only.]");
        return sb.ToString();
    }

    private static int SafeFloor(RunState runState) => SafeInt(() => runState.TotalFloor, 0);

    private static int SafeInt(Func<int> f, int fallback)
    {
        try { return f(); } catch { return fallback; }
    }

    private static string SafeString(Func<string> f, string fallback)
    {
        try { return f(); } catch { return fallback; }
    }
}

/// <summary>
/// The narrator's words on the game-over screen, in the game's own Ancient dialogue bubble (their
/// icon, their bubble colour; see <see cref="NativeDialogue"/>). Shows "…" until the reply lands.
/// </summary>
internal sealed partial class EpiloguePanel : Control
{
    private Control? _line;

    public static EpiloguePanel? Attach(Control screen, NpcDefinition npc)
    {
        try
        {
            var panel = new EpiloguePanel();
            panel.Build(npc);
            screen.AddChild(panel);
            return panel;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Epilogue] panel failed: {ex.Message}", 1);
            return null;
        }
    }

    private void Build(NpcDefinition npc)
    {
        Name = "SpireTalkEpilogue";
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 50;
        // Right of the fallen character. The bubble sits on a line 200px above the bottom edge and
        // grows UPWARDS, so a long farewell can never cover the Continue button below it.
        SetAnchorsPreset(LayoutPreset.CenterBottom);
        OffsetLeft = -230;
        OffsetRight = 790;
        OffsetTop = -760;
        OffsetBottom = -200;
        Modulate = new Color(1, 1, 1, 0);

        _line = NativeDialogue.ForNpc(npc, Loc.EventText("epilogueWaiting"));
        if (_line != null)
        {
            _line.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
            _line.GrowVertical = GrowDirection.Begin;
            AddChild(_line);
            ThinkingTicker.Attach(_line, Loc.EventText("epilogueWaiting"));
        }

        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 1f, 0.6).SetDelay(1.2);
    }

    private Control? _continueButton;
    private bool _sawContinue;
    private bool _leaving;

    /// <summary>
    /// The farewell belongs to the death screen. Its Continue button is shown there and hidden once
    /// pressed (the run summary takes over), so the bubble fades out the moment that happens.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_leaving) return;
        _continueButton ??= GetParent()?.FindChild("ContinueButton", true, false) as Control;
        if (_continueButton == null || !GodotObject.IsInstanceValid(_continueButton)) return;
        var shown = _continueButton.Visible && _continueButton.Modulate.A > 0.5f;
        if (shown)
        {
            _sawContinue = true;
            return;
        }
        if (!_sawContinue) return;
        _leaving = true;
        var tween = CreateTween();
        tween.TweenProperty(this, "modulate:a", 0f, 0.25);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public void Fill(string text)
    {
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(this)) return;
            NativeDialogue.SetText(_line, Loc.Sanitize(text));
        }).CallDeferred();
    }

    public void Dismiss()
    {
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(this)) QueueFree();
        }).CallDeferred();
    }
}
