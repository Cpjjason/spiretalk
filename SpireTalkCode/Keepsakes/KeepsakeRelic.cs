using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using SpireTalk.Store;

namespace SpireTalk.Keepsakes;

/// <summary>
/// A keepsake: the relic an NPC hands the climber once they are friends, and the thing that grows
/// with the relationship. Levels I / II / III / the vow follow the affection tier (stranger / friendly / close / devoted)
/// and never fall within a run; each deal kept with that NPC adds an engraving, which the effect
/// counts and which the profile remembers across runs. Given only through conversation — the relic
/// lives in the event pool so it is never rolled as a reward and never sold.
///
/// Numbers live in <see cref="Values"/>; the description reads them through the usual dynamic
/// vars, so the text on the relic is always what the relic does right now.
/// </summary>
[Pool(typeof(EventRelicPool))]
public abstract class KeepsakeRelic : CustomRelicModel
{
    /// <summary>I stranger, II friendly, III close, IV the vow (devoted).</summary>
    public const int MaxLevel = 4;
    public const int MaxEngravings = 5;

    private int _level = 1;

    /// <summary>Key of the NPC this keepsake belongs to (NEOW, …, MERCHANT).</summary>
    public abstract string NpcKey { get; }

    /// <summary>The vanilla relic whose art this keepsake borrows (atlas sprite name).</summary>
    protected abstract string BorrowedIcon { get; }

    protected override string IconBaseName => BorrowedIcon;

    /// <summary>The mod's own picture for this level (keepsake_neow_2 …), else the borrowed vanilla sprite.</summary>
    private string IconName => $"keepsake_{NpcKey.ToLowerInvariant()}_{Level}";
    private bool IsMerchant => NpcKey == "MERCHANT";

    public override string PackedIconPath => Tokens.RelicArt.Icon(IconName, IsMerchant) ?? base.PackedIconPath;
    protected override string PackedIconOutlinePath => Tokens.RelicArt.Outline(IconName, IsMerchant) ?? base.PackedIconOutlinePath;
    protected override string BigIconPath => Tokens.RelicArt.Big(IconName, IsMerchant) ?? base.BigIconPath;

    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>The engravings show on the icon's counter, the way a relic's charges do.</summary>
    public override bool ShowCounter => Engravings > 0;

    public override int DisplayAmount => Engravings;

    private bool _vowMoment;

    public override string FlashSfx => _vowMoment ? "event:/sfx/ui/timeline/ui_timeline_unlock" : base.FlashSfx;

    /// <summary>The keepsake just became a vow: flash it with the sound the timeline uses for an unlock.</summary>
    public void CelebrateVow()
    {
        _vowMoment = true;
        Flash();
        if (Godot.Engine.GetMainLoop() is Godot.SceneTree tree) tree.CreateTimer(1.5).Timeout += () => _vowMoment = false;
        else _vowMoment = false;
    }

    public override bool IsAllowedInShops => false;

    public override bool IsAllowed(IRunState runState) => runState.Players.Count == 1;

    [SavedProperty]
    public int Level
    {
        get => _level;
        set
        {
            AssertMutable();
            var before = _level;
            _level = Math.Clamp(value, 1, MaxLevel);
            ApplyValues();
            InvokeDisplayAmountChanged();
            if (before != _level)
            {
                RelicIconChanged();
                Tokens.RelicArt.Redraw(this);
            }
        }
    }

    /// <summary>Deals kept with this NPC, ever, on this profile (capped).</summary>
    public int Engravings => Math.Min(MaxEngravings, TalkStore.Instance.Get(NpcKey).Engravings);

    public override LocString Title
    {
        get
        {
            if (Level >= MaxLevel) return new LocString("relics", Id.Entry + ".vowTitle");
            var title = new LocString("relics", Id.Entry + ".title");
            title.Add("Level", Level switch { 3 => "III", 2 => "II", _ => "I" });
            return title;
        }
    }

    /// <summary>The numbers for the current level and engravings, by dynamic-var name.</summary>
    protected abstract IEnumerable<(string key, decimal value)> Values(int level, int engravings);

    protected override IEnumerable<DynamicVar> CanonicalVars
    {
        get
        {
            var vars = new List<DynamicVar> { new("Level", 1m), new("Engravings", 0m) };
            foreach (var (key, value) in Values(1, 0)) vars.Add(MakeVar(key, value));
            foreach (var key in TextVars) vars.Add(new StringVar(key));
            vars.Add(new StringVar("Extra"));
            return vars;
        }
    }

    /// <summary>Text parts of the description that depend on the level (set in <see cref="ApplyText"/>).</summary>
    protected virtual IEnumerable<string> TextVars => Array.Empty<string>();

    /// <summary>Fill the <see cref="TextVars"/> for the current level.</summary>
    protected virtual void ApplyText() { }

    protected void SetText(string key, string value)
    {
        if (DynamicVars.TryGetValue(key, out var v) && v is StringVar sv) sv.StringValue = value;
    }

    /// <summary>The level at which the extra effect (".extra" in the text) switches on: the vow, or III for a few.</summary>
    protected virtual int UnlockLevel => MaxLevel;

    /// <summary>
    /// The extra effect: in normal colours once it is unlocked; before that, greyed on a line of its
    /// own with the relationship it needs, so a stranger's keepsake does not read as if it had it.
    /// </summary>
    private void ApplyExtra()
    {
        var unlocked = Level >= UnlockLevel;
        var text = LocString.GetIfExists("relics", Id.Entry + (unlocked ? ".extra" : ".extraLocked"))?.GetRawText() ?? "";
        SetText("Extra", text.Length == 0 ? "" : unlocked ? text : "\n[color=#8a9898]" + text + "[/color]");
    }

    /// <summary>The game formats some vars specially (energy icons, card counts); those need their own type.</summary>
    protected virtual DynamicVar MakeVar(string key, decimal value) => new(key, value);

    /// <summary>Push the current level/engravings into the dynamic vars the description and hooks read.</summary>
    public void ApplyValues()
    {
        var engravings = Engravings;
        DynamicVars["Level"].BaseValue = Level;
        DynamicVars["Engravings"].BaseValue = engravings;
        foreach (var (key, value) in Values(Level, engravings))
        {
            DynamicVars[key].BaseValue = value;
        }
        ApplyExtra();
        ApplyText();
        if (IsMutable) InvokeDisplayAmountChanged();
    }

    /// <summary>The value behind a description var, for the hooks.</summary>
    protected decimal V(string key) => DynamicVars[key].BaseValue;

    protected int VI(string key) => DynamicVars[key].IntValue;

    /// <summary>A freshly obtained keepsake starts at the level the relationship is at right now.</summary>
    public override async Task AfterObtained()
    {
        ApplyValues();
        await base.AfterObtained();
    }
}
