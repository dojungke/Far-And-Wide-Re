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
using UnityEngine.Rendering.Universal;
using TMPro;
using Random = UnityEngine.Random;
namespace CardOpen.Prototype
{
    public sealed partial class PackOnlyPrototype
    {
        private void SetupScene()
        {
            font = Resources.Load<Font>("Fonts/CardFont");
            if (font == null)
                font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial Unicode MS", "Arial" }, 64);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            PrewarmCardTextCharacters();
            EnsureRuntimeUiCanvas();
            SetupScorePopupAudio();
            SetupAbilityEffectAudio();
            SetupCardRarityAudio();
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.transform.position = new Vector3(0f, 0.95f, -10.8f);
            camera.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            camera.fieldOfView = 43f;
            camera.backgroundColor = new Color(0.018f, 0.022f, 0.038f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
            }
            CreateBackground(camera);
            GameObject stackObject = new GameObject("Card Stack");
            cardStack = stackObject.transform;
            stackObject.AddComponent<CardStackVisual>();
            GameObject deckObject = new GameObject("Stored Card Deck");
            deckRoot = deckObject.transform;
            CreateEmptyDeckPlaceholder();
            CreateDeckInspectionBackdrop();
            CreateUsedCardPile();
            // Legacy pack-opening visuals are intentionally not created. The game starts from combat/stage cards.

            CreateEnemyVisual();
        }
        private void EnsureShopRewardPackVisual()
        {
            if (pack != null && tearVisual != null) return;
            GameObject packObject = new GameObject("Shop Reward Pack");
            packObject.transform.position = CurrentPackHome;
            pack = packObject.AddComponent<PackVisual>();
            Material front = GetTextureMaterial("PackFrontArtwork", activePackData != null ? activePackData.FrontImage : null, true, 0);
            Material back = GetTextureMaterial("PackBackArtwork", activePackData != null ? activePackData.BackImage : null, true, 0);
            pack.Build(GetMaterial("Pack", new Color(0.18f, 0.07f, 0.32f), 0.18f), front, back);
            tearVisual = packObject.AddComponent<PackTearVisual>();
            tearVisual.Initialize(null);
        }
        private void CreateEnemyVisual()
        {
            RefreshEnemyVisual();
        }
        private global::EnemyDefinition LoadDefaultEnemyDefinition()
        {
            if (defaultEnemyDefinition == null)
                defaultEnemyDefinition = Resources.Load<global::EnemyDefinition>("Combat/Enemies/Wolf");
            return defaultEnemyDefinition;
        }
        private global::StageEnemyList LoadRandomNormalBattleEnemies()
        {
            if (normalBattleEncounters == null)
                normalBattleEncounters = Resources.Load<global::BattleEncounters>("Combat/BattleEncounters/일반전투");
            global::BattleEncounters encounters = selectedStageEncounters != null ? selectedStageEncounters : normalBattleEncounters;
            currentStageEnemies = encounters != null ? encounters.PickRandomStage() : null;
            return currentStageEnemies;
        }
        private EnemyState CreateEnemyState(global::EnemyDefinition definition)
        {
            if (definition == null) return null;
            EnemyState state = new EnemyState
            {
                Definition = definition,
                Health = Mathf.Max(1, definition.MaximumHealth),
                Shield = 0,
                Burn = 0,
                Regeneration = 0,
                Stun = 0,
                Scales = 0,
                ActionTurnsRemaining = Mathf.Max(1, definition.ActionInterval)
            };
            ApplyStartingEnemyBuffs(state);
            return state;
        }
        private void ApplyStartingEnemyBuffs(EnemyState enemy)
        {
            if (enemy == null || enemy.Definition == null || enemy.Definition.StartingBuffs == null) return;
            for (int i = 0; i < enemy.Definition.StartingBuffs.Count; i++)
            {
                global::EnemyStartingBuff startingBuff = enemy.Definition.StartingBuffs[i];
                if (startingBuff == null || startingBuff.Buff == null || startingBuff.Amount <= 0) continue;
                ApplyEnemyBuff(enemy, startingBuff.Buff, startingBuff.Amount);
            }
        }

