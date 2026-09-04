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
        private StoredCard GetHoveredHandCard(Vector2 screenPoint, out Rect cardRect)
        {
            cardRect = default;
            Camera camera = Camera.main;
            if (camera == null) return null;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                if (cards[i] == null || !cards[i].gameObject.activeSelf) continue;
                Rect currentRect = GetVisualScreenRect(cards[i].gameObject, camera);
                if (!currentRect.Contains(screenPoint)) continue;
                cardRect = currentRect;
                return i < currentPackCards.Count ? currentPackCards[i] : null;
            }
            return null;
        }

        private global::CombatBuffDefinition GetCombatBuffDefinition(string resourceName)
        {
            if (resourceName == "Shield")
            {
                if (shieldBuffDefinition == null)
                    shieldBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Shield");
                return shieldBuffDefinition;
            }
            if (resourceName == "Bleeding")
            {
                if (bleedingBuffDefinition == null)
                    bleedingBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Bleeding");
                return bleedingBuffDefinition;
            }
            if (resourceName == "Stun")
            {
                if (stunBuffDefinition == null)
                    stunBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Stun");
                return stunBuffDefinition;
            }
            if (resourceName == "Regeneration")
            {
                if (regenerationBuffDefinition == null)
                    regenerationBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Regeneration");
                return regenerationBuffDefinition;
            }
            if (resourceName == "Wood")
            {
                if (woodBuffDefinition == null)
                    woodBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Wood");
                return woodBuffDefinition;
            }
            if (resourceName == "Cleverness")
            {
                if (clevernessBuffDefinition == null)
                    clevernessBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Cleverness");
                return clevernessBuffDefinition;
            }
            if (resourceName == "Bind")
            {
                if (bindBuffDefinition == null)
                    bindBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Bind");
                return bindBuffDefinition;
            }
            if (resourceName == "Scales")
            {
                if (scalesBuffDefinition == null)
                    scalesBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Scales");
                return scalesBuffDefinition;
            }
            if (burnBuffDefinition == null)
                burnBuffDefinition = Resources.Load<global::CombatBuffDefinition>("Combat/Buffs/Burn");
            return burnBuffDefinition;
        }
        private global::CombatRelicDefinition GetGoldCurrencyDefinition()
        {
            if (goldCurrencyDefinition == null)
                goldCurrencyDefinition = Resources.Load<global::CombatRelicDefinition>("Combat/Relics/Gold");
            return goldCurrencyDefinition;
        }
        private void LoadStarterRelics()
        {
            ownedRelics.Clear();
            ResetRelicTurnState();
        }
        public bool EditorDebugAddRelic(global::CombatRelicDefinition relic)
        {
            if (relic == null) return false;
            AddRelic(relic);
            combatRelicVisualHash = int.MinValue;
            return true;
        }
        private void AddRelic(global::CombatRelicDefinition relic)
        {
            if (relic == null) return;
            ownedRelics.Add(relic);
            // Refresh the active combat hand so relic-adjusted damage is reflected immediately.
            RefreshLocalizedCardDisplays();
        }
        private bool HasCombatRelicEffect(global::CombatRelicEffect effect)
        {
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                global::CombatRelicDefinition relic = ownedRelics[i];
                if (relic != null && relic.Effect == effect) return true;
            }
            return false;
        }
        private void ResetRelicTurnState()
        {
            relicDamageBonusThisTurn = 0;
            combatRelicVisualHash = int.MinValue;
        }
        private void RollGreenDiceDamageMultiplier(StoredCard card)
        {
            if (card == null || card.Color != global::CardColor.Green) return;
            float multiplier = 1f;
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                global::CombatRelicDefinition relic = ownedRelics[i];
                if (relic == null || relic.Effect != global::CombatRelicEffect.GreenCardRandomAttackDamagePercent) continue;
                int selectedTens = UnityEngine.Random.Range(2, 13);
                multiplier *= 1f + selectedTens * 0.1f;
            }
            card.GreenDiceDamageMultiplier = multiplier;
        }
        private void TriggerCardUseRelics()
        {
            // Multiple Magitech Engines on the same trigger add their flat bonuses first.
            int engineBonusThisUse = 0;
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                global::CombatRelicDefinition relic = ownedRelics[i];
                if (relic != null && relic.Effect == global::CombatRelicEffect.CardUseDamagePercent)
                    engineBonusThisUse += relic.Amount;
            }
            relicDamageBonusThisTurn += engineBonusThisUse;
            if (engineBonusThisUse > 0) RefreshCombatHandCardDescriptions();
            // Magitech Engine bonuses stack additively and are applied as flat damage.
            combatRelicVisualHash = int.MinValue;
        }
        private int GetRelicModifiedDamage(StoredCard card, int damage)
        {
            if (damage <= 0 || card == null) return damage;
            float modifiedDamage = damage;
            for (int i = 0; i < ownedRelics.Count; i++)
            {
                global::CombatRelicDefinition relic = ownedRelics[i];
                if (relic == null || card == null) continue;
                if (relic.Effect == global::CombatRelicEffect.AllCardAttackDamagePlusTwo)
                    modifiedDamage += relic.Amount;
                bool matchesColor = (relic.Effect == global::CombatRelicEffect.GreenCardFlatDamage && card.Color == global::CardColor.Green)
                    || (relic.Effect == global::CombatRelicEffect.RedCardFlatDamage && card.Color == global::CardColor.Red)
                    || (relic.Effect == global::CombatRelicEffect.BlueCardFlatDamage && card.Color == global::CardColor.Blue)
                    || (relic.Effect == global::CombatRelicEffect.BlackWhiteCardFlatDamage && (card.Color == global::CardColor.Black || card.Color == global::CardColor.White));
                if (matchesColor) modifiedDamage += relic.Amount;
            }
            modifiedDamage += relicDamageBonusThisTurn;
            if (card.Color == global::CardColor.Green && card.GreenDiceDamageMultiplier > 0f && card.GreenDiceDamageMultiplier != 1f)
                modifiedDamage *= card.GreenDiceDamageMultiplier;
            return Mathf.RoundToInt(modifiedDamage);
        }
        private void RefreshCombatHandCardDescriptions()
        {
            if (restStageActive || eventChoiceActive || shopRewardOpeningActive || rewardChoiceActive || shopChoiceActive) return;
            int count = Mathf.Min(cards.Count, currentPackCards.Count);
            for (int i = 0; i < count; i++)
            {
                CardVisual visual = cards[i];
                StoredCard card = currentPackCards[i];
                if (visual == null || card == null || card.Data == null) continue;
                visual.SetDisplayDescription(card.Data, GetHandCardDisplayDescription(card), IsEnglishUi, string.Empty);
            }
        }
        private string GetHandCardDisplayDescription(StoredCard card)
        {
            if (card == null || card.CombatType == null) return GetStoredCardDisplayDescription(card);
            string description = GetStoredCardDisplayDescription(card);
            if (rewardChoiceActive || shopChoiceActive || eventChoiceActive || shopRewardOpeningActive) return description;
            if (card.CombatType.Abilities == null) return description;
            for (int i = 0; i < card.CombatType.Abilities.Count; i++)
            {
                CombatCardAbility ability = card.CombatType.Abilities[i];
                if (ability == null || (ability.Effect != global::CombatAbilityEffect.Damage && ability.Effect != global::CombatAbilityEffect.IgnoreShieldDamage)) continue;
                int modified = GetRelicModifiedDamage(card, ability.Amount);
                if (modified == ability.Amount) continue;
                string originalAmount = ability.Amount.ToString();
                string damageMarker = IsEnglishUi ? "damage" : "피해";
                int markerIndex = description.IndexOf(damageMarker, StringComparison.OrdinalIgnoreCase);
                int amountIndex = -1;
                if (markerIndex >= 0)
                {
                    // Most descriptions place the amount before the word damage/피해.
                    int beforeIndex = description.LastIndexOf(originalAmount, markerIndex, StringComparison.Ordinal);
                    amountIndex = beforeIndex >= 0 ? beforeIndex : description.IndexOf(originalAmount, markerIndex + damageMarker.Length, StringComparison.Ordinal);
                }
                if (amountIndex >= 0) description = description.Substring(0, amountIndex) + modified + description.Substring(amountIndex + originalAmount.Length);
            }
            return description;
        }
        private void AddEffectPopupLine(List<string> lines, global::CombatBuffDefinition definition, int count)
        {
            if (definition == null) return;
            string description = definition.GetLocalizedDescription(IsEnglishUi);
            if (!string.IsNullOrEmpty(description) && !lines.Contains(description)) lines.Add(description);
        }
        private void EnsureEffectPopupStyles()
        {
            if (effectPopupTitleStyle != null && effectPopupBodyStyle != null) return;
            effectPopupTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.black }
            };
            effectPopupBodyStyle = new GUIStyle(GUI.skin.label)
            {
                font = font, fontSize = 36, wordWrap = true, alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.06f, 0.06f, 0.06f) }
            };
        }
        private void HandleStartingHandPointer(Vector2 screenPoint, Event inputEvent)
        {
            if (leafMeteorResolving) { inputEvent.Use(); return; }
            Camera camera = Camera.main;
            if (camera == null) return;
            if (inputEvent.type == EventType.MouseDown)
            {
                if (IsPointOverUsedCardPile(screenPoint))
                {
                    if (!rewardChoiceActive && !shopChoiceActive)
                        OpenCombatDeckInspection(CombatDeckInspectionTarget.Discard);
                    inputEvent.Use();
                    return;
                }
                for (int i = cards.Count - 1; i >= 0; i--)
                {
                    CardVisual card = cards[i];
                    if (card == null || !card.gameObject.activeSelf
                        || !GetVisualScreenRect(card.gameObject, camera).Contains(screenPoint)) continue;
                    cardIndex = i;
                    pressedHandIndex = i;
                    pressedHandScreenPosition = screenPoint;
                    inputEvent.Use();
                    return;
                }
                return;
            }
            if (inputEvent.type == EventType.MouseDrag && pressedHandIndex >= 0)
            {
                if ((screenPoint - pressedHandScreenPosition).sqrMagnitude < 25f)
                {
                    inputEvent.Use();
                    return;
                }
                int pressedIndex = pressedHandIndex;
                pressedHandIndex = -1;
                if (pressedIndex >= 0 && pressedIndex < cards.Count && cards[pressedIndex] != null)
                {
                    CardVisual pressedCard = cards[pressedIndex];
                    if (highlightedHandCard == pressedCard)
                    {
                        RestoreStartingHandCard(pressedIndex);
                        highlightedHandCard = null;
                    }
                    draggedHandIndex = pressedIndex;
                    draggedHandRaisedEnough = false;
                    draggedHandStartPosition = pressedCard.transform.position;
                    pressedCard.transform.localScale = Vector3.one * 1.18f;
                    pressedCard.SetSortingOrder(1000);
                }
            }
            if (inputEvent.type == EventType.MouseDrag && draggedHandIndex >= 0
                && draggedHandIndex < cards.Count && cards[draggedHandIndex] != null)
            {
                if (screenPoint.y <= pressedHandScreenPosition.y - 140f) draggedHandRaisedEnough = true;
                Vector3 screenPosition = camera.WorldToScreenPoint(draggedHandStartPosition);
                Vector3 targetPosition = camera.ScreenToWorldPoint(new Vector3(
                    screenPoint.x, Screen.height - screenPoint.y, screenPosition.z));
                cards[draggedHandIndex].transform.position = new Vector3(
                    targetPosition.x, targetPosition.y, draggedHandStartPosition.z - 0.18f);
                // The held card moves with the pointer; its fan angle remains unchanged.
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseUp && pressedHandIndex >= 0)
            {
                int pressedIndex = pressedHandIndex;
                pressedHandIndex = -1;
                if (pressedIndex >= 0 && pressedIndex < cards.Count && cards[pressedIndex] != null)
                {
                    if (highlightedHandCard != null)
                    {
                        int previousIndex = cards.IndexOf(highlightedHandCard);
                        if (previousIndex >= 0) RestoreStartingHandCard(previousIndex);
                    }
                    highlightedHandCard = null;
                    RestoreStartingHandCard(pressedIndex);
                }
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseUp && draggedHandIndex >= 0)
            {
                int targetEnemyIndex;
                if (restStageActive)
                {
                    bool accepted = !IsPointOverUsedCardPile(screenPoint) && IsCardRaisedForCast(draggedHandIndex) && draggedHandRaisedEnough;
                    if (accepted) UseRestCard(draggedHandIndex); else RestoreStartingHandCard(draggedHandIndex);
                    draggedHandIndex = -1; draggedHandRaisedEnough = false; inputEvent.Use(); return;
                }
                if (shopRewardOpeningActive)
                {
                    bool accepted = !IsPointOverUsedCardPile(screenPoint) && IsCardRaisedForCast(draggedHandIndex) && draggedHandRaisedEnough;
                    if (accepted) UseShopRewardCard(draggedHandIndex); else RestoreStartingHandCard(draggedHandIndex);
                    draggedHandIndex = -1; draggedHandRaisedEnough = false; inputEvent.Use(); return;
                }
                if (rewardChoiceActive || shopChoiceActive || eventChoiceActive)
                {
                    bool rejected = IsPointOverUsedCardPile(screenPoint);
                    bool accepted = !rejected && IsCardRaisedForCast(draggedHandIndex) && draggedHandRaisedEnough
                        && (!shopChoiceActive || CanPurchaseShopCard(draggedHandIndex));
                    if (accepted || (rewardChoiceActive && rejected))
                    {
                        ResolveOffer(accepted, (shopChoiceActive || eventChoiceActive) ? draggedHandIndex : -1);
                        draggedHandIndex = -1;
                        inputEvent.Use();
                        return;
                    }
                    if (shopChoiceActive && rejected)
                    {
                        DiscardShopOfferCard(draggedHandIndex);
                        draggedHandIndex = -1;
                        inputEvent.Use();
                        return;
                    }
                }
                StoredCard draggedCard = draggedHandIndex < currentPackCards.Count
                    ? currentPackCards[draggedHandIndex] : null;
                if (IsPointOverUsedCardPile(screenPoint))
                {
                    DiscardStartingHandCard(draggedHandIndex);
                    draggedHandIndex = -1;
                    draggedHandRaisedEnough = false;
                    inputEvent.Use();
                    return;
                }
                if (IsTargetedSpell(draggedCard) && TryGetEnemyIndexAt(screenPoint, out targetEnemyIndex))
                {
                    UseStartingHandCard(draggedHandIndex, targetEnemyIndex);
                    draggedHandIndex = -1;
                    draggedHandRaisedEnough = false;
                    inputEvent.Use();
                    return;
                }
                if (IsNonTargetedSpell(draggedCard) && IsCardRaisedForCast(draggedHandIndex))
                {
                    UseStartingHandCard(draggedHandIndex, -1);
                    draggedHandIndex = -1;
                    draggedHandRaisedEnough = false;
                    inputEvent.Use();
                    return;
                }
                if (draggedHandIndex < cards.Count && cards[draggedHandIndex] != null)
                {
                    CardVisual releasedCard = cards[draggedHandIndex];
                    bool droppedBelowHand = draggedHandIndex < startingHandHomePositions.Count
                        && releasedCard.transform.position.y < startingHandHomePositions[draggedHandIndex].y + 0.04f;
                    if (droppedBelowHand)
                    {
                        int targetIndex = FindStartingHandTarget(draggedHandIndex);
                        if (targetIndex >= 0) MoveStartingHandCard(draggedHandIndex, targetIndex);
                        else RestoreStartingHandCard(draggedHandIndex);
                    }
                    else RestoreStartingHandCard(draggedHandIndex);
                }
                draggedHandIndex = -1;
                inputEvent.Use();
            }
        }
        private void DrawUsedPileOverlay(float scale, float offsetX, float offsetY)
        {
            UpdateCanvasUsedPileInspectionHud();
            if (usedPileDetailCard != null)
            {
                DrawUsedPileInspectionRarity(scale, offsetX, offsetY);
                HandleUsedPileInspectionPointer(Event.current.mousePosition, Event.current);
                return;
            }
            if (Event.current.type != EventType.MouseDown) return;
            CardVisual clickedCard = GetExpandedUsedPileCardAt(Event.current.mousePosition);
            if (clickedCard != null) OpenUsedPileCardInspection(clickedCard);
            else SetUsedPileOverlayVisible(false);
            Event.current.Use();
        }

        private void DrawUsedPileInspectionRarity(float scale, float offsetX, float offsetY)
        {
            if (canvasUsedPileInspectionRarity != null) return;
            int historyIndex = usedPileHistory.IndexOf(usedPileDetailCard);
            if (historyIndex < 0 || historyIndex >= usedPileCardData.Count || usedPileCardData[historyIndex] == null) return;
            EnsureDiscardStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            GUI.color = GetRarityDisplayColor(usedPileCardData[historyIndex].Rare);
            GUI.Label(UiRect(new Rect(490f, 18f, 300f, 48f),
                new Rect(210f, 205f + PortraitExtraHeight * 0.5f, 300f, 52f)),
                GetRarityDisplayName(usedPileCardData[historyIndex].Rare), deckRarityStyle);
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private void HandleUsedPileInspectionPointer(Vector2 screenPoint, Event inputEvent)
        {
            Camera camera = Camera.main;
            if (camera == null || usedPileDetailCard == null) return;
            if (inputEvent.type == EventType.MouseDown)
            {
                if (deckInspectionReturnRoutine != null) StopCoroutine(deckInspectionReturnRoutine);
                deckInspectionReturnRoutine = null;
                deckInspectionReturning = false;
                deckInspectionDragging = true;
                deckInspectionHasDragged = false;
                deckInspectionPressOutside = !GetVisualScreenRect(usedPileDetailCard.gameObject, camera).Contains(screenPoint);
                deckInspectionDragStart = screenPoint;
                deckInspectionStartRotation = usedPileDetailCard.transform.rotation;
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseDrag && deckInspectionDragging)
            {
                Vector2 delta = screenPoint - deckInspectionDragStart;
                if (delta.sqrMagnitude >= 16f) deckInspectionHasDragged = true;
                if (deckInspectionHasDragged)
                    usedPileDetailCard.transform.rotation = Quaternion.Euler(-delta.y * 0.24f, delta.x * 0.28f, 0f)
                        * deckInspectionStartRotation;
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseUp && deckInspectionDragging)
            {
                deckInspectionDragging = false;
                if (deckInspectionPressOutside && !deckInspectionHasDragged) CloseUsedPileCardInspection();
                else if (deckInspectionHasDragged)
                    deckInspectionReturnRoutine = StartCoroutine(ReturnInspectedDeckCard(usedPileDetailCard.gameObject));
                deckInspectionPressOutside = false;
                deckInspectionHasDragged = false;
                inputEvent.Use();
            }
        }

        private void OpenUsedPileCardInspection(CardVisual card)
        {
            if (card == null) return;
            usedPileDetailCard = card;
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
            if (combatPlayerCharacter != null) combatPlayerCharacter.SetActive(false);
            if (usedPileRoot != null)
                for (int i = 0; i < usedPileRoot.childCount; i++)
                    if (usedPileRoot.GetChild(i) != card.transform) usedPileRoot.GetChild(i).gameObject.SetActive(false);
            deckInspectionDragging = false;
            deckInspectionReturning = false;
            deckInspectionPressOutside = false;
            deckInspectionHasDragged = false;
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(true);
            LayoutUsedCardPile();
        }

        private void CloseUsedPileCardInspection()
        {
            if (deckInspectionReturnRoutine != null) StopCoroutine(deckInspectionReturnRoutine);
            deckInspectionReturnRoutine = null;
            deckInspectionDragging = false;
            deckInspectionReturning = false;
            usedPileDetailCard = null;
            if (usedPileRoot != null)
            {
                usedPileRoot.gameObject.SetActive(!stageSelectionVisible);
                for (int i = 0; i < usedPileRoot.childCount; i++) usedPileRoot.GetChild(i).gameObject.SetActive(true);
            }
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(stageSelectionVisible && !tutorialOpen);
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(false);
            LayoutUsedCardPile();
        }
        private void SetUsedPileOverlayVisible(bool visible)
        {
            usedPileExpanded = visible;
            if (!visible)
            {
                usedPileDetailCard = null;
                if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(false);
            }
            SpriteRenderer backgroundRenderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
            if (backgroundRenderer != null)
            {
                if (visible && !usedPileBackgroundDimmed)
                {
                    usedPileBackgroundColor = backgroundRenderer.color;
                    backgroundRenderer.color = new Color(usedPileBackgroundColor.r * 0.42f,
                        usedPileBackgroundColor.g * 0.42f, usedPileBackgroundColor.b * 0.42f,
                        usedPileBackgroundColor.a);
                    usedPileBackgroundDimmed = true;
                }
                else if (!visible && usedPileBackgroundDimmed)
                {
                    backgroundRenderer.color = usedPileBackgroundColor;
                    usedPileBackgroundDimmed = false;
                }
            }
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].gameObject.SetActive(!visible);
            if (visible) LayoutUsedCardPile();
            else LayoutStartingHand();
        }
        private CardVisual GetExpandedUsedPileCardAt(Vector2 screenPoint)
        {
            Camera camera = Camera.main;
            if (camera == null) return null;
            for (int i = usedPileHistory.Count - 1; i >= 0; i--)
            {
                CardVisual card = usedPileHistory[i];
                if (card != null && card.gameObject.activeSelf
                    && GetVisualScreenRect(card.gameObject, camera).Contains(screenPoint)) return card;
            }
            return null;
        }
        private bool IsPointOverExpandedUsedPileCard(Vector2 screenPoint)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            for (int i = 0; i < usedPileHistory.Count; i++)
            {
                CardVisual card = usedPileHistory[i];
                if (card != null && card.gameObject.activeSelf
                    && GetVisualScreenRect(card.gameObject, camera).Contains(screenPoint)) return true;
            }
            return false;
        }
        private void ClearUsedCardPile()
        {
            if (usedPileRoutine != null) StopCoroutine(usedPileRoutine);
            for (int i = 0; i < usedPileHistory.Count; i++)
                if (usedPileHistory[i] != null) Destroy(usedPileHistory[i].gameObject);
            usedPileHistory.Clear();
            usedPileCardData.Clear();
            usedPileStoredCards.Clear();
            usedPileCard = null;
            if (usedPileBackgroundDimmed)
            {
                SpriteRenderer backgroundRenderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
                if (backgroundRenderer != null) backgroundRenderer.color = usedPileBackgroundColor;
                usedPileBackgroundDimmed = false;
            }
            usedPileExpanded = false;
            usedPileAnimating = false;
            if (usedPilePlaceholder != null) usedPilePlaceholder.gameObject.SetActive(true);
            LayoutUsedCardPile();
        }
        private int GetEnemyActionBuffCount(EnemyState enemy)
        {
            if (enemy == null || enemy.Definition == null || enemy.HasSummonAction) return 0;
            if (enemy.IsSmallStone) return 1;
            if (!enemy.Definition.HasActionAbilities) return enemy.BleedingStacks > 0 ? 1 : 0;
            int count = 0;
            for (int i = 0; i < enemy.Definition.Abilities.Count; i++)
            {
                global::EnemyActionAbility ability = enemy.Definition.Abilities[i];
                if (ability == null || ability.Target != global::CombatAbilityTarget.Player
                    || ability.Effect != global::EnemyActionEffect.ApplyBuff
                    || ability.RelatedBuff == null || ability.Amount <= 0) continue;
                bool alreadyCounted = false;
                for (int previous = 0; previous < i; previous++)
                {
                    global::EnemyActionAbility earlier = enemy.Definition.Abilities[previous];
                    if (earlier != null && earlier.Target == global::CombatAbilityTarget.Player
                        && earlier.Effect == global::EnemyActionEffect.ApplyBuff
                        && earlier.RelatedBuff == ability.RelatedBuff && earlier.Amount > 0)
                    {
                        alreadyCounted = true;
                        break;
                    }
                }
                if (!alreadyCounted) count++;
            }
            return count;
        }
        private void CollectEnemyActionBuffEntries(EnemyState enemy,
            List<CombatBuffListVisual.Entry> output)
        {
            output.Clear();
            if (enemy == null || enemy.Definition == null || enemy.HasSummonAction) return;
            if (enemy.IsSmallStone)
            {
                output.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition(enemy.SmallStoneShieldAction ? "Shield" : "Stun"),
                    enemy.SmallStoneShieldAction ? 30 : 1));
                return;
            }
            if (!enemy.Definition.HasActionAbilities)
            {
                if (enemy.BleedingStacks > 0)
                    output.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Bleeding"), enemy.BleedingStacks));
                return;
            }
            for (int i = 0; i < enemy.Definition.Abilities.Count; i++)
            {
                global::EnemyActionAbility ability = enemy.Definition.Abilities[i];
                if (ability == null || ability.Target != global::CombatAbilityTarget.Player
                    || ability.Effect != global::EnemyActionEffect.ApplyBuff
                    || ability.RelatedBuff == null || ability.Amount <= 0) continue;
                int existingIndex = output.FindIndex(entry => entry.Definition == ability.RelatedBuff);
                if (existingIndex < 0)
                    output.Add(new CombatBuffListVisual.Entry(ability.RelatedBuff, ability.Amount));
                else
                {
                    CombatBuffListVisual.Entry entry = output[existingIndex];
                    output[existingIndex] = new CombatBuffListVisual.Entry(entry.Definition, entry.Amount + ability.Amount);
                }
            }
        }
        private void GetEnemyActionUiPositions(EnemyState enemy, float x, float width,
            out float countdownX, out float damageX, out float healX, out float bleedingX)
        {
            int actionCount = 1;
            bool hasDamage = enemy != null && enemy.ActionDamage > 0;
            bool hasHeal = enemy != null && enemy.Definition != null
                && (enemy.SelfHealAmount > 0
                    || enemy.AllEnemyHealAmount > 0 || enemy.HasSummonAction);
            int buffCount = GetEnemyActionBuffCount(enemy);
            if (hasDamage) actionCount++;
            if (hasHeal) actionCount++;
            actionCount += buffCount;
            const float actionSpacing = 64f;
            float nextX = x + width * 0.5f - (actionCount - 1) * actionSpacing * 0.5f;
            countdownX = nextX;
            nextX += actionSpacing;
            damageX = hasDamage ? nextX : float.NaN;
            if (hasDamage) nextX += actionSpacing;
            healX = hasHeal ? nextX : float.NaN;
            if (hasHeal) nextX += actionSpacing;
            bleedingX = buffCount > 0 ? nextX : float.NaN;
        }
        private bool TryGetHoveredEnemyAction(Vector2 screenPoint, out EnemyState enemy,
            out PlannedActionInfo actionInfo, out global::CombatBuffDefinition actionBuffDefinition,
            out Rect actionAnchor)
        {
            actionBuffDefinition = null;
            Vector2 point = ScreenToReferencePoint(screenPoint);
            float topY = GetEnemyUiTopOffset();
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState candidate = enemies[i];
                if (candidate == null || candidate.IsDefeated) continue;
                float x = GetEnemyUiX(i);
                float width = IsPortraitUi ? 220f : 240f;
                GetEnemyActionUiPositions(candidate, x, width, out float countdownX, out float damageX,
                    out float healX, out float bleedingX);
                Rect countdownRect = new Rect(countdownX - 24f, 150f + topY, 48f, 48f);
                Rect damageRect = new Rect(damageX - 24f, 150f + topY, 48f, 48f);
                Rect healRect = new Rect(healX - 24f, 150f + topY, 48f, 48f);
                Rect bleedingRect = new Rect(bleedingX - 24f, 150f + topY, 48f, 48f);
                if (i < canvasEnemyActionBuffLists.Count)
                {
                    CanvasIconList actionBuffList = canvasEnemyActionBuffLists[i];
                    if (actionBuffList != null && actionBuffList.Root != null && actionBuffList.Root.activeInHierarchy)
                    {
                        enemyActionBuffEntries.Clear();
                        CollectEnemyActionBuffEntries(candidate, enemyActionBuffEntries);
                        for (int buffIndex = 0; buffIndex < enemyActionBuffEntries.Count
                            && buffIndex < actionBuffList.Slots.Count; buffIndex++)
                        {
                            CanvasIconSlot slot = actionBuffList.Slots[buffIndex];
                            if (slot == null || slot.Root == null || !slot.Root.activeInHierarchy
                                || !TryGetCanvasIconReferenceRect(slot.Icon, out Rect canvasBuffRect)
                                || !canvasBuffRect.Contains(point)) continue;
                            enemy = candidate;
                            actionInfo = PlannedActionInfo.Buff;
                            actionBuffDefinition = enemyActionBuffEntries[buffIndex].Definition;
                            actionAnchor = canvasBuffRect;
                            return actionBuffDefinition != null;
                        }
                    }
                }
                if (countdownRect.Contains(point))
                {
                    enemy = candidate;
                    actionInfo = PlannedActionInfo.Countdown;
                    actionAnchor = countdownRect;
                    return true;
                }
                if (candidate.ActionDamage > 0 && damageRect.Contains(point))
                {
                    enemy = candidate;
                    actionInfo = PlannedActionInfo.Damage;
                    actionAnchor = damageRect;
                    return true;
                }
                int selfHeal = candidate.Definition != null
                    ? candidate.SelfHealAmount : 0;
                int allHeal = candidate.Definition != null
                    ? candidate.AllEnemyHealAmount : 0;
                if ((selfHeal > 0 || allHeal > 0 || candidate.HasSummonAction) && healRect.Contains(point))
                {
                    enemy = candidate;
                    actionInfo = candidate.HasSummonAction ? PlannedActionInfo.Summon : (allHeal > 0 ? PlannedActionInfo.HealAllEnemies : PlannedActionInfo.HealSelf);
                    actionAnchor = healRect;
                    return true;
                }
                if (GetEnemyActionBuffCount(candidate) > 0 && bleedingRect.Contains(point))
                {
                    enemy = candidate;
                    actionInfo = PlannedActionInfo.Buff;
                    enemyActionBuffEntries.Clear();
                    CollectEnemyActionBuffEntries(candidate, enemyActionBuffEntries);
                    actionBuffDefinition = enemyActionBuffEntries.Count > 0
                        ? enemyActionBuffEntries[0].Definition : GetCombatBuffDefinition("Bleeding");
                    actionAnchor = bleedingRect;
                    return actionBuffDefinition != null;
                }
            }
            enemy = null;
            actionInfo = PlannedActionInfo.Countdown;
            actionBuffDefinition = null;
            actionAnchor = default;
            return false;
        }
        private bool TryGetCanvasIconReferenceRect(Image icon, out Rect iconRect)
        {
            iconRect = default;
            if (icon == null || !icon.gameObject.activeInHierarchy) return false;
            RectTransform rect = icon.rectTransform;
            rect.GetWorldCorners(canvasHoverCorners);
            Camera canvasCamera = runtimeUiCanvas != null && runtimeUiCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? runtimeUiCanvas.worldCamera : null;
            Vector2 bottomLeftScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, canvasHoverCorners[0]);
            Vector2 topRightScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, canvasHoverCorners[2]);
            // Canvas screen coordinates start at the bottom; IMGUI popup coordinates start at the top.
            bottomLeftScreen.y = Screen.height - bottomLeftScreen.y;
            topRightScreen.y = Screen.height - topRightScreen.y;
            Vector2 bottomLeft = ScreenToReferencePoint(bottomLeftScreen);
            Vector2 topRight = ScreenToReferencePoint(topRightScreen);
            iconRect = Rect.MinMaxRect(Mathf.Min(bottomLeft.x, topRight.x), Mathf.Min(bottomLeft.y, topRight.y),
                Mathf.Max(bottomLeft.x, topRight.x), Mathf.Max(bottomLeft.y, topRight.y));
            return true;
        }
        private bool TryGetHoveredEnemyBuff(Vector2 screenPoint, out global::CombatBuffDefinition definition,
            out Rect iconAnchor)
        {
            Vector2 point = ScreenToReferencePoint(screenPoint);
            float topY = GetEnemyUiTopOffset();
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = enemies[i];
                if (enemy == null || enemy.IsDefeated) continue;
                float width = IsPortraitUi ? 220f : 240f;
                float panelWidth = IsPortraitUi ? 170f : 180f;
                float x = GetEnemyUiX(i) + (width - panelWidth) * 0.5f;
                float scrollX = i < enemyBuffScrollPositions.Count ? enemyBuffScrollPositions[i].x : 0f;
                if (enemy.Shield > 0 && i < canvasEnemyHuds.Count
                    && TryGetCanvasIconReferenceRect(canvasEnemyHuds[i].ShieldIcon, out Rect shieldRect)
                    && shieldRect.Contains(point))
                {
                    definition = GetCombatBuffDefinition("Shield");
                    iconAnchor = shieldRect;
                    return definition != null;
                }
                int buffSlot = 0;                if (enemy.Burn > 0)
                {
                    Rect iconRect = new Rect(x + 18f + buffSlot * 52f - scrollX, 349f + topY, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = GetCombatBuffDefinition("Burn");
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    buffSlot++;
                }
                if (enemy.Regeneration > 0)
                {
                    Rect iconRect = new Rect(x + 18f + buffSlot * 52f - scrollX, 349f + topY, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = GetCombatBuffDefinition("Regeneration");
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    buffSlot++;
                }
                if (enemy.Wood > 0)
                {
                    Rect iconRect = new Rect(x + 18f + buffSlot * 52f - scrollX, 349f + topY, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = GetCombatBuffDefinition("Wood");
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    buffSlot++;
                }
                if (enemy.Cleverness > 0)
                {
                    Rect iconRect = new Rect(x + 18f + buffSlot * 52f - scrollX, 349f + topY, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = GetCombatBuffDefinition("Cleverness");
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    buffSlot++;
                }                if (enemy.Scales > 0)
                {
                    Rect iconRect = new Rect(x + 18f + buffSlot * 52f - scrollX, 349f + topY, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = GetCombatBuffDefinition("Scales");
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    buffSlot++;
                }
                for (int bleedingIndex = 0; bleedingIndex < enemy.BleedingDurations.Count; bleedingIndex++)
                {
                    Rect iconRect = new Rect(x + 18f + buffSlot * 52f - scrollX, 349f + topY, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = GetCombatBuffDefinition("Bleeding");
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    buffSlot++;
                }
            }
            definition = null;
            iconAnchor = default;
            return false;
        }
        private bool TryGetHoveredPlayerBuff(Vector2 screenPoint, out global::CombatBuffDefinition definition,
            out Rect iconAnchor)
        {
            definition = null;
            iconAnchor = default;
            Vector2 point = ScreenToReferencePoint(screenPoint);
            if (playerShield > 0 && TryGetCanvasIconReferenceRect(canvasPlayerShieldIcon, out Rect shieldRect)
                && shieldRect.Contains(point))
            {
                definition = GetCombatBuffDefinition("Shield");
                iconAnchor = shieldRect;
                return definition != null;
            }
            if (canvasPlayerBuffList != null && canvasPlayerBuffList.Root != null
                && canvasPlayerBuffList.Root.activeInHierarchy)
            {
                int count = Mathf.Min(playerBuffEntries.Count, canvasPlayerBuffList.Slots.Count);
                for (int i = 0; i < count; i++)
                {
                    CombatBuffListVisual.Entry entry = playerBuffEntries[i];
                    CanvasIconSlot canvasSlot = canvasPlayerBuffList.Slots[i];
                    if (entry.Definition == null || entry.Amount <= 0 || canvasSlot == null || canvasSlot.Root == null
                        || !canvasSlot.Root.activeInHierarchy
                        || !TryGetCanvasIconReferenceRect(canvasSlot.Icon, out Rect canvasBuffRect)
                        || !canvasBuffRect.Contains(point)) continue;
                    definition = entry.Definition;
                    iconAnchor = canvasBuffRect;
                    return true;
                }
                return false;
            }
            const float barX = 24f;
            const float barY = 420f;
            const float barWidth = 300f;
            int slot = 0;
            global::CombatBuffDefinition[] definitions = { GetCombatBuffDefinition("Burn"), GetCombatBuffDefinition("Wood"), GetCombatBuffDefinition("Regeneration"), GetCombatBuffDefinition("Stun"), GetCombatBuffDefinition("Bind"), GetCombatBuffDefinition("Scales"), GetCombatBuffDefinition("Bleeding") };
            int[] counts = { playerBurn, playerWood, playerRegeneration, playerStun, playerBindDuration, playerScales, playerBleedingStacks.Count };
            for (int i = 0; i < definitions.Length; i++)
            {
                if (counts[i] <= 0) continue;
                int instances = i == definitions.Length - 1 ? counts[i] : 1;
                for (int instance = 0; instance < instances; instance++)
                {
                    Rect iconRect = new Rect(barX + barWidth - 47f, barY - 10f - (slot + 1) * 48f + 4f, 44f, 40f);
                    if (iconRect.Contains(point))
                    {
                        definition = definitions[i];
                        iconAnchor = iconRect;
                        return definition != null;
                    }
                    slot++;
                }
            }
            return false;
        }
        private bool TryGetEnemyIndexAt(Vector2 screenPoint, out int enemyIndex)
        {
            Vector2 point = ScreenToReferencePoint(screenPoint);
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].IsDefeated) continue;
                float x = GetEnemyUiX(i);
                // Targeted card use gets a forgiving hit area; tooltip bounds remain limited to the image itself.
                Rect imageRect = new Rect(x + (IsPortraitUi ? 30f : 45f),
                    202f + GetEnemyUiTopOffset(),
                    IsPortraitUi ? 160f : 150f, 82f);
                Rect targetRect = new Rect(imageRect.xMin - 28f, imageRect.yMin - 26f,
                    imageRect.width + 56f, imageRect.height + 52f);
                if (targetRect.Contains(point)) { enemyIndex = i; return true; }
            }
            enemyIndex = -1;
            return false;
        }
        private bool IsPointOverUsedCardPile(Vector2 screenPoint)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            CardVisual target = usedPileCard != null ? usedPileCard : usedPilePlaceholder;
            return target != null && target.gameObject.activeSelf
                && GetVisualScreenRect(target.gameObject, camera).Contains(screenPoint);
        }
        private bool IsTargetedSpell(StoredCard card)
        {
            return card != null && card.CombatType != null && card.CombatType.RequiresEnemyTarget;
        }
        private bool IsNonTargetedSpell(StoredCard card)
        {
            return card != null && card.CombatType != null && !IsTargetedSpell(card);
        }
        private bool IsCardRaisedForCast(int index)
        {
            return index >= 0 && index < cards.Count && cards[index] != null
                && (index >= startingHandHomePositions.Count
                    || cards[index].transform.position.y > startingHandHomePositions[index].y + 3.00f);
        }
        private void UseStartingHandCard(int index, int targetEnemyIndex)
        {
            if (index < 0 || index >= cards.Count || cards[index] == null) return;
            if (!CanUseCardAtIndex(index, out bool enhancedCast)) { RestoreStartingHandCard(index); return; }
            StoredCard usedStoredCard = index < currentPackCards.Count ? currentPackCards[index] : null;
            if (IsTargetedSpell(usedStoredCard)
                && (targetEnemyIndex < 0 || targetEnemyIndex >= enemies.Count || enemies[targetEnemyIndex].IsDefeated))
            { RestoreStartingHandCard(index); return; }
            int castCount = enhancedCast && !IsLeafMeteorCard(usedStoredCard) ? 2 : 1;
            CardVisual usedCard = cards[index];
            global::CardData usedData = usedStoredCard != null ? usedStoredCard.Data : null;
            Vector3 startPosition = usedCard.transform.position;
            cards.RemoveAt(index);
            if (index < currentPackCards.Count) currentPackCards.RemoveAt(index);
            if (usedPileRoot == null) CreateUsedCardPile();
            usedPileCard = usedCard;
            usedPileHistory.Add(usedCard);
            usedPileCardData.Add(usedData);
            usedPileStoredCards.Add(usedStoredCard);
            usedPileCard.transform.SetParent(usedPileRoot, true);
            usedPileCard.SetInteractionState(true, false);
            usedPileCard.gameObject.SetActive(true);
            if (usedPilePlaceholder != null) usedPilePlaceholder.gameObject.SetActive(usedPileCard == null);
            TriggerCardUseRelics();
            ResolveUsedCardCast(usedStoredCard, castCount, targetEnemyIndex, enhancedCast);
            lastUsedCard = usedStoredCard;
            hasPlayedCardThisTurn = true;
            usedCastCount += castCount;
            if (enhancedCast)
                AddScorePopup(IsLeafMeteorCard(usedStoredCard) ? Ui("강화 시전!\n피해량 11", "Enhanced cast!\n11 damage") : Ui("강화 시전!\n2회 사용", "Enhanced cast!\nCounts as 2 uses"),
                    new Color(1f, 0.76f, 0.18f), Time.unscaledTime, scorePopups.Count, 0);
            cardIndex = Mathf.Clamp(index, 0, Mathf.Max(0, cards.Count - 1));
            usedPileExpanded = false;
            LayoutStartingHand();
            if (usedPileRoutine != null) StopCoroutine(usedPileRoutine);
            usedPileRoutine = StartCoroutine(AnimateCardIntoUsedPile(usedCard, startPosition));
        }
        private bool IsLeafMeteorCard(StoredCard card)
        {
            return card != null && card.CombatType != null && card.CombatType.name == "LeafMeteor";
        }

        private void DiscardStartingHandCard(int index)
        {
            if (index < 0 || index >= cards.Count || cards[index] == null) return;
            if (ShouldBlockTutorialDiscard())
            {
                RestoreStartingHandCard(index);
                AddScorePopup(Ui("튜토리얼\n잎을 버리지 말고 안내된 잎을 사용하세요.",
                    "Tutorial\nUse the instructed card instead of discarding it."),
                    new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
                return;
            }
            bool firstCardState = hasPlayedCardThisTurn;
            int castState = usedCastCount;
            CardVisual discardedCard = cards[index];
            StoredCard discardedStoredCard = index < currentPackCards.Count ? currentPackCards[index] : null;
            global::CardData discardedData = discardedStoredCard != null ? discardedStoredCard.Data : null;
            Vector3 startPosition = discardedCard.transform.position;
            cards.RemoveAt(index);
            if (index < currentPackCards.Count) currentPackCards.RemoveAt(index);
            if (usedPileRoot == null) CreateUsedCardPile();
            usedPileHistory.Insert(0, discardedCard);
            usedPileCardData.Insert(0, discardedData);
            usedPileStoredCards.Insert(0, discardedStoredCard);
            discardedCard.transform.SetParent(usedPileRoot, true);
            discardedCard.SetInteractionState(true, false);
            discardedCard.gameObject.SetActive(true);
            if (usedPilePlaceholder != null) usedPilePlaceholder.gameObject.SetActive(usedPileCard == null);
            cardIndex = Mathf.Clamp(index, 0, Mathf.Max(0, cards.Count - 1));
            usedPileExpanded = false;
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            if (usedPileRoutine != null) StopCoroutine(usedPileRoutine);
            usedPileRoutine = StartCoroutine(AnimateCardIntoUsedPile(discardedCard, startPosition));
            hasPlayedCardThisTurn = firstCardState;
            usedCastCount = castState;
        }
        private IEnumerator ResolveLeafMeteorSequentially(StoredCard card, global::CombatCardAbility ability,
            string koreanName, string englishName, bool enhancedCast)
        {
            int baseDamage = enhancedCast && IsLeafMeteorCard(card) ? 11 : ability.Amount;
            int discardedCount = 0;
            while (cards.Count > 0)
            {
                int before = cards.Count;
                DiscardStartingHandCard(0);
                if (cards.Count >= before) break;
                discardedCount++;
                int damage = GetRelicModifiedDamage(card, baseDamage);
                for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                    if (GetLivingEnemy(enemyIndex) != null && damage > 0)
                        DealDamage(enemyIndex, damage, koreanName, englishName);
                yield return new WaitForSecondsRealtime(0.1f);
            }
            AddScorePopup(Ui(koreanName + "\n잎 " + discardedCount + "장 버림",
                englishName + "\nDiscarded " + discardedCount + " card(s)"),
                new Color(1f, 0.72f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            leafMeteorResolving = false;
            RefreshHandCardInteractionStates();
        }
        private void ResolveUsedCardCast(StoredCard card, int castCount, int targetEnemyIndex, bool enhancedCast)
        {
            if (card == null || card.CombatType == null || card.CombatType.Abilities == null) return;
            for (int cast = 0; cast < Mathf.Max(1, castCount); cast++)
            {
                for (int i = 0; i < card.CombatType.Abilities.Count; i++)
                {
                    global::CombatCardAbility ability = card.CombatType.Abilities[i];
                    if (ability != null) ResolveSpellEffect(card, ability, targetEnemyIndex, enhancedCast);
                }
            }
            previousRevealedCard = card;
            RefreshHandCardInteractionStates();
        }
        private void ResolveSpellEffect(StoredCard card, global::CombatCardAbility ability,
            int selectedEnemyIndex, bool enhancedCast)
        {
            string koreanName = GetStoredCardDisplayName(card);
            string englishName = card != null && card.CombatType != null
                && !string.IsNullOrEmpty(card.CombatType.EnglishName)
                ? card.CombatType.EnglishName : card.Data.GetLocalizedName(true);
            if (ability.Effect == global::CombatAbilityEffect.DiscardHandAndDamageAll)
            {
                leafMeteorResolving = true;
                RefreshHandCardInteractionStates();
                StartCoroutine(ResolveLeafMeteorSequentially(card, ability, koreanName, englishName, enhancedCast));
                return;
            }
            if (ability.Effect == global::CombatAbilityEffect.DrawCards)
            {
                int drawnCount = 0;
                for (int i = 0; i < ability.Amount; i++)
                    if (DrawStarterCardToHand()) drawnCount++;
                if (drawnCount > 0)
                    AddScorePopup(Ui(koreanName + "\n잎 " + drawnCount + "장 드로우",
                        englishName + "\nDraw " + drawnCount + " card(s)"),
                        new Color(0.45f, 0.9f, 1f), Time.unscaledTime, scorePopups.Count, 0);
                return;
            }
                        if (ability.Effect == global::CombatAbilityEffect.GainShieldAfterUses)
            {
                lightStoryUseCount++;
                int requiredUses = Mathf.Max(1, ability.UsesRequired);
                if (lightStoryUseCount < requiredUses)
                {
                    AddScorePopup(Ui(koreanName + "\n" + lightStoryUseCount + " / " + requiredUses,
                        englishName + "\n" + lightStoryUseCount + " / " + requiredUses),
                        new Color(0.8f, 0.9f, 1f), Time.unscaledTime, scorePopups.Count, 0);
                    return;
                }
                lightStoryUseCount = 0;
                int shieldAmount = Mathf.Max(0, ability.Amount);
                playerShield += shieldAmount;
                AddScorePopup(Ui(koreanName + "\n보호막 +" + shieldAmount,
                    englishName + "\nShield +" + shieldAmount),
                    new Color(0.65f, 0.85f, 1f), Time.unscaledTime, scorePopups.Count, 0);
                return;
            }
            if (ability.Effect == global::CombatAbilityEffect.HealAfterUses)
            {
                theFoolUseCount++;
                int requiredUses = Mathf.Max(1, ability.UsesRequired);
                if (theFoolUseCount < requiredUses)
                {
                    AddScorePopup(Ui(koreanName + "\n" + theFoolUseCount + " / " + requiredUses,
                        englishName + "\n" + theFoolUseCount + " / " + requiredUses),
                        new Color(0.5f, 0.95f, 0.75f), Time.unscaledTime, scorePopups.Count, 0);
                    return;
                }
                theFoolUseCount = 0;
                int healthBefore = playerHealth;
                playerHealth = Mathf.Min(PlayerMaximumHealth, playerHealth + Mathf.Max(0, ability.Amount));
                AddScorePopup(Ui(koreanName + "\n체력 +" + (playerHealth - healthBefore),
                    englishName + "\nHP +" + (playerHealth - healthBefore)),
                    new Color(0.4f, 1f, 0.58f), Time.unscaledTime, scorePopups.Count, 0);
                return;
            }
            if (ability.Effect == global::CombatAbilityEffect.ApplyBuff
                && ability.Target == global::CombatAbilityTarget.Player)
            {
                ApplyPlayerBuff(ability.RelatedBuff, ability.Amount);
                return;
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                if (!DoesAbilityTargetEnemy(ability, i, selectedEnemyIndex)) continue;
                if (ability.Effect == global::CombatAbilityEffect.Damage || ability.Effect == global::CombatAbilityEffect.IgnoreShieldDamage)
                {
                    EnemyState target = GetLivingEnemy(i);
                    int damage = GetRelicModifiedDamage(card, ability.Amount);
                    if (ability.DoubleAmountAgainstShield && target != null && target.Shield > 0) damage *= 2;
                    bool ignoresShield = ability.Effect == global::CombatAbilityEffect.IgnoreShieldDamage;
                    int healthDamage = DealDamage(i, damage, koreanName, englishName, ignoresShield);
                    if (card.Color == global::CardColor.Red && healthDamage > 0)
                        ApplyFlamingSwordBurn(i, healthDamage);
                    ApplyBlueBlueEffect(card, i, damage);
                }
                else if (ability.Effect == global::CombatAbilityEffect.Burn)
                    AddBurn(i, ability.Amount);
                else if (ability.Effect == global::CombatAbilityEffect.Scales)
                    AddScales(i, ability.Amount);
                else if (ability.Effect == global::CombatAbilityEffect.ApplyBuff)
                    ApplyEnemyBuff(GetLivingEnemy(i), ability.RelatedBuff, ability.Amount);
            }
        }
        private static bool DoesAbilityTargetEnemy(global::CombatCardAbility ability,
            int enemyIndex, int selectedEnemyIndex)
        {
            return ability.Target == global::CombatAbilityTarget.AllEnemies
                || (ability.Target == global::CombatAbilityTarget.SelectedEnemy && enemyIndex == selectedEnemyIndex);
        }
        private EnemyState GetLivingEnemy(int enemyIndex)
        {
            return enemyIndex >= 0 && enemyIndex < enemies.Count && !enemies[enemyIndex].IsDefeated ? enemies[enemyIndex] : null;
        }
        private int DealDamage(int enemyIndex, int amount, string koreanSource, string englishSource, bool ignoreShield = false)
        {
            EnemyState enemy = GetLivingEnemy(enemyIndex);
            if (enemy == null || amount <= 0) return 0;
            int shieldDamage = ignoreShield ? 0 : Mathf.Min(enemy.Shield, amount);
            enemy.Shield -= shieldDamage;
            int healthDamage = ignoreShield ? amount : amount - shieldDamage;
            healthDamage = Mathf.Min(enemy.Health, healthDamage);
            enemy.Health = Mathf.Max(0, enemy.Health - healthDamage);
            UpdateClevernessAction(enemy);
            string result = ignoreShield
                ? Ui(koreanSource + "\n방어 무시 피해 " + healthDamage, englishSource + "\nIgnore-shield damage " + healthDamage)
                : shieldDamage > 0 ? Ui(koreanSource + "\n보호막 -" + shieldDamage, englishSource + "\nShield -" + shieldDamage)
                : Ui(koreanSource + "\n피해 " + healthDamage, englishSource + "\nDamage " + healthDamage);
            AddScorePopup(result, enemy.IsDefeated ? new Color(1f, 0.55f, 0.25f) : Color.white, Time.unscaledTime, scorePopups.Count, 0);
            if (enemy.IsDefeated) PlayEnemyDefeatEffect(enemyIndex);
            if (enemies.Count > 0 && enemies.TrueForAll(item => item.IsDefeated))
            {
                startingHandVisible = false;
                BeginCombatVictoryAfterDefeatDelay();
            }
            return healthDamage;
        }
        private void UpdateClevernessAction(EnemyState enemy)
        {
            if (enemy == null || enemy.IsDefeated || enemy.Definition == null || enemy.Cleverness <= 0 || enemy.ClevernessActionChanged) return;
            if (enemy.Definition.name != "GlassSnake" || enemy.Health * 2 >= enemy.MaximumHealth) return;
            enemy.ClevernessActionChanged = true;
            enemy.ChangedActionHealAmount = Mathf.Max(0, enemy.Definition.GetActionDamage());
            AddScorePopup(Ui(enemy.Name + "\n영리함: 회복으로 전환", enemy.EnglishName + "\nCleverness: switches to Heal"),
                new Color(0.45f, 0.85f, 1f), Time.unscaledTime, scorePopups.Count, 0);
        }
        private void ApplyFlamingSwordBurn(int enemyIndex, int healthDamage)
        {
            if (!HasCombatRelicEffect(global::CombatRelicEffect.RedCardHealthDamageAppliesBurn)) return;
            AddBurn(enemyIndex, healthDamage);
        }
        private void ApplyBlueBlueEffect(StoredCard card, int targetEnemyIndex, int damage)
        {
            if (card == null || card.Color != global::CardColor.Blue
                || !HasCombatRelicEffect(global::CombatRelicEffect.BlueCardShieldBreakAndSplash)) return;
            EnemyState target = GetLivingEnemy(targetEnemyIndex);
            if (target != null) target.Shield = 0;
            int splashDamage = Mathf.RoundToInt(damage * 0.5f);
            if (splashDamage <= 0) return;
            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
                if (GetLivingEnemy(enemyIndex) != null)
                    DealDamage(enemyIndex, splashDamage, "파란파란", "Blue Blue");
        }
        private void AddBurn(int enemyIndex, int amount) { EnemyState enemy = GetLivingEnemy(enemyIndex); if (enemy != null) enemy.Burn += Mathf.Max(0, amount); }
        private void AddScales(int enemyIndex, int amount) { EnemyState enemy = GetLivingEnemy(enemyIndex); if (enemy != null) enemy.Scales += Mathf.Max(0, amount); }
        private void ApplyBleedingAtTurnStart()
        {
            for (int enemyIndex = 0; enemyIndex < enemies.Count; enemyIndex++)
            {
                EnemyState enemy = GetLivingEnemy(enemyIndex);
                if (enemy == null || enemy.BleedingDurations.Count == 0) continue;
                for (int stackIndex = enemy.BleedingDurations.Count - 1; stackIndex >= 0; stackIndex--)
                {
                    if (enemy.BleedingDurations[stackIndex] <= 0)
                    {
                        enemy.BleedingDurations.RemoveAt(stackIndex);
                        continue;
                    }
                    DealDamage(enemyIndex, 7, "출혈", "Bleeding", true);
                    if (enemy.IsDefeated) break;
                    enemy.BleedingDurations[stackIndex]--;
                    if (enemy.BleedingDurations[stackIndex] <= 0) enemy.BleedingDurations.RemoveAt(stackIndex);
                }
            }
        }
        private void ApplyEnemyStunAtTurnStart()
        {
            stunnedEnemyIndicesThisTurn.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = GetLivingEnemy(i);
                if (enemy == null || enemy.Stun <= 0) continue;
                enemy.Stun--;
                stunnedEnemyIndicesThisTurn.Add(i);
                AddScorePopup(Ui(enemy.Name + "\n기절", enemy.EnglishName + "\nStunned"),
                    new Color(0.95f, 0.82f, 0.28f), Time.unscaledTime, scorePopups.Count, 0);
            }
        }
        private void ApplyRegenerationAtTurnStart()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = GetLivingEnemy(i);
                if (enemy == null || enemy.Regeneration <= 0) continue;
                int before = enemy.Health;
                enemy.Health = Mathf.Min(enemy.MaximumHealth, enemy.Health + enemy.Regeneration);
                int recovered = enemy.Health - before;
                if (recovered > 0)
                    AddScorePopup(Ui(enemy.Name + "\n체력 +" + recovered, enemy.EnglishName + "\nHP +" + recovered),
                        new Color(0.4f, 1f, 0.58f), Time.unscaledTime, scorePopups.Count, 0);
            }
        }
        private void ApplyScalesAtTurnStart()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = GetLivingEnemy(i);
                if (enemy != null && enemy.Scales > 0 && enemy.Shield <= 0)
                    enemy.Shield += enemy.Scales;
            }
        }
        private void ApplyBurnAtTurnStart()        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = GetLivingEnemy(i);
                if (enemy == null || enemy.Burn <= 0) continue;
                int damage = enemy.Burn;
                DealDamage(i, damage, "화상", "Burn");
                if (enemy.IsDefeated) continue;
                if (enemy.Wood > 0)
                {
                    enemy.Burn += 6;
                    enemy.Wood--;
                }
                else
                    enemy.Burn /= 2;
            }
        }
        private IEnumerator AnimateCardIntoUsedPile(CardVisual card, Vector3 startPosition)
        {
            usedPileAnimating = true;
            Vector3 targetPosition = GetUsedPileWorldPosition();
            const float duration = 0.30f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (card == null) break;
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                Vector3 position = Vector3.Lerp(startPosition, targetPosition, progress);
                position.y -= Mathf.Sin(progress * Mathf.PI) * 0.48f;
                card.transform.position = position;
                yield return null;
            }
            usedPileAnimating = false;
            usedPileRoutine = null;
            LayoutUsedCardPile();
        }
        private int FindStartingHandTarget(int draggedIndex)
        {
            if (draggedIndex < 0 || draggedIndex >= cards.Count || cards[draggedIndex] == null) return -1;
            float closestDistance = 0.82f;
            int closestIndex = -1;
            Vector3 draggedPosition = cards[draggedIndex].transform.position;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == draggedIndex || cards[i] == null) continue;
                Vector3 otherPosition = cards[i].transform.position;
                float distance = Mathf.Abs(draggedPosition.x - otherPosition.x)
                    + Mathf.Abs(draggedPosition.y - otherPosition.y) * 0.25f;
                if (distance >= closestDistance) continue;
                closestDistance = distance;
                closestIndex = i;
            }
            if (closestIndex >= 0) return closestIndex;
            // A card dropped below the hand and beyond its width joins the nearest end.
            float leftEdge = float.PositiveInfinity;
            float rightEdge = float.NegativeInfinity;
            for (int i = 0; i < cards.Count; i++)
            {
                if (i == draggedIndex || cards[i] == null) continue;
                float cardX = cards[i].transform.position.x;
                leftEdge = Mathf.Min(leftEdge, cardX);
                rightEdge = Mathf.Max(rightEdge, cardX);
            }
            if (draggedPosition.x <= leftEdge) return 0;
            if (draggedPosition.x >= rightEdge) return cards.Count - 1;
            return -1;
        }
        private void MoveStartingHandCard(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= cards.Count || targetIndex < 0 || targetIndex >= cards.Count) return;
            CardVisual movedVisual = cards[sourceIndex];
            StoredCard movedData = sourceIndex < currentPackCards.Count ? currentPackCards[sourceIndex] : null;
            bool insertAfterTarget = movedVisual != null && cards[targetIndex] != null
                && movedVisual.transform.position.x > cards[targetIndex].transform.position.x;
            cards.RemoveAt(sourceIndex);
            if (sourceIndex < currentPackCards.Count) currentPackCards.RemoveAt(sourceIndex);
            if (sourceIndex < targetIndex) targetIndex--;
            int insertIndex = Mathf.Clamp(targetIndex + (insertAfterTarget ? 1 : 0), 0, cards.Count);
            cards.Insert(insertIndex, movedVisual);
            if (movedData != null) currentPackCards.Insert(insertIndex, movedData);
            cardIndex = insertIndex;
            LayoutStartingHand();
        }
        private StoredCard GetTopCombatDiscardCard()
        {
            return lastUsedCard;
        }

        private bool CanUseCardAtIndex(int index, out bool enhancedCast)
        {
            enhancedCast = false;
            if (leafMeteorResolving) return false;
            if (index < 0 || index >= currentPackCards.Count || currentPackCards[index] == null) return false;
            StoredCard candidate = currentPackCards[index];
            if (tutorialOpen && (tutorialFlowPhase == TutorialFlowPhase.CombatEndTurn
                || tutorialFlowPhase == TutorialFlowPhase.CombatWaitEnemy
                || tutorialFlowPhase == TutorialFlowPhase.CombatRefillEndTurn))
                return false;

            if (tutorialOpen && (tutorialFlowPhase == TutorialFlowPhase.CombatTarget
                || tutorialFlowPhase == TutorialFlowPhase.CombatRefillAttack
                || tutorialFlowPhase == TutorialFlowPhase.CombatFinish))
            {
                int requiredNumber = tutorialFlowPhase == TutorialFlowPhase.CombatFinish ? 2 : 4;
                if (candidate.Color != global::CardColor.Green || candidate.Number != requiredNumber)
                    return false;
            }

            if (tutorialOpen && tutorialFlowPhase == TutorialFlowPhase.CardRules
                && tutorialPracticeStage == 3)
            {
                // Make the chain easy to read: green 4 is enabled first, then green 5.
                if (cards.Count == tutorialPracticeHandCount
                    && !(candidate.Color == global::CardColor.Green && candidate.Number == 4))
                    return false;
                if (cards.Count == tutorialPracticeHandCount - 1
                    && !(candidate.Color == global::CardColor.Green && candidate.Number == 5))
                    return false;
            }

            StoredCard topDiscard = GetTopCombatDiscardCard();
            if (topDiscard == null) return false;

            bool matchingNumber = candidate.Number == topDiscard.Number;
            bool matchingColor = ColorsMatchForCast(candidate.Color, topDiscard.Color);
            bool sameColorWithNextNumber = matchingColor
                && NumbersAreAdjacent(candidate.Number, topDiscard.Number);
            bool firstCardColorBonus = !hasPlayedCardThisTurn && matchingColor;
            bool normalCast = matchingNumber || sameColorWithNextNumber || firstCardColorBonus;
            if (!normalCast) return false;
            enhancedCast = IsEnhancedColorSequence(topDiscard.Color, candidate.Color);
            return true;
        }
        private static bool ColorsMatchForCast(global::CardColor left, global::CardColor right)
        {
            if (left == right) return true;
            return (left == global::CardColor.Black && right == global::CardColor.White)
                || (left == global::CardColor.White && right == global::CardColor.Black);
        }
        private static bool NumbersAreAdjacent(int left, int right)
        {
            return Mathf.Abs(left - right) == 1 || (left == 1 && right == 6) || (left == 6 && right == 1);
        }
        private static bool IsEnhancedColorSequence(global::CardColor previous, global::CardColor next)
        {
            if ((previous == global::CardColor.Black && next == global::CardColor.White)
                || (previous == global::CardColor.White && next == global::CardColor.Black)) return true;
            // Elemental advantage order: fire (red) -> water (blue) -> grass (green) -> fire.
            return (previous == global::CardColor.Green && next == global::CardColor.Red)
                || (previous == global::CardColor.Red && next == global::CardColor.Blue)
                || (previous == global::CardColor.Blue && next == global::CardColor.Green);
        }
        private void RefreshHandCardInteractionStates()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] == null) continue;
                if (restStageActive || eventChoiceActive || shopRewardOpeningActive || rewardChoiceActive)
                {
                    cards[i].SetInteractionState(true, false);
                    continue;
                }
                if (shopChoiceActive)
                {
                    cards[i].SetInteractionState(CanPurchaseShopCard(i), false);
                    continue;
                }
                bool playable = CanUseCardAtIndex(i, out bool enhancedCast);
                cards[i].SetInteractionState(playable, enhancedCast);
            }
        }
        private void LayoutStartingHand()
        {
            RefreshHandCardInteractionStates();
            if (handLayoutRoutine != null) StopCoroutine(handLayoutRoutine);
            highlightedHandCard = null;
            handHoverPointerDirty = true;
            startingHandHomePositions.Clear();
            startingHandHomeRotations.Clear();
            int handCount = cards.Count;
            const int maxCardsPerRow = 10;
            int columns = handCount > maxCardsPerRow ? maxCardsPerRow : Mathf.Max(1, handCount);
            float cardSpacing = handCount <= 6 ? 1.16f : Mathf.Lerp(1.16f, 0.78f,
                Mathf.InverseLerp(6f, maxCardsPerRow, Mathf.Min(handCount, maxCardsPerRow)));
            for (int i = 0; i < handCount; i++)
            {
                CardVisual visual = cards[i];
                if (visual == null) continue;
                int row = i / columns;
                int indexInRow = i % columns;
                int rowCount = Mathf.Min(columns, handCount - row * columns);
                float fanOffset = indexInRow - (rowCount - 1) * 0.5f;
                float edgeAmount = rowCount <= 1 ? 0f : Mathf.Abs(fanOffset) / ((rowCount - 1) * 0.5f);
                Vector3 homePosition = new Vector3(fanOffset * cardSpacing,
                    -3.08f - row * 0.92f - edgeAmount * 0.32f, -0.20f + i * 0.02f);
                startingHandHomePositions.Add(homePosition);
                float angle = fanOffset * -3f;
                startingHandHomeRotations.Add(Quaternion.Euler(-4f, 0f, angle));
                visual.gameObject.SetActive(true);
                visual.SetSortingOrder(i);
            }
            handLayoutRoutine = StartCoroutine(AnimateStartingHandLayout());
        }
        private IEnumerator AnimateStartingHandLayout()
        {
            const float duration = 0.24f;
            Vector3[] startPositions = new Vector3[cards.Count];
            Quaternion[] startRotations = new Quaternion[cards.Count];
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null)
                {
                    startPositions[i] = cards[i].transform.position;
                    startRotations[i] = cards[i].transform.localRotation;
                }
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                for (int i = 0; i < cards.Count && i < startingHandHomePositions.Count; i++)
                {
                    if (cards[i] == null) continue;
                    cards[i].transform.position = Vector3.Lerp(startPositions[i], startingHandHomePositions[i], progress);
                    if (i < startingHandHomeRotations.Count)
                        cards[i].transform.localRotation = Quaternion.Slerp(startRotations[i], startingHandHomeRotations[i], progress);
                }
                yield return null;
            }
            for (int i = 0; i < cards.Count && i < startingHandHomePositions.Count; i++)
                if (cards[i] != null)
                {
                    cards[i].transform.position = startingHandHomePositions[i];
                    if (i < startingHandHomeRotations.Count)
                        cards[i].transform.localRotation = startingHandHomeRotations[i];
                    cards[i].transform.localScale = Vector3.one * CurrentHandCardScale;
                }
            handLayoutRoutine = null;
        }
        private void UpdateHandCardHover()
        {
            if (!startingHandVisible || handLayoutRoutine != null || draggedHandIndex >= 0 || pressedHandIndex >= 0)
                return;
            Camera camera = Camera.main;
            if (camera == null || !hasHandHoverPointer) return;
            if (handHoverPointerDirty)
            {
                CardVisual hovered = null;
                for (int i = cards.Count - 1; i >= 0; i--)
                {
                    CardVisual card = cards[i];
                    if (card != null && card.gameObject.activeSelf
                        && GetVisualScreenRect(card.gameObject, camera).Contains(lastHandHoverPointer))
                    {
                        hovered = card;
                        break;
                    }
                }
                if (highlightedHandCard != hovered)
                {
                    highlightedHandCard = hovered;
                    handHoverAnimationUntil = Time.unscaledTime + 0.20f;
                }
                handHoverPointerDirty = false;
            }
            if (Time.unscaledTime >= handHoverAnimationUntil) return;
            float transition = 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime);
            for (int i = 0; i < cards.Count && i < startingHandHomePositions.Count; i++)
            {
                CardVisual card = cards[i];
                if (card == null || !card.gameObject.activeSelf) continue;
                bool isHovered = card == highlightedHandCard;
                Vector3 targetPosition = startingHandHomePositions[i] + (isHovered ? Vector3.up * 2.15f : Vector3.zero);
                float targetScale = CurrentHandCardScale * (isHovered ? 1.20f : 1f);
                card.transform.position = Vector3.Lerp(card.transform.position, targetPosition, transition);
                card.transform.localScale = Vector3.Lerp(card.transform.localScale, Vector3.one * targetScale, transition);
                card.SetSortingOrder(isHovered ? 1000 : i);
            }
        }
        private void ResetHandHoverVisuals()
        {
            highlightedHandCard = null;
            pressedHandIndex = -1;
            draggedHandIndex = -1;
            hasHandHoverPointer = false;
            handHoverPointerDirty = true;
            handHoverAnimationUntil = 0f;
            for (int i = 0; i < cards.Count; i++) RestoreStartingHandCard(i);
        }
        private void RestoreStartingHandCard(int index)
        {
            if (index < 0 || index >= cards.Count || index >= startingHandHomePositions.Count) return;
            CardVisual card = cards[index];
            if (card == null) return;
            card.transform.position = startingHandHomePositions[index];
            if (index < startingHandHomeRotations.Count)
                card.transform.localRotation = startingHandHomeRotations[index];
            card.transform.localScale = Vector3.one * CurrentHandCardScale;
            card.SetSortingOrder(index);
        }
        private void HandlePointer(Vector2 point, Event inputEvent)
        {
            if (phase == RevealPhase.Animating)
            {
                HandleAnimatingCardSwipe(point, inputEvent);
                return;
            }
            if (inputEvent.type == EventType.MouseDown
                && new Rect(0f, 0f, UiReferenceWidth, UiReferenceHeight).Contains(point))
            {
                dragStart = point;
                dragDelta = Vector2.zero;
                Rect packZone = IsPortraitUi
                    ? new Rect(145f, 175f, 430f, 580f + PortraitExtraHeight * 0.45f) : PackTearZone;
                Rect cardZone = IsPortraitUi
                    ? new Rect(145f, 185f, 430f, 800f + PortraitExtraHeight * 0.85f) : CardGestureZone;
                bool objectGesture = phase == RevealPhase.Pack ? packZone.Contains(point) : cardZone.Contains(point);
                if (objectGesture) BeginObjectGesture(); else BeginInspection();
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseDrag)
            {
                dragDelta = point - dragStart;
                if (inspectionDragging) UpdateInspectionRotation();
                else if (gestureDragging) UpdateObjectGesture();
                else return;
                inputEvent.Use();
                return;
            }
            if (inputEvent.type != EventType.MouseUp) return;
            if (inspectionDragging)
            {
                inspectionDragging = false;
                Transform releasedTarget = inspectionTarget;
                inspectionTarget = null;
                BeginInspectionReturn(releasedTarget);
            }
            else if (gestureDragging) { gestureDragging = false; CompleteObjectGesture(); }
            else return;
            inputEvent.Use();
        }
        private void HandleAnimatingCardSwipe(Vector2 point, Event inputEvent)
        {
            if (!cardTransitionActive) return;
            Rect cardZone = IsPortraitUi
                ? new Rect(145f, 185f, 430f, 800f + PortraitExtraHeight * 0.85f) : CardGestureZone;
            if (inputEvent.type == EventType.MouseDown && cardZone.Contains(point))
            {
                dragStart = point;
                dragDelta = Vector2.zero;
                transitionDragActive = true;
                transitionSwipeCommitted = false;
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseDrag && transitionDragActive)
            {
                dragDelta = point - dragStart;
                if (!transitionSwipeCommitted && Mathf.Abs(dragDelta.x) >= 70f)
                {
                    transitionSwipeCommitted = true;
                    queuedSwipeDirection = Mathf.Sign(dragDelta.x);
                    queuedCardSwipes = Mathf.Min(queuedCardSwipes + 1, cards.Count);
                    if (activeSlidingCard != null) activeSlidingCard.AccelerateSlideAway();
                }
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseUp && transitionDragActive)
            {
                transitionDragActive = false;
                inputEvent.Use();
            }
        }
        private void BeginObjectGesture()
        {
            if (inspectionReturnRoutine != null)
            {
                StopCoroutine(inspectionReturnRoutine);
                inspectionReturnRoutine = null;
            }
            gestureDragging = true;
            inspectionDragging = false;
            Transform target = CurrentGestureTarget();
            gestureStartPosition = target.position;
            gestureStartRotation = target.rotation;
            if (phase == RevealPhase.Pack) tearVisual.BeginGesture();
        }
        private void BeginInspection()
        {
            inspectionTarget = CurrentInspectionTarget();
            if (inspectionTarget == null) return;
            if (inspectionReturnRoutine != null)
            {
                StopCoroutine(inspectionReturnRoutine);
                inspectionReturnRoutine = null;
            }
            inspectionDragging = true;
            gestureDragging = false;
            inspectionStartRotation = inspectionTarget.rotation;
            if (inspectionTarget == cardStack)
                inspectionPivotWorld = inspectionTarget.position
                    + inspectionStartRotation * CardHome;
        }
        private void UpdateInspectionRotation()
        {
            if (inspectionTarget == null) return;
            Quaternion rotation = Quaternion.Euler(-dragDelta.y * 0.24f,
                dragDelta.x * 0.28f, 0f) * inspectionStartRotation;
            inspectionTarget.rotation = rotation;
            if (inspectionTarget == cardStack)
                inspectionTarget.position = inspectionPivotWorld - rotation * CardHome;
        }
        private void BeginInspectionReturn(Transform target)
        {
            if (target == null) return;
            if (inspectionReturnRoutine != null) StopCoroutine(inspectionReturnRoutine);
            Vector3 restPosition = target == pack.transform ? CurrentPackHome : Vector3.zero;
            inspectionReturnRoutine = StartCoroutine(ReturnInspectionPose(target, restPosition));
        }
        private IEnumerator ReturnInspectionPose(Transform target, Vector3 restPosition)
        {
            Vector3 startPosition = target.position;
            Quaternion startRotation = target.rotation;
            const float duration = 0.38f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (target == null) break;
                float u = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                target.position = Vector3.Lerp(startPosition, restPosition, u);
                target.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, u);
                yield return null;
            }
            if (target != null)
            {
                target.position = restPosition;
                target.rotation = Quaternion.identity;
            }
            inspectionReturnRoutine = null;
        }
        private void UpdateObjectGesture()
        {
            if (phase == RevealPhase.Pack)
            {
                tearVisual.PreviewTilt(dragDelta);
                if (dragDelta.magnitude >= 145f) { gestureDragging = false; StartCoroutine(RemovePack(dragDelta)); }
            }
            else if (phase == RevealPhase.CardFront)
            {
                CardVisual card = cards[cardIndex];
                card.transform.position = gestureStartPosition + new Vector3(dragDelta.x * 0.008f, dragDelta.y * -0.004f, 0f);
                card.transform.rotation = Quaternion.Euler(0f, 0f, dragDelta.x * -0.045f) * gestureStartRotation;
            }
        }
        private void CompleteObjectGesture()
        {
            if (phase == RevealPhase.Pack) tearVisual.CancelGesture();
            else if (phase == RevealPhase.CardBack)
            {
                if (dragDelta.magnitude < 80f) StartCoroutine(FlipCard());
            }
            else if (phase == RevealPhase.CardFront)
            {
                if (Mathf.Abs(dragDelta.x) >= 115f) StartCoroutine(MoveToNextCard(Mathf.Sign(dragDelta.x)));
                else RestoreGesturePose(cards[cardIndex].transform);
            }
        }
        private void RestoreGesturePose(Transform target) { target.position = gestureStartPosition; target.rotation = gestureStartRotation; }
        private Transform CurrentGestureTarget() { return phase == RevealPhase.Pack ? pack.transform : cards[cardIndex].transform; }
        private Transform CurrentInspectionTarget()
        {
            if (phase == RevealPhase.Pack) return pack.transform;
            if (phase == RevealPhase.CardBack || phase == RevealPhase.CardFront) return cardStack;
            return null;
        }
        private Material GetTextureMaterial(string key, Texture2D texture, bool transparent, int queueOffset = 0)
        {
            if (texture == null) return null;
            if (materials.TryGetValue(key, out Material cached)) return cached;
            Material material = CreateTextureMaterial(key, texture, transparent, queueOffset);
            materials.Add(key, material);
            return material;
        }
        private static void ApplyTextureOrFallback(Material material, Texture2D texture, Texture2D fallback)
        {
            Texture2D selectedTexture = texture != null ? texture : fallback;
            material.mainTexture = selectedTexture;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", selectedTexture);
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
            }
        }
        private Material CreateTextureMaterial(string key, Texture texture, bool transparent, int queueOffset)
        {
            // Card surfaces are flat 2D artwork; avoid per-pixel lighting for every card layer.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find(transparent ? "Unlit/Transparent" : "Standard");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                throw new InvalidOperationException("CardOpen could not load a runtime texture shader. Check Always Included Shaders.");
            Material material = new Material(shader) { name = key, color = Color.white };
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", transparent ? 0f : 0.24f);
            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.renderQueue = 3000 + queueOffset;
            }
            return material;
        }
        private void CreateBackground(Camera camera)
        {
            string backgroundPath = currentStageChapter >= 3 ? "Textures/Chapter3Background" : currentStageChapter >= 2 ? "Textures/Chapter2Background" : "Textures/BattleBackground";
            Texture2D texture = Resources.Load<Texture2D>(backgroundPath);
            if (texture == null) texture = Resources.Load<Texture2D>("Textures/SimpleBackground");
            if (texture == null || camera == null) return;
            background = new GameObject("2D Background");
            SpriteRenderer renderer = background.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            renderer.sortingOrder = -1000;
            LayoutBackground(camera);
        }
        private void UpdateChapterBackground()
        {
            if (background == null) return;
            Texture2D texture = Resources.Load<Texture2D>(currentStageChapter >= 3 ? "Textures/Chapter3Background" : currentStageChapter >= 2 ? "Textures/Chapter2Background" : "Textures/BattleBackground");
            if (texture == null) return;
            SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
            if (renderer == null) return;
            renderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            LayoutBackground(Camera.main);
        }
        private void LayoutBackground(Camera camera)
        {
            if (background == null || camera == null) return;
            SpriteRenderer renderer = background.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) return;
            const float distance = 24f;
            float height = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector2 spriteSize = renderer.sprite.bounds.size;
            background.transform.position = camera.transform.position + camera.transform.forward * distance;
            background.transform.rotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);
            background.transform.localScale = new Vector3(
                height * camera.aspect * 1.05f / Mathf.Max(0.01f, spriteSize.x),
                height * 1.05f / Mathf.Max(0.01f, spriteSize.y), 1f);
        }
        private static GameObject CreateQuadObject(string objectName)
        {
            GameObject quadObject = new GameObject(objectName);
            MeshFilter filter = quadObject.AddComponent<MeshFilter>();
            quadObject.AddComponent<MeshRenderer>();
            Mesh mesh = new Mesh { name = objectName + " Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            return quadObject;
        }
        private Material GetTextureMaterial(string key, string resourcePath, bool transparent, int queueOffset = 0)
        {
            if (materials.TryGetValue(key, out Material cached)) return cached;
            Material material = CreateTextureMaterial(key, Resources.Load<Texture2D>(resourcePath), transparent, queueOffset);
            materials.Add(key, material);
            return material;
        }
        private Material GetMaterial(string key, Color color, float smoothness)
        {
            if (materials.TryGetValue(key, out Material material)) return material;
            // All prototype world surfaces are authored as flat 2D-style colors.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null)
                throw new InvalidOperationException("CardOpen could not load a runtime material shader. Check Always Included Shaders.");
            material = new Material(shader) { name = key, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", smoothness * 0.25f);
            materials.Add(key, material);
            return material;
        }
    }
}
