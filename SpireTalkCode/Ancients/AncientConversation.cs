using System.Reflection;
using System.Text;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using SpireTalk.Affection;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;
using SpireTalk.Yumina;

namespace SpireTalk.Ancients;

/// <summary>
/// Drives one visit's conversation inside an Ancient event. The vanilla event stays intact: we
/// add a "Talk" option to the initial page; while talking we swap the page's options for AI
/// choices and render the NPC's lines as the game's own dialogue bubbles. Ending the talk
/// restores the vanilla options (or the rerolled ones, if the NPC rerolled them).
///
/// Every backend produces an <see cref="NpcTurn"/>; this class applies <see cref="ConversationRules"/>:
/// the 3-line budget, the clamped affection delta, and reward validation + execution.
/// </summary>
public sealed class AncientConversation
{
    private const int MaxDialogueLines = 4;
    private const int MaxLineChars = 220;
    private const string FallbackSfx = AncientDialogueLine.sfxFallbackPath;
    internal const string TalkTextKey = "SPIRETALK_TALK";

    private static readonly MethodInfo SetEventStateMethod =
        AccessTools.Method(typeof(EventModel), "SetEventState", new[] { typeof(LocString), typeof(IEnumerable<EventOption>) })
        ?? throw new InvalidOperationException("EventModel.SetEventState not found — game version mismatch?");

    private static readonly MethodInfo GenerateOptionsMethod =
        AccessTools.Method(typeof(EventModel), "GenerateInitialOptionsWrapper")
        ?? throw new InvalidOperationException("EventModel.GenerateInitialOptionsWrapper not found");

    private static readonly FieldInfo OptionsContainerField =
        AccessTools.Field(typeof(NEventLayout), "_optionsContainer")
        ?? throw new InvalidOperationException("NEventLayout._optionsContainer not found");

    private static readonly Dictionary<AncientEventModel, AncientConversation> Active = new(ReferenceEqualityComparer.Instance);

    private readonly AncientEventModel _ancient;
    private readonly string _key;
    private readonly NpcDefinition _npc;
    private readonly LocString _vanillaDescription;
    private readonly string _sfx;
    private readonly TalkStore.NpcRecord _record;
    private readonly string _runKey;

    private List<EventOption> _vanillaOptions;
    private VisitContext? _ctx;
    private bool _busy;
    private bool _ended;
    private int _linesUsed;
    private int _netDeltaThisVisit;
    private int _rewardsThisVisit;
    private bool _rerollUsed;
    private string? _previousPlayerLine;
    private TalkInputBox? _inputBox;

    private AncientConversation(AncientEventModel ancient, NpcDefinition npc, IReadOnlyList<EventOption> vanillaOptions)
    {
        _ancient = ancient;
        _key = ancient.Id.Entry;
        _npc = npc;
        _vanillaDescription = ancient.Description ?? ancient.InitialDescription;
        _vanillaOptions = vanillaOptions.Where(o => o.TextKey != TalkTextKey).ToList();
        _sfx = PickSfx(ancient);
        _record = TalkStore.Instance.Get(_key);
        _runKey = ancient.Owner != null ? RewardExecutor.RunKey(ancient.Owner) : "no-run";
    }

    // ── Entry points ─────────────────────────────────────────────────────

    /// <summary>Build the "Talk with …" option that the option-list patch appends to the initial page.</summary>
    public static EventOption CreateTalkOption(AncientEventModel ancient, IReadOnlyList<EventOption> vanillaOptions)
    {
        var title = Loc.Event("talk.title");
        title.Add("AncientName", ancient.Title);
        var description = Loc.Event("talk.description");
        description.Add("AncientName", ancient.Title);

        return new EventOption(ancient, () => Begin(ancient, vanillaOptions), title, description, TalkTextKey, Array.Empty<IHoverTip>())
            .ThatWontSaveToChoiceHistory();
    }

