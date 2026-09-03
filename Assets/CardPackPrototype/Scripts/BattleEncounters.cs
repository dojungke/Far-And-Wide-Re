using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class StageEnemyList
{
    public List<EnemyDefinition> Enemies = new List<EnemyDefinition>();
}

[CreateAssetMenu(fileName = "BattleEncounters", menuName = "CardOpen/Battle Encounters")]
public sealed class BattleEncounters : ScriptableObject
{
    [Header("Stage Enemy Lists")]
    public List<StageEnemyList> Stages = new List<StageEnemyList>();

    public StageEnemyList PickRandomStage()
    {
        List<StageEnemyList> validStages = new List<StageEnemyList>();
        for (int i = 0; i < Stages.Count; i++)
            if (Stages[i] != null && Stages[i].Enemies != null && Stages[i].Enemies.Count > 0)
                validStages.Add(Stages[i]);
        return validStages.Count > 0 ? validStages[UnityEngine.Random.Range(0, validStages.Count)] : null;
    }
}