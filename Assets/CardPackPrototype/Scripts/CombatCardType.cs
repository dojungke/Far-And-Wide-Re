using System;
using System.Collections.Generic;
using UnityEngine;

public enum CombatAbilityTarget
{
    SelectedEnemy,
    AllEnemies,
    Player
}

public enum CombatAbilityEffect
{
    Damage,
    IgnoreShieldDamage,
    Burn,
    DrawCards,
    HealAfterUses,
    Scales,
    ApplyBuff,
    DiscardHandAndDamageAll,
    GainShieldAfterUses
}

[Serializable]
public sealed class CombatCardAbility
{
    public CombatAbilityTarget Target;
    public CombatAbilityEffect Effect;
    [Min(0)] public int Amount;
    [Min(1)] public int UsesRequired = 1;
    public bool DoubleAmountAgainstShield;
    public CombatBuffDefinition RelatedBuff;
}

[CreateAssetMenu(fileName = "CombatCardType", menuName = "CardOpen/Combat Card Type")]
public sealed class CombatCardType : ScriptableObject
{
    [Header("Presentation")]
    public string CardName;
    [TextArea(2, 5)] public string Description;
    public string EnglishName;
    [TextArea(2, 5)] public string EnglishDescription;
    public CardRarity Rarity;
    public Texture2D Image;

    [Header("Shop Reward Pack")]
    [Tooltip("Whether this card can appear in a shop reward pack.")] public bool CanAppearInShopRewardPack = true;

    [Header("Use")]
    public bool RequiresEnemyTarget;

    [Header("Abilities")]
    public List<CombatCardAbility> Abilities = new List<CombatCardAbility>();

    public string GetLocalizedName(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishName) ? EnglishName : CardName;
    }

    public string GetLocalizedDescription(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishDescription) ? EnglishDescription : Description;
    }

    public CardData CreateRuntimeCardData()
    {
        CardData data = ScriptableObject.CreateInstance<CardData>();
        data.Name = CardName;
        data.Description = Description;
        data.EnglishName = EnglishName;
        data.EnglishDescription = EnglishDescription;
        data.Rare = Rarity;
        data.Image = Image;
        data.FitBackgroundImageToWidth = true;
        return data;
    }
}