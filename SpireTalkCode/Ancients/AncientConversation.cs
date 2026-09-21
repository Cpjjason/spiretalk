using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Saves;
using SpireTalk.Store;
using SpireTalk.Yumina;

namespace SpireTalk.Ancients;

/// <summary>
/// Drives one AI conversation inside an Ancient event. The vanilla event stays intact: we add a
/// "Talk" option to the initial page, and while talking we swap the page's description/options
/// for AI-generated dialogue lines and choices. Ending the talk restores the vanilla options.
/// </summary>
public sealed class AncientConversation
{
    private const int MaxDialogueLines = 4;
    private const int MaxLineChars = 220;
    private const string FallbackSfx = AncientDialogueLine.sfxFallbackPath;

    private static readonly MethodInfo SetEventStateMethod =
        AccessTools.Method(typeof(EventModel), "SetEventState", new[] { typeof(LocString), typeof(IEnumerable<EventOption>) })
        ?? throw new InvalidOperationException("EventModel.SetEventState not found — game version mismatch?");

    private static readonly FieldInfo OptionsContainerField =
        AccessTools.Field(typeof(NEventLayout), "_optionsContainer")
        ?? throw new InvalidOperationException("NEventLayout._optionsContainer not found");

    private static readonly Dictionary<AncientEventModel, AncientConversation> Active = new(ReferenceEqualityComparer.Instance);

    private readonly AncientEventModel _ancient;
    private readonly string _key;
    private readonly LocString _vanillaDescription;
    private readonly List<EventOption> _vanillaOptions;
    private readonly string _sfx;
    private readonly TalkStore.NpcRecord _record;

    private string? _sessionId;
    private bool _busy;
    private bool _ended;
    private int _turnsThisVisit;
    private TalkInputBox? _inputBox;

    private AncientConversation(AncientEventModel ancient, IReadOnlyList<EventOption> vanillaOptions)
    {
        _ancient = ancient;
        _key = ancient.Id.Entry;
        _vanillaDescription = ancient.Description ?? ancient.InitialDescription;
        _vanillaOptions = vanillaOptions.ToList();
        _sfx = PickSfx(ancient);
        _record = TalkStore.Instance.Get(_key);
    }

    // ── Entry points ─────────────────────────────────────────────────────

    /// <summary>Build the "Talk with …" option that the option-list patch appends to the initial page.</summary>
    public static EventOption CreateTalkOption(AncientEventModel ancient, IReadOnlyList<EventOption> vanillaOptions)
    {
        var title = Loc.Event("talk.title");
        title.Add("AncientName", ancient.Title);
        var description = Loc.Event("talk.description");
        description.Add("AncientName", ancient.Title);

        return new EventOption(ancient, () => Begin(ancient, vanillaOptions), title, description, "SPIRETALK_TALK", Array.Empty<IHoverTip>())
            .ThatWontSaveToChoiceHistory();
    }

