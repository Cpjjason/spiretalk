using BaseLib.Abstracts;
using BaseLib.Utils;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;

namespace SpireTalk.Bond;

/// <summary>
/// An Ancient's relic as they hand it to someone they love: the 28 Ancient relics Ancient Affection
/// left without affection versions (two of them are Neow's, newer than that mod). Each keeps what
/// the original does and adds the Ancient looking out for you; tier 2 ("Devoted", affection 50+)
/// and tier 3 ("Beloved", 100+ or married) follow Ancient Affection's naming so they read as one
/// set with its 74. They only ever appear through that mod's relic awakening (see
/// <see cref="BondCatalog"/>): the event pool keeps them out of rewards and shops.
///
/// The originals are referred to by name, not type: two of them don't exist before game 0.111, and
/// one DLL runs on every branch.
/// </summary>
[Pool(typeof(EventRelicPool))]
public abstract class BondRelic : CustomRelicModel
{
    /// <summary>The vanilla relic's class name (MegaCrit.Sts2.Core.Models.Relics.*).</summary>
    public abstract string OriginalName { get; }

    /// <summary>2 = Devoted (affection 50+), 3 = Beloved (100+ or married).</summary>
    public abstract int Tier { get; }

    public bool Beloved => Tier >= 3;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool IsAllowedInShops => false;

    public Type? OriginalType => BondCatalog.VanillaType(OriginalName);

    public RelicModel? Original => OriginalType is { } t ? ModelDb.GetById<RelicModel>(ModelDb.GetId(t)) : null;

    /// <summary>"Devoted Sea Glass": the vanilla title inside the tier's wording, in every language.</summary>
    public override LocString Title
    {
        get
        {
            var title = new LocString("relics", $"SPIRETALK-BOND.tier{Tier}.title");
            if (Original is { } o) title.Add("Relic", o.Title);
            return title;
        }
    }

    // The vanilla relic's own art: an Ancient's relic, given with more love, looks the same.
    private string BaseName => Original?.Id.Entry.ToLowerInvariant() ?? "";

    protected override string IconBaseName => BaseName;

    public override string PackedIconPath => ImageHelper.GetImagePath("atlases/relic_atlas.sprites/" + BaseName + ".tres");

    protected override string PackedIconOutlinePath => ImageHelper.GetImagePath("atlases/relic_outline_atlas.sprites/" + BaseName + ".tres");

    protected override string BigIconPath => ImageHelper.GetImagePath("relics/" + BaseName + ".png");

    /// <summary>
    /// Copy what the game set up on the vanilla relic when it built the Ancient's page (Sea Glass's
    /// character, Dusty Tome's card, Touch of Orobas' starter relic): Ancient Affection swaps the
    /// option's relic for ours after that, and ours has to offer the same thing.
    /// </summary>
    public virtual void SetupFrom(RelicModel original) { }

    /// <summary>A property of the vanilla relic, read by name (its type may not exist at compile time on every branch).</summary>
    protected static T? Read<T>(RelicModel original, string property)
    {
        try { return AccessTools.Property(original.GetType(), property)?.GetValue(original) is T v ? v : default; }
        catch { return default; }
    }
}
