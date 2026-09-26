using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes.Events;
using SpireTalk.Npc;
using SpireTalk.Rewards;
using SpireTalk.Store;
using SpireTalk.Ui;

namespace SpireTalk.Encounters;

/// <summary>
/// A "?" room where one of the mod's NPCs turns up away from home. Shared shape: a picture in
/// the player's style, an optional one-line chat (once), choices the subclass owns, and an
/// ending where the NPC reacts to what the climber did before the event closes.
/// </summary>
public abstract class EncounterEvent : CustomEventModel
{
    protected abstract string Key { get; }
    protected abstract string Scene { get; }
    protected abstract bool Waifu { get; }
    protected abstract string FallbackPortrait { get; }
    /// <summary>Who turns up for this climber (null: nobody, and the event is not offered).</summary>
    protected abstract NpcDefinition? NpcFor(Player? player);

    protected NpcDefinition? Npc => NpcFor(Owner);

    /// <summary>What the NPC is doing when the climber walks up, for the model.</summary>
    protected abstract string Situation { get; }

    /// <summary>The page's choices (the chat option is added in front while it is unused).</summary>
    protected abstract List<EventOption> Choices();

    private bool _talked;
    private bool _waiting;
    protected bool Finished { get; private set; }

    public override string? CustomInitialPortraitPath => EncounterArt.For(Scene, Waifu, FallbackPortrait);

    public override bool IsAllowed(IRunState runState)
    {
        if (runState.Players.Count != 1) return false;
        var player = runState.Players[0];
        if (NpcFor(player) == null) return false;
        return EncounterBudget.Allows(player, Id.Entry) && IsAllowedFor(player);
    }

    protected virtual bool IsAllowedFor(Player player) => true;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (Owner != null)
        {
            EncounterBudget.Spend(Owner, Id.Entry);
            var npc = Npc;
            if (npc != null) TalkStore.Instance.BeginVisit(npc.Key, RewardExecutor.RunKey(Owner));
        }
        return Page();
    }

    protected List<EventOption> Page()
    {
        var options = new List<EventOption>();
        if (!_talked && Backend.Backends.AiReady) options.Add(Option(Talk, "INITIAL"));
        options.AddRange(Choices());
        return options;
    }

    protected LocString Text(string key) => L10NLookup(Key + "." + key);

    // ── The chat: one line, then back to the choices ─────────────────────

    private async Task Talk()
    {
        if (_talked || _waiting || Owner == null || Npc == null) return;
        _talked = true;
        _waiting = true;
        // Keep the other choices on screen: an event page with no options closes itself.
        var waiting = Text("pages.WAITING.description");
        SetEventState(waiting, Page());
        ThinkingTicker.Attach(Node, waiting.GetFormattedText());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var words = await EncounterVoice.SayAsync(Npc, Owner, Situation + " The climber has just spoken to you.", cts.Token);
        _waiting = false;
        if (Finished || Node == null || !Godot.GodotObject.IsInstanceValid(Node)) return;
        var page = Text(words != null ? "pages.TALK.description" : "pages.TALK.silent");
        AddNames(page);
        page.Add("Words", words ?? "");
        SetEventState(page, Page());
    }

    // ── Ending: the NPC reacts, then the event closes ────────────────────

    /// <summary>Let the NPC answer what just happened, then finish on <paramref name="pageKey"/> (which shows {Words}).</summary>
    /// <remarks>
    /// The event closes at once (the result shows, "Proceed" is there); the reply is written in
    /// under the result when it arrives, with moving marks until then. Nobody has to wait for it.
    /// </remarks>
    protected async Task ReactAndFinish(string whatHappened, string pageKey, Action<LocString>? fill = null)
    {
        if (Finished) return;
        Finished = true;
        var marks = Text("pages.WAITING.description").GetFormattedText();
        var speaks = Owner != null && Npc != null && Backend.Backends.AiReady;
        SetEventFinished(Page(speaks ? pageKey : pageKey + "Silent", speaks ? marks : "", fill));
        if (!speaks) return;
        ThinkingTicker.Attach(Node, marks, asSuffix: true);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var words = await EncounterVoice.SayAsync(Npc!, Owner!, Situation + " " + whatHappened, cts.Token);
        if (Node is not NEventLayout layout || !Godot.GodotObject.IsInstanceValid(layout)) return;
        var final = words != null ? Page(pageKey, words, fill) : Page(pageKey + "Silent", "", fill);
        layout.SetDescription(final.GetFormattedText());
    }

    private LocString Page(string key, string words, Action<LocString>? fill)
    {
        var page = Text(key);
        AddNames(page);
        page.Add("Words", words);
        fill?.Invoke(page);
        return page;
    }

    protected void AddNames(LocString loc)
    {
        loc.Add("Npc", Npc?.DisplayName ?? "?");
    }

    public override LocString InitialDescription
    {
        get
        {
            var loc = Text("pages.INITIAL.description");
            AddNames(loc);
            FillInitial(loc);
            return loc;
        }
    }

    protected virtual void FillInitial(LocString loc) { }
}
