using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireTalk.Keepsakes;

/// <summary>Neow: blood that keeps coming back. Heals after every fight; at III, one death a run is refused.</summary>
public sealed class KeepsakeNeow : KeepsakeRelic
{
    private bool _lifeUsed;

    public override string NpcKey => "NEOW";
    protected override string BorrowedIcon => "neows_talisman";

    [SavedProperty]
    public bool LifeUsed
    {
        get => _lifeUsed;
        set { AssertMutable(); _lifeUsed = value; }
    }

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Heal", level switch { >= 4 => 8, 3 => 6, 2 => 3, _ => 2 } + engravings),
    };

    public override async Task AfterCombatVictory(CombatRoom _)
    {
        if (Owner.Creature.IsDead) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, V("Heal"));
    }

    public override bool ShouldDieLate(Creature creature)
    {
        if (creature != Owner.Creature || Level < MaxLevel || LifeUsed) return true;
        return false;
    }

    public override async Task AfterPreventingDeath(Creature creature)
    {
        Flash();
        LifeUsed = true;
        await CreatureCmd.Heal(creature, 1m);
    }
}

/// <summary>Orobas: shiny finds after every fight — gold, sometimes a potion; at III, elites drop an extra relic.</summary>
public sealed class KeepsakeOrobas : KeepsakeRelic
{
    public override string NpcKey => "OROBAS";
    protected override string BorrowedIcon => "radiant_pearl";

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Gold", level switch { >= 4 => 30, 3 => 25, 2 => 15, _ => 10 } + engravings * 5),
        ("Chance", level switch { >= 4 => 40, 3 => 35, 2 => 20, _ => 10 } + engravings * 5),
    };

    public override async Task AfterCombatVictory(CombatRoom _)
    {
        if (Owner.Creature.IsDead) return;
        Flash();
        await PlayerCmd.GainGold(V("Gold"), Owner);
        if (Rng.Chaotic.NextInt(100) < VI("Chance"))
        {
            var potion = PotionFactory.CreateRandomPotionOutOfCombat(Owner, Rng.Chaotic).ToMutable();
            await PotionCmd.TryToProcure(potion, Owner);
        }
    }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner || Level < MaxLevel) return false;
        if (room == null || room.RoomType != RoomType.Elite) return false;
        rewards.Add(new RelicReward(player));
        return true;
    }
}

/// <summary>Nonupeipe: dressed for the occasion. Block at the start of every fight; at III, Dexterity too.</summary>
public sealed class KeepsakeNonupeipe : KeepsakeRelic
{
    public override string NpcKey => "NONUPEIPE";
    protected override string BorrowedIcon => "jewelry_box";

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Block", level switch { >= 4 => 6, 3 => 5, 2 => 3, _ => 2 } + engravings),
        ("Dexterity", level >= 4 ? 1 : 0),
    };

    public override async Task BeforeCombatStart()
    {
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, V("Block"), ValueProp.Unpowered, null);
        if (VI("Dexterity") > 0)
        {
            await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, V("Dexterity"), Owner.Creature, null);
        }
    }
}

/// <summary>Pael: flesh that grows. Max HP after every win, up to a cap per act.</summary>
public sealed class KeepsakePael : KeepsakeRelic
{
    private int _gainedThisAct;

    public override string NpcKey => "PAEL";
    protected override string BorrowedIcon => "paels_eye";

    [SavedProperty]
    public int GainedThisAct
    {
        get => _gainedThisAct;
        set { AssertMutable(); _gainedThisAct = value; }
    }

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("MaxHp", level switch { >= 4 => 3, 3 => 2, _ => 1 }),
        ("Cap", (level <= 1 ? 6 : 10) + engravings * 2),
    };

    public override async Task AfterCombatVictory(CombatRoom _)
    {
        if (Owner.Creature.IsDead) return;
        var room = Math.Min(VI("MaxHp"), VI("Cap") - GainedThisAct);
        if (room <= 0) return;
        Flash();
        GainedThisAct += room;
        await CreatureCmd.GainMaxHp(Owner.Creature, room);
    }

    public override Task AfterActEntered()
    {
        GainedThisAct = 0;
        return Task.CompletedTask;
    }
}

/// <summary>Darv: a well-thumbed tome. Extra cards on the first turn; at III, an extra energy too.</summary>
public sealed class KeepsakeDarv : KeepsakeRelic
{
    public override string NpcKey => "DARV";
    protected override int UnlockLevel => 3;
    protected override string BorrowedIcon => "dusty_tome";

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Cards", level switch { >= 4 => 3, 3 or 2 => 2, _ => 1 }),
        ("Energy", 1), // the III bonus; the text names it, the level gates it
    };

    protected override DynamicVar MakeVar(string key, decimal value) => key switch
    {
        "Cards" => new CardsVar((int)value),
        "Energy" => new EnergyVar((int)value),
        _ => base.MakeVar(key, value),
    };

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner || Owner.PlayerCombatState.TurnNumber > 1) return count;
        return count + V("Cards");
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (Level < 3) return; // the energy arrives with level III (close)
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState.TurnNumber > 1) return;
        Flash();
        await PlayerCmd.GainEnergy(V("Energy"), Owner);
    }
}