        private void ApplyEnemyBuff(EnemyState enemy, global::CombatBuffDefinition buff, int amount)
        {
            if (enemy == null || buff == null || amount <= 0) return;
            if (buff == GetCombatBuffDefinition("Shield")) enemy.Shield += amount;
            else if (buff == GetCombatBuffDefinition("Burn")) enemy.Burn += amount;
            else if (buff == GetCombatBuffDefinition("Wood")) enemy.Wood += amount;
            else if (buff == GetCombatBuffDefinition("Regeneration")) enemy.Regeneration += amount;
            else if (buff == GetCombatBuffDefinition("Stun")) enemy.Stun += amount;
            else if (buff == GetCombatBuffDefinition("Cleverness")) enemy.Cleverness += amount;
            else if (buff == GetCombatBuffDefinition("Scales")) enemy.Scales += amount;
            else if (buff == GetCombatBuffDefinition("Bleeding"))
                enemy.BleedingDurations.Add(amount);
            else Debug.LogWarning("Unsupported starting enemy buff: " + buff.name);
        }
        private EnemyVisual CreateSingleEnemyVisual(int index)
        {
            GameObject enemyObject = new GameObject("Current Enemy " + (index + 1));
            enemyObject.transform.position = new Vector3(0f, 2.28f, 0.65f);
            enemyObject.transform.localScale = Vector3.one * 0.9f;
            EnemyVisual visual = enemyObject.AddComponent<EnemyVisual>();
            global::EnemyDefinition definition = index >= 0 && index < enemies.Count ? enemies[index].Definition : null;
            Color fallbackColor = definition != null ? definition.FallbackColor : Color.gray;
            visual.Build(definition != null ? definition.Image : null, GetMaterial("EnemyBody", fallbackColor, 0.28f));
            return visual;
        }
        private void CreateEnemyStates()
        {
            StopEnemyVisualEffects();
            enemies.Clear();
            global::StageEnemyList stageEnemies = LoadRandomNormalBattleEnemies();
            if (stageEnemies == null || stageEnemies.Enemies == null || stageEnemies.Enemies.Count == 0)
            {
                Debug.LogError("Combat/BattleEncounters/일반전투 asset is missing or has no non-empty enemy list.");
            }
            else
            {
                for (int i = 0; i < stageEnemies.Enemies.Count && enemies.Count < MaxSimultaneousEnemies; i++)
                {
                    EnemyState state = CreateEnemyState(stageEnemies.Enemies[i]);
                    if (state != null) enemies.Add(state);
                }
            }

            ClearPlayerCombatBuffs();
        }
        private void BeginGoldRewardChoice()
        {
            shopRewardOpeningActive = false;
            pendingShopRewardOffer = null;
            shopRewardCards.Clear();
            int minimumGold = 50;
            int maximumGoldExclusive = 151;
            if (lastUsedStageCard != null && lastUsedStageCard.Kind == global::StageCardKind.EliteBattle)
            {
                minimumGold = 200;
                maximumGoldExclusive = 401;
            }
            else if (lastUsedStageCard != null && lastUsedStageCard.Kind == global::StageCardKind.BossBattle)
            {
                minimumGold = 550;
                maximumGoldExclusive = 651;
            }
            pendingOfferGold = UnityEngine.Random.Range(minimumGold, maximumGoldExclusive);
            rewardChoiceActive = true;
            shopChoiceActive = false;
            shopOfferDrawPending = true;
            BeginOfferHand(Ui("전투 보상", "Combat Reward"),
                Ui(pendingOfferGold + " 골드를 받습니다.", "Receive " + pendingOfferGold + " gold."), 1);
        }