    private static Task Begin(AncientEventModel ancient, IReadOnlyList<EventOption> vanillaOptions)
    {
        if (!Active.TryGetValue(ancient, out var convo))
        {
            convo = new AncientConversation(ancient, vanillaOptions);
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
            ShowThinking();
            var client = MainFile.Client;
            SpireTalkConfig.ApplyToClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

            var worldId = await MainFile.ResolveWorldIdAsync(_key, cts.Token);
            if (worldId == null)
            {
                throw new YuminaException(string.Format(Loc.Ui("ErrNoWorld"), _key), 404, "NO_WORLD");
            }

            _sessionId = await EnsureSessionAsync(client, worldId, cts.Token);
            await client.PatchStateAsync(_sessionId, GameContext.Collect(_ancient, _record), cts.Token);

            _record.Talks++;
            _record.LastTalkUtc = DateTime.UtcNow.ToString("o");
            TalkStore.Instance.Save();

            var opener = BuildOpener();
            var result = await client.SendMessageAsync(_sessionId, opener, ModelOrNull(), cts.Token);
            Present(result);
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
        if (_busy || _ended || _sessionId == null) return;
        _busy = true;
        try
        {
            ShowThinking();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var result = await MainFile.Client.SendMessageAsync(_sessionId, playerLine, ModelOrNull(), cts.Token);
            _turnsThisVisit++;
            Present(result);
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

    private void Present(TurnResult result)
    {
        RememberAffection(result.Variables);
        if (!string.IsNullOrWhiteSpace(result.Text))
        {
            _record.LastLine = Truncate(result.Text, 300);
        }
        TalkStore.Instance.Save();

        var choices = result.Choices.Count > 0 ? result.Choices : DefaultChoices();
        var options = new List<EventOption>();
        foreach (var choice in choices.Take(4))
        {
            var text = choice;
            options.Add(new EventOption(_ancient, () => TurnAsync(text), Loc.Event("empty"), Loc.Dynamic(text), "SPIRETALK_CHOICE", Array.Empty<IHoverTip>())
                .ThatWontSaveToChoiceHistory());
        }
        options.Add(new EventOption(_ancient, () => { EndConversation(); return Task.CompletedTask; }, Loc.Event("empty"), Loc.Event("end"), "SPIRETALK_END", Array.Empty<IHoverTip>())
            .ThatWontSaveToChoiceHistory());

        SetEventState(_vanillaDescription, options);

        var layout = Layout;
        if (layout == null) return;

        var lines = SplitIntoLines(result.Text);
        if (lines.Count == 0) lines.Add(Loc.EventText("silence"));
        ShowLines(layout, lines.Select(Loc.Dynamic).ToList());

        if (SpireTalkConfig.FreeTextInput)
        {
            AttachInputBox(layout);
        }

        if (SpireTalkConfig.DebugLogging)
        {
            MainFile.Logger.Info($"[Talk] {_key}: {lines.Count} line(s), {choices.Count} choice(s), model={result.ModelUsed}, credits={result.CreditsBalance}", 1);
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

    private void AttachInputBox(NAncientEventLayout layout)
    {
        try
        {
            if (OptionsContainerField.GetValue(layout) is not Control container) return;
            _inputBox = TalkInputBox.Create(Loc.EventText("typePlaceholder"), text =>
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

    // ── Helpers ──────────────────────────────────────────────────────────

    private NAncientEventLayout? Layout => _ancient.Node as NAncientEventLayout;

    private void SetEventState(LocString description, IEnumerable<EventOption> options)
    {
        SetEventStateMethod.Invoke(_ancient, new object[] { description, options });
    }

    private async Task<string> EnsureSessionAsync(YuminaClient client, string worldId, CancellationToken ct)
    {
        if (SpireTalkConfig.RememberAcrossRuns && !string.IsNullOrEmpty(_record.SessionId))
        {
            try
            {
                if (await client.SessionExistsAsync(_record.SessionId, ct))
                {
                    return _record.SessionId;
                }
            }
            catch (YuminaException ex) when (ex.StatusCode is 403 or 404)
            {
                // fall through and create a fresh one
            }
        }
        var name = "STS2 · " + _ancient.Title.GetFormattedText();
        var id = await client.CreateSessionAsync(worldId, name, ct);
        _record.SessionId = id;
        TalkStore.Instance.Save();
        return id;
    }

    private string BuildOpener()
    {
        var owner = _ancient.Owner;
        var character = owner != null ? owner.Character.Title.GetFormattedText() : "The traveler";
        var visit = _record.Talks;
        var sb = new StringBuilder();
        sb.Append('[').Append(character).Append(" approaches you");
        sb.Append(visit <= 1 ? " for the first time." : $" again (talk #{visit}).");
        if (_record.LastLine != null && visit > 1)
        {
            sb.Append(" Last time you said: \"").Append(Truncate(_record.LastLine, 120)).Append("\".");
        }
        sb.Append(" Reply in ").Append(Loc.ReplyLanguageName()).Append(".]");
        return sb.ToString();
    }

    private static string? ModelOrNull()
    {
        var m = SpireTalkConfig.Model?.Trim();
        if (!string.IsNullOrEmpty(m)) return m;
        return MainFile.Manifest?.DefaultModel;
    }

    private void RememberAffection(JsonObject? variables)
    {
        if (variables == null) return;
        var node = variables["affection"];
        if (node is JsonValue v)
        {
            if (v.TryGetValue<int>(out var i)) _record.Affection = i;
            else if (v.TryGetValue<double>(out var d)) _record.Affection = (int)Math.Round(d);
            else if (v.TryGetValue<string>(out var s) && int.TryParse(s, out var p)) _record.Affection = p;
        }
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

    /// <summary>
    /// Break the AI reply into dialogue-bubble sized pieces: paragraphs first, then sentences
    /// when a paragraph is too long. Anything past the cap is folded into the last line.
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
                // swallow closing quotes / repeated terminators
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
