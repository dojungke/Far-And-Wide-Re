using UnityEngine;

public enum StageCardKind
{
    Battle,
    EliteBattle,
    BossBattle,
    Rest,
    Event
}

[CreateAssetMenu(fileName = "StageCardType", menuName = "CardOpen/Stage Card Type")]
public sealed class StageCardType : ScriptableObject
{
    [Header("Presentation")]
    public string StageName;
    [TextArea(2, 5)] public string Description;
    public string EnglishName;
    [TextArea(2, 5)] public string EnglishDescription;
    public Texture2D Image;
    public CardColor Color = CardColor.Green;
    [Range(1, 6)] public int Number = 1;

    [Header("Stage")]
    public StageCardKind Kind;
    public BattleEncounters Encounters;

    public string GetLocalizedName(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishName) ? EnglishName : StageName;
    }

    public string GetLocalizedDescription(bool english)
    {
        return english && !string.IsNullOrEmpty(EnglishDescription) ? EnglishDescription : Description;
    }

    public CardData CreateRuntimeCardData()
    {
        CardData data = ScriptableObject.CreateInstance<CardData>();
        data.Name = StageName;
        data.Description = Description;
        data.EnglishName = EnglishName;
        data.EnglishDescription = EnglishDescription;
        data.Image = Image;
        data.Rare = Kind == StageCardKind.BossBattle ? CardRarity.Epic
            : Kind == StageCardKind.EliteBattle ? CardRarity.Rare : CardRarity.Common;
        data.FitBackgroundImageToWidth = true;
        return data;
    }
}