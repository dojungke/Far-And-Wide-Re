using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class EnemyStartingBuff
{
    public CombatBuffDefinition Buff;
    [Min(0)] public int Amount;
}

public enum EnemyActionEffect
{
    Damage,
    ApplyBuff,
    HealSelf,
    HealAllEnemies
}

[System.Serializable]
public sealed class EnemyActionAbility
{
    public CombatAbilityTarget Target = CombatAbilityTarget.Player;
    public EnemyActionEffect Effect;
    [Min(0)] public int Amount;
    public CombatBuffDefinition RelatedBuff;
}

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "CardOpen/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [Header("Presentation")]
    public string EnemyName;
    public string EnglishName;
    public Texture2D Image;
    public Color FallbackColor = Color.white;

    [Header("Stats")]
    [Min(1)] public int MaximumHealth = 50;

    [Header("Starting Buffs")]
    public List<EnemyStartingBuff> StartingBuffs = new List<EnemyStartingBuff>();

    [Header("Planned Action")]
    public string ActionName = "공격";
    public string EnglishActionName = "Attack";
    [Min(1)] public int ActionInterval = 3;

    [Header("Action Abilities")]
    public List<EnemyActionAbility> Abilities = new List<EnemyActionAbility>();

    [Header("Legacy Action Fallback")]
    [Min(0)] public int AttackDamage = 15;
    [Min(0)] public int BleedingStacks = 2;

    public bool HasActionAbilities => Abilities != null && Abilities.Count > 0;

    public int GetActionDamage()
    {
        if (!HasActionAbilities) return Mathf.Max(0, AttackDamage);
        int total = 0;
        for (int i = 0; i < Abilities.Count; i++)
        {
            EnemyActionAbility ability = Abilities[i];
            if (ability != null && ability.Target == CombatAbilityTarget.Player
                && ability.Effect == EnemyActionEffect.Damage)
                total += Mathf.Max(0, ability.Amount);
        }
        return total;
    }

    public int GetActionHealAmount(EnemyActionEffect effect)
    {
        if (!HasActionAbilities) return 0;
        int total = 0;
        for (int i = 0; i < Abilities.Count; i++)
        {
            EnemyActionAbility ability = Abilities[i];
            if (ability != null && ability.Effect == effect)
                total += Mathf.Max(0, ability.Amount);
        }
        return total;
    }
    public int GetActionBuffAmount(string englishBuffName)
    {
        if (!HasActionAbilities)
            return englishBuffName == "Bleeding" ? Mathf.Max(0, BleedingStacks) : 0;
        int total = 0;
        for (int i = 0; i < Abilities.Count; i++)
        {
            EnemyActionAbility ability = Abilities[i];
            if (ability == null || ability.Target != CombatAbilityTarget.Player
                || ability.Effect != EnemyActionEffect.ApplyBuff || ability.RelatedBuff == null) continue;
            if (ability.RelatedBuff.EnglishName == englishBuffName)
                total += Mathf.Max(0, ability.Amount);
        }
        return total;
    }

    public string GetLocalizedName(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishName) ? EnglishName : EnemyName;
    }

    public string GetLocalizedActionName(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishActionName) ? EnglishActionName : ActionName;
    }
}