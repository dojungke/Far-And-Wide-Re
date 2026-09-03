using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StageDeckEntry
{
    public StageCardType Type;
    [Min(1)] public int Copies = 1;
}

[CreateAssetMenu(fileName = "StageDeck", menuName = "CardOpen/Stage Deck")]
public sealed class StageDeckDefinition : ScriptableObject
{
    public List<StageDeckEntry> Entries = new List<StageDeckEntry>();
}