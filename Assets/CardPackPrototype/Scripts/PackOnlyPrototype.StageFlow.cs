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
        private void BeginSequence()
        {
            BeginSequence(true);
        }
        private void BeginSequence(bool chooseRandomPack)
        {
            currentPackOpenedForGoal = false;
            startingHandVisible = false;
            packTearInProgress = false;
            if (chooseRandomPack)
            {
                global::CardPackData selectedPack = LoadCardPackData();
                if (selectedPack != null) activePackData = selectedPack;
            }
            RefreshActivePackArtwork();
            ResetPerPackAccumulatedBonuses();
            ResetOncePerPackAbilityUsage();
            ClearCards();
            cardStack.position = Vector3.zero;
            cardStack.rotation = Quaternion.identity;
            cardStack.localScale = Vector3.one;
            currentPackIsHolographic = RollCurrentPackHolographic();
            if (shopRewardOpeningActive) BuildShopRewardPackPlaceholder();
            else BuildHiddenCardStack();
            cardIndex = 0;
            gestureDragging = false;
            inspectionDragging = false;
            dragDelta = Vector2.zero;
            activeSlidingCard = null;
            cardTransitionActive = false;
            transitionDragActive = false;
            transitionSwipeCommitted = false;
            queuedCardSwipes = 0;
            pack.ResetVisual();
            pack.SetHolographic(currentPackIsHolographic);
            tearVisual.ResetTear();
            pack.transform.position = CurrentPackHome;
            pack.transform.localScale = Vector3.one * ResponsiveWorldScale(1.95f, 1.50f);
            pack.transform.rotation = Quaternion.identity;
            phase = RevealPhase.Pack;
            if (inspectedDeckIndex >= 0)
            {
                inspectionPackWasActive = true;
                inspectionStackWasActive = true;
                pack.gameObject.SetActive(false);
                cardStack.gameObject.SetActive(false);
            }
        }
        private void EnsureStageSelectionCharacter()
        {
            if (stageSelectionCharacter != null) return;
            Texture2D texture = Resources.Load<Texture2D>("Textures/StageSelectionCharacter");
            if (texture == null)
            {
                Debug.LogWarning("Textures/StageSelectionCharacter could not be loaded.");
                return;
            }
            stageSelectionCharacter = new GameObject("Stage Selection Character");
            stageSelectionCharacterRenderer = stageSelectionCharacter.AddComponent<SpriteRenderer>();
            stageSelectionCharacterRenderer.sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            stageSelectionCharacterRenderer.sortingOrder = 1500;
        }

        private void LayoutStageSelectionCharacter()
        {
            EnsureStageSelectionCharacter();
            if (stageSelectionCharacter == null || stageSelectionCharacterRenderer == null) return;
            stageSelectionCharacter.SetActive(stageSelectionVisible);
            if (!stageSelectionVisible) return;
            stageSelectionCharacter.transform.position = new Vector3(0f, -2f, 0.18f);
            stageSelectionCharacter.transform.rotation = Quaternion.identity;
            stageSelectionCharacter.transform.localScale = Vector3.one * 0.7f;
        }

        private void EnsureRestCharacter()
        {
            if (restCharacter != null) return;
            Texture2D texture = Resources.Load<Texture2D>("Textures/RestCharacter");
            if (texture == null)
            {
                Debug.LogWarning("Textures/RestCharacter could not be loaded.");
                return;
            }
            restCharacter = new GameObject("Rest Character");
            restCharacterRenderer = restCharacter.AddComponent<SpriteRenderer>();
            restCharacterRenderer.sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            restCharacterRenderer.sortingOrder = 1500;
        }

        private void SetRestCharacterVisible(bool visible)
        {
            if (visible) EnsureRestCharacter();
            if (restCharacter == null || restCharacterRenderer == null) return;
            restCharacter.SetActive(visible);
            if (!visible) return;
            restCharacter.transform.position = new Vector3(0f, 0.5f, 0.18f);
            restCharacter.transform.rotation = Quaternion.identity;
            restCharacter.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        }
        private void SetChoiceCharacterVisible(bool visible)
        {
            if (visible && choiceCharacter == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("Textures/ChoiceCharacter");
                if (texture != null) { choiceCharacter = new GameObject("Choice Character"); choiceCharacterRenderer = choiceCharacter.AddComponent<SpriteRenderer>(); choiceCharacterRenderer.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f); choiceCharacterRenderer.sortingOrder = 1500; }
            }
            if (choiceCharacter == null) return;
            choiceCharacter.SetActive(visible);
            if (visible) { choiceCharacter.transform.position = new Vector3(0f, -2f, 0.18f); choiceCharacter.transform.localScale = Vector3.one * 0.7f; }
        }
        private Vector3 GetResponsiveDiscardPileWorldPosition()
        {
            Camera camera = Camera.main;
            if (camera == null) return Vector3.zero;
            Rect safeArea = Screen.safeArea;
            float x = safeArea.xMin + safeArea.width * (IsPortraitUi ? 0.88f : 0.89f);
            float y = safeArea.yMin + safeArea.height * (IsPortraitUi ? 0.055f : 0.075f);
            float depth = camera.WorldToScreenPoint(CardHome).z;
            return camera.ScreenToWorldPoint(new Vector3(x, y, depth));
        }
        private void BeginStageSelection()
        {
            if (!stageChapterInitialized)
            {
                ClearStageHand(true);
                ClearStageDiscardPileVisuals();
                stageDrawPile.Clear();
                finalBossStageSpawned = false;
                firstStageChoiceBonusAvailable = true;
                completedStageCount = 0;
                const string stageDeckPath = "Combat/StageDeck";
                global::StageDeckDefinition stageDeck = Resources.Load<global::StageDeckDefinition>(stageDeckPath);
                if (stageDeck == null || stageDeck.Entries == null || stageDeck.Entries.Count == 0)
                {
                    Debug.LogError("Combat/StageDeck asset is missing or empty.");
                    return;
                }
                List<global::StageCardType> shuffled = new List<global::StageCardType>();
                for (int i = 0; i < stageDeck.Entries.Count; i++)
                {
                    global::StageDeckEntry entry = stageDeck.Entries[i];
                    if (entry == null || entry.Type == null) continue;
                    for (int copy = 0; copy < Mathf.Max(1, entry.Copies); copy++) shuffled.Add(entry.Type);
                }
                ShuffleStageCards(shuffled);
                for (int i = 0; i < shuffled.Count; i++) stageDrawPile.Enqueue(shuffled[i]);
                stageChapterInitialized = true;
            }

            bool chapterFinished = completedStageCount >= 6 || (stageHand.Count == 0 && stageDiscardPile.Count > 0 && stageDrawPile.Count == 0);
            if (chapterFinished)
            {
                stageDrawPile.Clear();
                ClearStageHand(false);
                SpawnFinalBossStageIfNeeded();
            }
            else
            {
                while (stageHand.Count < 5 && DrawStageCardToHand()) { }
                SpawnFinalBossStageIfNeeded();
            }
            phase = RevealPhase.PackChoice;
            stageSelectionVisible = true;
            startingHandVisible = false;
            ClearCards();
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(false);
            for (int i = 0; i < enemyVisuals.Count; i++)
                if (enemyVisuals[i] != null) enemyVisuals[i].gameObject.SetActive(false);
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            CreateStageDiscardPile();
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(true);
            LayoutStageSelectionHand();
            LayoutStageDiscardPile();
            LayoutStageSelectionCharacter();

            MatchStageDiscardNumberWhenNoCardIsPlayable();
            LayoutStageDiscardPile();
            RefreshStageCardInteractionStates();
        }

        private string GetStageSelectionTitle()
        {
            bool bossVisible = finalBossStageSpawned;
            if (!bossVisible)
            {
                for (int i = 0; i < stageHand.Count; i++)
                {
                    if (stageHand[i] != null && stageHand[i].Kind == global::StageCardKind.BossBattle)
                    {
                        bossVisible = true;
                        break;
                    }
                }
            }

            if (bossVisible) return Ui("스테이지 선택 (보스)", "Choose a Stage (Boss)");
            int completedStages = Mathf.Clamp(completedStageCount + 1, 1, 6);
            return Ui("스테이지 선택 (" + completedStages + "/6)", "Choose a Stage (" + completedStages + "/6)");
        }
        private void ShuffleStageCards(List<global::StageCardType> cardsToShuffle)
        {
            for (int i = cardsToShuffle.Count - 1; i > 0; i--)
            {
                int swap = UnityEngine.Random.Range(0, i + 1);
                global::StageCardType temp = cardsToShuffle[i];
                cardsToShuffle[i] = cardsToShuffle[swap];
                cardsToShuffle[swap] = temp;
            }
        }

        private bool DrawStageCardToHand()
        {
            // Stage cards are consumed for this chapter. Discarded cards stay discarded;
            // a chapter ends with its boss instead of recycling the same route indefinitely.
            if (stageDrawPile.Count == 0) return false;
            global::StageCardType stage = stageDrawPile.Dequeue();
            stageHand.Add(stage);
            stageHandVisuals.Add(CreateStageCardVisual(stage));
            return true;
        }

        private global::BattleEncounters GetChapterStageEncounters(global::StageCardType stage)
        {
            if (stage == null || currentStageChapter <= 1) return stage != null ? stage.Encounters : null;
            string encounterName;
            switch (stage.Kind)
            {
                case global::StageCardKind.Battle: encounterName = "일반전투"; break;
                case global::StageCardKind.EliteBattle: encounterName = "정예전투"; break;
                case global::StageCardKind.BossBattle: encounterName = "보스전투"; break;
                default: return stage.Encounters;
            }
            string path = "Combat/BattleEncounters/챕터" + currentStageChapter + encounterName;
            global::BattleEncounters chapterEncounters = Resources.Load<global::BattleEncounters>(path);
            return chapterEncounters != null ? chapterEncounters : stage.Encounters;
        }
        private void SpawnFinalBossStageIfNeeded()
        {
            if (finalBossStageSpawned || stageHand.Count > 0 || stageDrawPile.Count > 0) return;
            const string bossPath = "Combat/Stages/보스전투";
            global::StageCardType bossTemplate = Resources.Load<global::StageCardType>(bossPath);
            if (bossTemplate == null)
            {
                Debug.LogError(bossPath + " asset is missing.");
                return;
            }

            global::StageCardType previous = GetTopStageDiscardCard();
            global::StageCardType boss = ScriptableObject.CreateInstance<global::StageCardType>();
            boss.StageName = bossTemplate.StageName;
            boss.Description = bossTemplate.Description;
            boss.EnglishName = bossTemplate.EnglishName;
            boss.EnglishDescription = bossTemplate.EnglishDescription;
            boss.Image = bossTemplate.Image;
            boss.Kind = global::StageCardKind.BossBattle;
            boss.Encounters = bossTemplate.Encounters;
            boss.Number = previous != null ? previous.Number : 1;
            global::CardColor previousColor = GetStageRuntimeColor(previous);
            // Opposite black/white is cast-compatible and always grants the enhanced sequence bonus.
            boss.Color = previousColor == global::CardColor.Black ? global::CardColor.White : global::CardColor.Black;
            boss.name = "Final Boss Stage";

            finalBossStageSpawned = true;
            stageHand.Add(boss);
            stageHandVisuals.Add(CreateStageCardVisual(boss));
        }
        private static global::CardColor GetStageRuntimeColor(global::StageCardType stage)
        {
            return stage != null && stage.Color == global::CardColor.Black
                ? global::CardColor.Black : global::CardColor.White;
        }
        private global::StageCardType GetTopStageDiscardCard()
        {
            return stageDiscardPile.Count > 0 ? stageDiscardPile[stageDiscardPile.Count - 1] : null;
        }

        private bool CanUseStageCard(int index, out bool enhancedCast)
        {
            enhancedCast = false;
            if (index < 0 || index >= stageHand.Count || stageHand[index] == null) return false;
            global::StageCardType candidate = stageHand[index];
            global::StageCardType topDiscard = GetTopStageDiscardCard();
            if (topDiscard == null || firstStageChoiceBonusAvailable)
            {
                // Discarding does not consume the first-stage color bonus; only entering a stage does.
                enhancedCast = true;
                return true;
            }
            bool matchingNumber = candidate.Number == topDiscard.Number;
            bool matchingColor = ColorsMatchForCast(GetStageRuntimeColor(candidate), GetStageRuntimeColor(topDiscard));
            bool adjacentColorNumber = matchingColor && NumbersAreAdjacent(candidate.Number, topDiscard.Number);
            bool normalCast = matchingNumber || adjacentColorNumber;
            if (!normalCast) return false;
            enhancedCast = IsEnhancedColorSequence(GetStageRuntimeColor(topDiscard), GetStageRuntimeColor(candidate));
            return true;
        }
        private void MatchStageDiscardNumberWhenNoCardIsPlayable()
        {
            if (stageDiscardPile.Count == 0 || stageHand.Count == 0) return;

            bool hasPlayableCard = false;
            for (int i = 0; i < stageHand.Count; i++)
            {
                if (stageHand[i] == null) continue;
                if (CanUseStageCard(i, out _))
                {
                    hasPlayableCard = true;
                    break;
                }
            }
            if (hasPlayableCard) return;

            global::StageCardType topDiscard = GetTopStageDiscardCard();
            if (topDiscard == null) return;

            List<int> handNumbers = new List<int>();
            for (int i = 0; i < stageHand.Count; i++)
            {
                if (stageHand[i] != null) handNumbers.Add(Mathf.Clamp(stageHand[i].Number, 1, 6));
            }
            if (handNumbers.Count == 0) return;

            int newNumber = handNumbers[UnityEngine.Random.Range(0, handNumbers.Count)];
            if (topDiscard.Number == newNumber) return;
            topDiscard.Number = newNumber;

            CardVisual oldVisual = stageDiscardPileTop;
            int visualIndex = stageDiscardPileVisuals.IndexOf(oldVisual);
            if (oldVisual != null) Destroy(oldVisual.gameObject);
            if (visualIndex < 0) return;

            CardVisual replacement = CreateStageCardVisual(topDiscard);
            if (replacement == null) return;
            replacement.transform.SetParent(stageDiscardPileRoot, true);
            replacement.SetInteractionState(true, false);
            replacement.SetFaceUp(true);
            stageDiscardPileVisuals[visualIndex] = replacement;
            stageDiscardPileTop = replacement;
        }
        private void RefreshStageCardInteractionStates()
        {
            for (int i = 0; i < stageHandVisuals.Count; i++)
            {
                CardVisual visual = stageHandVisuals[i];
                if (visual == null) continue;
                bool playable = CanUseStageCard(i, out bool enhancedCast);
                visual.SetInteractionState(playable, enhancedCast);
            }
        }

        private CardVisual CreateStageCardVisual(global::StageCardType stage)
        {
            if (stage == null) return null;
            global::CardData data = stage.CreateRuntimeCardData();
            CardVisual visual = CardVisual.CreatePrefabInstance("Stage Card - " + data.Name);
            global::CardColor stageColor = GetStageRuntimeColor(stage);
            string colorKey = stageColor.ToString();
            visual.BuildFromData(data, stageColor,
                GetTextureMaterial("StageAttribute_" + colorKey, "CardAssets/Attributes/Attribute" + colorKey, false),
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                GetTextureMaterial("StagePattern_" + data.RarityAssetKey,
                    "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0),
                GetTextureMaterial("StageImage_" + stage.name, stage.Image, true, 10),
                GetTextureMaterial("StageCost_" + stage.Number, "CardAssets/Costs/Cost" + stage.Number, true, 20),
                font, IsEnglishUi);
            visual.SetDisplayName(stage.GetLocalizedName(IsEnglishUi));
            visual.SetDisplayDescription(data, stage.GetLocalizedDescription(IsEnglishUi), IsEnglishUi, string.Empty);
            visual.SetSortingOrder(800);
            return visual;
        }

        private void LayoutStageSelectionHand()
        {
            if (!stageSelectionVisible) return;
            RefreshStageCardInteractionStates();
            highlightedStageHandCard = null;
            stageHandHoverPointerDirty = true;
            stageHandHomePositions.Clear();
            stageHandHomeRotations.Clear();
            int handCount = stageHandVisuals.Count;
            int columns = handCount > 6 ? 6 : Mathf.Max(1, handCount);
            for (int i = 0; i < handCount; i++)
            {
                CardVisual visual = stageHandVisuals[i];
                if (visual == null) continue;
                int row = i / columns;
                int indexInRow = i % columns;
                int rowCount = Mathf.Min(columns, handCount - row * columns);
                float fanOffset = indexInRow - (rowCount - 1) * 0.5f;
                float edgeAmount = rowCount <= 1 ? 0f : Mathf.Abs(fanOffset) / ((rowCount - 1) * 0.5f);
                Vector3 homePosition = new Vector3(fanOffset * 1.16f,
                    -3.08f - row * 0.92f - edgeAmount * 0.32f, -0.20f + i * 0.02f);
                Quaternion homeRotation = Quaternion.Euler(-4f, 0f, fanOffset * -3f);
                stageHandHomePositions.Add(homePosition);
                stageHandHomeRotations.Add(homeRotation);
                visual.gameObject.SetActive(true);
                visual.transform.position = homePosition;
                visual.transform.localRotation = homeRotation;
                visual.transform.localScale = Vector3.one * CurrentHandCardScale;
                visual.SetSortingOrder(i);
                visual.SetFaceUp(true);
            }
        }

        private void UpdateDiscardPileHover()
        {
            discardPileHovered = false;
            Camera camera = Camera.main;
            if (camera == null || usedPileExpanded || combatDeckInspectionVisible) return;
            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            CardVisual target = null;
            Vector3 homePosition = Vector3.zero;
            float homeScale = 1f;
            if (stageSelectionVisible)
            {
                target = stageDiscardPileTop != null ? stageDiscardPileTop : stageDiscardPilePlaceholder;
                homePosition = GetStageDiscardPileWorldPosition();
                homeScale = CurrentHandCardScale * 0.9f;
            }
            else if (usedPileRoot != null && usedPileRoot.gameObject.activeSelf && !usedPileAnimating)
            {
                target = usedPileCard != null ? usedPileCard : usedPilePlaceholder;
                homePosition = GetUsedPileWorldPosition();
                homeScale = CurrentHandCardScale * 0.9f;
            }
            if (target == null || !target.gameObject.activeSelf) return;
            Rect targetRect = GetVisualScreenRect(target.gameObject, camera);
            bool hovered = targetRect.Contains(mouse);
            if (!hovered && discardPileHovered)
            {
                float movedPixels = Mathf.Abs(camera.WorldToScreenPoint(target.transform.position).y
                    - camera.WorldToScreenPoint(homePosition).y);
                Rect retainedHoverRect = new Rect(targetRect.xMin, targetRect.yMin,
                    targetRect.width, targetRect.height + movedPixels);
                hovered = retainedHoverRect.Contains(mouse);
            }
            discardPileHovered = hovered;
            float transition = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            target.transform.position = Vector3.Lerp(target.transform.position,
                homePosition + (hovered ? Vector3.up * 1.15f : Vector3.zero), transition);
            target.transform.localScale = Vector3.Lerp(target.transform.localScale,
                Vector3.one * (homeScale * (hovered ? 1.06f : 1f)), transition);
            target.transform.rotation = camera.transform.rotation;
            discardPileHoverOffsetY = target.transform.position.y - homePosition.y;
            TextMeshPro countLabel = stageSelectionVisible ? stageDiscardPileCountText : usedPileCountText;
            if (countLabel != null)
            {
                Vector3 desiredPosition = new Vector3(0f, 1.75f + discardPileHoverOffsetY, -0.02f);
                if ((countLabel.transform.localPosition - desiredPosition).sqrMagnitude > 0.000001f)
                    countLabel.transform.localPosition = desiredPosition;
            }
        }
        private void UpdateStageHandHover()
        {
            if (!stageSelectionVisible || draggedStageHandIndex >= 0 || pressedStageHandIndex >= 0)
                return;
            Camera camera = Camera.main;
            if (camera == null || !hasStageHandHoverPointer) return;
            if (stageHandHoverPointerDirty)
            {
                CardVisual hovered = null;
                for (int i = stageHandVisuals.Count - 1; i >= 0; i--)
                {
                    CardVisual card = stageHandVisuals[i];
                    if (card != null && card.gameObject.activeSelf
                        && GetVisualScreenRect(card.gameObject, camera).Contains(lastStageHandHoverPointer))
                    {
                        hovered = card;
                        break;
                    }
                }
                if (highlightedStageHandCard != hovered)
                {
                    highlightedStageHandCard = hovered;
                    stageHandHoverAnimationUntil = Time.unscaledTime + 0.20f;
                }
                stageHandHoverPointerDirty = false;
            }
            if (Time.unscaledTime >= stageHandHoverAnimationUntil) return;
            float transition = 1f - Mathf.Exp(-13f * Time.unscaledDeltaTime);
            for (int i = 0; i < stageHandVisuals.Count && i < stageHandHomePositions.Count; i++)
            {
                CardVisual card = stageHandVisuals[i];
                if (card == null || !card.gameObject.activeSelf) continue;
                bool isHovered = card == highlightedStageHandCard;
                Vector3 targetPosition = stageHandHomePositions[i] + (isHovered ? Vector3.up * 1.84f : Vector3.zero);
                float targetScale = CurrentHandCardScale * (isHovered ? 1.20f : 1f);
                card.transform.position = Vector3.Lerp(card.transform.position, targetPosition, transition);
                card.transform.localScale = Vector3.Lerp(card.transform.localScale, Vector3.one * targetScale, transition);
                card.SetSortingOrder(isHovered ? 1000 : i);
            }
        }

        private void RestoreStageHandCard(int index)
        {
            if (index < 0 || index >= stageHandVisuals.Count || index >= stageHandHomePositions.Count) return;
            CardVisual card = stageHandVisuals[index];
            if (card == null) return;
            card.transform.position = stageHandHomePositions[index];
            if (index < stageHandHomeRotations.Count)
                card.transform.localRotation = stageHandHomeRotations[index];
            card.transform.localScale = Vector3.one * CurrentHandCardScale;
            card.SetSortingOrder(index);
        }

        private bool IsStageCardRaisedForCast(int index)
        {
            return index >= 0 && index < stageHandVisuals.Count && stageHandVisuals[index] != null
                && (index >= stageHandHomePositions.Count
                    || stageHandVisuals[index].transform.position.y > stageHandHomePositions[index].y + 3.00f);
        }
        private void CreateStageDiscardPile()
        {
            if (stageDiscardPileRoot == null)
            {
                GameObject pileObject = new GameObject("Stage Discard Pile");
                stageDiscardPileRoot = pileObject.transform;
            }
            if (stageDiscardPilePlaceholder != null) return;

            stageDiscardPilePlaceholderData = ScriptableObject.CreateInstance<global::CardData>();
            stageDiscardPilePlaceholderData.Name = "버린 스테이지 잎";
            stageDiscardPilePlaceholderData.Description = "여기에 스테이지 선택지를 드래그해 버릴 수 있습니다.\n빈자리는 다음 스테이지 완료시 다른 선택지로 채워집니다.\n손에 남은 스테이지 선택지가 없거나 6개 스테이지 진행후 보스 스테이지에 진입합니다";
            stageDiscardPilePlaceholderData.Rare = global::CardRarity.Common;
            stageDiscardPilePlaceholder = CardVisual.CreatePrefabInstance("Stage Discard Placeholder", stageDiscardPileRoot);
            stageDiscardPilePlaceholder.BuildFromData(stageDiscardPilePlaceholderData, global::CardColor.Black,
                GetTextureMaterial("StageDiscardAttribute", "CardAssets/Attributes/AttributeBlack", false),
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                GetTextureMaterial("StageDiscardPattern", "CardAssets/Rarities/PatternCommon", true, 0),
                GetTextureMaterial("StageDiscardMana", "CardAssets/Content/Mana", true, 10), GetTextureMaterial("StageDiscardCost", "CardAssets/Costs/Cost1", true, 20), font, IsEnglishUi);
            stageDiscardPilePlaceholder.SetDisplayName(Ui("스테이지 버린 잎", "Stage Discard"));
            stageDiscardPilePlaceholder.SetFaceUp(true);
        }

        private void ClearStageDiscardPileVisuals()
        {
            for (int i = 0; i < stageDiscardPileVisuals.Count; i++)
                if (stageDiscardPileVisuals[i] != null) Destroy(stageDiscardPileVisuals[i].gameObject);
            stageDiscardPileVisuals.Clear();
            stageDiscardPileTop = null;
            if (stageDiscardPilePlaceholder != null) stageDiscardPilePlaceholder.gameObject.SetActive(true);
        }

        private Vector3 GetStageDiscardPileWorldPosition()
        {
            return GetResponsiveDiscardPileWorldPosition();
        }
        private void LayoutStageDiscardPile()
        {
            if (stageDiscardPileRoot == null || !stageDiscardPileRoot.gameObject.activeSelf) return;
            CreateStageDiscardPile();
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 pilePosition = GetStageDiscardPileWorldPosition();
            CardVisual visibleCard = stageDiscardPileTop != null ? stageDiscardPileTop : stageDiscardPilePlaceholder;
            if (stageDiscardPilePlaceholder != null)
                stageDiscardPilePlaceholder.gameObject.SetActive(stageDiscardPileTop == null);
            for (int i = 0; i < stageDiscardPileVisuals.Count; i++)
            {
                CardVisual card = stageDiscardPileVisuals[i];
                if (card == null) continue;
                bool isTop = card == stageDiscardPileTop;
                card.gameObject.SetActive(isTop);
            }
            if (visibleCard == null) return;
            visibleCard.gameObject.SetActive(true);
            visibleCard.transform.position = pilePosition;
            visibleCard.transform.localScale = Vector3.one * (CurrentHandCardScale * 0.9f);
            visibleCard.transform.rotation = camera.transform.rotation;
            visibleCard.SetSortingOrder(1600);
            UpdateDiscardPileCountLabel(ref stageDiscardPileCountText, stageDiscardPileRoot, pilePosition,
                CurrentHandCardScale * 0.9f, stageDrawPile.Count, stageDiscardPile.Count);
        }

        private bool IsPointOverStageDiscardPile(Vector2 screenPoint)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            CardVisual target = stageDiscardPileTop != null ? stageDiscardPileTop : stageDiscardPilePlaceholder;
            return target != null && target.gameObject.activeSelf
                && GetVisualScreenRect(target.gameObject, camera).Contains(screenPoint);
        }

        private void MoveStageHandCardToDiscard(int index, bool placeOnTop)
        {
            if (index < 0 || index >= stageHand.Count || index >= stageHandVisuals.Count) return;
            global::StageCardType stage = stageHand[index];
            CardVisual card = stageHandVisuals[index];
            if (stage == null || card == null) return;
            CreateStageDiscardPile();
            stageHand.RemoveAt(index);
            stageHandVisuals.RemoveAt(index);
            if (placeOnTop)
            {
                stageDiscardPile.Add(stage);
                stageDiscardPileVisuals.Add(card);
                stageDiscardPileTop = card;
            }
            else
            {
                stageDiscardPile.Insert(0, stage);
                stageDiscardPileVisuals.Insert(0, card);
            }
            card.transform.SetParent(stageDiscardPileRoot, true);
            card.SetInteractionState(true, false);
            card.SetFaceUp(true);
        }

        private void DiscardStageHandCard(int index)
        {
            bool discardedBoss = index >= 0 && index < stageHand.Count && stageHand[index] != null
                && stageHand[index].Kind == global::StageCardKind.BossBattle;
            MoveStageHandCardToDiscard(index, false);
            if (discardedBoss) finalBossStageSpawned = false;
            if (stageHand.Count == 0 && stageDiscardPile.Count > 0)
            {
                stageDrawPile.Clear();
                SpawnFinalBossStageIfNeeded();
            }
            LayoutStageSelectionHand();
            LayoutStageDiscardPile();
        }

        private void ClearStageHand(bool clearDiscard = false)
        {
            for (int i = 0; i < stageHandVisuals.Count; i++)
                if (stageHandVisuals[i] != null) Destroy(stageHandVisuals[i].gameObject);
            stageHandVisuals.Clear();
            stageHandHomePositions.Clear();
            stageHandHomeRotations.Clear();
            highlightedStageHandCard = null;
            pressedStageHandIndex = -1;
            draggedStageHandIndex = -1;
            stageHand.Clear();
            if (clearDiscard) stageDiscardPile.Clear();
        }

        private void StartSelectedStage(int index)
        {
            if (combatEntryRoutine != null || index < 0 || index >= stageHand.Count) return;
            global::StageCardType precheckStage = stageHand[index];
            bool enhancedCast = false;
            if (precheckStage == null || !CanUseStageCard(index, out enhancedCast)) return;
            global::StageCardType stage = stageHand[index];
            if (stage == null) return;
            if (stage.Kind != global::StageCardKind.Rest && stage.Kind != global::StageCardKind.Event && stage.Encounters == null) return;
            if (stage.Kind != global::StageCardKind.BossBattle) completedStageCount = Mathf.Min(6, completedStageCount + 1);
            firstStageChoiceBonusAvailable = false;
            if (stage.Kind == global::StageCardKind.Event) { StartEventStage(index, stage); return; }
            SetCombatEntryFade(0f);
            combatEntryRoutine = StartCoroutine(stage.Kind == global::StageCardKind.Rest ? EnterRestWithFade(stage) : EnterCombatWithFade(stage, enhancedCast));
        }
        private global::StageCardType CreateRuntimeEventStage(int id)
        {
            global::StageCardType s = ScriptableObject.CreateInstance<global::StageCardType>();
            s.Kind = global::StageCardKind.Event; s.Number = id == 1 ? 5 : 6; s.Color = id == 1 ? global::CardColor.Green : global::CardColor.White;
            s.StageName = id == 1 ? "떨어진 잎" : "수상한 요구";
            s.Description = id == 1 ? "바닥에 떨어진 잎을 발견했다." : "수상한 인물이 잎을 요구했다.";
            s.EnglishName = id == 1 ? "A Fallen Card" : "A Suspicious Request"; s.EnglishDescription = id == 1 ? "You found a card on the ground." : "A suspicious figure asks for a card.";
            return s;
        }
        private void StartEventStage(int index, global::StageCardType stage)
        {
            MoveStageHandCardToDiscard(index, true); lastUsedStageCard = stage; stageSelectionVisible = false; eventChoiceActive = true; activeEventId = UnityEngine.Random.Range(1, 3);
            BeginOfferHand(stage.StageName, stage.Description + "\\n선택지 잎을 위로 드래그해 사용하세요.", 0);
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            if (combatPlayerCharacter != null) combatPlayerCharacter.SetActive(false);
            if (stageSelectionCharacter != null) stageSelectionCharacter.SetActive(false);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
            for (int i = 0; i < stageHandVisuals.Count; i++) if (stageHandVisuals[i] != null) stageHandVisuals[i].gameObject.SetActive(false);
            for (int i = 0; i < enemyVisuals.Count; i++) if (enemyVisuals[i] != null) enemyVisuals[i].gameObject.SetActive(false);
            AddOfferCard(activeEventId == 1 ? "줍는다" : "잎을 건낸다", activeEventId == 1 ? "무작위 고급 잎 1장을 획득합니다." : "무작위 잎을 잃고 별빛 150을 획득합니다.", -1, global::CardColor.White, 1);
            AddOfferCard("무시한다", "스테이지 선택으로 이동합니다.", -1, global::CardColor.Black, 2); LayoutStartingHand(); RefreshHandCardInteractionStates();
        }
        private IEnumerator EnterRestWithFade(global::StageCardType stage)
        {
            yield return FadeCombatEntry(0f, 1f, 0.08f);
            int stageIndex = stageHand.IndexOf(stage);
            if (stageIndex < 0) { SetCombatEntryFade(0f); combatEntryRoutine = null; yield break; }
            MoveStageHandCardToDiscard(stageIndex, true);
            lastUsedStageCard = stage;
            stageSelectionVisible = false;
            stageDeckInspectionMode = false;
            if (stageSelectionCharacter != null) stageSelectionCharacter.SetActive(false);
            SetRestCharacterVisible(true);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
            for (int i = 0; i < stageHandVisuals.Count; i++) if (stageHandVisuals[i] != null) stageHandVisuals[i].gameObject.SetActive(false);
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            ClearCards();
            restStageActive = true;
            startingHandVisible = true;
            BeginOfferHand(Ui("휴식", "Rest"), Ui("사용하면 잃은 체력의 30% + 10을 회복합니다.", "Restore 30% of missing HP + 10."), 1);
            // Rest is a single mandatory choice: keep the discard pile hidden and make its card stand out.
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            if (cards.Count > 0 && cards[0] != null) cards[0].SetInteractionState(true, true);
            yield return FadeCombatEntry(1f, 0f, 0.10f);
            combatEntryRoutine = null;
        }
        private void UseRestCard(int index)
        {
            if (index < 0 || index >= cards.Count) return;
            int missing = Mathf.Max(0, PlayerMaximumHealth - playerHealth);
            int recovered = Mathf.Min(missing, Mathf.CeilToInt(missing * 0.30f) + 10);
            playerHealth += recovered;
            AddScorePopup(Ui("휴식\n체력 +" + recovered, "Rest\nHP +" + recovered), new Color(0.4f, 1f, 0.58f), Time.unscaledTime, scorePopups.Count, 0);
            restStageActive = false;
            SetRestCharacterVisible(false);
            ClearCards();
            BeginStageSelection();
        }
        private IEnumerator EnterCombatWithFade(global::StageCardType stage, bool enhancedCast)
        {
            // Keep the stage screen visible while it fades out, then hide all synchronous setup behind black.
            yield return FadeCombatEntry(0f, 1f, 0.08f);
            int stageIndex = stageHand.IndexOf(stage);
            if (stageIndex < 0)
            {
                SetCombatEntryFade(0f);
                combatEntryRoutine = null;
                yield break;
            }
            MoveStageHandCardToDiscard(stageIndex, true);
            lastUsedStageCard = stage;
            if (enhancedCast)
            {
                gold += 50;
                AddScorePopup(Ui("스테이지 강화 시전!\n50 골드 획득", "Enhanced stage cast!\nGained 50 gold"),
                    new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            }
            selectedStageEncounters = GetChapterStageEncounters(stage);
            stageSelectionVisible = false;
            stageDeckInspectionMode = false;
            if (stageSelectionCharacter != null) stageSelectionCharacter.SetActive(false);
            for (int i = 0; i < stageHandVisuals.Count; i++)
                if (stageHandVisuals[i] != null) stageHandVisuals[i].gameObject.SetActive(false);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);

            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(true);
            CreateEnemyStates();
            RefreshEnemyVisual();
            BeginStartingHand();
            yield return null;
            yield return FadeCombatEntry(1f, 0f, 0.10f);
            combatEntryRoutine = null;
        }
        private IEnumerator FadeCombatEntry(float from, float to, float duration)
        {
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                SetCombatEntryFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
            SetCombatEntryFade(to);
        }
        private void SetCombatEntryFade(float alpha)
        {
            combatEntryFade = Mathf.Clamp01(alpha);
            EnsureCombatEntryFadeOverlay();
            Color color = combatEntryFadeImage.color;
            color.a = combatEntryFade;
            combatEntryFadeImage.color = color;
            combatEntryFadeImage.transform.SetAsLastSibling();
        }
    }
}
 