    private static Task Begin(AncientEventModel ancient, IReadOnlyList<EventOption> vanillaOptions)
    {
        if (!Active.TryGetValue(ancient, out var convo))
        {
            var npc = NpcCatalog.Get(ancient.Id.Entry);
            if (npc == null)
            {
                MainFile.Logger.Warn($"[Talk] no npc definition for {ancient.Id.Entry}", 1);
                return Task.CompletedTask;
            }
            convo = new AncientConversation(ancient, npc, vanillaOptions);
            Active[ancient] = convo;
        }
        return convo.StartAsync();
    }

    public static void Forget(AncientEventModel ancient)
    {
        if (Active.Remove(ancient, out var convo))
        {
            convo.DisposeInput();
        }
    }

    // ── Conversation flow ────────────────────────────────────────────────

    private async Task StartAsync()
    {
        if (_busy) return;
        _busy = true;
        _ended = false;
        try
        {
            var backend = MainFile.Backend;
            if (!backend.IsConfigured)
            {
                throw new YuminaException(Loc.Ui("ErrNotConfigured"), null, "NOT_CONFIGURED");
            }

            ShowThinking();
            _ctx = BuildContext();
            _record.Talks++;
            _record.LastTalkUtc = DateTime.UtcNow.ToString("o");
            _ctx.Talks = _record.Talks;
            _ctx.SyncRelationshipVariables();
            TalkStore.Instance.Save();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var opener = BuildOpener();
            var turn = await backend.ExchangeAsync(_ctx, opener, true, cts.Token);
            await HandleTurnAsync(turn, opener, isOpener: true);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task TurnAsync(string playerLine)
    {
        if (_busy || _ended || _ctx == null) return;
        if (_linesUsed >= ConversationRules.LinesPerVisit) return;
        _busy = true;
        try
        {
            _linesUsed++;
            _ctx.LinesLeft = ConversationRules.LinesPerVisit - _linesUsed;
            _ctx.AllowedActions = ConversationRules.AllowedActions(NpcKind.Ancient, _ctx.Tier, RewardState());
            _ctx.SyncRelationshipVariables();

            ShowThinking();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var turn = await MainFile.Backend.ExchangeAsync(_ctx, playerLine, false, cts.Token);
            await HandleTurnAsync(turn, playerLine, isOpener: false);
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task HandleTurnAsync(NpcTurn turn, string playerLine, bool isOpener)
    {
        if (_ctx == null) return;

        // 1. Memory — both directions, so any backend (and the next visit) can read it back.
        TalkStore.Instance.AppendHistory(_key, "player", playerLine);
        var npcText = turn.JoinedText;
        if (!string.IsNullOrWhiteSpace(npcText))
        {
            TalkStore.Instance.AppendHistory(_key, "npc", npcText);
            _record.LastLine = Truncate(npcText, 300);
        }

        // 2. Affection — clamp what the NPC proposed, then write it where it lives.
        var appliedDelta = 0;
        if (!isOpener)
        {
            appliedDelta = ConversationRules.ClampDelta(turn.AffectionDelta, _netDeltaThisVisit, playerLine, _previousPlayerLine);
            _netDeltaThisVisit += appliedDelta;
            _ctx.Affection = AffectionService.Apply(_key, NpcKind.Ancient, appliedDelta);
            _previousPlayerLine = playerLine;
        }

        // 3. Reward — validate against the rules, then let the game do it.
        var reward = ConversationRules.Validate(turn.Action, NpcKind.Ancient, _ctx.Tier, RewardState(), MaxHp());
        var rewardSummary = "none";
        if (!reward.IsNone && _ancient.Owner != null)
        {
            if (reward.Type == "reroll_relics")
            {
                if (RerollRelics())
                {
                    _rewardsThisVisit++;
                    _rerollUsed = true;
                    rewardSummary = "reroll";
                    RewardExecutor.Toast(string.Format(Loc.EventText("rewardReroll"), _npc.Name));
                }
            }
            else
            {
                var outcome = await RewardExecutor.ApplyAsync(reward, _ancient.Owner, _key, _ancient.Title.GetFormattedText(), _runKey);
                if (outcome.Applied) _rewardsThisVisit++;
                rewardSummary = outcome.Summary;
            }
        }
        TalkStore.Instance.Save();

        if (SpireTalkConfig.DebugLogging)
        {
            MainFile.Logger.Info($"[Talk] {_key} line {_linesUsed}/{ConversationRules.LinesPerVisit}: delta {turn.AffectionDelta}→{appliedDelta} (now {_ctx.Affection}, {ConversationRules.TierName(_ctx.Tier)}) reason='{turn.Reason}' action={turn.Action.Type}→{rewardSummary} model={turn.ModelUsed} credits={turn.CreditsBalance}", 1);
        }

        // 4. Render.
        Present(turn);
    }

    private void EndConversation()
    {
        if (_ended) return;
        _ended = true;
        DisposeInput();
        TalkStore.Instance.Save();

        var restored = new List<EventOption>(_vanillaOptions);
        restored.Add(CreateTalkOption(_ancient, _vanillaOptions));
        SetEventState(_vanillaDescription, restored);

        var layout = Layout;
        if (layout != null)
        {
            var bye = Loc.Event("farewell");
            bye.Add("AncientName", _ancient.Title);
            ShowLines(layout, new[] { bye });
        }
        // Next Talk this visit continues the same budget: the counter only resets with the room.
        _busy = false;
    }

    // ── Rendering ────────────────────────────────────────────────────────

    private void ShowThinking()
    {
        DisposeInput();
        var layout = Layout;
        if (layout == null) return;
        try
        {
            layout.ClearOptions();
            ShowLines(layout, new[] { Loc.Event("thinking") });
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Talk] thinking UI failed: {ex.Message}", 1);
        }
    }

    private void Present(NpcTurn turn)
    {
        if (_ctx == null) return;
        var outOfLines = _linesUsed >= ConversationRules.LinesPerVisit;

        var options = new List<EventOption>();
        if (!outOfLines)
        {
            var choices = turn.Choices.Count > 0 ? turn.Choices : DefaultChoices();
            foreach (var choice in choices.Take(3))
            {
                var text = choice;
                options.Add(new EventOption(_ancient, () => TurnAsync(text), Loc.Event("empty"), Loc.Dynamic(text), "SPIRETALK_CHOICE", Array.Empty<IHoverTip>())
                    .ThatWontSaveToChoiceHistory());
            }
        }
        var endLabel = Loc.Event(outOfLines ? "endNoLines" : "end");
        endLabel.Add("Affection", (decimal)_ctx.Affection);
        endLabel.Add("Tier", Loc.EventText("tier." + ConversationRules.TierName(_ctx.Tier)));
        endLabel.Add("LinesLeft", (decimal)Math.Max(0, ConversationRules.LinesPerVisit - _linesUsed));
        options.Add(new EventOption(_ancient, () => { EndConversation(); return Task.CompletedTask; }, Loc.Event("empty"), endLabel, "SPIRETALK_END", Array.Empty<IHoverTip>())
            .ThatWontSaveToChoiceHistory());

        SetEventState(_vanillaDescription, options);

        var layout = Layout;
        if (layout == null) return;

        var lines = turn.Lines.Count > 0 ? NormalizeLines(turn.Lines) : new List<string>();
        if (lines.Count == 0) lines.Add(Loc.EventText("silence"));
        ShowLines(layout, lines.Select(Loc.Dynamic).ToList());

        if (SpireTalkConfig.FreeTextInput && !outOfLines)
        {
            AttachInputBox(layout, ConversationRules.LinesPerVisit - _linesUsed);
        }
    }

    private void Fail(Exception ex)
    {
        MainFile.Logger.Warn($"[Talk] {_key} failed: {ex.GetType().Name}: {ex.Message}", 1);
        _ended = true;
        DisposeInput();

        var restored = new List<EventOption>(_vanillaOptions);
        restored.Add(CreateTalkOption(_ancient, _vanillaOptions));
        try
        {
            SetEventState(_vanillaDescription, restored);
            var layout = Layout;
            if (layout != null)
            {
                var line = Loc.Event("failed");
                line.Add("Reason", Loc.Sanitize(Loc.DescribeError(ex)));
                ShowLines(layout, new[] { line });
            }
        }
        catch (Exception inner)
        {
            MainFile.Logger.Error($"[Talk] could not restore vanilla options: {inner}", 1);
        }
    }

    private void ShowLines(NAncientEventLayout layout, IReadOnlyList<LocString> texts)
    {
        var lines = new List<AncientDialogueLine>(texts.Count);
        for (var i = 0; i < texts.Count; i++)
        {
            lines.Add(new AncientDialogueLine(_sfx)
            {
                LineText = texts[i],
                NextButtonText = i < texts.Count - 1 ? Loc.Event("next") : null,
                Speaker = AncientDialogueSpeaker.Ancient,
            });
        }
        layout.ClearDialogue();
        layout.SetDialogue(lines);
        layout.OnSetupComplete();
    }

    private void AttachInputBox(NAncientEventLayout layout, int linesLeft)
    {
        try
        {
            if (OptionsContainerField.GetValue(layout) is not Control container) return;
            var placeholder = string.Format(Loc.EventText("typePlaceholder"), linesLeft);
            _inputBox = TalkInputBox.Create(placeholder, text =>
            {
                if (_busy || _ended) return;
                _ = TurnAsync(text);
            });
            container.AddChild(_inputBox);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Talk] input box failed: {ex.Message}", 1);
        }
    }

    private void DisposeInput()
    {
        var box = _inputBox;
        _inputBox = null;
        if (box != null && GodotObject.IsInstanceValid(box))
        {
            box.QueueFree();
        }
    }

    // ── Rewards ──────────────────────────────────────────────────────────

    private ConversationRules.RewardState RewardState()
    {
        if (_ancient.Owner == null) return new ConversationRules.RewardState { RewardsThisVisit = 99 };
        return RewardExecutor.BuildState(_ancient.Owner, _key, _runKey, _rewardsThisVisit, _rerollUsed, discountUsed: true);
    }

    private int MaxHp()
    {
        try
        {
            return _ancient.Owner?.Creature.MaxHp ?? 60;
        }
        catch
        {
            return 60;
        }
    }

    /// <summary>Ask the event for a fresh initial page; those become the options restored when the talk ends.</summary>
    private bool RerollRelics()
    {
        try
        {
            if (GenerateOptionsMethod.Invoke(_ancient, null) is IReadOnlyList<EventOption> fresh && fresh.Count > 0)
            {
                _vanillaOptions = fresh.Where(o => o.TextKey != TalkTextKey && !o.IsProceed).ToList();
                return _vanillaOptions.Count > 0;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"[Talk] reroll failed: {ex.Message}", 1);
        }
        return false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private NAncientEventLayout? Layout => _ancient.Node as NAncientEventLayout;

    private void SetEventState(LocString description, IEnumerable<EventOption> options)
    {
        SetEventStateMethod.Invoke(_ancient, new object[] { description, options });
    }

    private VisitContext BuildContext()
    {
        var ctx = new VisitContext { NpcKey = _key, Npc = _npc, Language = Loc.ReplyLanguageName() };
        foreach (var kv in GameContext.Collect(_ancient))
        {
            ctx.Variables[kv.Key] = kv.Value?.DeepClone();
        }
        var (affection, married) = AffectionService.Read(_key);
        ctx.Affection = affection;
        ctx.Married = married;
        ctx.LinesLeft = ConversationRules.LinesPerVisit;
        ctx.AllowedActions = ConversationRules.AllowedActions(NpcKind.Ancient, ctx.Tier, RewardState());
        ctx.History.AddRange(TalkStore.Instance.HistoryOf(_key));
        ctx.Variables["affection_source"] = AffectionService.UsingAncientAffection ? "Ancient Affection mod" : "SpireTalk";
        return ctx;
    }

    private string BuildOpener()
    {
        var owner = _ancient.Owner;
        var character = owner != null ? owner.Character.Title.GetFormattedText() : "The traveler";
        var sb = new StringBuilder();
        sb.Append('[').Append(character).Append(" walks up to you");
        sb.Append(_record.Talks <= 1 ? " for the first time." : $" again (conversation #{_record.Talks}).");
        sb.Append(" They may say up to ").Append(ConversationRules.LinesPerVisit).Append(" lines this visit. Greet them in character and reply in ").Append(Loc.ReplyLanguageName()).Append(".]");
        return sb.ToString();
    }

    private static List<string> DefaultChoices()
    {
        return new List<string>
        {
            Loc.EventText("defaultChoice1"),
            Loc.EventText("defaultChoice2"),
            Loc.EventText("defaultChoice3"),
        };
    }

    private static string PickSfx(AncientEventModel ancient)
    {
        try
        {
            foreach (var dialogue in ancient.DialogueSet.GetAllDialogues())
            {
                foreach (var line in dialogue.Lines)
                {
                    var path = line.GetSfxOrFallbackPath();
                    if (!string.IsNullOrEmpty(path)) return path;
                }
            }
        }
        catch
        {
            // fall back below
        }
        return FallbackSfx;
    }

    /// <summary>Bubble-size the backend's lines: paragraphs first, then sentences when a line is too long.</summary>
    private static List<string> NormalizeLines(IReadOnlyList<string> lines)
    {
        var result = new List<string>();
        foreach (var l in lines)
        {
            result.AddRange(SplitIntoLines(l));
        }
        if (result.Count > MaxDialogueLines)
        {
            var tail = string.Join(" ", result.Skip(MaxDialogueLines - 1));
            result = result.Take(MaxDialogueLines - 1).ToList();
            result.Add(tail);
        }
        return result;
    }

    /// <summary>
    /// Break a reply into dialogue-bubble sized pieces: paragraphs first, then sentences when a
    /// paragraph is too long. Anything past the cap is folded into the last line.
    /// </summary>
    internal static List<string> SplitIntoLines(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var normalized = text.Replace("\r\n", "\n").Trim();
        var paragraphs = normalized.Split(new[] { "\n\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length <= MaxLineChars)
            {
                result.Add(paragraph);
                continue;
            }
            var current = new StringBuilder();
            foreach (var sentence in SplitSentences(paragraph))
            {
                if (current.Length > 0 && current.Length + sentence.Length > MaxLineChars)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                current.Append(sentence);
            }
            if (current.Length > 0) result.Add(current.ToString().Trim());
        }

        if (result.Count > MaxDialogueLines)
        {
            var tail = string.Join(" ", result.Skip(MaxDialogueLines - 1));
            result = result.Take(MaxDialogueLines - 1).ToList();
            result.Add(tail);
        }
        return result;
    }

    private static IEnumerable<string> SplitSentences(string paragraph)
    {
        var start = 0;
        for (var i = 0; i < paragraph.Length; i++)
        {
            var c = paragraph[i];
            if (c is '.' or '!' or '?' or '。' or '！' or '？' or '…')
            {
                var end = i + 1;
                while (end < paragraph.Length && (paragraph[end] is '"' or '”' or '」' or '』' or '’' or ')' or '）' or '.' or '!' or '?' or '。' or '！' or '？' or '…'))
                    end++;
                yield return paragraph.Substring(start, end - start);
                start = end;
                i = end - 1;
            }
        }
        if (start < paragraph.Length) yield return paragraph.Substring(start);
    }

    private static string Truncate(string s, int max)
    {
        s = s.Replace('\n', ' ').Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }
}
