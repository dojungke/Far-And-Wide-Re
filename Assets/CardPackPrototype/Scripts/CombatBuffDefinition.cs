using UnityEngine;

[CreateAssetMenu(fileName = "CombatBuff", menuName = "CardOpen/Combat Buff")]
public sealed class CombatBuffDefinition : ScriptableObject
{
    [Header("Presentation")]
    public string BuffName;
    [TextArea(2, 5)] public string Description;
    public string EnglishName;
    [TextArea(2, 5)] public string EnglishDescription;
    public Texture2D Image;

    public string GetLocalizedName(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishName) ? EnglishName : BuffName;
    }

    public string GetLocalizedDescription(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishDescription) ? EnglishDescription : Description;
    }
}