/// <summary>
/// Tanx: the first blow lands harder (Vigor on turn one, as Akabeko does: it survives damage
/// previews and multi-hits, which a damage hook counting "first" does not). At the vow, Strength
/// against elites and bosses.
/// </summary>
public sealed class KeepsakeTanx : KeepsakeRelic
{
    public override string NpcKey => "TANX";
    protected override string BorrowedIcon => "tanxs_whistle";

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Damage", level switch { >= 4 => 10, 3 => 8, 2 => 4, _ => 3 } + engravings),
        ("Strength", level >= 4 ? 1 : 0),
    };

    public override async Task BeforeCombatStart()
    {
        if (VI("Strength") <= 0) return;
        var type = Owner.RunState.CurrentRoom?.RoomType;
        if (type != RoomType.Elite && type != RoomType.Boss) return;
        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, V("Strength"), Owner.Creature, null);
    }

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState.TurnNumber > 1) return;
        Flash();
        await PowerCmd.Apply<VigorPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, V("Damage"), Owner.Creature, null);
    }
}

/// <summary>Tezcatara: warm soup at every rest. At III, room for one more potion.</summary>
public sealed class KeepsakeTezcatara : KeepsakeRelic
{
    private bool _slotGranted;

    public override string NpcKey => "TEZCATARA";
    protected override string BorrowedIcon => "storybook";

    [SavedProperty]
    public bool SlotGranted
    {
        get => _slotGranted;
        set { AssertMutable(); _slotGranted = value; }
    }

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Heal", level switch { >= 4 => 24, 3 => 20, 2 => 10, _ => 6 } + engravings * 2),
        ("PotionSlots", level >= 4 ? 1 : 0),
    };

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount)
    {
        if (creature.Player != Owner) return amount;
        return amount + V("Heal");
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player == Owner) Flash();
        return Task.CompletedTask;
    }

    /// <summary>The rest option's tooltip lists the extra heal, as Regal Pillow's does.</summary>
    public override IReadOnlyList<LocString> ModifyExtraRestSiteHealText(Player player, IReadOnlyList<LocString> currentExtraText)
    {
        if (!LocalContext.IsMe(Owner)) return currentExtraText;
        var line = AdditionalRestSiteHealText;
        return line == null ? currentExtraText : currentExtraText.Append(line).ToList();
    }

    /// <summary>The extra slot arrives with level III, whenever that happens, once.</summary>
    public async Task GrantSlotIfDue()
    {
        if (SlotGranted || VI("PotionSlots") <= 0) return;
        SlotGranted = true;
        await PlayerCmd.GainMaxPotionCount(VI("PotionSlots"), Owner);
    }
}

/// <summary>Vakuu: a contract. Strength at the start of every fight, paid in blood — until the contract is complete.</summary>
public sealed class KeepsakeVakuu : KeepsakeRelic
{
    public override string NpcKey => "VAKUU";
    protected override string BorrowedIcon => "jeweled_mask";

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Strength", level >= 3 ? 2 : 1),
        ("HpLoss", level switch { >= 4 => 0, 1 => 3, _ => 2 }),
    };

    protected override IEnumerable<string> TextVars => new[] { "Blood" };

    protected override void ApplyText()
    {
        var loss = VI("HpLoss");
        SetText("Blood", loss > 0 ? string.Format(Loc.EventText("vakuuBlood"), loss) : "");
    }

    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, V("Strength"), Owner.Creature, null);
        if (VI("HpLoss") > 0 && Owner.Creature.CurrentHp > VI("HpLoss"))
        {
            await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner.Creature, V("HpLoss"), ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        }
    }
}

/// <summary>The Merchant's ledger. A running discount; at II removals are on the house.</summary>
public sealed class KeepsakeMerchant : KeepsakeRelic
{
    public override string NpcKey => "MERCHANT";
    protected override int UnlockLevel => 3;
    protected override string BorrowedIcon => "membership_card";

    protected override IEnumerable<(string, decimal)> Values(int level, int engravings) => new (string, decimal)[]
    {
        ("Discount", level switch { >= 4 => 20, 3 => 10, 2 => 5, _ => 3 } + engravings * 2),
        ("FreeRemoval", level >= 3 ? 1 : 0),
    };

    public override decimal ModifyMerchantPrice(Player player, MerchantEntry entry, decimal originalPrice)
    {
        if (player != Owner) return originalPrice;
        if (entry is MerchantCardRemovalEntry && VI("FreeRemoval") > 0) return 0m;
        return originalPrice * (1m - V("Discount") / 100m);
    }
}
