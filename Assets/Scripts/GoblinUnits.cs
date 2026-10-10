using UnityEngine;

public static class GoblinLoadouts
{
    public enum RogueLoadout { Dagger, Bow }
    public enum AssassinLoadout { PoisonShiv, ParalyticShiv }
    public enum BrawlerLoadout { Shield, Club }
}

/// <summary>
/// Status effects that an attack may apply during combat resolution.
/// </summary>
public enum DebuffType
{
    Poison,
    Paralysis
}

/// <summary>
/// Goblin Rogue: dagger (+1 dex) OR bow (+1 range)
/// </summary>
public class GoblinRogue : Unit
{
    [SerializeField] private GoblinLoadouts.RogueLoadout loadout;

    public static GoblinRogue Create()
    {
        GameObject go = new GameObject("Goblin Rogue");
        GoblinRogue unit = go.AddComponent<GoblinRogue>();
        return unit;
    }

    protected override void Awake()
    {
        isMilitary = true;
        strength = 1;
        constitution = 1;
        dexterity = 2;
        agility = 1;
        charisma = 0;
        range = 1;
        autoMove = true;

        loadout = Random.Range(0, 2) == 0 ? GoblinLoadouts.RogueLoadout.Dagger : GoblinLoadouts.RogueLoadout.Bow;
        if (loadout == GoblinLoadouts.RogueLoadout.Dagger) dexterity += 1;
        else range += 1;

        base.Awake();
    }
}

/// <summary>
/// Goblin Assassin: poison shiv (poison debuff) OR paralytic shiv (paralysis debuff)
/// Debuff application hooks are provided via GetOnHitDebuff(), but actual combat resolution
/// will decide when to call TryApplyDebuff() on the target.
/// </summary>
public class GoblinAssassin : Unit
{
    [SerializeField] private GoblinLoadouts.AssassinLoadout loadout;

    public static GoblinAssassin Create()
    {
        GameObject go = new GameObject("Goblin Assassin");
        GoblinAssassin unit = go.AddComponent<GoblinAssassin>();
        return unit;
    }

    protected override void Awake()
    {
        isMilitary = true;
        strength = 1;
        constitution = 1;
        dexterity = 2;
        agility = 1;
        charisma = 0;
        range = 1;
        autoMove = true;

        loadout = Random.Range(0, 2) == 0 ? GoblinLoadouts.AssassinLoadout.PoisonShiv : GoblinLoadouts.AssassinLoadout.ParalyticShiv;
        base.Awake();
    }

    public DebuffType GetOnHitDebuff()
    {
        return loadout == GoblinLoadouts.AssassinLoadout.PoisonShiv
            ? DebuffType.Poison
            : DebuffType.Paralysis;
    }
}

/// <summary>
/// Goblin Brawler: shield (blocks 1 incoming damage, cooldown 5 turns) OR club (+1 attack)
/// </summary>
public class GoblinBrawler : Unit
{
    [SerializeField] private GoblinLoadouts.BrawlerLoadout loadout;
    [SerializeField] private int shieldCooldownTurns = 5;
    [SerializeField] private int shieldCooldownRemaining = 0;

    public static GoblinBrawler Create()
    {
        GameObject go = new GameObject("Goblin Brawler");
        GoblinBrawler unit = go.AddComponent<GoblinBrawler>();
        return unit;
    }

    protected override void Awake()
    {
        isMilitary = true;
        strength = 2;
        constitution = 2;
        dexterity = 1;
        agility = 1;
        charisma = 0;
        range = 1;
        autoMove = true;

        loadout = Random.Range(0, 2) == 0 ? GoblinLoadouts.BrawlerLoadout.Shield : GoblinLoadouts.BrawlerLoadout.Club;
        if (loadout == GoblinLoadouts.BrawlerLoadout.Club)
        {
            attackBonus += 1;
        }

        base.Awake();
    }

    public override void TakeDamage(int amount, Unit source = null)
    {
        if (amount <= 0) return;

        int finalAmount = amount;
        if (loadout == GoblinLoadouts.BrawlerLoadout.Shield && shieldCooldownRemaining <= 0 && finalAmount > 0)
        {
            finalAmount = Mathf.Max(0, finalAmount - 1);
            shieldCooldownRemaining = shieldCooldownTurns;
        }

        base.TakeDamage(finalAmount, source);
    }

    protected override void OnUnitTurn()
    {
        base.OnUnitTurn();

        if (shieldCooldownRemaining > 0)
        {
            shieldCooldownRemaining--;
        }
    }
}

