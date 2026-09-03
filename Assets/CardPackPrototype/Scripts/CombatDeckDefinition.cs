using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CombatDeck", menuName = "CardOpen/Combat Deck")]
public sealed class CombatDeckDefinition : ScriptableObject
{
    public List<CombatCard> Cards = new List<CombatCard>();
}