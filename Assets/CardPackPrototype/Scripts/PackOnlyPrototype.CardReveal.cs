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
        private void BeginStartingHand()
        {
            currentPackOpenedForGoal = false;
            startingHandVisible = true;
            highlightedHandCard = null;
            pressedHandIndex = -1;
            draggedHandIndex = -1;
            packTearInProgress = false;
            RefreshActivePackArtwork();
            ResetPerPackAccumulatedBonuses();
            ResetOncePerPackAbilityUsage();
            ClearCards();
            cardStack.position = Vector3.zero;
            cardStack.rotation = Quaternion.identity;
            cardStack.localScale = Vector3.one;
            currentPackIsHolographic = RollCurrentPackHolographic();
            if (shopRewardOpeningActive) BuildShopRewardHand();
            else BuildStarterDeck();
            CreateUsedCardPile();
            LayoutUsedCardPile();
            cardIndex = 0;
            gestureDragging = false;
            inspectionDragging = false;
            dragDelta = Vector2.zero;
            activeSlidingCard = null;
            cardTransitionActive = false;
            transitionDragActive = false;
            transitionSwipeCommitted = false;
            queuedCardSwipes = 0;
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(true);
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            phase = RevealPhase.CardFront;
        }
        private void PrepareShopRewardChoices()
        {
            if (shopRewardCards.Count > 0 || pendingShopRewardOffer == null) return;
            int count = Mathf.Max(1, pendingShopRewardOffer.ChoiceCount);
            for (int i = 0; i < count; i++)
            {
                ShopReward reward = pendingShopRewardOffer.IsRelic
                    ? new ShopReward { Relic = DrawShopRelic(pendingShopRewardOffer.RarityTier) }
                    : new ShopReward { Card = DrawShopCombatCard(pendingShopRewardOffer.RarityTier) };
                if (reward.Card != null || reward.Relic != null) shopRewardCards.Add(reward);
            }
        }
        private void BuildShopRewardHand()
        {
            PrepareShopRewardChoices();
            for (int i = 0; i < shopRewardCards.Count; i++) AddShopRewardVisual(shopRewardCards[i], true);
        }
        private void AddShopRewardVisual(ShopReward reward, bool active)
        {
            string name = reward.Relic != null ? reward.Relic.GetLocalizedName(IsEnglishUi) : reward.Card.Type.GetLocalizedName(IsEnglishUi);
            string description = reward.Relic != null ? reward.Relic.GetLocalizedDescription(IsEnglishUi) : reward.Card.Type.GetLocalizedDescription(IsEnglishUi);
            ShopOffer displayOffer = new ShopOffer { RarityTier = pendingShopRewardOffer.RarityTier, Card = reward.Card, Relic = reward.Relic };
            AddOfferCard(name, description, -1, reward.Card != null ? reward.Card.Color : global::CardColor.White,
                reward.Card != null ? reward.Card.Number : 1, displayOffer);
            cards[cards.Count - 1].gameObject.SetActive(active);
        }
        private void UseShopRewardCard(int index)
        {
            if (index < 0 || index >= cards.Count || index >= shopRewardCards.Count) return;
            ShopReward reward = shopRewardCards[index];
            if (reward.Card != null) runCombatDeck.Add(reward.Card);
            else if (reward.Relic != null) { AddRelic(reward.Relic); combatRelicVisualHash = int.MinValue; }
            for (int i = 0; i < cards.Count; i++) if (cards[i] != null) Destroy(cards[i].gameObject);
            cards.Clear(); shopRewardCards.Clear();
            shopRewardOpeningActive = false; pendingShopRewardOffer = null;
            ClearCards(); BeginShopChoice();
        }
        private void ResetRunCombatDeckToStarter()
        {
            runCombatDeck.Clear();
            runCombatDeckInitialized = true;
            global::CombatDeckDefinition deck = Resources.Load<global::CombatDeckDefinition>("Combat/StarterDeck");
            if (deck == null)
            {
                Debug.LogError("Combat/StarterDeck asset could not be loaded.");
                return;
            }
            if (deck.Cards == null || deck.Cards.Count == 0)
            {
                Debug.LogError("Combat/StarterDeck has no card instances.");
                return;
            }
            for (int i = 0; i < deck.Cards.Count; i++)
                if (deck.Cards[i] != null) runCombatDeck.Add(deck.Cards[i]);
        }
        private void BuildStarterDeck()
        {
            CloseCombatDeckInspection();
            starterDrawPile.Clear();
            if (!runCombatDeckInitialized) ResetRunCombatDeckToStarter();
            List<global::CombatCard> shuffledCards = new List<global::CombatCard>(runCombatDeck);
            for (int i = 0; i < shuffledCards.Count; i++)
            {
                int swapIndex = UnityEngine.Random.Range(i, shuffledCards.Count);
                global::CombatCard temp = shuffledCards[i];
                shuffledCards[i] = shuffledCards[swapIndex];
                shuffledCards[swapIndex] = temp;
            }
            for (int i = 0; i < shuffledCards.Count; i++) starterDrawPile.Enqueue(shuffledCards[i]);
            for (int i = 0; i < StartingHandSize; i++) DrawStarterCardToHand();
        }        private bool DrawStarterCardToHand()
        {
            if (cards.Count >= MaximumCombatHandSize)
            {
                AddScorePopup(Ui("손이 가득 찼습니다", "Hand is full"),
                    new Color(1f, 0.7f, 0.24f), Time.unscaledTime, scorePopups.Count, 0);
                return false;
            }
            if (starterDrawPile.Count == 0) RecycleUsedPileIntoDrawPile();
            if (starterDrawPile.Count == 0) return false;
            global::CombatCard card = starterDrawPile.Dequeue();
            global::CombatCardType definition = card != null ? card.Type : null;
            if (definition == null)
            {
                Debug.LogWarning("Combat card type is missing.");
                return false;
            }
            AddStarterDeckCard(card);
            return true;
        }
        private void RecycleUsedPileIntoDrawPile()
        {
            if (usedPileStoredCards.Count == 0) return;
            List<global::CombatCard> recycledCards = new List<global::CombatCard>();
            for (int i = 0; i < usedPileStoredCards.Count; i++)
            {
                StoredCard stored = usedPileStoredCards[i];
                if (stored == null || stored.CombatType == null) continue;
                recycledCards.Add(new global::CombatCard
                {
                    Type = stored.CombatType,
                    Color = stored.Color,
                    Number = stored.Number
                });
            }
            for (int i = 0; i < recycledCards.Count; i++)
            {
                int swapIndex = UnityEngine.Random.Range(i, recycledCards.Count);
                global::CombatCard temp = recycledCards[i];
                recycledCards[i] = recycledCards[swapIndex];
                recycledCards[swapIndex] = temp;
            }
            for (int i = 0; i < recycledCards.Count; i++) starterDrawPile.Enqueue(recycledCards[i]);
            ClearUsedCardPile();
        }
        private void AddStarterDeckCard(global::CombatCard card)
        {
            global::CombatCardType definition = card.Type;
            global::CardData data = definition.CreateRuntimeCardData();
            CardVisual visual = CardVisual.CreatePrefabInstance("Card - " + data.Name, cardStack);
            string attributeKey = card.Color.ToString();
            Material attributeMaterial = GetTextureMaterial("Attribute_" + attributeKey,
                "CardAssets/Attributes/Attribute" + attributeKey, false);
            Material rarityPatternMaterial = GetTextureMaterial("Pattern_" + data.RarityAssetKey,
                "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0);
            Material costMaterial = GetTextureMaterial("Cost_" + card.Number,
                "CardAssets/Costs/Cost" + card.Number, true, 20);
            Texture2D illustration = definition.Image;
            Material illustrationMaterial = GetTextureMaterial("CardImage_" + data.GetHashCode() + definition.name, illustration, true, 10);
            visual.BuildFromData(data, card.Color, attributeMaterial,
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                rarityPatternMaterial, illustrationMaterial, costMaterial, font, IsEnglishUi);
            StoredCard stored = new StoredCard
            {
                Name = definition.CardName,
                Data = data, CombatType = definition, Rarity = data.Rare,
                Color = card.Color, Number = card.Number, IsHolographic = false
            };
            currentPackCards.Add(stored);
            visual.SetDisplayName(IsEnglishUi && !string.IsNullOrEmpty(definition.EnglishName)
                ? definition.EnglishName : GetStoredCardDisplayName(stored));
            visual.SetDisplayDescription(data, GetHandCardDisplayDescription(stored), IsEnglishUi, string.Empty);
            visual.PrepareFaceUp(CardHome, 1.08f, 0f);
            visual.gameObject.SetActive(false);
            cards.Add(visual);
        }
        private void BuildShopRewardPackPlaceholder()
        {
            PrepareShopRewardChoices();
            if (shopRewardCards.Count > 0) AddShopRewardVisual(shopRewardCards[0], false);
        }
        private void BuildHiddenCardStack(int requestedCardCount = -1)
        {
            int baseCardCount = requestedCardCount > 0 ? requestedCardCount : activePackData != null ? activePackData.CardsPerPack : FallbackCardsPerPack;
            int cardCount = baseCardCount + GetAdditionalNextPackCardCount();
            bool replaceWithMinerals = ShouldReplaceCurrentPackWithMinedMinerals();
            for (int i = 0; i < cardCount; i++)
            {
                global::CardPackEntry entry = replaceWithMinerals
                    ? DrawMinedMineralCard()
                    : i == 4 && activePackData != null
                        ? activePackData.DrawRandomCardAtLeast(global::CardRarity.Rare)
                        : null;
                if (entry == null && !replaceWithMinerals) entry = DrawCard();
                if (entry == null || entry.Card == null) continue;
                global::CardData data = entry.Card;
                CardVisual visual = CardVisual.CreatePrefabInstance("Card - " + data.Name, cardStack);
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
                visual.PrepareFaceUp(CardHome + new Vector3(0f, i * 0.025f, i * 0.065f), CurrentRevealedCardScale,
                    (i - (cardCount - 1) * 0.5f) * 0.7f);
                visual.gameObject.SetActive(false);
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
            }
        }
        private global::CardPackEntry DrawCard()
        {
            if (activePackData != null)
            {
                global::CardPackEntry includedCard = activePackData.DrawRandomCard();
                if (includedCard != null) return includedCard;
            }
            if (fallbackCards == null || fallbackCards.Length == 0)
                fallbackCards = Resources.LoadAll<global::CardData>(string.Empty);
            if (fallbackCards != null && fallbackCards.Length > 0)
            {
                return new global::CardPackEntry
                {
                    Card = fallbackCards[Random.Range(0, fallbackCards.Length)],
                    Number = 1,
                    Color = global::CardColor.Green,
                    InclusionRate = 100f
                };
            }
            if (runtimeFallbackCard == null)
            {
                runtimeFallbackCard = ScriptableObject.CreateInstance<global::CardData>();
                runtimeFallbackCard.Name = "마법 총알";
                runtimeFallbackCard.Description = "7\uC758 \uD53C\uD574\uB97C \uC90D\uB2C8\uB2E4.";
                runtimeFallbackCard.EnglishName = "Magic Bullet";
                runtimeFallbackCard.EnglishDescription = "Deals 7 damage.";
                runtimeFallbackCard.Rare = global::CardRarity.Common;
                runtimeFallbackEntry = new global::CardPackEntry
                {
                    Card = runtimeFallbackCard,
                    Number = 5,
                    Color = global::CardColor.Green,
                    InclusionRate = 100f
                };
            }
            return runtimeFallbackEntry;
        }
        private void BeginPackChoice()
        {
            ClearCards();
            ClearPackChoiceVisuals();
            currentPackOpenedForGoal = false;
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(false);
            leftPackChoice = LoadCardPackData();
            rightPackChoice = DrawAlternativePack(leftPackChoice);
            if (leftPackChoice == null)
            {
                activePackData = Resources.Load<global::CardPackData>("CardPacks/TaleTail");
                if (pack != null) pack.gameObject.SetActive(true);
                if (cardStack != null) cardStack.gameObject.SetActive(true);
                BeginSequence(false);
                return;
            }
            if (rightPackChoice == null)
            {
                SelectPackChoice(leftPackChoice);
                return;
            }
            CreatePackChoiceVisuals();
            phase = RevealPhase.PackChoice;
        }
        private global::CardPackData DrawAlternativePack(global::CardPackData excludedPack)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                global::CardPackData candidate = LoadCardPackData();
                if (candidate != null && candidate != excludedPack) return candidate;
            }
            if (randomPackPool == null)
                randomPackPool = Resources.LoadAll<global::CardPackData>("CardPacks");
            for (int i = 0; i < randomPackPool.Length; i++)
                if (randomPackPool[i] != null && randomPackPool[i] != excludedPack)
                    return randomPackPool[i];
            return null;
        }
        private void SelectPackChoice(global::CardPackData selectedPack)
        {
            if (selectedPack == null) return;
            ClearPackChoiceVisuals();
            activePackData = selectedPack;
            leftPackChoice = null;
            rightPackChoice = null;
            if (pack != null) pack.gameObject.SetActive(true);
            if (cardStack != null) cardStack.gameObject.SetActive(true);
            BeginSequence(false);
        }
        private void CreatePackChoiceVisuals()
        {
            float choiceX = IsPortraitUi ? 1.05f : 1.8f;
            float choiceY = IsPortraitUi ? 0.42f : 0.55f;
            leftPackChoiceVisual = CreatePackChoiceVisual(
                "Left Pack Choice", leftPackChoice, new Vector3(-choiceX, choiceY, -0.65f));
            rightPackChoiceVisual = CreatePackChoiceVisual(
                "Right Pack Choice", rightPackChoice, new Vector3(choiceX, choiceY, -0.65f));
        }
        private PackVisual CreatePackChoiceVisual(
            string objectName, global::CardPackData data, Vector3 position)
        {
            if (data == null) return null;
            Texture2D frontTexture = data.FrontImage != null
                ? data.FrontImage
                : Resources.Load<Texture2D>("Textures/CardPackFrontStoryTailBlueSky");
            Texture2D backTexture = data.BackImage != null
                ? data.BackImage
                : Resources.Load<Texture2D>("Textures/CardPackBackStoryTail");
            Material frontMaterial = CreateTextureMaterial(objectName + " Front", frontTexture, false, 0);
            Material backMaterial = CreateTextureMaterial(objectName + " Back", backTexture, false, 0);
            packChoiceMaterials.Add(frontMaterial);
            packChoiceMaterials.Add(backMaterial);
            GameObject choiceObject = new GameObject(objectName);
            choiceObject.transform.position = position;
            choiceObject.transform.localScale = Vector3.one * ResponsiveWorldScale(1.45f, 1.18f);
            PackVisual choiceVisual = choiceObject.AddComponent<PackVisual>();
            choiceVisual.Build(
                GetMaterial("Pack", new Color(0.18f, 0.07f, 0.32f), 0.18f),
                frontMaterial,
                backMaterial);
            return choiceVisual;
        }
        private void ClearPackChoiceVisuals()
        {
            if (leftPackChoiceVisual != null)
            {
                leftPackChoiceVisual.gameObject.SetActive(false);
                Destroy(leftPackChoiceVisual.gameObject);
                leftPackChoiceVisual = null;
            }
            if (rightPackChoiceVisual != null)
            {
                rightPackChoiceVisual.gameObject.SetActive(false);
                Destroy(rightPackChoiceVisual.gameObject);
                rightPackChoiceVisual = null;
            }
            for (int i = 0; i < packChoiceMaterials.Count; i++)
            {
                if (packChoiceMaterials[i] != null) Destroy(packChoiceMaterials[i]);
            }
            packChoiceMaterials.Clear();
            ClearPackContentsPreview();
            inspectedPackChoice = null;
            packContentsScroll = Vector2.zero;
        }
        private global::CardPackData LoadCardPackData()
        {
            if (packPoolData == null)
                packPoolData = Resources.Load<global::CardPackPoolData>("CardPacks/CardPackPool");
            if (packPoolData != null)
                return packPoolData.DrawRandomPack();
            if (randomPackPool == null)
                randomPackPool = Resources.LoadAll<global::CardPackData>("CardPacks");
            if (randomPackPool.Length > 0)
                return randomPackPool[Random.Range(0, randomPackPool.Length)];
            return null;
        }
        private void RefreshActivePackArtwork()
        {
            if (materials.TryGetValue("PackFrontArtwork", out Material front))
                ApplyTextureOrFallback(front, activePackData != null ? activePackData.FrontImage : null,
                    Resources.Load<Texture2D>("Textures/CardPackFrontStoryTailBlueSky"));
            if (materials.TryGetValue("PackBackArtwork", out Material back))
                ApplyTextureOrFallback(back, activePackData != null ? activePackData.BackImage : null,
                    Resources.Load<Texture2D>("Textures/CardPackBackStoryTail"));
        }
        public void SetCardPackData(global::CardPackData data)
        {
            // Legacy pack selection is no longer part of a run.
        }
        private IEnumerator RemovePack(Vector2 direction)
        {
            phase = RevealPhase.Animating;
            packTearInProgress = true;
            PlayPackTearSound();
            currentPackOpenedForGoal = true;
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].transform.localScale = Vector3.one * CurrentRevealedCardScale;
                cards[i].SetFaceDetailsVisible(true);
                cards[i].gameObject.SetActive(true);
            }
            cards[0].PrepareFaceUp(CardHome, CurrentRevealedCardScale, 0f);
            Vector3 tearCardOffset = IsPortraitUi ? Vector3.zero : PackedCardOffset;
            yield return tearVisual.PeelInDirection(direction, cardStack, CardHome, tearCardOffset);
            packTearInProgress = false;
            pack.gameObject.SetActive(false);
            if (shopRewardOpeningActive)
            {
                currentPackOpenedForGoal = false;
                yield return ReturnCardStackToFront();
                BeginStartingHand();
                yield break;
            }
            TriggerPackOpenedDeckAbilities();
            yield return ReturnCardStackToFront();
            PlayCardRarityRevealSound(currentPackCards[cardIndex].Rarity);
            AwardCurrentCardScore();
            phase = RevealPhase.CardFront;
        }
        private IEnumerator ReturnCardStackToFront()
        {
            Vector3 startPosition = cardStack.position;
            Quaternion startRotation = cardStack.rotation;
            const float duration = 0.22f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / duration);
                cardStack.position = Vector3.Lerp(startPosition, Vector3.zero, u);
                cardStack.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, u);
                yield return null;
            }
            cardStack.position = Vector3.zero;
            cardStack.rotation = Quaternion.identity;
        }
        private IEnumerator FlipCard()
        {
            phase = RevealPhase.Animating;
            yield return cards[cardIndex].RevealInPlace();
            PlayCardRarityRevealSound(currentPackCards[cardIndex].Rarity);
            phase = RevealPhase.CardFront;
        }
        private IEnumerator MoveToNextCard(float direction)
        {
            phase = RevealPhase.Animating;
            cardTransitionActive = true;
            queuedCardSwipes = 0;
            float currentDirection = direction;
            while (true)
            {
                CommitPendingScoreImmediately();
                CardVisual current = cards[cardIndex];
                if (cardIndex + 1 < cards.Count)
                {
                    CardVisual next = cards[cardIndex + 1];
                    next.gameObject.SetActive(true);
                    next.PrepareFaceUp(CardHome + new Vector3(0f, 0.035f, 0.035f), CurrentRevealedCardScale, 0f);
                    next.SetFaceDetailsVisible(true);
                }
                activeSlidingCard = current;
                yield return current.SlideAway(currentDirection);
                activeSlidingCard = null;
                if (cardIndex >= 0 && cardIndex < currentPackCards.Count)
                    StoreCurrentCardInDeck(currentPackCards[cardIndex]);
                current.gameObject.SetActive(false);
                cardIndex++;
                if (cardIndex >= cards.Count)
                {
                    cardTransitionActive = false;
                    queuedCardSwipes = 0;
                    yield return new WaitForSeconds(0.35f);
                    CompletePackAndBeginNextSequence();
                    yield break;
                }
                yield return cards[cardIndex].MoveToFront(CardHome, CurrentRevealedCardScale, 0f);
                yield return RestoreCardStackRotation();
                PlayCardRarityRevealSound(currentPackCards[cardIndex].Rarity);
                AwardCurrentCardScore();
                if (queuedCardSwipes <= 0) break;
                queuedCardSwipes--;
                currentDirection = queuedSwipeDirection;
            }
            cardTransitionActive = false;
            phase = RevealPhase.CardFront;
        }
        private IEnumerator RestoreCardStackRotation()
        {
            Quaternion startRotation = cardStack.rotation;
            if (Quaternion.Angle(startRotation, Quaternion.identity) < 0.05f)
            {
                cardStack.rotation = Quaternion.identity;
                yield break;
            }
            const float duration = 0.16f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                cardStack.rotation = Quaternion.Slerp(startRotation, Quaternion.identity, u);
                yield return null;
            }
            cardStack.rotation = Quaternion.identity;
        }
        private static bool IsPortraitUi
        {
            get
            {
                Rect safeArea = Screen.safeArea;
                return safeArea.height > safeArea.width;
            }
        }
        private static float UiReferenceWidth { get { return IsPortraitUi ? PortraitWidth : ReferenceWidth; } }
        private static float PortraitExtraHeight
        {
            get
            {
                if (!IsPortraitUi) return 0f;
                Rect safeArea = Screen.safeArea;
                if (safeArea.width <= 0f) return 0f;
                float widthScale = safeArea.width / PortraitWidth;
                if (widthScale <= 0f) return 0f;
                return Mathf.Max(0f, safeArea.height / widthScale - PortraitHeight);
            }
        }
        private static float UiReferenceHeight { get { return IsPortraitUi ? PortraitHeight + PortraitExtraHeight : ReferenceHeight; } }
        private static float PortraitWorldScaleFactor
        {
            get
            {
                if (!IsPortraitUi) return 1f;
                GetUiLayout(out float uiScale, out _, out _);
                float screenHeightScale = Screen.height > 0 ? Screen.height / ReferenceHeight : 1f;
                return screenHeightScale > 0f ? uiScale / screenHeightScale : 1f;
            }
        }
        private static float UiWorldScaleFactor
        {
            get
            {
                GetUiLayout(out float uiScale, out _, out _);
                float screenHeightScale = Screen.height > 0 ? Screen.height / ReferenceHeight : 1f;
                return screenHeightScale > 0f ? uiScale / screenHeightScale : 1f;
            }
        }
        private static float ResponsiveWorldScale(float portraitScale, float landscapeScale)
        {
            return (IsPortraitUi ? portraitScale : landscapeScale) * UiWorldScaleFactor;
        }
        private static float CurrentRevealedCardScale { get { return ResponsiveWorldScale(2.10f, RevealedCardScale); } }
        private static float CurrentHandCardScale { get { return 1.00f * UiWorldScaleFactor; } }
        private static Rect UiRect(Rect landscape, Rect portrait)
        {
            return IsPortraitUi ? portrait : landscape;
        }
        private static void GetUiLayout(out float scale, out float offsetX, out float offsetY)
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
                safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
            scale = Mathf.Min(safeArea.width / UiReferenceWidth, safeArea.height / UiReferenceHeight);
            offsetX = safeArea.xMin + (safeArea.width - UiReferenceWidth * scale) * 0.5f;
            float safeTop = Screen.height - safeArea.yMax;
            offsetY = safeTop + (safeArea.height - UiReferenceHeight * scale) * 0.5f;
        }
        private static Vector3 CurrentPackHome
        {
            get
            {
                if (!IsPortraitUi) return PackHome;
                Camera camera = Camera.main;
                if (camera == null) return PackHome;
                Vector3 cardScreenPosition = camera.WorldToScreenPoint(CardHome);
                float packDepth = camera.WorldToScreenPoint(PackHome).z;
                return camera.ScreenToWorldPoint(new Vector3(cardScreenPosition.x, cardScreenPosition.y, packDepth));
            }
        }
    }
}
