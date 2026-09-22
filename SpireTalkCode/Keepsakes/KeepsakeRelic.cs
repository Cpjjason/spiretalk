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
/// with the relationship. Level I / II / III follow the affection tier (friendly / close / devoted)
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
    public const int MaxLevel = 3;
    public const int MaxEngravings = 5;

    private int _level = 1;

    /// <summary>Key of the NPC this keepsake belongs to (NEOW, …, MERCHANT).</summary>
    public abstract string NpcKey { get; }

    /// <summary>The vanilla relic whose art this keepsake borrows (atlas sprite name).</summary>
    protected abstract string BorrowedIcon { get; }

    protected override string IconBaseName => BorrowedIcon;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsAllowedInShops => false;

    public override bool IsAllowed(IRunState runState) => runState.Players.Count == 1;

    [SavedProperty]
    public int Level
    {
        get => _level;
        set
        {
            AssertMutable();
            _level = Math.Clamp(value, 1, MaxLevel);
            ApplyValues();
            InvokeDisplayAmountChanged();
        }
    }

    /// <summary>Deals kept with this NPC, ever, on this profile (capped).</summary>
    public int Engravings => Math.Min(MaxEngravings, TalkStore.Instance.Get(NpcKey).Engravings);

    public override LocString Title
    {
        get
        {
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
            return vars;
        }
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