        private void BeginShopChoice()
        {
            shopRewardOpeningActive = false;
            pendingShopRewardOffer = null;
            shopRewardCards.Clear();
            if (shopOfferDrawPending)
            {
                while (shopOffers.Count < 4) shopOffers.Add(CreateShopOffer());
                shopOfferDrawPending = false;
            }
            rewardChoiceActive = false;
            shopChoiceActive = true;
            shopDeckRemovalSelectionActive = false;
            BeginOfferHand(string.Empty, string.Empty, 0);
            if (usedPilePlaceholder != null && usedPilePlaceholderData != null)
            {
                string discardDescription = Ui("여기에 마음이 안 드는 상품을 드래그해 버릴 수 있습니다.\n빈자리는 다음번 상점 진입 시 다른 상품으로 채워집니다.", "Drag unwanted products here to discard them.\nEmpty slots are replaced on your next shop visit.");
                usedPilePlaceholderData.Description = discardDescription;
                usedPilePlaceholder.SetDisplayDescription(usedPilePlaceholderData, discardDescription, IsEnglishUi, string.Empty);
            }
            AddOfferCard(Ui("덱 잎 제거", "Remove Deck Card"), Ui(shopDeckRemovalPrice + " 골드\n덱 잎 1장을 제거합니다.", shopDeckRemovalPrice + " gold\nRemove one deck card."), -1, global::CardColor.Black, 1);
            for (int i = 0; i < shopOffers.Count; i++)
            {
                ShopOffer offer = shopOffers[i];
                AddOfferCard(GetShopOfferTitle(offer), GetShopOfferDescription(offer), offer.RemainingSalesPeriods, offer.Card != null ? offer.Card.Color : global::CardColor.White, offer.Number, offer);
            }
            LayoutStartingHand(); RefreshHandCardInteractionStates();
        }
        private ShopOffer CreateShopOffer()
        {
            int priceRarityTier = RollShopRarityTier();
            int rarityTier = UnityEngine.Random.value < 0.1f ? Mathf.Min(2, priceRarityTier + 1) : priceRarityTier;
            int choiceCount = UnityEngine.Random.Range(1, 6);
            global::CombatRelicDefinition relic = UnityEngine.Random.value < 0.5f ? DrawShopRelic(rarityTier) : null;
            global::CombatCard card = relic == null ? DrawShopCombatCard(rarityTier) : null;
            if (card == null && relic == null) relic = DrawShopRelic(rarityTier);
            ShopOffer offer = new ShopOffer { RarityTier = rarityTier, ChoiceCount = choiceCount, RemainingSalesPeriods = UnityEngine.Random.Range(1, 6), Card = card, Relic = relic, Number = card != null ? card.Number : 1 };
            offer.Price = offer.IsRelic ? (priceRarityTier + 1) * 100 + UnityEngine.Random.Range(choiceCount, choiceCount + 5) * 10 : ((int)Mathf.Pow(priceRarityTier + 1, UnityEngine.Random.Range(2, 4)) + UnityEngine.Random.Range(1, 8 + choiceCount)) * 10;
            return offer;
        }
        private static int RollShopRarityTier()
        {
            int roll = UnityEngine.Random.Range(1, 101);
            if (roll <= 60) return 0;
            if (roll <= 85) return 1;
            if (roll <= 95) return 2;
            return 2;
        }
        private global::CombatCard DrawShopCombatCard(int rarityTier)
        {
            global::CombatCardType[] allTypes = Resources.LoadAll<global::CombatCardType>("Combat/CardTypes");
            List<global::CombatCardType> candidates = new List<global::CombatCardType>();
            for (int i = 0; i < allTypes.Length; i++) if (allTypes[i] != null && allTypes[i].CanAppearInShopRewardPack) candidates.Add(allTypes[i]);
            global::CombatCardType type = DrawShopItemByRarity(candidates, rarityTier, item => item.Rarity);
            return CreateRandomShopLeaf(type);
        }
        private static global::CombatCard CreateRandomShopLeaf(global::CombatCardType type)
        {
            if (type == null) return null;
            return new global::CombatCard
            {
                Type = type,
                Color = (global::CardColor)UnityEngine.Random.Range(0, 5),
                Number = UnityEngine.Random.Range(1, 7)
            };
        }
        private global::CombatRelicDefinition DrawShopRelic(int rarityTier)
        {
            global::CombatRelicDefinition[] allRelics = Resources.LoadAll<global::CombatRelicDefinition>("Combat/Relics");
            List<global::CombatRelicDefinition> candidates = new List<global::CombatRelicDefinition>();
            for (int i = 0; i < allRelics.Length; i++) if (allRelics[i] != null && allRelics[i].CanAppearInShopRewardPack && allRelics[i] != GetGoldCurrencyDefinition() && !shopOffers.Exists(offer => offer.Relic == allRelics[i])) candidates.Add(allRelics[i]);
            return DrawShopItemByRarity(candidates, rarityTier, item => item.Rarity);
        }
        private static T DrawShopItemByRarity<T>(IList<T> items, int tier, Func<T, global::CardRarity> rarityOf) where T : class
        {
            List<T> exact = new List<T>();
            for (int i = 0; items != null && i < items.Count; i++)
            {
                T item = items[i];
                if (item != null && Mathf.Clamp((int)rarityOf(item), 0, 2) == tier) exact.Add(item);
            }
            return exact.Count == 0 ? null : exact[UnityEngine.Random.Range(0, exact.Count)];
        }
        private string GetShopOfferTitle(ShopOffer offer)
        {
            string rarity = GetShopRarityName(offer != null ? offer.RarityTier : 0);
            return offer != null && offer.IsRelic ? Ui("가지 구매 · " + rarity, "Branch Purchase · " + rarity) : Ui("잎 구매 · " + rarity, "Leaf Purchase · " + rarity);
        }
        private string GetShopOfferDescription(ShopOffer offer)
        {
            if (offer == null) return string.Empty;
            string rarity = GetShopRarityName(offer.RarityTier);
            int count = Mathf.Max(1, offer.ChoiceCount);
            return offer.IsRelic
                ? Ui(rarity + " 등급 가지 " + count + "개 중 1개를 획득합니다.\n가격: " + offer.Price + " 골드", rarity + " relics: choose 1 of " + count + ".\nPrice: " + offer.Price + " gold")
                : Ui(rarity + " 등급 잎 " + count + "장 중 1장을 획득합니다.\n가격: " + offer.Price + " 골드", rarity + " cards: choose 1 of " + count + ".\nPrice: " + offer.Price + " gold");
        }
        private string GetShopRarityName(int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2); return IsEnglishUi ? (tier == 0 ? "Common" : tier == 1 ? "Uncommon" : "Rare") : (tier == 0 ? "일반" : tier == 1 ? "고급" : "희귀");
        }
        private int GetShopCardPrice(int cardIndex)
        {
            if (cardIndex == 0) return shopDeckRemovalPrice;
            int offerIndex = cardIndex - 1;
            return offerIndex >= 0 && offerIndex < shopOffers.Count ? shopOffers[offerIndex].Price : int.MaxValue;
        }

