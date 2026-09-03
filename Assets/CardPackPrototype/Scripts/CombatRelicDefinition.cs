using UnityEngine;

public enum CombatRelicEffect
{
    GreenCardFlatDamage,
    CardUseDamagePercent,
    ShopCurrency
}

[CreateAssetMenu(fileName = "CombatRelic", menuName = "CardOpen/Combat Relic")]
public sealed class CombatRelicDefinition : ScriptableObject
{
    [Header("Presentation")]
    public string RelicName;
    [TextArea(2, 5)] public string Description;
    public string EnglishName;
    [TextArea(2, 5)] public string EnglishDescription;
    public Texture2D Image;
    [Tooltip("0: Common, 1: Uncommon, 2: Rare")] public CardRarity Rarity = CardRarity.Common;
    [Header("Shop Reward Pack")]
    [Tooltip("Whether this relic can appear in a shop reward pack.")] public bool CanAppearInShopRewardPack = true;    [Header("Effect")]
    public CombatRelicEffect Effect;
    [Min(0)] public int Amount;
    public bool ShowAmountAsPercent;

    public string GetLocalizedName(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishName) ? EnglishName : RelicName;
    }

    public string GetLocalizedDescription(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishDescription) ? EnglishDescription : Description;
    }
}
