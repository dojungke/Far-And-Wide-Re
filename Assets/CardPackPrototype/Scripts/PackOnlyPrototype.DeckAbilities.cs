using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Random = UnityEngine.Random;
namespace CardOpen.Prototype
{
    public sealed partial class PackOnlyPrototype
    {
        private void AwardCurrentCardScore()
        {
            if (cardIndex < 0 || cardIndex >= currentPackCards.Count) return;
            scorePopupBatchStartIndex = scorePopups.Count;
            StoredCard currentCard = currentPackCards[cardIndex];
            ApplyDeckCardTransformEffects(currentCard);
            int earnedScore;
            string reason;
            Color popupColor;
            switch (currentCard.Rarity)
            {
                case global::CardRarity.Uncommon:
                    earnedScore = 200; reason = Ui("\uACE0\uAE09 \uCE74\uB4DC", "Uncommon card"); popupColor = new Color(0.45f, 1f, 0.72f); break;
                case global::CardRarity.Rare:
                    earnedScore = 300; reason = Ui("\uD76C\uADC0 \uCE74\uB4DC", "Rare card"); popupColor = new Color(0.72f, 0.88f, 1f); break;
                case global::CardRarity.Epic:
                    earnedScore = 500; reason = Ui("\uC601\uC6C5 \uCE74\uB4DC", "Epic card"); popupColor = new Color(1f, 0.73f, 0.22f); break;
                case global::CardRarity.Legendary:
                    earnedScore = 1000; reason = Ui("\uC804\uC124 \uCE74\uB4DC", "Legendary card"); popupColor = new Color(1f, 0.82f, 0.28f); break;
                default:
                    earnedScore = 100; reason = Ui("\uC77C\uBC18 \uCE74\uB4DC", "Common card"); popupColor = Color.white; break;
            }
            int baseCardScoreTotal = earnedScore;
            AddScorePopup(reason + "\n+" + earnedScore + Ui("\uC810", " pts"), popupColor,
                Time.unscaledTime, scorePopups.Count, earnedScore);
            RegisterOtherCardScoreEvent(currentCard);
            TriggerDeckAbilities(currentCard, baseCardScoreTotal);
            previousRevealedCard = currentCard;
        }
        // Legacy score/deck abilities are intentionally disabled for the combat-card ruleset.
        private void TriggerDeckAbilities(StoredCard revealedCard, int baseCardScoreTotal)
        {
        }
        private void TriggerRevealedMineralEnchantments(StoredCard revealedCard)
        {
            // Hologram enchantments are retired.
        }
        private void PrepareNatureAbilityChain(StoredCard revealedCard)
        {
            ClearNatureAbilityChain();
            foreach (StoredCard pendingSource in pendingPackOpenNatureSources)
            {
                if (pendingSource != null && pendingSource.Data != null
                    && pendingSource.Data.HasTag(global::CardTag.Nature))
                {
                    AddNaturallyTriggeredNatureCount(pendingSource, GetEffectiveDeckCopyCount(pendingSource));
                }
            }
            pendingPackOpenNatureSources.Clear();
            if (revealedCard != null)
            {
                for (int i = 0; i < GetAbilityOwnerCount(); i++)
                {
                    StoredCard owner = GetAbilityOwnerAt(i);
                    if (owner == null || owner.Data == null
                        || !owner.Data.HasTag(global::CardTag.Nature)
                        || owner.Data.DeckAbilities == null) continue;
                    for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                    {
                        global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                        if (!IsNatureChainEligibleAbility(ability)) continue;
                        int naturalTriggerCount;
                        if (ability.Effect == global::DeckAbilityEffect.GrantStackToOtherStackCardsAtThreshold)
                            naturalTriggerCount = GetProjectedStackTriggerCount(owner, j, revealedCard);
                        else
                            naturalTriggerCount = GetEffectiveDeckCopyCount(owner)
                                * GetNormalDeckAbilityTriggerCount(ability, owner, revealedCard);
                        if (naturalTriggerCount <= 0) continue;
                        AddNaturallyTriggeredNatureCount(owner, naturalTriggerCount);
                    }
                }
            }
            if (natureAbilityChainTriggerCount == 0) return;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner != null && owner.Data != null
                    && owner.Data.HasTag(global::CardTag.Nature)
                    && natureAbilityChainTriggerCount > GetNaturallyTriggeredNatureCount(owner)
                    && HasNatureChainTargetAbility(owner.Data))
                {
                    natureAbilityChainActive = true;
                    return;
                }
            }
        }
        private void ClearNatureAbilityChain()
        {
            natureAbilityChainActive = false;
            natureAbilityChainTriggerCount = 0;
            naturallyTriggeredNatureCounts.Clear();
        }
        private void AddNaturallyTriggeredNatureCount(StoredCard owner, int count)
        {
            if (owner == null || count <= 0) return;
            naturallyTriggeredNatureCounts.TryGetValue(owner, out int currentCount);
            naturallyTriggeredNatureCounts[owner] = currentCount + count;
            natureAbilityChainTriggerCount += count;
        }
        private int GetNaturallyTriggeredNatureCount(StoredCard owner)
        {
            if (owner == null) return 0;
            return naturallyTriggeredNatureCounts.TryGetValue(owner, out int count) ? count : 0;
        }
        private static bool HasNatureChainTargetAbility(global::CardData data)
        {
            if (data == null || data.DeckAbilities == null) return false;
            for (int i = 0; i < data.DeckAbilities.Count; i++)
                if (IsNatureChainEligibleAbility(data.DeckAbilities[i])) return true;
            return false;
        }
        private static bool IsNatureChainEligibleAbility(global::CardDeckAbility ability)
        {
            return ability != null && ability.CanBeTriggeredByNatureChain();
        }
        private static bool IsNatureChainEligibleEffect(global::DeckAbilityEffect effect)
        {
            return global::CardDeckAbility.IsNatureChainEffectSupported(effect);
        }
        private void ActivateTemporaryDrawBonuses(StoredCard revealedCard)
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null
                        || ability.Effect != global::DeckAbilityEffect.GrantTemporaryPercentForNextDraws
                        || ability.DurationDrawCount <= 0
                        || !DoesDeckAbilityTrigger(ability, owner, revealedCard)) continue;
                    owner.RemainingDrawsByAbility.TryGetValue(j, out int remainingDraws);
                    owner.RemainingDrawsByAbility[j] = remainingDraws + ability.DurationDrawCount;
                }
            }
        }
        private int CountTriggeredDeckEffects(StoredCard revealedCard)
        {
            int count = 0;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null || ability.Trigger == global::DeckAbilityTrigger.TriggeredEffectsAtLeastThree
                        || ability.Effect == global::DeckAbilityEffect.AddNextPackCards) continue;
                    if (ability.Effect == global::DeckAbilityEffect.GrantTemporaryPercentForNextDraws)
                    {
                        if (owner.RemainingDrawsByAbility.TryGetValue(j, out int remainingDraws)
                            && remainingDraws > 0 && ability.PercentBonus > 0f) count += effectiveCopies;
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateFlatScorePerDraw)
                    {
                        if (owner.AccumulatedFlatScoreByAbility.TryGetValue(j, out int accumulatedFlatScore)
                            && accumulatedFlatScore > 0) count++;
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AccumulatePercentAtStackThreshold)
                    {
                        if (owner.AccumulatedPercentByAbility.TryGetValue(j, out float accumulatedPercent)
                            && accumulatedPercent > 0f) count++;
                        continue;
                    }
                    if (IsStackThresholdEffect(ability.Effect))
                    {
                        for (int copy = 0; copy < effectiveCopies; copy++)
                            count += GetStackTriggerCount(owner, j, copy);
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusEfficiencyByNumber)
                    {
                        owner.AccumulatedPercentByAbility.TryGetValue(j, out float currentEfficiency);
                        float maximumEfficiency = ability.MaximumPercent > 0f ? ability.MaximumPercent : 100f;
                        if (ability.NumberMultiplier > 0 && currentEfficiency < maximumEfficiency
                            && DoesDeckAbilityTrigger(ability, owner, revealedCard))
                            count += effectiveCopies;
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AddScorePercentPerPackStack)
                    {
                        for (int copy = 0; copy < effectiveCopies; copy++)
                        {
                            owner.StackByAbilityCopy.TryGetValue(
                                GetAbilityCopyKey(j, copy), out int currentStacks);
                            if (currentStacks > 0 && ability.PercentBonus > 0f) count++;
                        }
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AddScorePerDecayingStack)
                    {
                        for (int copy = 0; copy < effectiveCopies; copy++)
                        {
                            owner.StackByAbilityCopy.TryGetValue(
                                GetAbilityCopyKey(j, copy), out int currentStacks);
                            if (currentStacks > 0 && ability.Score > 0) count++;
                        }
                        continue;
                    }
                    bool hasScoreValue = IsFlatScoreEffect(ability.Effect)
                        ? GetFlatDeckAbilityScore(ability, revealedCard) > 0
                        : (ability.Effect == global::DeckAbilityEffect.AddTriggeredScorePercent
                            || ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusPerDraw)
                            && ability.PercentBonus > 0f;
                    if (hasScoreValue)
                        count += effectiveCopies * GetDeckAbilityTriggerCount(
                            ability, owner, revealedCard);
                }
            }
            return count;
        }
        private void PrepareStackBonusTriggers(StoredCard revealedCard)
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null) continue;
                owner.TriggeredStackCountsThisDraw.Clear();
                if (owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                int sharedStackGain = GetSharedStackTagGainPerDraw(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability != null
                        && ability.Effect == global::DeckAbilityEffect.AddScorePercentPerPackStack
                        && DoesDeckAbilityTrigger(ability, owner, revealedCard))
                    {
                        int perPackStackGain = Mathf.Max(0,
                            revealedCard.Number * ability.NumberMultiplier);
                        if (owner.Data.HasTag(global::CardTag.Stack))
                            perPackStackGain += sharedStackGain;
                        for (int copy = 0; copy < effectiveCopies; copy++)
                        {
                            int stackKey = GetAbilityCopyKey(j, copy);
                            owner.StackByAbilityCopy.TryGetValue(stackKey, out int currentStacks);
                            owner.StackByAbilityCopy[stackKey] = currentStacks + perPackStackGain;
                        }
                        continue;
                    }
                    if (ability != null
                        && ability.Effect == global::DeckAbilityEffect.AddScorePerDecayingStack
                        && DoesDeckAbilityTrigger(ability, owner, revealedCard))
                    {
                        int decayingStackGain = Mathf.Max(0,
                            revealedCard.Number * ability.NumberMultiplier);
                        if (owner.Data.HasTag(global::CardTag.Stack))
                            decayingStackGain += sharedStackGain;
                        for (int copy = 0; copy < effectiveCopies; copy++)
                        {
                            int stackKey = GetAbilityCopyKey(j, copy);
                            owner.StackByAbilityCopy.TryGetValue(stackKey, out int currentStacks);
                            owner.StackByAbilityCopy[stackKey] = currentStacks / 2 + decayingStackGain;
                        }
                        continue;
                    }
                    bool countsDraws = ability != null
                        && (ability.Effect == global::DeckAbilityEffect.TriggerPercentEveryDrawCount
                            || ability.Effect == global::DeckAbilityEffect.TriggerScoreAndPercentAtStackThresholdEveryDraw);
                    bool gainsSharedStacks = owner.Data.HasTag(global::CardTag.Stack)
                        && sharedStackGain > 0;
                    if (ability == null || !IsStackThresholdEffect(ability.Effect)
                        || ability.StackThreshold <= 0
                        || (!countsDraws && ability.NumberMultiplier <= 0 && !gainsSharedStacks)
                        || !DoesDeckAbilityTrigger(ability, owner, revealedCard)) continue;
                    int gainedStacks = countsDraws
                        ? Mathf.Max(1, ability.NumberMultiplier)
                        : Mathf.Max(0, revealedCard.Number * ability.NumberMultiplier);
                    if (gainsSharedStacks) gainedStacks += sharedStackGain;
                    int preparedTriggerCount = 0;
                    owner.PerPackTriggerCountByAbility.TryGetValue(j, out int usedThisPack);
                    for (int copy = 0; copy < effectiveCopies; copy++)
                    {
                        int stackKey = GetAbilityCopyKey(j, copy);
                        owner.StackByAbilityCopy.TryGetValue(stackKey, out int currentStacks);
                        int nextStacks = currentStacks + gainedStacks;
                        int triggerCount = nextStacks / ability.StackThreshold;
                        if ((ability.Effect == global::DeckAbilityEffect.AddSpecificCardAtStackThreshold
                            || ability.Effect == global::DeckAbilityEffect.AddMinedMineralCardAtStackThreshold)
                            && ability.MaxTriggersPerPack > 0)
                        {
                            int availableTriggers = Mathf.Max(0,
                                ability.MaxTriggersPerPack - usedThisPack - preparedTriggerCount);
                            triggerCount = Mathf.Min(triggerCount, availableTriggers);
                            owner.StackByAbilityCopy[stackKey] =
                                nextStacks - triggerCount * ability.StackThreshold;
                        }
                        else
                        {
                            owner.StackByAbilityCopy[stackKey] = nextStacks % ability.StackThreshold;
                        }
                        if (triggerCount <= 0) continue;
                        owner.TriggeredStackCountsThisDraw[stackKey] = triggerCount;
                        preparedTriggerCount += triggerCount;
                    }
                }
            }
        }
        private int GetProjectedStackTriggerCount(StoredCard owner, int abilityIndex,
            StoredCard revealedCard)
        {
            if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null
                || abilityIndex < 0 || abilityIndex >= owner.Data.DeckAbilities.Count
                || revealedCard == null) return 0;
            global::CardDeckAbility ability = owner.Data.DeckAbilities[abilityIndex];
            if (ability == null || ability.StackThreshold <= 0) return 0;
            int gainedStacks = Mathf.Max(0, revealedCard.Number * ability.NumberMultiplier);
            if (owner.Data.HasTag(global::CardTag.Stack))
                gainedStacks += GetSharedStackTagGainPerDraw(owner);
            int triggerCount = 0;
            int effectiveCopies = GetEffectiveDeckCopyCount(owner);
            for (int copy = 0; copy < effectiveCopies; copy++)
            {
                owner.StackByAbilityCopy.TryGetValue(
                    GetAbilityCopyKey(abilityIndex, copy), out int currentStacks);
                triggerCount += (currentStacks + gainedStacks) / ability.StackThreshold;
            }
            return triggerCount;
        }
        private void TriggerStackSharingAbilities()
        {
            int stackGrantCount = 0;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard source = GetAbilityOwnerAt(i);
                if (source == null || source.Data == null || source.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(source);
                for (int abilityIndex = 0; abilityIndex < source.Data.DeckAbilities.Count; abilityIndex++)
                {
                    global::CardDeckAbility ability = source.Data.DeckAbilities[abilityIndex];
                    if (ability == null
                        || ability.Effect != global::DeckAbilityEffect.GrantStackToOtherStackCardsAtThreshold)
                        continue;
                    for (int copy = 0; copy < effectiveCopies; copy++)
                        stackGrantCount += GetStackTriggerCount(source, abilityIndex, copy);
                    if (IsNatureChainForcedTrigger(source, ability))
                        stackGrantCount += Mathf.Max(0, natureAbilityChainTriggerCount
                            - GetNaturallyTriggeredNatureCount(source));
                }
            }
            if (stackGrantCount <= 0) return;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard target = GetAbilityOwnerAt(i);
                if (target == null || target.Data == null || target.Data.DeckAbilities == null
                    || target.Data.IgnoreExternalStackGrants
                    || !target.Data.HasTag(global::CardTag.Stack)) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(target);
                for (int abilityIndex = 0; abilityIndex < target.Data.DeckAbilities.Count; abilityIndex++)
                {
                    global::CardDeckAbility ability = target.Data.DeckAbilities[abilityIndex];
                    if (ability == null) continue;
                    bool storesRawStacks = ability.Effect == global::DeckAbilityEffect.AddScorePerDecayingStack
                        || ability.Effect == global::DeckAbilityEffect.AddScorePercentPerPackStack;
                    if (!storesRawStacks && (!IsStackThresholdEffect(ability.Effect)
                        || ability.StackThreshold <= 0)) continue;
                    target.PerPackTriggerCountByAbility.TryGetValue(abilityIndex, out int usedThisPack);
                    int preparedTriggerCount = 0;
                    for (int copy = 0; copy < effectiveCopies; copy++)
                    {
                        int stackKey = GetAbilityCopyKey(abilityIndex, copy);
                        target.StackByAbilityCopy.TryGetValue(stackKey, out int currentStacks);
                        if (storesRawStacks)
                        {
                            target.StackByAbilityCopy[stackKey] = currentStacks + stackGrantCount;
                            continue;
                        }
                        int nextStacks = currentStacks + stackGrantCount;
                        int triggerCount = nextStacks / ability.StackThreshold;
                        if ((ability.Effect == global::DeckAbilityEffect.AddSpecificCardAtStackThreshold
                            || ability.Effect == global::DeckAbilityEffect.AddMinedMineralCardAtStackThreshold)
                            && ability.MaxTriggersPerPack > 0)
                        {
                            target.TriggeredStackCountsThisDraw.TryGetValue(stackKey,
                                out int alreadyPreparedTriggers);
                            int availableTriggers = Mathf.Max(0, ability.MaxTriggersPerPack
                                - usedThisPack - preparedTriggerCount - alreadyPreparedTriggers);
                            triggerCount = Mathf.Min(triggerCount, availableTriggers);
                            target.StackByAbilityCopy[stackKey] = nextStacks
                                - triggerCount * ability.StackThreshold;
                        }
                        else
                        {
                            target.StackByAbilityCopy[stackKey] = nextStacks % ability.StackThreshold;
                        }
                        if (triggerCount <= 0) continue;
                        target.TriggeredStackCountsThisDraw.TryGetValue(stackKey,
                            out int existingTriggerCount);
                        target.TriggeredStackCountsThisDraw[stackKey] = existingTriggerCount + triggerCount;
                        preparedTriggerCount += triggerCount;
                    }
                }
            }
        }
        private int GetSharedStackTagGainPerDraw(StoredCard excludedOwner)
        {
            int stackCardCopies = 0;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null
                    || !owner.Data.HasTag(global::CardTag.Stack)) continue;
                stackCardCopies += GetEffectiveDeckCopyCount(owner);
            }
            if (excludedOwner != null && excludedOwner.Data != null
                && excludedOwner.Data.HasTag(global::CardTag.Stack))
            {
                stackCardCopies -= GetEffectiveDeckCopyCount(excludedOwner);
            }
            return Mathf.Max(0, stackCardCopies);
        }
        private void AccumulatePerPackEffects(StoredCard revealedCard)
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null) continue;
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateFlatScorePerDraw)
                    {
                        if (ability.Score <= 0 || !DoesDeckAbilityTrigger(ability, owner, revealedCard)) continue;
                        owner.AccumulatedFlatScoreByAbility.TryGetValue(j, out int accumulatedScore);
                        owner.AccumulatedFlatScoreByAbility[j] =
                            accumulatedScore + ability.Score * effectiveCopies;
                        continue;
                    }
                    if (ability.Effect != global::DeckAbilityEffect.AccumulatePercentAtStackThreshold
                        || ability.PercentBonus <= 0f) continue;
                    int totalTriggerCount = 0;
                    for (int copy = 0; copy < effectiveCopies; copy++)
                        totalTriggerCount += GetStackTriggerCount(owner, j, copy);
                    if (totalTriggerCount <= 0) continue;
                    owner.AccumulatedPercentByAbility.TryGetValue(j, out float accumulatedPercent);
                    owner.AccumulatedPercentByAbility[j] =
                        accumulatedPercent + ability.PercentBonus * totalTriggerCount;
                }
            }
        }
        private static bool IsStackThresholdEffect(global::DeckAbilityEffect effect)
        {
            return effect == global::DeckAbilityEffect.TriggerPercentAtStackThreshold
                || effect == global::DeckAbilityEffect.TriggerScoreAtStackThreshold
                || effect == global::DeckAbilityEffect.AccumulatePercentAtStackThreshold
                || effect == global::DeckAbilityEffect.AddSpecificCardAtStackThreshold
                || effect == global::DeckAbilityEffect.TriggerPercentEveryDrawCount
                || effect == global::DeckAbilityEffect.TriggerScoreAndPercentAtStackThresholdEveryDraw
                || effect == global::DeckAbilityEffect.GrantStackToOtherStackCardsAtThreshold
                || effect == global::DeckAbilityEffect.AddMinedMineralCardAtDrawThreshold
                || effect == global::DeckAbilityEffect.AddMinedMineralCardAtStackThreshold;
        }
        private static int GetStackTriggerCount(StoredCard owner, int abilityIndex, int copyIndex)
        {
            if (owner == null) return 0;
            owner.TriggeredStackCountsThisDraw.TryGetValue(
                GetAbilityCopyKey(abilityIndex, copyIndex), out int triggerCount);
            return Mathf.Max(0, triggerCount);
        }
        private static int GetAbilityCopyKey(int abilityIndex, int copyIndex)
        {
            return abilityIndex * 100 + copyIndex;
        }
        private void AccumulateDeckScoreBonuses(StoredCard revealedCard, int triggerRequirementCount)
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null
                        || !DoesDeckAbilityTrigger(ability, owner, revealedCard, triggerRequirementCount)) continue;
                    owner.AccumulatedPercentByAbility.TryGetValue(j, out float accumulatedPercent);
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusEfficiencyByNumber)
                    {
                        if (ability.NumberMultiplier <= 0) continue;
                        float gainedEfficiency = revealedCard.Number * ability.NumberMultiplier * effectiveCopies;
                        float maximumEfficiency = ability.MaximumPercent > 0f ? ability.MaximumPercent : 100f;
                        owner.AccumulatedPercentByAbility[j] =
                            Mathf.Min(maximumEfficiency, accumulatedPercent + gainedEfficiency);
                        continue;
                    }
                    if (ability.Effect != global::DeckAbilityEffect.AccumulateScoreBonusPerDraw
                        || ability.PercentBonus <= 0f) continue;
                    owner.AccumulatedPercentByAbility[j] =
                        accumulatedPercent + ability.PercentBonus * effectiveCopies;
                }
            }
        }
        private static bool IsFlatScoreEffect(global::DeckAbilityEffect effect)
        {
            return effect == global::DeckAbilityEffect.AddScore
                || effect == global::DeckAbilityEffect.AddRevealedNumberTimesScore
                || effect == global::DeckAbilityEffect.TriggerScoreAtStackThreshold
                || effect == global::DeckAbilityEffect.AccumulateFlatScorePerDraw
                || effect == global::DeckAbilityEffect.AddScorePerDecayingStack
                || effect == global::DeckAbilityEffect.TriggerScoreAndPercentAtStackThresholdEveryDraw;
        }
        private static int GetFlatDeckAbilityScore(global::CardDeckAbility ability, StoredCard revealedCard)
        {
            if (ability.Effect == global::DeckAbilityEffect.AddRevealedNumberTimesScore)
                return Mathf.Max(0, revealedCard.Number * ability.NumberMultiplier);
            return Mathf.Max(0, ability.Score);
        }
        private void ResetOncePerPackAbilityUsage()
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null) continue;
                owner.UsedOncePerPackAbilityCopies.Clear();
                owner.PerPackTriggerCountByAbility.Clear();
            }
        }
        private void TriggerPackCardGenerationAbilities(StoredCard revealedCard)
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null || !DoesDeckAbilityTrigger(ability, owner, revealedCard)) continue;
                    if (ability.Effect == global::DeckAbilityEffect.AddMinedMineralCardToPackEnd)
                    {
                        owner.PerPackTriggerCountByAbility.TryGetValue(j, out int usedThisPack);
                        int maximumTriggers = ability.MaxTriggersPerPack > 0
                            ? ability.MaxTriggersPerPack : int.MaxValue;
                        for (int copy = 0; copy < effectiveCopies && usedThisPack < maximumTriggers; copy++)
                        {
                            if (!AppendMinedMineralCardToCurrentPack()) break;
                            usedThisPack++;
                        }
                        owner.PerPackTriggerCountByAbility[j] = usedThisPack;
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AddMinedMineralCardAtDrawThreshold)
                    {
                        owner.PerPackTriggerCountByAbility.TryGetValue(j, out int usedThisPack);
                        int maximumTriggers = ability.MaxTriggersPerPack > 0
                            ? ability.MaxTriggersPerPack : int.MaxValue;
                        int threshold = Mathf.Max(1, ability.StackThreshold);
                        for (int copy = 0; copy < effectiveCopies; copy++)
                        {
                            int abilityCopyKey = GetAbilityCopyKey(j, copy);
                            owner.StackByAbilityCopy.TryGetValue(abilityCopyKey, out int currentCount);
                            currentCount++;
                            int generatedCount = currentCount / threshold;
                            owner.StackByAbilityCopy[abilityCopyKey] = currentCount % threshold;
                            for (int generated = 0; generated < generatedCount
                                && usedThisPack < maximumTriggers; generated++)
                            {
                                if (!AppendMinedMineralCardToCurrentPack()) break;
                                usedThisPack++;
                            }
                        }
                        owner.PerPackTriggerCountByAbility[j] = usedThisPack;
                        continue;
                    }
                    if (ability.Effect != global::DeckAbilityEffect.AddRandomCommonCardToPackEnd) continue;
                    for (int copy = 0; copy < effectiveCopies; copy++)
                    {
                        int usageKey = GetAbilityCopyKey(j, copy);
                        if (owner.UsedOncePerPackAbilityCopies.Contains(usageKey)) continue;
                        if (!AppendRandomCommonCardToCurrentPack()) continue;
                        owner.UsedOncePerPackAbilityCopies.Add(usageKey);
                    }
                }
            }
        }
        private void TriggerStackCardGenerationAbilities()
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null) continue;
                    bool generatesSpecificCard = ability.Effect ==
                        global::DeckAbilityEffect.AddSpecificCardAtStackThreshold;
                    bool generatesMinedMineral = ability.Effect ==
                        global::DeckAbilityEffect.AddMinedMineralCardAtStackThreshold;
                    if ((!generatesSpecificCard && !generatesMinedMineral)
                        || (generatesSpecificCard && ability.GeneratedCard == null)) continue;
                    owner.PerPackTriggerCountByAbility.TryGetValue(j, out int usedThisPack);
                    int maximumTriggers = ability.MaxTriggersPerPack > 0
                        ? ability.MaxTriggersPerPack
                        : int.MaxValue;
                    for (int copy = 0; copy < effectiveCopies && usedThisPack < maximumTriggers; copy++)
                    {
                        int triggerCount = GetStackTriggerCount(owner, j, copy);
                        for (int trigger = 0; trigger < triggerCount && usedThisPack < maximumTriggers; trigger++)
                        {
                            bool generated = generatesMinedMineral
                                ? AppendMinedMineralCardToCurrentPack()
                                : AppendSpecificCardToCurrentPack(ability.GeneratedCard);
                            if (!generated) break;
                            usedThisPack++;
                        }
                    }
                    owner.PerPackTriggerCountByAbility[j] = usedThisPack;
                }
            }
        }
        private bool AppendRandomCommonCardToCurrentPack()
        {
            global::CardPackEntry entry = DrawCommonCard();
            return AppendCardToCurrentPack(entry);
        }
        private bool AppendMinedMineralCardToCurrentPack()
        {
            return AppendCardToCurrentPack(DrawMinedMineralCard());
        }
        private global::CardPackEntry DrawMinedMineralCard()
        {
            int miningLevel = Mathf.Max(1, GetMiningCardCount());
            if (fallbackCards == null || fallbackCards.Length == 0)
                fallbackCards = Resources.LoadAll<global::CardData>("Cards");
            float totalWeight = 0f;
            for (int i = 0; fallbackCards != null && i < fallbackCards.Length; i++)
            {
                global::CardData candidate = fallbackCards[i];
                if (candidate == null || !candidate.HasTag(global::CardTag.Mineral)) continue;
                totalWeight += candidate.GetMiningChance(miningLevel);
            }
            if (totalWeight <= 0f)
            {
                Debug.LogError("Mining failed: no mineral has a positive chance at level " + miningLevel + ".");
                return null;
            }
            float roll = Random.value * totalWeight;
            global::CardData selected = null;
            for (int i = 0; i < fallbackCards.Length; i++)
            {
                global::CardData candidate = fallbackCards[i];
                if (candidate == null || !candidate.HasTag(global::CardTag.Mineral)) continue;
                float weight = candidate.GetMiningChance(miningLevel);
                if (weight <= 0f) continue;
                roll -= weight;
                if (roll > 0f) continue;
                selected = candidate;
                break;
            }
            if (selected == null)
            {
                for (int i = fallbackCards.Length - 1; i >= 0; i--)
                {
                    global::CardData candidate = fallbackCards[i];
                    if (candidate != null && candidate.HasTag(global::CardTag.Mineral)
                        && candidate.GetMiningChance(miningLevel) > 0f)
                    {
                        selected = candidate;
                        break;
                    }
                }
            }
            if (selected == null) return null;
            return new global::CardPackEntry
            {
                Card = selected,
                Number = Random.Range(1, 7),
                Color = (global::CardColor)Random.Range(0, 5),
                InclusionRate = 100f
            };
        }
        private bool ShouldReplaceCurrentPackWithMinedMinerals()
        {
            int leftmostIndex = -1;
            int leftmostSlot = int.MaxValue;
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard candidate = deckCards[i];
                if (candidate == null || candidate.DeckSlot < 0 || candidate.DeckSlot >= leftmostSlot)
                    continue;
                leftmostSlot = candidate.DeckSlot;
                leftmostIndex = i;
            }
            if (leftmostIndex < 0 || leftmostIndex >= deckCards.Count) return false;
            StoredCard leftmostCard = deckCards[leftmostIndex];
            if (leftmostCard == null || leftmostCard.Data == null
                || leftmostCard.Data.DeckAbilities == null) return false;
            for (int i = 0; i < leftmostCard.Data.DeckAbilities.Count; i++)
            {
                global::CardDeckAbility ability = leftmostCard.Data.DeckAbilities[i];
                if (ability != null && ability.Effect ==
                    global::DeckAbilityEffect.ReplaceNextPackWithMinedMineralsWhenLeftmost)
                    return true;
            }
            return false;
        }
        private int GetMiningCardCount()
        {
            int count = 0;
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard card = deckCards[i];
                if (card != null && card.Data != null && card.Data.HasTag(global::CardTag.Mining))
                    count += GetEffectiveDeckCopyCount(card);
            }
            return count;
        }
        private string GetMineralMiningOddsLine(global::CardData data)
        {
            if (data == null || !data.HasTag(global::CardTag.Mineral)) return null;
            int miningLevel = Mathf.Max(1, GetMiningCardCount());
            float chance = data.GetMiningChance(miningLevel);
            return Ui("현재 채굴 수준 " + miningLevel + ": 등장 확률 " + chance.ToString("0.#") + "%",
                "Current Mining Level " + miningLevel + ": " + chance.ToString("0.#") + "% chance");
        }
        private bool AppendRandomTaggedCardToCurrentPack(global::CardTag tag)
        {
            if (activePackData == null) return false;
            global::CardPackEntry entry = activePackData.DrawRandomCard(tag);
            return AppendCardToCurrentPack(entry);
        }
        private bool AppendSpecificCardToCurrentPack(global::CardData data)
        {
            if (data == null) return false;
            return AppendCardToCurrentPack(new global::CardPackEntry
            {
                Card = data,
                Number = Random.Range(1, 7),
                Color = (global::CardColor)Random.Range(0, 5),
                InclusionRate = 100f
            });
        }
        private bool AppendCardToCurrentPack(global::CardPackEntry entry)
        {
            if (entry == null || entry.Card == null) return false;
            global::CardData data = entry.Card;
            int index = cards.Count;
            CardVisual visual = CardVisual.CreatePrefabInstance("Card - " + data.Name + " (Generated)", cardStack);
            GameObject cardObject = visual.gameObject;
            Material attributeMaterial = GetTextureMaterial("Attribute_" + entry.AttributeAssetKey,
                "CardAssets/Attributes/Attribute" + entry.AttributeAssetKey, false);
            Material rarityPatternMaterial = GetTextureMaterial("Pattern_" + data.RarityAssetKey,
                "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0);
            string costAsset = "Cost" + entry.DisplayNumber;
            Material costMaterial = GetTextureMaterial("Cost_" + entry.DisplayNumber,
                "CardAssets/Costs/" + costAsset, true, 20);
            Material illustrationMaterial = GetTextureMaterial("CardImage_" + data.GetHashCode(), data.Image, true, 10);
            visual.BuildFromData(data, entry.Color, attributeMaterial,
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                rarityPatternMaterial, illustrationMaterial, costMaterial, font, IsEnglishUi);
            visual.SetDisplayDescription(data, data.GetLocalizedDescription(IsEnglishUi), IsEnglishUi, GetMineralMiningOddsLine(data));
            bool isHolographic = false;
            visual.PrepareFaceUp(CardHome + new Vector3(0f, index * 0.025f, index * 0.065f),
                CurrentRevealedCardScale, index * 0.35f);
            // Cards generated by deck abilities are appended after the pack is already open.
            // Keep them visible immediately so the physical card stack grows at trigger time.
            visual.gameObject.SetActive(true);
            visual.SetFaceDetailsVisible(true);
            cards.Add(visual);
            currentPackCards.Add(new StoredCard
            {
                Name = data.Name,
                Data = data,
                Rarity = data.Rare,
                Color = entry.Color,
                Number = entry.DisplayNumber,
                IsHolographic = isHolographic
            });
            return true;
        }
        private global::CardPackEntry DrawCommonCard()
        {
            if (activePackData != null)
            {
                global::CardPackEntry entry = activePackData.DrawRandomCard(global::CardRarity.Common);
                if (entry != null) return entry;
            }
            if (fallbackCards == null || fallbackCards.Length == 0)
                fallbackCards = Resources.LoadAll<global::CardData>(string.Empty);
            int commonCount = 0;
            for (int i = 0; fallbackCards != null && i < fallbackCards.Length; i++)
                if (fallbackCards[i] != null && fallbackCards[i].Rare == global::CardRarity.Common) commonCount++;
            if (commonCount <= 0) return null;
            int selectedIndex = Random.Range(0, commonCount);
            for (int i = 0; i < fallbackCards.Length; i++)
            {
                global::CardData card = fallbackCards[i];
                if (card == null || card.Rare != global::CardRarity.Common) continue;
                if (selectedIndex-- > 0) continue;
                return new global::CardPackEntry
                {
                    Card = card,
                    Number = Random.Range(1, 7),
                    Color = (global::CardColor)Random.Range(0, 5),
                    InclusionRate = 100f
                };
            }
            return null;
        }
        private void ResetPerPackAccumulatedBonuses()
        {
            bool changed = false;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null || !ability.ResetAccumulationAfterPack) continue;
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusPerDraw
                        || ability.Effect == global::DeckAbilityEffect.AccumulatePercentAtStackThreshold
                        || ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusEfficiencyByNumber)
                        owner.AccumulatedPercentByAbility.Remove(j);
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateFlatScorePerDraw)
                        owner.AccumulatedFlatScoreByAbility.Remove(j);
                    if (ability.Effect == global::DeckAbilityEffect.AddScorePercentPerPackStack)
                    {
                        ClearAbilityStacks(owner, j);
                        changed = true;
                    }
                }
            }
            if (changed)
            {
                RefreshDeckCardDisplayNames();
                LayoutDeckVisuals();
            }
        }
        private static void ClearAbilityStacks(StoredCard owner, int abilityIndex)
        {
            if (owner == null || owner.StackByAbilityCopy.Count == 0) return;
            List<int> keysToRemove = new List<int>();
            foreach (int key in owner.StackByAbilityCopy.Keys)
                if (key / 100 == abilityIndex) keysToRemove.Add(key);
            for (int i = 0; i < keysToRemove.Count; i++)
                owner.StackByAbilityCopy.Remove(keysToRemove[i]);
        }
        private int GetAdditionalNextPackCardCount()
        {
            int additionalCards = 0;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null || ability.Effect != global::DeckAbilityEffect.AddNextPackCards) continue;
                    additionalCards += Mathf.Max(0, ability.PackCardCount) * GetEffectiveDeckCopyCount(owner);
                }
            }
            return additionalCards;
        }
        private void TriggerPackOpenedDeckAbilities()
        {
            int triggeredCount = 0;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null) continue;
                    if (ability.Effect == global::DeckAbilityEffect.AddNextPackCards)
                    {
                        if (ability.PackCardCount <= 0) continue;
                        if (owner.Data.HasTag(global::CardTag.Nature))
                            pendingPackOpenNatureSources.Add(owner);
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AddRandomTaggedCardOnPackOpen)
                    {
                        int cardsPerCopy = Mathf.Max(1, ability.PackCardCount);
                        for (int copy = 0; copy < effectiveCopies; copy++)
                            for (int generated = 0; generated < cardsPerCopy; generated++)
                                AppendRandomTaggedCardToCurrentPack(ability.GeneratedCardTag);
                        continue;
                    }
                    if (ability.Effect == global::DeckAbilityEffect.AddMinedMineralCardOnPackOpen)
                    {
                        int cardsPerCopy = Mathf.Max(1, ability.PackCardCount);
                        for (int copy = 0; copy < effectiveCopies; copy++)
                            for (int generated = 0; generated < cardsPerCopy; generated++)
                                AppendMinedMineralCardToCurrentPack();
                        continue;
                    }
                    if (ability.Effect != global::DeckAbilityEffect.AddScoreOnPackOpen
                        || ability.Score <= 0) continue;
                    for (int copy = 0; copy < effectiveCopies; copy++)
                        AddDeckAbilityPopup(owner, ability, ability.Score, copy, triggeredCount++);
                }
            }
        }
        private void ApplyDeckCardTransformEffects(StoredCard revealedCard)
        {
            // Hologram transform effects are retired.
        }
        private float GetScoreBonusEfficiencyMultiplier()
        {
            float addedEfficiency = 0f;
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(owner);
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null) continue;
                    if (ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusEfficiencyByNumber)
                    {
                        owner.AccumulatedPercentByAbility.TryGetValue(j, out float accumulatedEfficiency);
                        addedEfficiency += Mathf.Max(0f, accumulatedEfficiency) * 0.01f;
                        continue;
                    }
                    if (ability.Effect != global::DeckAbilityEffect.IncreaseScoreBonusEfficiency
                        || ability.PercentBonus <= 0f) continue;
                    addedEfficiency += ability.PercentBonus * 0.01f * effectiveCopies;
                }
            }
            return 1f + addedEfficiency;
        }
        private static bool IsGroupedRepeatedScoreAbility(
            StoredCard owner, global::CardDeckAbility ability)
        {
            return owner != null && owner.Data != null
                && owner.Data.ScorePopupAggregation == global::ScorePopupAggregation.GroupRepeatedTriggers
                && ability != null
                && ability.Effect == global::DeckAbilityEffect.AddScore;
        }
        private static bool IsMergedCopyScoreAbility(
            StoredCard owner, global::CardDeckAbility ability)
        {
            return owner != null && owner.Data != null
                && owner.Data.ScorePopupAggregation == global::ScorePopupAggregation.MergeEffectiveCopies
                && ability != null
                && ability.Effect == global::DeckAbilityEffect.AddScore;
        }
        private void AddDeckAbilityPopup(StoredCard owner, global::CardDeckAbility ability, int score,
            int copyIndex, int triggeredIndex, bool countForOtherCardScoreEvents = true,
            int popupMultiplier = 1, int otherCardScoreEventCount = 1)
        {
            popupMultiplier = Mathf.Max(1, popupMultiplier);
            string ownerReason = (IsNatureChainForcedTrigger(owner, ability)
                    ? Ui("\uC790\uC5F0-", "Nature - ") : string.Empty)
                + GetStoredCardDisplayName(owner);
            if (copyIndex > 0) ownerReason += Ui(" \uD640\uB85C\uADF8\uB7A8", " Holographic");
            string multiplierText = popupMultiplier > 1 ? " \u00D7 " + popupMultiplier : string.Empty;
            AddScorePopup(ownerReason + "\n+" + score + Ui("\uC810", " pts") + multiplierText,
                copyIndex > 0 ? new Color(0.55f, 0.9f, 1f) : new Color(0.66f, 1f, 0.48f),
                Time.unscaledTime + triggeredIndex * 0.16f, 1 + triggeredIndex % 4,
                score * popupMultiplier);
            if (countForOtherCardScoreEvents)
            {
                for (int eventIndex = 0; eventIndex < Mathf.Max(1, otherCardScoreEventCount); eventIndex++)
                    RegisterOtherCardScoreEvent(owner);
            }
        }
        private void RegisterOtherCardScoreEvent(StoredCard scoringOwner)
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard listener = GetAbilityOwnerAt(i);
                if (listener == null || object.ReferenceEquals(listener, scoringOwner)
                    || listener.Data == null || listener.Data.DeckAbilities == null) continue;
                int effectiveCopies = GetEffectiveDeckCopyCount(listener);
                for (int abilityIndex = 0; abilityIndex < listener.Data.DeckAbilities.Count; abilityIndex++)
                {
                    global::CardDeckAbility ability = listener.Data.DeckAbilities[abilityIndex];
                    if (ability == null
                        || ability.Effect != global::DeckAbilityEffect.AddScoreEveryOtherCardScoreEvents
                        || ability.Score <= 0) continue;
                    int threshold = Mathf.Max(1, ability.StackThreshold);
                    for (int copy = 0; copy < effectiveCopies; copy++)
                    {
                        int stackKey = GetAbilityCopyKey(abilityIndex, copy);
                        listener.StackByAbilityCopy.TryGetValue(stackKey, out int currentStack);
                        currentStack++;
                        int triggerCount = currentStack / threshold;
                        listener.StackByAbilityCopy[stackKey] = currentStack % threshold;
                        for (int trigger = 0; trigger < triggerCount; trigger++)
                        {
                            AddDeckAbilityPopup(listener, ability, ability.Score, copy,
                                scorePopups.Count, false);
                        }
                    }
                }
            }
        }
        private void AddScorePopup(string text, Color color, float startTime, int lane, int score)
        {
            const float baseSameLaneSpacing = 1.36f;
            int batchStartIndex = Mathf.Clamp(scorePopupBatchStartIndex, 0, scorePopups.Count);
            int burstCount = scorePopups.Count - batchStartIndex + 1;
            float burstSpeed = GetScorePopupBurstPlaybackSpeed(burstCount);
            float audioVolumeScale = GetScorePopupBurstAudioScale(burstCount);
            int normalizedLane = ((lane % ScorePopupTrailCapacity) + ScorePopupTrailCapacity)
                % ScorePopupTrailCapacity;
            float now = Time.unscaledTime;
            float requestedDelay = Mathf.Max(0f, startTime - now);
            float scheduledStartTime = now + requestedDelay / Mathf.Max(1f, burstSpeed);
            for (int i = 0; i < scorePopups.Count; i++)
            {
                ScorePopup existing = scorePopups[i];
                if (i >= batchStartIndex)
                {
                    int batchOrder = i - batchStartIndex;
                    float targetSpeed = batchOrder < ScorePopupTrailCapacity
                        ? Mathf.Min(burstSpeed, 5f)
                        : burstSpeed;
                    float previousSpeed = Mathf.Max(1f, existing.PlaybackSpeed);
                    float visualAge = (now - existing.StartTime) * previousSpeed;
                    existing.PlaybackSpeed = Mathf.Max(previousSpeed, targetSpeed);
                    existing.StartTime = now - visualAge / existing.PlaybackSpeed;
                    existing.AudioVolumeScale = Mathf.Min(existing.AudioVolumeScale, audioVolumeScale);
                }
                if (existing.Lane != normalizedLane) continue;
                float laneSpacing = baseSameLaneSpacing / existing.PlaybackSpeed;
                scheduledStartTime = Mathf.Max(scheduledStartTime, existing.StartTime + laneSpacing);
            }
            int newBatchOrder = scorePopups.Count - batchStartIndex;
            float popupSpeed = newBatchOrder < ScorePopupTrailCapacity
                ? Mathf.Min(burstSpeed, 5f)
                : burstSpeed;
            scorePopups.Add(new ScorePopup
            {
                Text = text,
                Color = color,
                StartTime = scheduledStartTime,
                Lane = normalizedLane,
                Score = Mathf.Max(0, score),
                PlaybackSpeed = popupSpeed,
                AudioVolumeScale = audioVolumeScale
            });
            pendingScoreCommitTime = Mathf.Max(pendingScoreCommitTime, scheduledStartTime + 0.2f);
        }
        private static float GetScorePopupBurstPlaybackSpeed(int popupCount)
        {
            if (popupCount >= 30) return 12f;
            if (popupCount >= 20) return 8f;
            if (popupCount >= 10) return 5f;
            if (popupCount >= 6) return 2.5f;
            return 1f;
        }
        private static float GetScorePopupBurstAudioScale(int popupCount)
        {
            if (popupCount >= 30) return 0.12f;
            if (popupCount >= 20) return 0.18f;
            if (popupCount >= 10) return 0.30f;
            if (popupCount >= 6) return 0.55f;
            return 1f;
        }
        private void CommitPendingScoreImmediately()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < scorePopups.Count; i++)
            {
                ScorePopup popup = scorePopups[i];
                float visualAge = (now - popup.StartTime) * Mathf.Max(1f, popup.PlaybackSpeed);
                popup.PlaybackSpeed = 12f;
                popup.StartTime = now - visualAge / popup.PlaybackSpeed;
                if (popup.AddedToPendingScore) continue;
                popup.AddedToPendingScore = true;
                pendingScore += popup.Score;
            }
            int remainingScore = Mathf.Max(0, pendingScore - scoreTransferApplied);
            totalScore += remainingScore;
            roundScore += remainingScore;
            pendingScore = 0;
            pendingScoreCommitTime = -1f;
            scoreTransferAmount = 0;
            scoreTransferApplied = 0;
            scoreTransferStartTime = -1f;
        }
        private void SetupCardRarityAudio()
        {
            cardRarityAudioSource = gameObject.AddComponent<AudioSource>();
            cardRarityAudioSource.playOnAwake = false;
            cardRarityAudioSource.loop = false;
            cardRarityAudioSource.spatialBlend = 0f;
            cardRarityAudioSource.volume = 0.62f;
            const int sampleRate = 44100;
            float[] rootFrequencies = { 392f, 440f, 587.33f, 783.99f, 987.77f };
            float[] durations = { 0.16f, 0.26f, 0.30f, 0.42f, 0.62f };
            for (int tier = 0; tier < cardRarityAudioClips.Length; tier++)
            {
                int sampleCount = Mathf.CeilToInt(sampleRate * durations[tier]);
                float[] samples = new float[sampleCount];
                float root = rootFrequencies[tier];
                for (int i = 0; i < sampleCount; i++)
                {
                    float time = i / (float)sampleRate;
                    float attack = Mathf.Clamp01(time / 0.005f);
                    float envelope = attack * Mathf.Exp(-time * (9.5f - tier * 0.9f));
                    float tone = Mathf.Sin(2f * Mathf.PI * root * time) * 0.62f;
                    if (tier == 1)
                    {
                        const float secondNoteStart = 0.065f;
                        float secondNoteTime = Mathf.Max(0f, time - secondNoteStart);
                        float secondNoteFade = Mathf.SmoothStep(0f, 1f,
                            Mathf.Clamp01(secondNoteTime / 0.018f));
                        tone *= 1f - secondNoteFade * 0.32f;
                        tone += Mathf.Sin(2f * Mathf.PI * root * 1.25f * secondNoteTime)
                            * 0.18f * secondNoteFade * Mathf.Exp(-secondNoteTime * 8f);
                    }
                    else if (tier >= 2)
                    {
                        float fifthFade = Mathf.Clamp01((time - 0.018f) / 0.012f);
                        tone += Mathf.Sin(2f * Mathf.PI * root * 1.4983f * time) * 0.18f * fifthFade;
                    }
                    if (tier >= 2)
                    {
                        float thirdFade = Mathf.Clamp01((time - 0.036f) / 0.014f);
                        tone += Mathf.Sin(2f * Mathf.PI * root * 1.2599f * time) * 0.13f * thirdFade;
                    }
                    if (tier >= 3)
                        tone += Mathf.Sin(2f * Mathf.PI * root * 2f * time) * 0.06f;
                    if (tier >= 4)
                        tone += Mathf.Sin(2f * Mathf.PI * root * 3f * time)
                            * 0.025f * Mathf.Exp(-time * 7f);
                    float tierVolume = tier == 1 ? 0.21f : 0.29f;
                    samples[i] = tone * envelope * tierVolume;
                }
                AudioClip clip = AudioClip.Create(
                    "Card Rarity Reveal " + tier, sampleCount, 1, sampleRate, false);
                clip.SetData(samples, 0);
                cardRarityAudioClips[tier] = clip;
            }
        }
        private void PlayCardRarityRevealSound(global::CardRarity rarity)
        {
            int tier = Mathf.Clamp((int)rarity, 0, cardRarityAudioClips.Length - 1);
            AudioClip clip = cardRarityAudioClips[tier];
            if (cardRarityAudioSource == null || clip == null) return;
            cardRarityAudioSource.Stop();
            cardRarityAudioSource.PlayOneShot(clip);
        }
        private void SetupPackTearAudio()
        {
            packTearAudioSource = gameObject.AddComponent<AudioSource>();
            packTearAudioSource.playOnAwake = false;
            packTearAudioSource.loop = false;
            packTearAudioSource.spatialBlend = 0f;
            packTearAudioSource.volume = 0.65f;
            const int sampleRate = 44100;
            const float duration = 0.26f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            uint noiseState = 0xA3C59AC3u;
            float smoothedNoise = 0f;
            float softenedSample = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float progress = time / duration;
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                float rawNoise = ((noiseState >> 8) / 16777215f) * 2f - 1f;
                smoothedNoise = Mathf.Lerp(smoothedNoise, rawNoise, 0.16f);
                float crispNoise = rawNoise - smoothedNoise * 0.62f;
                float crackle = Mathf.Abs(rawNoise) > 0.76f ? rawNoise : 0f;
                float pulse = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * 34f * time);
                float scrapePhase = 280f * time + 720f * time * time;
                float scrape = Mathf.Sin(2f * Mathf.PI * scrapePhase);
                float attack = Mathf.Clamp01(time / 0.012f);
                float envelope = attack * Mathf.Pow(Mathf.Clamp01(1f - progress), 1.35f);
                float mixedSample = crispNoise * 0.42f + smoothedNoise * 0.22f
                    + crackle * 0.12f + scrape * 0.06f;
                softenedSample = Mathf.Lerp(softenedSample, mixedSample, 0.34f);
                samples[i] = softenedSample * pulse * envelope * 0.44f;
            }
            packTearAudioClip = AudioClip.Create(
                "Card Pack Tear", sampleCount, 1, sampleRate, false);
            packTearAudioClip.SetData(samples, 0);
        }
        private void PlayPackTearSound()
        {
            if (packTearAudioSource == null || packTearAudioClip == null) return;
            packTearAudioSource.Stop();
            packTearAudioSource.PlayOneShot(packTearAudioClip);
        }
        private void SetupScorePopupAudio()
        {
            scorePopupAudioSource = gameObject.AddComponent<AudioSource>();
            scorePopupAudioSource.playOnAwake = false;
            scorePopupAudioSource.loop = false;
            scorePopupAudioSource.spatialBlend = 0f;
            scorePopupAudioSource.volume = 0.54f;
            const int sampleRate = 44100;
            const float duration = 0.38f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.028f));
                float envelope = attack * Mathf.Exp(-time * 7.4f);
                float fundamental = Mathf.Sin(2f * Mathf.PI * 440f * time);
                float warmThird = Mathf.Sin(2f * Mathf.PI * 554.37f * time);
                float softFifth = Mathf.Sin(2f * Mathf.PI * 659.25f * time);
                samples[i] = (fundamental * 0.72f + warmThird * 0.18f + softFifth * 0.07f)
                    * envelope * 0.22f;
            }
            scorePopupAudioClip = AudioClip.Create(
                "Score Popup Ding", sampleCount, 1, sampleRate, false);
            scorePopupAudioClip.SetData(samples, 0);
        }
        private void PlayScorePopupSound(float volumeScale)
        {
            if (scorePopupAudioSource == null || scorePopupAudioClip == null) return;
            scorePopupAudioSource.PlayOneShot(scorePopupAudioClip, Mathf.Clamp01(volumeScale));
        }
        private void SetupAbilityEffectAudio()
        {
            abilityEffectAudioSource = gameObject.AddComponent<AudioSource>();
            abilityEffectAudioSource.playOnAwake = false;
            abilityEffectAudioSource.loop = false;
            abilityEffectAudioSource.spatialBlend = 0f;
            abilityEffectAudioSource.volume = 0.65f;
            const int sampleRate = 44100;
            const float equipDuration = 0.34f;
            int equipSampleCount = Mathf.CeilToInt(sampleRate * equipDuration);
            float[] equipSamples = new float[equipSampleCount];
            float phase = 0f;
            for (int i = 0; i < equipSampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float progress = time / equipDuration;
                float frequency = Mathf.Lerp(440f, 1174.66f, Mathf.SmoothStep(0f, 1f, progress));
                phase += 2f * Mathf.PI * frequency / sampleRate;
                float attack = Mathf.Clamp01(time / 0.012f);
                float envelope = attack * Mathf.Pow(Mathf.Clamp01(1f - progress), 1.35f);
                float tone = Mathf.Sin(phase) * 0.62f + Mathf.Sin(phase * 2f) * 0.17f;
                float sparkle = Mathf.Sin(2f * Mathf.PI * 2349.32f * time)
                    * Mathf.Clamp01((time - 0.12f) / 0.04f) * 0.12f;
                equipSamples[i] = (tone + sparkle) * envelope * 0.38f;
            }
            magicEquipAudioClip = AudioClip.Create(
                "Magic Equip", equipSampleCount, 1, sampleRate, false);
            magicEquipAudioClip.SetData(equipSamples, 0);
            const float resonanceDuration = 0.72f;
            int resonanceSampleCount = Mathf.CeilToInt(sampleRate * resonanceDuration);
            float[] resonanceSamples = new float[resonanceSampleCount];
            for (int i = 0; i < resonanceSampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float progress = time / resonanceDuration;
                float attack = Mathf.Clamp01(time / 0.045f);
                float envelope = attack * Mathf.Pow(Mathf.Clamp01(1f - progress), 0.82f);
                float chord = Mathf.Sin(2f * Mathf.PI * 261.63f * time) * 0.42f
                    + Mathf.Sin(2f * Mathf.PI * 329.63f * time) * 0.34f
                    + Mathf.Sin(2f * Mathf.PI * 392f * time) * 0.28f
                    + Mathf.Sin(2f * Mathf.PI * 783.99f * time) * 0.08f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * (1174.66f + progress * 392f) * time)
                    * Mathf.Sin(Mathf.PI * progress) * 0.08f;
                resonanceSamples[i] = (chord + shimmer) * envelope * 0.42f;
            }
            runeResonanceAudioClip = AudioClip.Create(
                "Rune Resonance", resonanceSampleCount, 1, sampleRate, false);
            runeResonanceAudioClip.SetData(resonanceSamples, 0);
        }
        private void PlayMagicEquipSound()
        {
            if (abilityEffectAudioSource == null || magicEquipAudioClip == null) return;
            abilityEffectAudioSource.Stop();
            abilityEffectAudioSource.PlayOneShot(magicEquipAudioClip);
        }
        private void PlayRuneResonanceSound()
        {
            if (abilityEffectAudioSource == null || runeResonanceAudioClip == null) return;
            abilityEffectAudioSource.Stop();
            abilityEffectAudioSource.PlayOneShot(runeResonanceAudioClip);
        }
        private void UpdatePendingScore()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < scorePopups.Count; i++)
            {
                ScorePopup popup = scorePopups[i];
                if (now < popup.StartTime) continue;
                if (!popup.SoundPlayed)
                {
                    popup.SoundPlayed = true;
                    PlayScorePopupSound(popup.AudioVolumeScale);
                }
                if (popup.AddedToPendingScore) continue;
                popup.AddedToPendingScore = true;
                pendingScore += popup.Score;
            }
            if (scoreTransferStartTime >= 0f)
            {
                float progress = Mathf.Clamp01((now - scoreTransferStartTime) / 0.5f);
                int targetApplied = Mathf.RoundToInt(scoreTransferAmount * Mathf.SmoothStep(0f, 1f, progress));
                int scoreDelta = targetApplied - scoreTransferApplied;
                if (scoreDelta > 0)
                {
                    totalScore += scoreDelta;
                    roundScore += scoreDelta;
                    scoreTransferApplied = targetApplied;
                }
                if (progress < 1f) return;
                pendingScore = Mathf.Max(0, pendingScore - scoreTransferAmount);
                scoreTransferAmount = 0;
                scoreTransferApplied = 0;
                scoreTransferStartTime = -1f;
            }
            if (pendingScore <= 0 || pendingScoreCommitTime < 0f || now < pendingScoreCommitTime) return;
            scoreTransferAmount = pendingScore;
            scoreTransferApplied = 0;
            scoreTransferStartTime = now;
            pendingScoreCommitTime = -1f;
        }
        private static int GetEffectiveDeckCopyCount(StoredCard card)
        {
            return card == null ? 1 : Mathf.Max(1, card.CombinedCopies);
        }
        private static int GetCombinedHolographicCopyCount(StoredCard card)
        {
            return 0;
        }
        private int GetAbilityOwnerCount()
        {
            int count = deckCards.Count;
            for (int i = 0; i < deckCards.Count; i++)
            {
                if (deckCards[i] == null) continue;
                if (deckCards[i].EquippedMagic != null) count++;
                if (deckCards[i].EquippedWeapon != null) count++;
                for (int j = 0; j < deckCards[i].InheritedRelics.Count; j++)
                    if (deckCards[i].InheritedRelics[j] != null) count++;
            }
            return count + (revealedMineralAbilityOwner != null ? 1 : 0);
        }
        private StoredCard GetAbilityOwnerAt(int index)
        {
            if (index < 0) return null;
            if (index < deckCards.Count) return deckCards[index];
            index -= deckCards.Count;
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard host = deckCards[i];
                if (host == null) continue;
                if (host.EquippedMagic != null)
                {
                    if (index == 0) return host.EquippedMagic;
                    index--;
                }
                if (host.EquippedWeapon != null)
                {
                    if (index == 0) return host.EquippedWeapon;
                    index--;
                }
                for (int j = 0; j < host.InheritedRelics.Count; j++)
                {
                    StoredCard relic = host.InheritedRelics[j];
                    if (relic == null) continue;
                    if (index == 0) return relic;
                    index--;
                }
            }
            if (index == 0 && revealedMineralAbilityOwner != null) return revealedMineralAbilityOwner;
            return null;
        }
        private static bool IsRuneCard(StoredCard card)
        {
            return card != null && card.Data != null && card.Data.HasTag(global::CardTag.Rune);
        }
        private bool IsRuneResonanceActive()
        {
            HashSet<global::CardData> runeTypes = new HashSet<global::CardData>();
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard card = deckCards[i];
                if (IsRuneCard(card)) runeTypes.Add(card.Data);
            }
            return runeTypes.Count >= 2;
        }
        private string GetStoredCardDisplayName(StoredCard card)
        {
            if (card == null) return string.Empty;
            string localizedName = card.Data != null ? card.Data.GetLocalizedName(IsEnglishUi) : string.Empty;
            bool hasCustomName = card.Data != null && !string.IsNullOrWhiteSpace(card.Name)
                && card.Name != card.Data.Name;
            string baseName = card.Name == "붕괴" ? Ui("붕괴", "Collapse")
                : hasCustomName ? card.Name : !string.IsNullOrWhiteSpace(localizedName) ? localizedName : card.Name;
            string displayName = IsRuneCard(card) && IsRuneResonanceActive()
                ? baseName + Ui("-공명", " - Resonant") : baseName;
            if (card.CombinedCopies > 1) displayName += " * " + card.CombinedCopies;
            List<string> equippedNames = new List<string>();
            if (card.EquippedMagic != null && card.EquippedMagic.Data != null)
                equippedNames.Add(card.EquippedMagic.Data.GetLocalizedName(IsEnglishUi));
            if (card.EquippedWeapon != null && card.EquippedWeapon.Data != null)
                equippedNames.Add(card.EquippedWeapon.Data.GetLocalizedName(IsEnglishUi));
            return equippedNames.Count > 0
                ? displayName + "(" + string.Join(", ", equippedNames) + ")"
                : displayName;
        }
        private bool IsResonatingRuneAbility(StoredCard owner, global::CardDeckAbility ability,
            global::DeckAbilityEffect effect)
        {
            return ability != null && ability.Effect == effect && IsRuneCard(owner)
                && IsRuneResonanceActive();
        }
        private float GetRuneResonanceValue(StoredCard revealedCard, global::DeckAbilityEffect effect,
            out StoredCard popupOwner, out global::CardDeckAbility popupAbility)
        {
            popupOwner = null;
            popupAbility = null;
            if (revealedCard == null || !IsRuneResonanceActive()) return 0f;
            float total = 0f;
            bool matchesResonanceColor = false;
            HashSet<global::CardData> countedRuneTypes = new HashSet<global::CardData>();
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (!IsRuneCard(owner) || owner.Data.DeckAbilities == null
                    || !countedRuneTypes.Add(owner.Data)) continue;
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null || ability.Effect != effect) continue;
                    if (RevealedCardMatchesAnyColor(revealedCard, owner, ability.ApplicableColors))
                    {
                        matchesResonanceColor = true;
                        if (popupOwner == null)
                        {
                            popupOwner = owner;
                            popupAbility = ability;
                        }
                    }
                    total += GetRuneAbilityResonanceValue(ability, effect);
                }
            }
            return matchesResonanceColor ? total : 0f;
        }
        private float GetRuneResonanceTotalValue(global::DeckAbilityEffect effect)
        {
            if (!IsRuneResonanceActive()) return 0f;
            float total = 0f;
            HashSet<global::CardData> countedRuneTypes = new HashSet<global::CardData>();
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (!IsRuneCard(owner) || owner.Data.DeckAbilities == null
                    || !countedRuneTypes.Add(owner.Data)) continue;
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability == null || ability.Effect != effect) continue;
                    total += GetRuneAbilityResonanceValue(ability, effect);
                }
            }
            return total;
        }
        private static float GetRuneAbilityResonanceValue(global::CardDeckAbility ability,
            global::DeckAbilityEffect effect)
        {
            if (effect == global::DeckAbilityEffect.AddScore) return ability.Score;
            if (effect == global::DeckAbilityEffect.AddTriggeredScorePercent) return ability.PercentBonus;
            return ability.ChancePercent;
        }
        private string GetStoredCardDisplayDescription(StoredCard card)
        {
            if (card == null || card.Data == null) return string.Empty;
            string description = card.Data.GetLocalizedDescription(IsEnglishUi) ?? string.Empty;
            description = ApplyInheritedRelicDescription(card, description);
            description = ApplyEquippedMagicDescription(card, description);
            description = ApplyEquippedWeaponDescription(card, description);
            if (!IsRuneCard(card) || !IsRuneResonanceActive()
                || card.Data.DeckAbilities == null) return description;
            description = ApplyRuneResonanceIncrease(description, card,
                global::DeckAbilityEffect.AddTriggeredScorePercent);
            description = ApplyRuneResonanceIncrease(description, card,
                global::DeckAbilityEffect.AddScore);
            description = ApplyRuneResonanceIncrease(description, card,
                global::DeckAbilityEffect.GrantHologramChance);
            return description;
        }
        private string ApplyInheritedRelicDescription(StoredCard card, string description)
        {
            if (card == null || card.InheritedRelics.Count == 0) return description;
            List<string> lines = new List<string> { Ui("조립 유물 효과", "[Assembled Relic Effects]") };
            for (int i = 0; i < card.InheritedRelics.Count; i++)
            {
                StoredCard relic = card.InheritedRelics[i];
                if (relic == null || relic.Data == null || relic.Data.DeckAbilities == null) continue;
                int copies = GetEffectiveDeckCopyCount(relic);
                string label = relic.Data.GetLocalizedShortStatusName(IsEnglishUi);
                for (int j = 0; j < relic.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = relic.Data.DeckAbilities[j];
                    if (ability == null) continue;
                    switch (ability.Effect)
                    {
                        case global::DeckAbilityEffect.AddScore:
                            lines.Add(label + Ui(": 매 카드 +", ": +") + ability.Score * copies
                                + Ui("점", " pts each draw"));
                            break;
                        case global::DeckAbilityEffect.IncreaseScoreBonusEfficiency:
                            lines.Add(label + Ui(": 보너스 효율 +", ": bonus efficiency +")
                                + (ability.PercentBonus * copies).ToString("0.#") + "%");
                            break;
                        case global::DeckAbilityEffect.AccumulateScoreBonusPerDraw:
                            string suffix = ability.ResetAccumulationAfterPack
                                ? Ui(" (팩 종료 시 초기화)", " (resets after pack)")
                                : string.Empty;
                            lines.Add(label + Ui(": 매 카드 누적 +", ": accumulates +")
                                + (ability.PercentBonus * copies).ToString("0.#")
                                + Ui("%", "% each draw") + suffix);
                            break;
                    }
                }
            }
            return lines.Count <= 1 ? description : description + "\n" + string.Join("\n", lines);
        }
        private string ApplyRuneResonanceIncrease(string description, StoredCard card,
            global::DeckAbilityEffect effect)
        {
            for (int i = 0; i < card.Data.DeckAbilities.Count; i++)
            {
                global::CardDeckAbility ability = card.Data.DeckAbilities[i];
                if (ability == null || ability.Effect != effect) continue;
                float baseValue = GetRuneAbilityResonanceValue(ability, effect);
                if (baseValue <= 0f) continue;
                float totalValue = GetRuneResonanceTotalValue(effect);
                float increase = Mathf.Max(0f, totalValue - baseValue);
                if (increase <= 0f) return description;
                string unit = effect == global::DeckAbilityEffect.AddScore ? Ui("점", " pts") : "%";
                string baseText = baseValue.ToString("0.#") + unit;
                int valueIndex = description.IndexOf(baseText);
                if (valueIndex < 0) return description;
                string increaseText = "(+" + increase.ToString("0.#") + unit + ")";
                return description.Insert(valueIndex + baseText.Length, increaseText);
            }
            return description;
        }
        private string ApplyEquippedMagicDescription(StoredCard card, string description)
        {
            if (card.Data == null || !card.Data.CanEquipMagic || card.EquippedMagic == null
                || card.EquippedMagic.Data == null) return description;
            global::CardData magic = card.EquippedMagic.Data;
            string equippedEffect = magic.GetLocalizedName(IsEnglishUi) + ": "
                + (magic.GetLocalizedDescription(IsEnglishUi) ?? string.Empty)
                + Ui(" (\uC7A5\uCC29\uB428)", " (Equipped)");
            string[] markers =
            {
                "\uB9C8\uBC95\uC744 1\uC7A5 \uC7A5\uCC29\uD560 \uC218 \uC788\uB2E4.",
                "\uB9C8\uBC95\uC744 1\uC7A5 \uC7A5\uCC29\uD560 \uC218 \uC788\uB2E4",
                "\uB9C8\uBC95\uC744 \uD558\uB098 \uC7A5\uCC29\uD560 \uC218 \uC788\uB2E4.",
                "\uB9C8\uBC95\uC744 \uD558\uB098 \uC7A5\uCC29\uD560\uC218 \uC788\uB2E4.",
                "Can equip 1 spell.",
                "Can equip one spell."
            };
            for (int i = 0; i < markers.Length; i++)
            {
                if (description.Contains(markers[i]))
                    return ReplaceEquipmentMarkerWithLineBreak(description, markers[i], equippedEffect);
            }
            return string.IsNullOrWhiteSpace(description)
                ? equippedEffect : description + "\n" + equippedEffect;
        }
        private string ApplyEquippedWeaponDescription(StoredCard card, string description)
        {
            if (card.Data == null || !card.Data.CanEquipWeapon || card.EquippedWeapon == null
                || card.EquippedWeapon.Data == null) return description;
            global::CardData weapon = card.EquippedWeapon.Data;
            string equippedEffect = weapon.GetLocalizedName(IsEnglishUi) + ": "
                + (weapon.GetLocalizedDescription(IsEnglishUi) ?? string.Empty)
                + Ui(" (\uC7A5\uCC29\uB428)", " (Equipped)");
            string[] markers =
            {
                "\uBB34\uAE30\uB97C 1\uAC1C \uC7A5\uCC29\uD560 \uC218 \uC788\uB2E4.",
                "\uBB34\uAE30\uB97C 1\uAC1C \uC7A5\uCC29\uD560 \uC218 \uC788\uB2E4",
                "\uBB34\uAE30\uB97C \uD558\uB098 \uC7A5\uCC29\uD560 \uC218 \uC788\uB2E4.",
                "\uBB34\uAE30\uB97C \uD558\uB098 \uC7A5\uCC29\uD560\uC218 \uC788\uB2E4.",
                "Can equip 1 weapon.",
                "Can equip one weapon."
            };
            for (int i = 0; i < markers.Length; i++)
            {
                if (description.Contains(markers[i]))
                    return ReplaceEquipmentMarkerWithLineBreak(description, markers[i], equippedEffect);
            }
            return string.IsNullOrWhiteSpace(description)
                ? equippedEffect : description + "\n" + equippedEffect;
        }
        private static string ReplaceEquipmentMarkerWithLineBreak(
            string description, string marker, string equippedEffect)
        {
            int markerIndex = description.IndexOf(marker, System.StringComparison.Ordinal);
            if (markerIndex < 0) return description;
            string before = description.Substring(0, markerIndex).TrimEnd();
            if (before.EndsWith(",", System.StringComparison.Ordinal))
                before = before.Substring(0, before.Length - 1).TrimEnd();
            string after = description.Substring(markerIndex + marker.Length).TrimStart();
            if (after.StartsWith(",", System.StringComparison.Ordinal))
                after = after.Substring(1).TrimStart();
            string result = string.IsNullOrEmpty(before) ? equippedEffect : before + "\n" + equippedEffect;
            return string.IsNullOrEmpty(after) ? result : result + "\n" + after;
        }
        private void RefreshLocalizedCardDisplays()
        {
            for (int i = 0; i < cards.Count && i < currentPackCards.Count; i++)
            {
                CardVisual visual = cards[i];
                StoredCard card = currentPackCards[i];
                if (visual == null || card == null || card.Data == null) continue;
                visual.SetDisplayName(GetStoredCardDisplayName(card));
                visual.SetDisplayDescription(card.Data, GetStoredCardDisplayDescription(card), IsEnglishUi,
                    GetMineralMiningOddsLine(card.Data));
            }
            RefreshDeckCardDisplayNames();
            if (packContentsPreviewVisual != null) BuildPackContentsPreviewCard();
        }
        private void RefreshDeckCardDisplayNames()
        {
            for (int i = 0; i < cards.Count && i < currentPackCards.Count; i++)
            {
                StoredCard currentCard = currentPackCards[i];
                CardVisual currentVisual = cards[i];
                if (currentCard == null || currentCard.Data == null || currentVisual == null
                    || !currentCard.Data.HasTag(global::CardTag.Mineral)) continue;
                currentVisual.SetDisplayDescription(currentCard.Data,
                    GetStoredCardDisplayDescription(currentCard), IsEnglishUi,
                    GetMineralMiningOddsLine(currentCard.Data));
            }
            bool resonanceActive = IsRuneResonanceActive();
            if (resonanceActive && !runeResonanceWasActive) PlayRuneResonanceSound();
            runeResonanceWasActive = resonanceActive;
            for (int i = 0; i < deckCards.Count && i < deckVisuals.Count; i++)
            {
                GameObject visualObject = deckVisuals[i];
                if (visualObject == null) continue;
                CardVisual visual = visualObject.GetComponent<CardVisual>();
                if (visual == null) continue;
                visual.SetDisplayName(GetStoredCardDisplayName(deckCards[i]));
                visual.SetDisplayDescription(deckCards[i].Data,
                    GetStoredCardDisplayDescription(deckCards[i]), IsEnglishUi,
                    GetMineralMiningOddsLine(deckCards[i].Data));
            }
        }
        private int GetDeckAbilityTriggerCount(global::CardDeckAbility ability, StoredCard owner,
            StoredCard revealedCard, int triggeredEffectCount = 0)
        {
            int triggerCount = GetNormalDeckAbilityTriggerCount(
                ability, owner, revealedCard, triggeredEffectCount);
            if (IsNatureChainForcedTrigger(owner, ability))
                triggerCount += Mathf.Max(0, natureAbilityChainTriggerCount
                    - GetNaturallyTriggeredNatureCount(owner));
            return triggerCount;
        }
        private int GetNormalDeckAbilityTriggerCount(global::CardDeckAbility ability, StoredCard owner,
            StoredCard revealedCard, int triggeredEffectCount = 0)
        {
            if (!DoesDeckAbilityTriggerNormally(ability, owner, revealedCard, triggeredEffectCount)) return 0;
            return ability.Trigger == global::DeckAbilityTrigger.IncludedColors
                ? CountMatchingAbilityColors(revealedCard, owner, ability.ApplicableColors) : 1;
        }
        private bool DoesDeckAbilityTrigger(global::CardDeckAbility ability, StoredCard owner, StoredCard revealedCard, int triggeredEffectCount = 0)
        {
            return GetDeckAbilityTriggerCount(ability, owner, revealedCard, triggeredEffectCount) > 0;
        }
        private bool IsNatureChainForcedTrigger(StoredCard owner, global::CardDeckAbility ability)
        {
            return natureAbilityChainActive
                && owner != null && owner.Data != null
                && owner.Data.HasTag(global::CardTag.Nature)
                && natureAbilityChainTriggerCount > GetNaturallyTriggeredNatureCount(owner)
                && IsNatureChainEligibleAbility(ability);
        }
        private bool DoesDeckAbilityTriggerNormally(global::CardDeckAbility ability, StoredCard owner, StoredCard revealedCard, int triggeredEffectCount = 0)
        {
            if (owner != null && owner.Data != null && owner.Data.HasTag(global::CardTag.Mineral)
                && !object.ReferenceEquals(owner, revealedCard)) return false;
            switch (ability.Trigger)
            {
                case global::DeckAbilityTrigger.WhenDrawn:
                    return object.ReferenceEquals(owner, revealedCard);
                case global::DeckAbilityTrigger.IncludedNumbers:
                    return ability.ApplicableNumbers != null
                        && ability.ApplicableNumbers.Contains(revealedCard.Number);
                case global::DeckAbilityTrigger.DifferentColor:
                    return !CardColorsMatch(owner, revealedCard);
                case global::DeckAbilityTrigger.MatchingColorAndNumber:
                    return CardColorsMatch(owner, revealedCard)
                        && owner.Number == revealedCard.Number;
                case global::DeckAbilityTrigger.MatchingNumber:
                    return owner.Number == revealedCard.Number;
                case global::DeckAbilityTrigger.EveryCard:
                    return true;
                case global::DeckAbilityTrigger.PreviousCardDifferentColor:
                    return previousRevealedCard != null
                        && !CardColorsMatch(previousRevealedCard, revealedCard);
                case global::DeckAbilityTrigger.TriggeredEffectsAtLeastThree:
                    return triggeredEffectCount >= 3;
                case global::DeckAbilityTrigger.IncludedColors:
                    return RevealedCardMatchesAnyColor(revealedCard, owner, ability.ApplicableColors);
                default:
                    return false;
            }
        }
        private bool ColorsMatch(global::CardColor left, global::CardColor right)
        {
            if (left == right) return true;
            return HasWhiteCardsCountAsAllColors()
                && (left == global::CardColor.White || right == global::CardColor.White);
        }
        private bool CardColorsMatch(StoredCard left, StoredCard right)
        {
            if (IsAllColorCard(left) || IsAllColorCard(right)) return true;
            return left != null && right != null && ColorsMatch(left.Color, right.Color);
        }
        private static bool IsAllColorCard(StoredCard card)
        {
            return card != null && card.Rarity == global::CardRarity.Legendary;
        }
        private bool RevealedCardHasColor(StoredCard card, global::CardColor color)
        {
            if (card == null) return false;
            return IsAllColorCard(card) || card.Color == color
                || (card.Color == global::CardColor.White && HasWhiteCardsCountAsAllColors());
        }
        private bool RevealedCardMatchesAnyColor(StoredCard card, StoredCard owner,
            List<global::AbilityColor> colors)
        {
            return CountMatchingAbilityColors(card, owner, colors) > 0;
        }
        private int CountMatchingAbilityColors(StoredCard card, StoredCard owner,
            List<global::AbilityColor> colors)
        {
            if (card == null || colors == null || colors.Count == 0) return 0;
            int matchCount = 0;
            for (int i = 0; i < colors.Count; i++)
            {
                global::AbilityColor color = colors[i];
                if (color == global::AbilityColor.Self)
                {
                    if (owner != null && CardColorsMatch(owner, card)) matchCount++;
                    continue;
                }
                if (RevealedCardHasColor(card, (global::CardColor)color)) matchCount++;
            }
            return matchCount;
        }
        private bool HasWhiteCardsCountAsAllColors()
        {
            for (int i = 0; i < GetAbilityOwnerCount(); i++)
            {
                StoredCard owner = GetAbilityOwnerAt(i);
                if (owner == null || owner.Data == null || owner.Data.DeckAbilities == null) continue;
                for (int j = 0; j < owner.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = owner.Data.DeckAbilities[j];
                    if (ability != null
                        && ability.Effect == global::DeckAbilityEffect.WhiteCardsCountAsAllColors)
                        return true;
                }
            }
            return false;
        }
        private bool TryAutoEquipMagic(StoredCard magic)
        {
            if (magic == null || magic.Data == null || magic.IsStoredInDeck
                || IsStackableCardData(magic.Data)
                || !magic.Data.HasTag(global::CardTag.Magic)) return false;
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard host = deckCards[i];
                if (host == null || host.Data == null || !host.Data.CanEquipMagic
                    || host.EquippedMagic != null) continue;
                magic.IsStoredInDeck = true;
                magic.DeckSlot = -1;
                host.EquippedMagic = magic;
                PlayMagicEquipSound();
                RefreshDeckCardDisplayNames();
                return true;
            }
            return false;
        }
        private bool TryAutoEquipWeapon(StoredCard weapon)
        {
            if (weapon == null || weapon.Data == null || weapon.IsStoredInDeck
                || IsStackableCardData(weapon.Data)
                || !weapon.Data.HasTag(global::CardTag.Weapon)) return false;
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard host = deckCards[i];
                if (host == null || host.Data == null || !host.Data.CanEquipWeapon
                    || host.EquippedWeapon != null) continue;
                weapon.IsStoredInDeck = true;
                weapon.DeckSlot = -1;
                host.EquippedWeapon = weapon;
                PlayMagicEquipSound();
                RefreshDeckCardDisplayNames();
                return true;
            }
            return false;
        }
        private bool TryAutoEquipStoredCardsToHost(StoredCard host)
        {
            if (host == null || host.Data == null) return false;
            bool equipped = false;
            if (host.Data.CanEquipMagic && host.EquippedMagic == null)
            {
                int hostIndex = deckCards.IndexOf(host);
                for (int i = 0; i < deckCards.Count && hostIndex >= 0; i++)
                {
                    StoredCard candidate = deckCards[i];
                    if (candidate == host || candidate == null || candidate.Data == null
                        || IsStackableCardData(candidate.Data)
                        || !candidate.Data.HasTag(global::CardTag.Magic)) continue;
                    equipped |= TryEquipDeckMagic(i, hostIndex);
                    break;
                }
            }
            if (host.Data.CanEquipWeapon && host.EquippedWeapon == null)
            {
                int hostIndex = deckCards.IndexOf(host);
                for (int i = 0; i < deckCards.Count && hostIndex >= 0; i++)
                {
                    StoredCard candidate = deckCards[i];
                    if (candidate == host || candidate == null || candidate.Data == null
                        || IsStackableCardData(candidate.Data)
                        || !candidate.Data.HasTag(global::CardTag.Weapon)) continue;
                    equipped |= TryEquipDeckWeapon(i, hostIndex);
                    break;
                }
            }
            if (equipped)
            {
                RefreshDeckCardDisplayNames();
                LayoutDeckVisuals();
            }
            return equipped;
        }
        private static bool IsStackableCardData(global::CardData data)
        {
            return data != null && (data.UnlimitedMergeCount || data.MaxMergeCount > 1);
        }
        private bool TryMergeCardIntoDeck(StoredCard incoming)
        {
            if (incoming == null || incoming.Data == null
                || (!incoming.Data.UnlimitedMergeCount && incoming.Data.MaxMergeCount <= 1)
                || incoming.EquippedMagic != null || incoming.EquippedWeapon != null) return false;
            int incomingCopies = Mathf.Max(1, incoming.CombinedCopies);
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard target = deckCards[i];
                if (target == null || target.Data != incoming.Data || target.EquippedMagic != null
                    || target.EquippedWeapon != null) continue;
                int targetCopies = Mathf.Max(1, target.CombinedCopies);
                int mergeLimit = target.Data.UnlimitedMergeCount
                    ? int.MaxValue : Mathf.Max(1, target.Data.MaxMergeCount);
                if (incomingCopies > mergeLimit - targetCopies) continue;
                bool wasHolographic = target.IsHolographic;
                int holographicCopies = GetCombinedHolographicCopyCount(target)
                    + GetCombinedHolographicCopyCount(incoming);
                target.CombinedCopies = targetCopies + incomingCopies;
                target.CombinedHolographicCopies = Mathf.Clamp(holographicCopies, 0, target.CombinedCopies);
                target.IsHolographic = target.CombinedHolographicCopies > 0;
                incoming.IsStoredInDeck = true;
                incoming.DeckSlot = -1;
                if (!wasHolographic && target.IsHolographic && i < deckVisuals.Count
                    && deckVisuals[i] != null)
                {
                    CardVisual visual = deckVisuals[i].GetComponent<CardVisual>();
                    if (visual != null) visual.EnableHologram();
                }
                PlayMagicEquipSound();
                if (!TryFuseCardRecipe())
                {
                    RefreshDeckCardDisplayNames();
                    LayoutDeckVisuals();
                }
                return true;
            }
            return false;
        }
        private int FindFusionMaterialIndex(global::CardData materialData)
        {
            if (materialData == null) return -1;
            int requiredCopies = Mathf.Max(1, materialData.FusionRequiredCopies);
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard card = deckCards[i];
                if (card == null || card.Data != materialData) continue;
                if (Mathf.Max(1, card.CombinedCopies) >= requiredCopies) return i;
            }
            return -1;
        }
        private bool TryFuseCardRecipe()
        {
            HashSet<string> attemptedRecipes = new HashSet<string>();
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard card = deckCards[i];
                string recipeId = card != null && card.Data != null ? card.Data.FusionRecipeId : null;
                if (string.IsNullOrWhiteSpace(recipeId) || !attemptedRecipes.Add(recipeId)) continue;
                if (TryFuseCardRecipe(recipeId)) return true;
            }
            return false;
        }
        private bool TryFuseCardRecipe(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId)) return false;
            if (fallbackCards == null || fallbackCards.Length == 0)
                fallbackCards = Resources.LoadAll<global::CardData>("Cards");
            List<global::CardData> requirements = new List<global::CardData>();
            global::CardData resultData = null;
            HashSet<global::CardData> seenRequirements = new HashSet<global::CardData>();
            for (int i = 0; fallbackCards != null && i < fallbackCards.Length; i++)
            {
                global::CardData candidate = fallbackCards[i];
                if (candidate == null || candidate.FusionRecipeId != recipeId
                    || !seenRequirements.Add(candidate)) continue;
                requirements.Add(candidate);
                if (resultData == null && candidate.FusionResult != null)
                    resultData = candidate.FusionResult;
            }
            if (requirements.Count == 0 || resultData == null) return false;
            List<int> materialIndices = new List<int>();
            StoredCard appearanceSource = null;
            int resultSlot = int.MaxValue;
            bool resultIsHolographic = false;
            for (int i = 0; i < requirements.Count; i++)
            {
                int materialIndex = FindFusionMaterialIndex(requirements[i]);
                if (materialIndex < 0) return false;
                StoredCard material = deckCards[materialIndex];
                materialIndices.Add(materialIndex);
                resultSlot = Mathf.Min(resultSlot, material.DeckSlot);
                resultIsHolographic |= GetCombinedHolographicCopyCount(material) > 0;
                if (appearanceSource == null || material.Data.UseAsFusionAppearanceSource)
                    appearanceSource = material;
            }
            if (appearanceSource == null) return false;
            StoredCard result = new StoredCard
            {
                Name = resultData.Name,
                Data = resultData,
                Rarity = resultData.Rare,
                Color = appearanceSource.Color,
                Number = appearanceSource.Number,
                IsHolographic = resultIsHolographic,
                IsStoredInDeck = true,
                DeckSlot = resultSlot == int.MaxValue ? GetFirstEmptyDeckSlot() : resultSlot,
                CombinedCopies = 1,
                CombinedHolographicCopies = resultIsHolographic ? 1 : 0
            };
            materialIndices.Sort();
            for (int i = materialIndices.Count - 1; i >= 0; i--)
            {
                int materialIndex = materialIndices[i];
                GameObject materialVisual = materialIndex < deckVisuals.Count
                    ? deckVisuals[materialIndex] : null;
                deckCards.RemoveAt(materialIndex);
                if (materialIndex < deckVisuals.Count) deckVisuals.RemoveAt(materialIndex);
                if (materialVisual != null) Destroy(materialVisual);
            }
            deckCards.Add(result);
            deckVisuals.Add(BuildDeckVisualForStoredCard(result));
            PlayMagicEquipSound();
            RefreshDeckCardDisplayNames();
            LayoutDeckVisuals();
            return true;
        }
    }
}