        private bool CanPurchaseShopCard(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= cards.Count) return false;
            if (cardIndex == 0 && runCombatDeck.Count == 0) return false;
            return gold >= GetShopCardPrice(cardIndex);
        }

        private void RemovePurchasedShopCardVisual(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= cards.Count) return;
            CardVisual purchasedVisual = cards[cardIndex];
            cards.RemoveAt(cardIndex);
            if (purchasedVisual != null) Destroy(purchasedVisual.gameObject);
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
        }
        private void DiscardShopOfferCard(int cardIndex)
        {
            if (cardIndex <= 0 || cardIndex >= cards.Count) { RestoreStartingHandCard(cardIndex); return; }
            int offerIndex = cardIndex - 1;
            if (offerIndex >= 0 && offerIndex < shopOffers.Count) shopOffers.RemoveAt(offerIndex);
            RemovePurchasedShopCardVisual(cardIndex);
        }
        private void AdvanceShopOfferPeriods()
        {
            for (int i = shopOffers.Count - 1; i >= 0; i--)
            {
                shopOffers[i].RemainingSalesPeriods--;
                if (shopOffers[i].RemainingSalesPeriods <= 0)
                    shopOffers.RemoveAt(i);
            }
        }

        private void BeginOfferHand(string title, string description, int count)
        {
            ClearCards();
            startingHandVisible = true;
            highlightedHandCard = null;
            pressedHandIndex = -1;
            draggedHandIndex = -1;
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(true);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(true);
            for (int i = 0; i < count; i++) AddOfferCard(title, description);
            LayoutUsedCardPile();
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            phase = RevealPhase.CardFront;
        }

        private void AddOfferCard(string title, string description, int remainingSalesPeriods = -1, global::CardColor color = global::CardColor.Green, int number = 5, ShopOffer shopOffer = null)
        {
            global::CardData data = ScriptableObject.CreateInstance<global::CardData>();
            data.Name = title; data.Description = description;
            data.Rare = shopOffer == null ? global::CardRarity.Common : (global::CardRarity)Mathf.Clamp(shopOffer.RarityTier, 0, 2);
            if (shopOffer != null) data.Image = shopOffer.Card != null && shopOffer.Card.Type != null ? shopOffer.Card.Type.Image : shopOffer.Relic != null ? shopOffer.Relic.Image : null;
            CardVisual visual = CardVisual.CreatePrefabInstance("Offer Card - " + title, cardStack);
            Texture2D illustration = data.Image != null ? data.Image : Resources.Load<Texture2D>("CardAssets/Content/Mana");
            string attributeKey = color.ToString();
            visual.BuildFromData(data, color, GetTextureMaterial("OfferAttribute_" + attributeKey, "CardAssets/Attributes/Attribute" + attributeKey, false), GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false), GetTextureMaterial("OfferPattern_" + data.RarityAssetKey, "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0), GetTextureMaterial("OfferImage_" + data.GetHashCode(), illustration, true, 10), GetTextureMaterial(remainingSalesPeriods >= 0 ? "OfferClock" : "OfferCost_" + number, remainingSalesPeriods >= 0 ? "CardAssets/Costs/CostClock" : "CardAssets/Costs/Cost" + number, true, 20), font, IsEnglishUi);
            if (remainingSalesPeriods >= 0) { visual.SetCostBadge(GetTextureMaterial("OfferClockBadge", "CardAssets/Content/clock", true, 20)); visual.SetCostBadgeText(remainingSalesPeriods.ToString(), font); SetShopOfferCostBadgePosition(visual); }
            visual.SetDisplayName(title); visual.SetDisplayDescription(data, description, IsEnglishUi, string.Empty); visual.PrepareFaceUp(CardHome, 1.08f, 0f); visual.gameObject.SetActive(true); cards.Add(visual);
        }
        private static void SetShopOfferCostBadgePosition(CardVisual visual)
        {
            if (visual == null) return;
            Transform badge = visual.transform.Find("Cost Badge");
            if (badge != null) badge.localPosition = new Vector3(-0.59f, 1.32f, -0.0095f);
            Transform badgeText = visual.transform.Find("Cost Badge Text");
            if (badgeText != null) badgeText.localPosition = new Vector3(-0.59f, 1.35f, -0.011f);
        }
        private void CompleteShopDeckRemoval(int deckIndex)
        {
            if (!shopDeckRemovalSelectionActive) return;
            int price = shopDeckRemovalPrice;
            if (gold < price || deckIndex < 0 || deckIndex >= runCombatDeck.Count) return;

            runCombatDeck.RemoveAt(deckIndex);
            gold -= price;
            shopDeckRemovalPrice += 50;
            RemovePurchasedShopCardVisual(0);
            shopDeckRemovalSelectionActive = false;
            AddScorePopup(Ui(price + " 골드 사용\n덱 잎 제거", "Spent " + price + " gold\nRemoved a deck card"),
                new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            CloseCombatDeckInspection();
        }        private void ResolveEventChoice(int index)
        {
            eventChoiceActive = false;
            if (activeEventId == 1 && index == 0)
            {
                eventRewardOpeningActive = true;
                pendingShopRewardOffer = new ShopOffer
                { RarityTier = 1, ChoiceCount = 1, Card = new global::CombatCard() };
                shopRewardOpeningActive = true;
                shopChoiceActive = false;
                activePackData = Resources.Load<global::CardPackData>("CardPacks/FarAndWide");
                PrepareShopRewardChoices();
                EnsureShopRewardPackVisual();
                BeginSequence(false);
                return;
            }
            else if (activeEventId == 2 && index == 0)
            {
                string givenCardName = "잎";
                global::CardColor givenColor = global::CardColor.White;
                int givenNumber = 1;
                if (runCombatDeck.Count > 0)
                {
                    int removeIndex = UnityEngine.Random.Range(0, runCombatDeck.Count);
                    global::CombatCard givenCard = runCombatDeck[removeIndex];
                    if (givenCard != null)
                    {
                        givenCardName = givenCard.Type != null ? givenCard.Type.GetLocalizedName(IsEnglishUi) : "잎";
                        givenColor = givenCard.Color; givenNumber = Mathf.Clamp(givenCard.Number, 1, 6);
                    }
                    runCombatDeck.RemoveAt(removeIndex);
                }
                pendingOfferGold = 150; rewardChoiceActive = true; shopChoiceActive = false;
                string colorName = givenColor == global::CardColor.Black ? Ui("검정", "Black") : givenColor == global::CardColor.White ? Ui("흰색", "White") : Ui("초록", "Green");
                string title = Ui(colorName + " " + givenNumber + "의 " + givenCardName + "을(를) 주고 별빛을 받았다.", "Gave " + colorName + " " + givenNumber + " " + givenCardName + " and received starlight.");
                pendingRewardContextTitle = title; pendingRewardContextMessage = Ui("위로 드래그해 사용하세요.", "Drag upward to claim it.");
                BeginOfferHand(Ui("별빛 보상", "Starlight Reward"), Ui("별빛 150을 획득합니다.", "Gain 150 starlight."), 1);
                return;
            }
            ClearCards(); BeginStageSelection();
        }

        private void ResolveOffer(bool accepted, int selectedShopCardIndex = -1)
        {
            if (eventChoiceActive) { if (accepted) ResolveEventChoice(selectedShopCardIndex); else { eventChoiceActive = false; ClearCards(); BeginStageSelection(); } return; }
            if (rewardChoiceActive)
            {
                if (accepted)
                {
                    gold += pendingOfferGold;
                    AddScorePopup(Ui(pendingOfferGold + " 골드 획득", "Gained " + pendingOfferGold + " gold"),
                        new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
                }
                rewardChoiceActive = false;
                ClearCards();
                BeginShopChoice();
                return;
            }
            if (!shopChoiceActive) return;
            if (!accepted)
            {
                AdvanceShopOfferPeriods();
                shopChoiceActive = false;
                ClearCards();
                if (CompletePendingBossChapterTransition()) return;
                BeginStageSelection();
                return;
            }
            if (!CanPurchaseShopCard(selectedShopCardIndex)) return;
            if (selectedShopCardIndex == 0)
            {
                shopDeckRemovalSelectionActive = true;
                OpenCombatDeckInspection(CombatDeckInspectionTarget.Deck);
                return;
            }

            int price = GetShopCardPrice(selectedShopCardIndex);
            int offerIndex = selectedShopCardIndex - 1;
            if (offerIndex < 0 || offerIndex >= shopOffers.Count) return;
            ShopOffer purchasedOffer = shopOffers[offerIndex];
            if (purchasedOffer.Card == null && purchasedOffer.Relic == null) return;
            gold -= price;
            shopOffers.RemoveAt(offerIndex);
            RemovePurchasedShopCardVisual(selectedShopCardIndex);
            pendingShopRewardOffer = purchasedOffer;
            shopRewardOpeningActive = true;
            shopChoiceActive = false;
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            activePackData = Resources.Load<global::CardPackData>(purchasedOffer.IsRelic ? "CardPacks/StarStair" : "CardPacks/FarAndWide");
            PrepareShopRewardChoices();
            EnsureShopRewardPackVisual();
            BeginSequence(false);
            return;            AddScorePopup(Ui(price + " 골드 사용", "Spent " + price + " gold"),
                new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
        }
        private void CompletePackAndBeginNextSequence()
        {
            CommitPendingScoreImmediately();
            if (sharedPackPreviewActive)
            {
                ReturnToSharedResultAfterPackPreview();
                return;
            }
            ResetPerPackAccumulatedBonuses();
            AdvanceDeckTransformationsAfterPack();
            completedPacks++;
            if (completedPacks % PacksPerGoal == 0)
            {
                int targetScore = GoalScores[currentGoalIndex];
                if (roundScore < targetScore)
                {
                    phase = RevealPhase.GameOver;
                    return;
                }
                currentGoalIndex++;
                if (currentGoalIndex >= GoalScores.Length)
                {
                    phase = RevealPhase.RunCleared;
                    return;
                }
                roundScore = 0;
                RefreshEnemyVisual();
            }
            BeginPackChoice();
        }
        private void StartNewRun()
        {
            settingsOpen = false;
            abandonConfirmationVisible = false;
            if (canvasRunEndRoot != null) canvasRunEndRoot.SetActive(false);
            sharedPackPreviewActive = false;
            sharedResultSnapshotJson = null;
            sharedResultMode = false;
            challengeAbandoned = false;
            shareFeedback = null;
            CloseDeckInspection();
            ClearUsedCardPile();
            ClearPackChoiceVisuals();
            if (pack != null) pack.gameObject.SetActive(true);
            if (cardStack != null) cardStack.gameObject.SetActive(true);
            for (int i = 0; i < deckVisuals.Count; i++)
                if (deckVisuals[i] != null) Destroy(deckVisuals[i]);
            deckVisuals.Clear();
            deckCards.Clear();
            starterDrawPile.Clear();
            ResetRunCombatDeckToStarter();
            runeResonanceWasActive = false;
            scorePopups.Clear();
            totalScore = 0;
            roundScore = 0;
            pendingScore = 0;
            pendingScoreCommitTime = -1f;
            scoreTransferAmount = 0;
            scoreTransferApplied = 0;
            scoreTransferStartTime = -1f;
            completedPacks = 0;
            currentGoalIndex = 0;
            currentStageChapter = 1;
            completedStageCount = 0;
            pendingBossChapterTransition = false;
            playerHealth = PlayerMaximumHealth;
            stageChapterInitialized = false;
            stageDiscardPile.Clear();
            lastUsedStageCard = null;
            currentPackOpenedForGoal = false;
            previousRevealedCard = null;
            if (starterDrawPile.Count > 0 && starterDrawPile.Peek() != null)
            {
                global::CombatCard seedCard = starterDrawPile.Peek();
                lastUsedCard = new StoredCard { Color = seedCard.Color, Number = seedCard.Number };
            }
            else if (lastUsedCard == null)
                lastUsedCard = new StoredCard { Color = global::CardColor.Green, Number = 1 };
            hasPlayedCardThisTurn = false;
            usedCastCount = 0;
            LoadStarterRelics();
            theFoolUseCount = 0;
            lightStoryUseCount = 0;
            gold = 0;
            rewardChoiceActive = false;
            shopChoiceActive = false;
            pendingOfferGold = 0;
            shopDeckRemovalPrice = 50;
            shopDeckRemovalSelectionActive = false;
            shopOffers.Clear();
            pendingPackOpenNatureSources.Clear();
            ClearNatureAbilityChain();
            leftPackChoice = null;
            rightPackChoice = null;
            activePackData = Resources.Load<global::CardPackData>("CardPacks/DayLife");
            BeginStageSelection();
        }
        private void BeginSharedPackPreview()
        {
            if (!sharedResultMode || string.IsNullOrEmpty(sharedResultSnapshotJson)) return;
            CloseDeckInspection();
            ClearPackChoiceVisuals();
            sharedPackPreviewActive = true;
            sharedResultMode = false;
            shareFeedback = null;
            totalScore = 0;
            roundScore = 0;
            pendingScore = 0;
            pendingScoreCommitTime = -1f;
            scoreTransferAmount = 0;
            scoreTransferApplied = 0;
            scoreTransferStartTime = -1f;
            scorePopups.Clear();
            currentPackOpenedForGoal = false;
            previousRevealedCard = null;
            lastUsedCard = null;
            usedCastCount = 0;
            pendingPackOpenNatureSources.Clear();
            ClearNatureAbilityChain();
            BeginPackChoice();
        }
        private void ReturnToSharedResultAfterPackPreview()
        {
            string snapshotJson = sharedResultSnapshotJson;
            sharedPackPreviewActive = false;
            previousRevealedCard = null;
            lastUsedCard = null;
            usedCastCount = 0;
            pendingPackOpenNatureSources.Clear();
            ClearNatureAbilityChain();
            if (string.IsNullOrEmpty(snapshotJson))
            {
                StartNewRun();
                return;
            }
            SharedResultData snapshot = JsonUtility.FromJson<SharedResultData>(snapshotJson);
            if (snapshot == null || snapshot.Version != 1)
            {
                StartNewRun();
                return;
            }
            RestoreSharedResult(snapshot);
        }
        private void AdvanceDeckTransformationsAfterPack()
        {
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard card = deckCards[i];
                if (card == null || card.Data == null || card.Data.DeckAbilities == null) continue;
                for (int j = 0; j < card.Data.DeckAbilities.Count; j++)
                {
                    global::CardDeckAbility ability = card.Data.DeckAbilities[j];
                    if (ability == null || ability.Effect != global::DeckAbilityEffect.TransformAfterPacks
                        || ability.TransformedCard == null) continue;
                    int requiredPacks = Mathf.Max(1, ability.PacksToTransform);
                    card.PacksElapsedByAbility.TryGetValue(j, out int elapsedPacks);
                    elapsedPacks++;
                    card.PacksElapsedByAbility[j] = elapsedPacks;
                    if (elapsedPacks < requiredPacks) continue;
                    TransformStoredDeckCard(i, ability.TransformedCard);
                    break;
                }
            }
            LayoutDeckVisuals();
        }
        private void TransformStoredDeckCard(int deckIndex, global::CardData transformedData)
        {
            if (deckIndex < 0 || deckIndex >= deckCards.Count || transformedData == null) return;
            StoredCard card = deckCards[deckIndex];
            if (card == null) return;
            card.Name = transformedData.Name;
            card.Data = transformedData;
            card.Rarity = transformedData.Rare;
            card.AccumulatedPercentByAbility.Clear();
            card.AccumulatedFlatScoreByAbility.Clear();
            card.RemainingDrawsByAbility.Clear();
            card.StackByAbilityCopy.Clear();
            card.TriggeredStackCountsThisDraw.Clear();
            card.UsedOncePerPackAbilityCopies.Clear();
            card.PerPackTriggerCountByAbility.Clear();
            card.PacksElapsedByAbility.Clear();
            GameObject oldVisual = deckIndex < deckVisuals.Count ? deckVisuals[deckIndex] : null;
            GameObject newVisual = BuildDeckVisualForStoredCard(card);
            if (deckIndex < deckVisuals.Count) deckVisuals[deckIndex] = newVisual;
            if (oldVisual != null) Destroy(oldVisual);
        }
        private GameObject BuildDeckVisualForStoredCard(StoredCard card)
        {
            if (card == null || card.Data == null || deckRoot == null) return null;
            global::CardData data = card.Data;
            CardVisual visual = CardVisual.CreatePrefabInstance("Stored Card - " + data.Name, deckRoot);
            GameObject cardObject = visual.gameObject;
            string attributeKey = card.Color.ToString();
            Material attributeMaterial = GetTextureMaterial("Attribute_" + attributeKey,
                "CardAssets/Attributes/Attribute" + attributeKey, false);
            Material rarityPatternMaterial = GetTextureMaterial("Pattern_" + data.RarityAssetKey,
                "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0);
            string costAsset = "Cost" + card.Number;
            Material costMaterial = GetTextureMaterial("Cost_" + card.Number,
                "CardAssets/Costs/" + costAsset, true, 20);
            Material illustrationMaterial = GetTextureMaterial("CardImage_" + data.GetHashCode(), data.Image, true, 10);
            visual.BuildFromData(data, card.Color, attributeMaterial,
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                rarityPatternMaterial, illustrationMaterial, costMaterial, font, IsEnglishUi);
            visual.SetDisplayName(GetStoredCardDisplayName(card));
            visual.SetDisplayDescription(card.Data, GetStoredCardDisplayDescription(card), IsEnglishUi,
                GetMineralMiningOddsLine(card.Data));
            visual.PrepareFaceUp(Vector3.zero, 1f, 0f);
            visual.SetFaceDetailsVisible(true);
            cardObject.SetActive(true);
            SetStoredVisualShadowMode(cardObject);
            return cardObject;
        }
        private bool RollCurrentPackHolographic() { return false; }
        private void RefreshEnemyVisual()
        {
            int visibleCount = enemies.Count;
            while (enemyVisuals.Count < visibleCount) enemyVisuals.Add(CreateSingleEnemyVisual(enemyVisuals.Count));
            for (int i = 0; i < enemyVisuals.Count; i++)
            {
                EnemyVisual visual = enemyVisuals[i];
                if (visual == null) continue;
                bool visible = i < visibleCount && enemies[i] != null && !enemies[i].IsDefeated && !stageSelectionVisible;
                visual.gameObject.SetActive(visible);
                if (!visible) continue;
                global::EnemyDefinition definition = enemies[i].Definition;
                visual.SetAppearance(definition != null ? definition.Image : null,
                    GetMaterial("EnemyBody", definition != null ? definition.FallbackColor : Color.gray, 0.28f));
            }
            enemyVisual = enemyVisuals.Count > 0 ? enemyVisuals[0] : null;
            enemyVisualStatusHash = int.MinValue;
        }

        private void ClearPlayerCombatBuffs()
        {
            playerShield = 0;
            playerBurn = 0;
            playerWood = 0;
            playerRegeneration = 0;
            playerStun = 0;
            playerBindDuration = 0;
            playerScales = 0;
            playerBleedingStacks.Clear();
            combatBuffVisualHash = int.MinValue;
        }

        private void EnsurePlayerCombatStatusVisual()
        {
            if (playerCombatStatusVisual != null) return;
            GameObject statusObject = new GameObject("Player Combat Status");
            playerCombatStatusVisual = statusObject.AddComponent<PlayerCombatStatusVisual>();
        }

        private void SetPlayerCombatStatusVisible(bool visible)
        {
            if (playerCombatStatusVisual != null) playerCombatStatusVisual.gameObject.SetActive(visible);
        }

        private void StopEnemyVisualEffects()
        {
            foreach (Coroutine routine in enemyAttackRoutines.Values)
                if (routine != null) StopCoroutine(routine);
            foreach (Coroutine routine in enemyDeathRoutines.Values)
                if (routine != null) StopCoroutine(routine);
            enemyAttackRoutines.Clear();
            enemyDeathRoutines.Clear();
            if (combatVictoryRoutine != null) StopCoroutine(combatVictoryRoutine);
            combatVictoryRoutine = null;
        }

        private void PlayEnemyAttackEffect(EnemyState enemy)
        {
            int index = enemies.IndexOf(enemy);
            if (index < 0 || index >= enemyVisuals.Count) return;
            EnemyVisual visual = enemyVisuals[index];
            if (visual == null || !visual.gameObject.activeSelf) return;
            if (enemyAttackRoutines.TryGetValue(visual, out Coroutine running) && running != null) StopCoroutine(running);
            enemyAttackRoutines[visual] = StartCoroutine(PlayEnemyAttackEffectRoutine(visual));
        }

        private IEnumerator PlayEnemyAttackEffectRoutine(EnemyVisual visual)
        {
            Vector3 homePosition = visual.transform.position;
            Vector3 homeScale = visual.transform.localScale;
            for (float elapsed = 0f; elapsed < 0.10f; elapsed += Time.unscaledDeltaTime)
            {
                if (visual == null) yield break;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / 0.10f);
                visual.transform.position = Vector3.Lerp(homePosition, homePosition + Vector3.down * 0.16f, t);
                visual.transform.localScale = Vector3.Lerp(homeScale, homeScale * 1.06f, t);
                yield return null;
            }
            if (visual != null)
            {
                visual.transform.position = homePosition;
                visual.transform.localScale = homeScale;
                enemyAttackRoutines.Remove(visual);
            }
        }

        private void PlayEnemyDefeatEffect(int enemyIndex)
        {
            if (enemyIndex < 0 || enemyIndex >= enemyVisuals.Count) return;
            EnemyVisual visual = enemyVisuals[enemyIndex];
            if (visual == null) return;
            if (enemyDeathRoutines.TryGetValue(visual, out Coroutine running) && running != null) StopCoroutine(running);
            enemyDeathRoutines[visual] = StartCoroutine(PlayEnemyDefeatEffectRoutine(visual));
        }

        private IEnumerator PlayEnemyDefeatEffectRoutine(EnemyVisual visual)
        {
            Vector3 homeScale = visual.transform.localScale;
            for (float elapsed = 0f; elapsed < 0.18f; elapsed += Time.unscaledDeltaTime)
            {
                if (visual == null) yield break;
                visual.transform.localScale = Vector3.Lerp(homeScale, homeScale * 0.25f, elapsed / 0.18f);
                yield return null;
            }
            if (visual != null)
            {
                visual.gameObject.SetActive(false);
                enemyDeathRoutines.Remove(visual);
            }
        }

        private bool CompletePendingBossChapterTransition()
        {
            if (!pendingBossChapterTransition) return false;
            pendingBossChapterTransition = false;
            if (currentStageChapter < 3)
            {
                currentStageChapter++;
                playerHealth = PlayerMaximumHealth;
                stageChapterInitialized = false;
                finalBossStageSpawned = false;
                UpdateChapterBackground();
                BeginStageSelection();
            }
            else
            {
                stageChapterInitialized = false;
                phase = RevealPhase.RunCleared;
            }
            return true;
        }
        private void BeginCombatVictoryAfterDefeatDelay()
        {
            if (combatVictoryRoutine == null) combatVictoryRoutine = StartCoroutine(FinishCombatAfterDefeatDelay());
        }

        private IEnumerator FinishCombatAfterDefeatDelay()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (enemies.Count == 0 || !enemies.TrueForAll(enemy => enemy == null || enemy.IsDefeated)) yield break;
            while (leafMeteorResolving) yield return null;
            combatVictoryRoutine = null;
            ResetRelicTurnState();
            theFoolUseCount = 0;
            lightStoryUseCount = 0;
            ClearPlayerCombatBuffs();
            if (currentStageChapter >= 3 && finalBossStageSpawned && lastUsedStageCard != null
                && lastUsedStageCard.Kind == global::StageCardKind.BossBattle)
            {
                pendingBossChapterTransition = false;
                rewardChoiceActive = false;
                shopChoiceActive = false;
                shopRewardOpeningActive = false;
                pendingShopRewardOffer = null;
                ClearCards();
                phase = RevealPhase.RunCleared;
                yield break;
            }
            if (finalBossStageSpawned && lastUsedStageCard != null
                && lastUsedStageCard.Kind == global::StageCardKind.BossBattle)
            {
                pendingBossChapterTransition = true;
                BeginGoldRewardChoice();
            }
            else if (stageChapterInitialized && lastUsedStageCard != null
                && (lastUsedStageCard.Kind == global::StageCardKind.Battle
                    || lastUsedStageCard.Kind == global::StageCardKind.Event
                    || lastUsedStageCard.Kind == global::StageCardKind.EliteBattle
                    || lastUsedStageCard.Kind == global::StageCardKind.BossBattle))
                BeginGoldRewardChoice();
            else if (stageChapterInitialized)
                BeginStageSelection();
            else
                phase = RevealPhase.RunCleared;
        }
    }
}
