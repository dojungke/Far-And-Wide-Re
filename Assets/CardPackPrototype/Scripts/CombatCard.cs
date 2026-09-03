using System;
using UnityEngine;

[Serializable]
public sealed class CombatCard
{
    public CombatCardType Type;
    public CardColor Color;
    [Range(1, 6)] public int Number = 1;
}