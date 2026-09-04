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
        private void EndPlayerTurn()
        {
            if (!startingHandVisible || phase != RevealPhase.CardFront || playerHealth <= 0 || enemyTurnRoutine != null) return;
            if (tutorialOpen && tutorialFlowPhase == TutorialFlowPhase.CardRules
                && tutorialPracticeStage == TutorialPracticeStepCount - 1) return;
            startingHandVisible = false;
            ResetRelicTurnState();
            ResetHandHoverVisuals();
            enemyTurnRoutine = StartCoroutine(ResolveEnemyTurnSequentially());
        }

        private IEnumerator ResolveEnemyTurnSequentially()
        {
            // Defer resolution by one frame so one pointer release cannot also finish and start another turn.
            yield return null;
            // Resolve start-of-turn effects before an enemy can act.
            ApplyEnemyStunAtTurnStart();
            ApplyRegenerationAtTurnStart();
            ApplyScalesAtTurnStart();
            ApplyBleedingAtTurnStart();
            ApplyBurnAtTurnStart();
            RefreshEnemyVisual();
            if (enemies.Count > 0 && enemies.TrueForAll(enemy => enemy == null || enemy.IsDefeated))
            {
                enemyTurnRoutine = null;
                yield break;
            }            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = enemies[i];
                if (enemy == null || enemy.IsDefeated) continue;
                if (stunnedEnemyIndicesThisTurn.Contains(i)) continue;
                if (enemy.ActionTurnsRemaining > 0) enemy.ActionTurnsRemaining--;
                // Execute on the same enemy turn that changes the visible timer to 0.
                if (enemy.ActionTurnsRemaining > 0)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    continue;
                }
                ExecuteEnemyAction(enemy);
                enemy.ActionTurnsRemaining = enemy.ActionInterval;
                UpdateSmallWoodSoloAttackAction(enemy);
                yield return new WaitForSecondsRealtime(1f);
                if (playerHealth <= 0) break;
            }
            enemyTurnRoutine = null;
            if (playerHealth > 0) BeginPlayerTurn();
        }
        private void ExecuteEnemyAction(EnemyState enemy)
        {
            if (enemy == null || enemy.Definition == null) return;
            if (enemy.HasSummonAction)
            {
                SummonFireWolf(enemy);
                return;
            }
            if (enemy.HasSmallWoodSoloAttackAction)
            {
                DealPlayerDamage(10, enemy.Name + " Attack", enemy.EnglishName + " Attack");
                return;
            }
            if (enemy.IsSmallStone)
            {
                if (enemy.SmallStoneShieldAction)
                {
                    enemy.Shield += 30;
                    AddScorePopup(Ui(enemy.Name + "\n보호막 +30", enemy.EnglishName + "\nShield +30"),
                        new Color(0.55f, 0.78f, 1f), Time.unscaledTime, scorePopups.Count, 0);
                }
                else
                {
                    string stoneKoreanSource = enemy.Name + "의 공격";
                    string stoneEnglishSource = enemy.EnglishName + " Attack";
                    DealPlayerDamage(10, stoneKoreanSource, stoneEnglishSource);
                    if (playerHealth > 0)
                        ApplyEnemyActionBuffToPlayer(GetCombatBuffDefinition("Stun"), 1, stoneKoreanSource, stoneEnglishSource);
                }
                enemy.SmallStoneShieldAction = !enemy.SmallStoneShieldAction;
                return;
            }
            if (enemy.ClevernessActionChanged)
            {
                HealEnemy(enemy, enemy.SelfHealAmount, enemy.Name + "의 회복", enemy.EnglishName + " Heal");
                return;
            }
            string koreanSource = enemy.Name + "의 " + enemy.ActionName;
            string englishSource = enemy.EnglishName + " " + enemy.EnglishActionName;
            PlayEnemyAttackEffect(enemy);
            if (!enemy.Definition.HasActionAbilities)
            {
                DealPlayerDamage(enemy.ActionDamage, koreanSource, englishSource);
                if (playerHealth <= 0) return;
                ApplyEnemyActionBuffToPlayer(GetCombatBuffDefinition("Bleeding"), enemy.BleedingStacks,
                    koreanSource, englishSource);
                return;
            }

            for (int i = 0; i < enemy.Definition.Abilities.Count; i++)
            {
                global::EnemyActionAbility ability = enemy.Definition.Abilities[i];
                if (ability == null) continue;
                int amount = Mathf.Max(0, ability.Amount);
                if (ability.Effect == global::EnemyActionEffect.HealSelf)
                {
                    HealEnemy(enemy, amount, koreanSource, englishSource);
                    continue;
                }
                if (ability.Effect == global::EnemyActionEffect.HealAllEnemies)
                {
                    for (int targetIndex = 0; targetIndex < enemies.Count; targetIndex++)
                        HealEnemy(enemies[targetIndex], amount, koreanSource, englishSource);
                    continue;
                }
                if (ability.Target != global::CombatAbilityTarget.Player) continue;
                if (ability.Effect == global::EnemyActionEffect.Damage)
                {
                    DealPlayerDamage(amount, koreanSource, englishSource);
                    if (playerHealth <= 0) return;
                }
                else if (ability.Effect == global::EnemyActionEffect.ApplyBuff)
                {
                    ApplyEnemyActionBuffToPlayer(ability.RelatedBuff, amount, koreanSource, englishSource);
                }
            }
            UpdateBeastLeaderSummonAction(enemy);
        }

        private void UpdateSmallWoodSoloAttackAction(EnemyState enemy)
        {
            if (enemy == null || !enemy.IsSmallWood || enemy.IsDefeated || enemy.HasSmallWoodSoloAttackAction) return;
            bool hasOtherLivingEnemy = enemies.Exists(other => other != null && other != enemy && !other.IsDefeated);
            if (!hasOtherLivingEnemy) enemy.SmallWoodSoloAttackAction = true;
        }

        private void UpdateBeastLeaderSummonAction(EnemyState enemy)
        {
            if (enemy == null || !enemy.IsBeastLeader || enemy.IsDefeated || enemy.HasSummonAction) return;
            bool hasOtherLivingEnemy = enemies.Exists(other => other != null && other != enemy && !other.IsDefeated);
            if (!hasOtherLivingEnemy) enemy.BeastLeaderSummonAction = true;
        }

        private void SummonFireWolf(EnemyState leader)
        {
            if (leader == null) return;
            global::EnemyDefinition fireWolf = Resources.Load<global::EnemyDefinition>("Combat/Enemies/FireWolf");
            if (fireWolf == null) return;
            EnemyState summoned = CreateEnemyState(fireWolf);
            if (summoned == null) return;
            int defeatedIndex = enemies.FindIndex(item => item != null && item.IsDefeated);
            if (defeatedIndex >= 0) enemies[defeatedIndex] = summoned;
            else if (enemies.Count < MaxSimultaneousEnemies) enemies.Add(summoned);
            else return;
            leader.BeastLeaderSummonAction = false;
            AddScorePopup(Ui(leader.Name + "\n화이리 소환", leader.EnglishName + "\nSummons FireWolf"),
                new Color(1f, 0.72f, 0.35f), Time.unscaledTime, scorePopups.Count, 0);
            RefreshEnemyVisual();
        }
        private void HealEnemy(EnemyState target, int amount, string koreanSource, string englishSource)
        {
            if (target == null || target.IsDefeated || amount <= 0) return;
            int previousHealth = target.Health;
            target.Health = Mathf.Min(target.MaximumHealth, target.Health + amount);
            int recovered = target.Health - previousHealth;
            if (recovered <= 0) return;
            AddScorePopup(Ui(koreanSource + "\n" + target.Name + " 체력 +" + recovered,
                englishSource + "\n" + target.EnglishName + " HP +" + recovered),
                new Color(0.35f, 0.9f, 0.38f), Time.unscaledTime, scorePopups.Count, 0);
        }
        private void ApplyEnemyActionBuffToPlayer(global::CombatBuffDefinition buff, int amount,
            string koreanSource, string englishSource)
        {
            if (buff == null || amount <= 0) return;
            ApplyPlayerBuff(buff, amount);
            AddScorePopup(Ui(koreanSource + "\n" + buff.GetLocalizedName(false) + " " + amount + " 부여",
                englishSource + "\nInflicts " + amount + " " + buff.GetLocalizedName(true)),
                new Color(0.95f, 0.22f, 0.22f), Time.unscaledTime, scorePopups.Count, 0);
        }
        private void ApplyPlayerBuff(global::CombatBuffDefinition buff, int amount)
        {
            if (buff == null || amount <= 0) return;
            if (buff == GetCombatBuffDefinition("Shield")) playerShield += amount;
            else if (buff == GetCombatBuffDefinition("Burn")) playerBurn += amount;
            else if (buff == GetCombatBuffDefinition("Wood")) playerWood += amount;
            else if (buff == GetCombatBuffDefinition("Regeneration")) playerRegeneration += amount;
            else if (buff == GetCombatBuffDefinition("Stun")) playerStun += amount;
            else if (buff == GetCombatBuffDefinition("Bind")) playerBindDuration += amount;
            else if (buff == GetCombatBuffDefinition("Scales")) playerScales += amount;
            else if (buff == GetCombatBuffDefinition("Bleeding"))
                playerBleedingStacks.Add(amount);
        }
        private void BeginPlayerTurn()
        {
            startingHandVisible = true;
            hasPlayedCardThisTurn = false;
            if (playerStun > 0)
            {
                playerStun--;
                AddScorePopup(Ui("기절\n차례 종료", "Stunned\nTurn ends"),
                    new Color(0.95f, 0.82f, 0.28f), Time.unscaledTime, scorePopups.Count, 0);
                EndPlayerTurn();
                return;
            }
            ApplyPlayerRegenerationAtTurnStart();
            ApplyPlayerScalesAtTurnStart();
            ApplyPlayerBleedingAtTurnStart();
            ApplyPlayerBurnAtTurnStart();
            if (playerHealth <= 0) return;
            while (cards.Count < StartingHandSize && DrawStarterCardToHand()) { }
            if (tutorialOpen && tutorialFlowPhase == TutorialFlowPhase.CombatWaitEnemy)
            {
                // The refill is complete: expose the deterministic one-card attack step now.
                tutorialFlowPhase = TutorialFlowPhase.CombatRefillAttack;
                runtimeUiStateHash = int.MinValue;
            }
            else if (tutorialOpen && tutorialFlowPhase == TutorialFlowPhase.CombatRefillEndTurn)
            {
                // The second turn has refilled the hand; finish with those same cards.
                PrepareTutorialCombatFinisher();
            }
            if (playerBindDuration > 0) playerBindDuration--;
            EnsurePlayableCombatCardAtTurnStart();
            LayoutUsedCardPile();
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            combatTurn++;
        }
        private void ApplyPlayerRegenerationAtTurnStart()
        {
            if (playerRegeneration <= 0 || playerHealth <= 0) return;
            int before = playerHealth;
            playerHealth = Mathf.Min(PlayerMaximumHealth, playerHealth + playerRegeneration);
            int recovered = playerHealth - before;
            if (recovered > 0)
                AddScorePopup(Ui("재생\n체력 +" + recovered, "Regeneration\nHP +" + recovered),
                    new Color(0.4f, 1f, 0.58f), Time.unscaledTime, scorePopups.Count, 0);
        }
        private void ApplyPlayerScalesAtTurnStart()
        {
            if (playerScales > 0 && playerShield <= 0) playerShield += playerScales;
        }
        private void ApplyPlayerBurnAtTurnStart()
        {
            if (playerBurn <= 0) return;
            int damage = playerBurn;
            DealPlayerDamage(damage, "화상", "Burn");
            if (playerHealth <= 0) return;
            if (playerWood > 0)
            {
                playerBurn += 6;
                playerWood--;
            }
            else
                playerBurn /= 2;
        }
        private void ApplyPlayerBleedingAtTurnStart()
        {
            for (int i = playerBleedingStacks.Count - 1; i >= 0; i--)
            {
                if (playerBleedingStacks[i] <= 0) { playerBleedingStacks.RemoveAt(i); continue; }
                DealPlayerDamage(7, "출혈", "Bleeding");
                playerBleedingStacks[i]--;
                if (playerBleedingStacks[i] <= 0) playerBleedingStacks.RemoveAt(i);
                if (playerHealth <= 0) return;
            }
        }
        private void DealPlayerDamage(int damage, string koreanSource, string englishSource)
        {
            if (damage <= 0 || playerHealth <= 0) return;
            int shieldDamage = Mathf.Min(playerShield, damage);
            playerShield -= shieldDamage;
            int healthDamage = damage - shieldDamage;
            playerHealth = Mathf.Max(0, playerHealth - healthDamage);
            if (healthDamage > 0) TriggerPlayerDamageFlash();
            string damageText = shieldDamage > 0
                ? Ui(koreanSource + "\n보호막 -" + shieldDamage, englishSource + "\nShield -" + shieldDamage)
                : Ui(koreanSource + "\n피해 " + healthDamage, englishSource + "\nDamage " + healthDamage);
            AddScorePopup(damageText,
                new Color(1f, 0.38f, 0.34f), Time.unscaledTime, scorePopups.Count, 0);
            if (playerHealth <= 0)
            {
                ResetRelicTurnState();
                startingHandVisible = false;
                phase = RevealPhase.GameOver;
            }
        }
        private void TriggerPlayerDamageFlash()
        {
            if (playerDamageFlashRoutine != null) StopCoroutine(playerDamageFlashRoutine);
            playerDamageFlashRoutine = StartCoroutine(PlayPlayerDamageFlash());
        }

        private IEnumerator PlayPlayerDamageFlash()
        {
            EnsurePlayerDamageFlashOverlay();
            playerDamageFlashImage.gameObject.SetActive(true);
            playerDamageFlashImage.transform.SetAsLastSibling();
            const float peakAlpha = 0.20f;
            const float duration = 0.20f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float alpha = Mathf.Lerp(peakAlpha, 0f, elapsed / duration);
                playerDamageFlashImage.color = new Color(0.88f, 0.04f, 0.05f, alpha);
                yield return null;
            }
            playerDamageFlashImage.gameObject.SetActive(false);
            playerDamageFlashRoutine = null;
        }
        private void DrawTurnControls(float scale, float offsetX, float offsetY)
        {
            if (canvasDeckButton != null && canvasEndTurnButton != null) return;
            EnsureDiscardStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
bool canEndTurn = startingHandVisible && phase == RevealPhase.CardFront && playerHealth > 0
                && !(tutorialOpen && tutorialFlowPhase == TutorialFlowPhase.CardRules
                    && tutorialPracticeStage == TutorialPracticeStepCount - 1);

            if (rewardChoiceActive)
            {
                if (canvasContextTitle == null) GUI.Label(new Rect(0f, 40f, UiReferenceWidth, 54f), Ui("전투 보상", "Combat Reward"), deckRarityStyle);
                if (canvasContextMessage == null) GUI.Label(new Rect(0f, 92f, UiReferenceWidth, 34f),
                    Ui("잎를 위로 드래그해 보상을 받고, 사용한 잎 더미로 드래그해 거절하세요.",
                        "Drag the card upward to claim it, or drag it to the discard pile to decline."), discardMessageStyle);
                GUI.matrix = previousMatrix;
                return;
            }
            if (shopChoiceActive)
            {
                if (canvasContextTitle == null) GUI.Label(new Rect(0f, 40f, UiReferenceWidth, 54f), Ui("상점", "Shop"), deckRarityStyle);
                if (canvasContextMessage == null) GUI.Label(new Rect(0f, 92f, UiReferenceWidth, 34f),
                    Ui("잎를 위로 드래그해 구매하세요.", "Drag a card upward to purchase it."), discardMessageStyle);
                if (canvasLeaveShopButton == null && !discardPileHovered && GUI.Button(new Rect(UiReferenceWidth - 214f, 430f, 190f, 48f),
                    Ui("상점 나가기", "Leave Shop"), discardButtonStyle))
                    ResolveOffer(false);
                GUI.matrix = previousMatrix;
                return;
            }
            if (canvasDeckButton == null && GUI.Button(UiRect(new Rect(0f, 28f, 190f, 48f), new Rect(0f, 64f, 190f, 48f)), Ui("덱 확인", "View Deck"), discardButtonStyle))
                OpenCombatDeckInspection();
            GUI.enabled = canEndTurn;
            if (canvasEndTurnButton == null && !discardPileHovered
                && GUI.Button(new Rect(UiReferenceWidth - 214f, 430f, 190f, 48f), Ui("차례 종료", "End Turn"), discardButtonStyle))
                EndPlayerTurn();
            GUI.enabled = true;
            GUI.matrix = previousMatrix;
        }
        private void OpenCombatDeckInspection(CombatDeckInspectionTarget target = CombatDeckInspectionTarget.Deck)
        {
            if (combatDeckInspectionVisible) return;
            combatDeckInspectionTarget = target;
            combatDeckInspectionDetailIndex = -1;
            combatDeckInspectionVisible = true;
            if (combatDeckInspectionDetailCard != null) Destroy(combatDeckInspectionDetailCard.gameObject);
            combatDeckInspectionDetailCard = null;
            RefreshCombatDeckInspectionVisuals();
            EnsureCombatDeckInspectionToolbar();
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(true);
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null) cards[i].gameObject.SetActive(false);
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
            if (combatPlayerCharacter != null) combatPlayerCharacter.SetActive(false);
            SetPlayerCombatStatusVisible(false);
            if (combatDeckInspectionUiRoot != null) combatDeckInspectionUiRoot.SetActive(true);
            if (Camera.main != null)
                LayoutDeckInspectionBackdrop(Camera.main, Camera.main.WorldToScreenPoint(CardHome).z);
        }

        private void CloseCombatDeckInspection()
        {
            if (!combatDeckInspectionVisible && combatDeckInspectionCards.Count == 0) return;
            combatDeckInspectionVisible = false;
            shopDeckRemovalSelectionActive = false;
            combatDeckInspectionDragActive = false;
            combatDeckInspectionDetailIndex = -1;
            if (combatDeckInspectionDetailCard != null) Destroy(combatDeckInspectionDetailCard.gameObject);
            combatDeckInspectionDetailCard = null;
            SetRuntimeUiVisibleForDeckDetail(true);
            RestoreCombatDeckInspectionScene();
            ClearCombatDeckInspectionVisuals();
            if (combatDeckInspectionUiRoot != null) combatDeckInspectionUiRoot.SetActive(false);
            combatDeckInspectionCards.Clear();
            combatDeckInspectionSceneScrollY = 0f;
            if (deckInspectionBackdrop != null && inspectedDeckIndex < 0 && usedPileDetailCard == null)
                deckInspectionBackdrop.SetActive(false);
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(!stageSelectionVisible);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(stageSelectionVisible && !tutorialOpen);
            LayoutUsedCardPile();
            LayoutStartingHand();
            if (stageSelectionVisible) { LayoutStageSelectionHand(); LayoutStageSelectionCharacter(); }
        }

        private void AddStageInspectionCard(global::StageCardType stage)
        {
            if (stage == null) return;
            if (!stageInspectionTypes.TryGetValue(stage, out global::CombatCardType type) || type == null) { type = ScriptableObject.CreateInstance<global::CombatCardType>(); type.CardName = stage.StageName; type.Description = stage.Description; type.EnglishName = stage.EnglishName; type.EnglishDescription = stage.EnglishDescription; type.Image = stage.Image; type.Rarity = stage.Kind == global::StageCardKind.BossBattle ? global::CardRarity.Epic : stage.Kind == global::StageCardKind.EliteBattle ? global::CardRarity.Rare : global::CardRarity.Common; stageInspectionTypes[stage] = type; }
            combatDeckInspectionCards.Add(new global::CombatCard { Type = type, Color = GetStageRuntimeColor(stage), Number = stage.Number });
        }
        private void RefreshCombatDeckInspectionVisuals()
        {
            combatDeckInspectionCards.Clear();
            if (shopDeckRemovalSelectionActive)
            {
                for (int i = 0; i < runCombatDeck.Count; i++)
                    if (runCombatDeck[i] != null) combatDeckInspectionCards.Add(runCombatDeck[i]);
            }            else if (stageDeckInspectionMode && combatDeckInspectionTarget != CombatDeckInspectionTarget.Deck)
            {
                if (combatDeckInspectionTarget == CombatDeckInspectionTarget.Discard)
                    foreach (global::StageCardType card in stageDiscardPile) AddStageInspectionCard(card);
                else foreach (global::StageCardType card in stageDrawPile) AddStageInspectionCard(card);
            }
            else if (combatDeckInspectionTarget == CombatDeckInspectionTarget.Deck)            {                if (stageDeckInspectionMode && starterDrawPile.Count == 0)
                    for (int i = 0; i < runCombatDeck.Count; i++) if (runCombatDeck[i] != null) combatDeckInspectionCards.Add(runCombatDeck[i]);
                for (int i = 0; i < currentPackCards.Count; i++)
                    AddCombatDeckInspectionCard(combatDeckInspectionCards, currentPackCards[i]);
                foreach (global::CombatCard card in starterDrawPile) combatDeckInspectionCards.Add(card);
                for (int i = 0; i < usedPileStoredCards.Count; i++)
                    AddCombatDeckInspectionCard(combatDeckInspectionCards, usedPileStoredCards[i]);
            }
            else if (combatDeckInspectionTarget == CombatDeckInspectionTarget.Discard)
            {
                for (int i = 0; i < usedPileStoredCards.Count; i++)
                    AddCombatDeckInspectionCard(combatDeckInspectionCards, usedPileStoredCards[i]);
            }
            else
            {
                foreach (global::CombatCard card in starterDrawPile) combatDeckInspectionCards.Add(card);
            }
            combatDeckInspectionSceneScrollY = 0f;
            CreateCombatDeckInspectionVisuals();
        }

        private void ClearCombatDeckInspectionVisuals()
        {
            for (int i = 0; i < combatDeckInspectionVisuals.Count; i++)
                if (combatDeckInspectionVisuals[i] != null) Destroy(combatDeckInspectionVisuals[i].gameObject);
            combatDeckInspectionVisuals.Clear();
        }

        private void CreateCombatDeckInspectionVisuals()
        {
            ClearCombatDeckInspectionVisuals();
            for (int i = 0; i < combatDeckInspectionCards.Count; i++)
                combatDeckInspectionVisuals.Add(null);
            LayoutCombatDeckInspectionVisuals();
        }

        private void EnsureCombatDeckInspectionUiRoot()
        {
            if (combatDeckInspectionUiRoot != null)
            {
                if (combatDeckInspectionUiRoot.GetComponent<GraphicRaycaster>() == null)
                    combatDeckInspectionUiRoot.AddComponent<GraphicRaycaster>();
                return;
            }
            combatDeckInspectionUiRoot = new GameObject("Deck Inspection UI", typeof(RectTransform), typeof(Canvas),
                typeof(GraphicRaycaster));
            Canvas canvas = combatDeckInspectionUiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 3000;
            GameObject backdrop = new GameObject("Deck Inspection Backdrop UI", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(combatDeckInspectionUiRoot.transform, false);
            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            combatDeckInspectionUiBackdrop = backdrop.GetComponent<Image>();
            combatDeckInspectionUiBackdrop.color = new Color(0f, 0f, 0f, 0.84f);
            combatDeckInspectionUiBackdrop.raycastTarget = true;
            backdrop.SetActive(false);
        }

        private Button CreateDeckToolbarButton(string name, Transform parent, Vector2 anchor,
            Vector2 anchoredPosition, string text, UnityEngine.Events.UnityAction action)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = IsPortraitUi ? new Vector2(220f, 84f) : new Vector2(320f, 104f);
            Image image = root.GetComponent<Image>();
            image.sprite = GetRoundedCanvasButtonSprite();
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            Outline border = root.AddComponent<Outline>();
            border.effectColor = Color.black;
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = false;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Vector2 buttonSize = IsPortraitUi ? new Vector2(220f, 84f) : new Vector2(320f, 104f);
            TextMeshProUGUI label = CreateCanvasHudLabel("Label", Vector2.zero, buttonSize,
                IsPortraitUi ? 22f : 28f,
                TextAlignmentOptions.Center);
            label.rectTransform.SetParent(rect, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = text;
            label.color = Color.black;
            label.outlineColor = Color.white;
            label.outlineWidth = 0.12f;
            return button;
        }
        private void SelectCombatDeckInspectionTarget(CombatDeckInspectionTarget target)
        {
            if (shopDeckRemovalSelectionActive && target != CombatDeckInspectionTarget.Deck) return;
            if (combatDeckInspectionTarget == target) return;
            combatDeckInspectionTarget = target;
            RefreshCombatDeckInspectionVisuals();
            UpdateCombatDeckInspectionToolbar();
            if (combatDeckInspectionToolbarRoot != null) combatDeckInspectionToolbarRoot.transform.SetAsLastSibling();
        }
        private void EnsureCombatDeckInspectionToolbar()
        {
            EnsureCombatDeckInspectionUiRoot();
            if (combatDeckInspectionToolbarRoot != null) return;
            combatDeckInspectionToolbarRoot = new GameObject("Deck Inspection Toolbar", typeof(RectTransform));
            combatDeckInspectionToolbarRoot.transform.SetParent(combatDeckInspectionUiRoot.transform, false);
            RectTransform toolbarRect = combatDeckInspectionToolbarRoot.GetComponent<RectTransform>();
            toolbarRect.anchorMin = new Vector2(0.5f, 1f);
            toolbarRect.anchorMax = new Vector2(0.5f, 1f);
            toolbarRect.pivot = new Vector2(0.5f, 1f);
            toolbarRect.anchoredPosition = new Vector2(0f, -120f);
            float tabOffset = IsPortraitUi ? 225f : 330f;
            CreateDeckToolbarButton("Deck Tab", combatDeckInspectionToolbarRoot.transform, new Vector2(0.5f, 1f),
                new Vector2(-tabOffset, 0f), Ui(stageDeckInspectionMode ? "덱 확인" : "덱", stageDeckInspectionMode ? "Stage Deck" : "Deck"),
                () => SelectCombatDeckInspectionTarget(CombatDeckInspectionTarget.Deck));
            CreateDeckToolbarButton("Discard Tab", combatDeckInspectionToolbarRoot.transform, new Vector2(0.5f, 1f),
                Vector2.zero, Ui("버린 잎", "Discard"),
                () => SelectCombatDeckInspectionTarget(CombatDeckInspectionTarget.Discard));
            CreateDeckToolbarButton("Draw Tab", combatDeckInspectionToolbarRoot.transform, new Vector2(0.5f, 1f),
                new Vector2(tabOffset, 0f), Ui("뽑을 잎", "Draw pile"),
                () => SelectCombatDeckInspectionTarget(CombatDeckInspectionTarget.DrawPile));
            GameObject empty = new GameObject("Empty Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            empty.transform.SetParent(combatDeckInspectionToolbarRoot.transform, false);
            RectTransform emptyRect = empty.GetComponent<RectTransform>();
            emptyRect.anchorMin = new Vector2(0.5f, 1f);
            emptyRect.anchorMax = new Vector2(0.5f, 1f);
            emptyRect.pivot = new Vector2(0.5f, 1f);
            emptyRect.anchoredPosition = new Vector2(0f, -104f);
            emptyRect.sizeDelta = new Vector2(900f, 42f);
            combatDeckInspectionEmptyLabel = empty.GetComponent<TextMeshProUGUI>();
            combatDeckInspectionEmptyLabel.font = GetRuntimeUiFont();
            combatDeckInspectionEmptyLabel.fontSize = 20f;
            combatDeckInspectionEmptyLabel.alignment = TextAlignmentOptions.Center;
            combatDeckInspectionEmptyLabel.color = Color.white;
            combatDeckInspectionEmptyLabel.outlineColor = Color.black;
            combatDeckInspectionEmptyLabel.outlineWidth = 0.2f;
            combatDeckInspectionEmptyLabel.raycastTarget = false;
            combatDeckInspectionToolbarRoot.transform.SetAsLastSibling();
        }
        private void UpdateCombatDeckInspectionToolbar()
        {
            if (combatDeckInspectionToolbarRoot == null) return;
            bool showInspectionUi = combatDeckInspectionVisible && combatDeckInspectionDetailCard == null;
            combatDeckInspectionToolbarRoot.SetActive(showInspectionUi);
            if (combatDeckInspectionUiBackdrop != null) combatDeckInspectionUiBackdrop.gameObject.SetActive(showInspectionUi);
            if (!combatDeckInspectionToolbarRoot.activeSelf) return;
            combatDeckInspectionEmptyLabel.gameObject.SetActive(combatDeckInspectionVisuals.Count == 0);
            if (combatDeckInspectionEmptyLabel.gameObject.activeSelf)
                combatDeckInspectionEmptyLabel.text = Ui("표시할 잎가 없습니다.", "No cards to display.");
            SetDeckToolbarTab("Deck Tab", combatDeckInspectionTarget == CombatDeckInspectionTarget.Deck,
                Ui(stageDeckInspectionMode ? "덱 확인" : "덱", stageDeckInspectionMode ? "Stage Deck" : "Deck"));
            SetDeckToolbarTab("Discard Tab", combatDeckInspectionTarget == CombatDeckInspectionTarget.Discard,
                Ui("버린 잎", "Discard"));
            SetDeckToolbarTab("Draw Tab", combatDeckInspectionTarget == CombatDeckInspectionTarget.DrawPile,
                Ui("뽑을 잎", "Draw pile"));
        }
        private void SetDeckToolbarTab(string nodeName, bool active, string title)
        {
            Transform tab = combatDeckInspectionToolbarRoot.transform.Find(nodeName);
            if (tab == null) return;
            TextMeshProUGUI label = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = active ? title + Ui(" [활성]", " [Active]") : title;
            Image background = tab.GetComponent<Image>();
            if (background != null) background.color = active ? new Color(0.84f, 0.84f, 0.84f, 1f) : Color.white;
        }

        private static RawImage AddCombatDeckUiLayer(Transform parent, string name, Texture texture, Rect anchor)
        {
            GameObject layer = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            layer.transform.SetParent(parent, false);
            RectTransform rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            RawImage image = layer.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private RectTransform CreateCombatDeckInspectionVisual(int index)
        {
            if (index < 0 || index >= combatDeckInspectionCards.Count) return null;
            global::CombatCard card = combatDeckInspectionCards[index];
            if (card == null || card.Type == null) return null;
            EnsureCombatDeckInspectionUiRoot();
            global::CombatCardType type = card.Type;
            GameObject cardObject = new GameObject("Deck Inspection UI Card - " + type.CardName, typeof(RectTransform));
            cardObject.transform.SetParent(combatDeckInspectionUiRoot.transform, false);
            RectTransform root = cardObject.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            Texture attribute = Resources.Load<Texture2D>("CardAssets/Attributes/Attribute" + card.Color);
            Texture pattern = Resources.Load<Texture2D>("CardAssets/Rarities/Pattern" + GetCombatDeckRarityAssetKey(type.Rarity));
            Texture cost = Resources.Load<Texture2D>("CardAssets/Costs/Cost" + card.Number);
            AddCombatDeckUiLayer(root, "Attribute Frame", attribute, new Rect(0f, 0f, 1f, 1f));
            AddCombatDeckUiLayer(root, "Rarity Pattern", pattern, new Rect(0f, 0f, 1f, 1f));
            RawImage illustration = AddCombatDeckUiLayer(root, "Card Illustration", type.Image, Rect.MinMaxRect(0.065f, 0.433f, 0.935f, 0.835f));
            CropCombatDeckUiImage(illustration, type.Image, 0.87f / 0.402f * (1.82f / 3.28f));
            AddCombatDeckUiLayer(root, "Cost Symbol", cost, new Rect(0f, 0f, 1f, 1f));
            bool dark = card.Color == global::CardColor.Black;
            TextMeshProUGUI title = CreateCombatDeckUiText(root, "Card Name", type.GetLocalizedName(IsEnglishUi), 51f,
                TextAlignmentOptions.Center, dark ? Color.white : Color.black);
            // CardVisual: local center (0.20, 1.39), size (1.254, 0.38) on a 1.82 x 3.28 card.
            SetCombatDeckUiTextRect(title.rectTransform, Rect.MinMaxRect(0.265f, 0.866f, 0.954f, 0.982f));
            TextMeshProUGUI description = CreateCombatDeckUiText(root, "Card Description", type.GetLocalizedDescription(IsEnglishUi), 45f,
                TextAlignmentOptions.Top, dark ? Color.white : Color.black);
            description.fontSize = 22f;
            description.fontSizeMax = 22f;
            description.fontSizeMin = 0f;
            // CardVisual: top pivot at local (0, -0.372), size (1.50, 1.08).
            SetCombatDeckUiTextRect(description.rectTransform, Rect.MinMaxRect(0.088f, 0.057f, 0.912f, 0.387f));
            return root;
        }

        private TextMeshProUGUI CreateCombatDeckUiText(Transform parent, string name, string value, float size,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = GetRuntimeUiFont();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.fontWeight = FontWeight.Heavy;
            text.alignment = alignment;
            text.color = color;
            text.enableWordWrapping = true;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(11f, size * 0.48f);
            text.overflowMode = TextOverflowModes.Truncate;
            text.margin = new Vector4(2f, 2f, 2f, 2f);
            text.raycastTarget = false;
            return text;
        }

        private static void CropCombatDeckUiImage(RawImage image, Texture texture, float targetAspect)
        {
            if (image == null || texture == null || texture.height <= 0 || targetAspect <= 0f) return;
            float sourceAspect = texture.width / (float)texture.height;
            if (sourceAspect > targetAspect)
            {
                float width = targetAspect / sourceAspect;
                image.uvRect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
            }
            else
            {
                float height = sourceAspect / targetAspect;
                image.uvRect = new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }
        }
        private static void SetCombatDeckUiTextRect(RectTransform rect, Rect anchor)
        {
            rect.anchorMin = new Vector2(anchor.xMin, anchor.yMin);
            rect.anchorMax = new Vector2(anchor.xMax, anchor.yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void ShowCombatDeckInspectionLegacyScene()
        {
            SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
            if (renderer != null)
            {
                combatDeckInspectionDetailPreviousBackground = renderer.sprite;
                if (combatDeckInspectionLegacyBackgroundSprite == null)
                {
                    Texture2D texture = Resources.Load<Texture2D>("Textures/SimpleBackground");
                    if (texture != null)
                        combatDeckInspectionLegacyBackgroundSprite = Sprite.Create(texture,
                            new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
                if (combatDeckInspectionLegacyBackgroundSprite != null)
                {
                    renderer.sprite = combatDeckInspectionLegacyBackgroundSprite;
                    LayoutBackground(Camera.main);
                }
            }
            for (int i = 0; i < enemyVisuals.Count; i++)
                if (enemyVisuals[i] != null) enemyVisuals[i].gameObject.SetActive(false);
        }

        private void RestoreCombatDeckInspectionScene()
        {
            SpriteRenderer renderer = background != null ? background.GetComponent<SpriteRenderer>() : null;
            if (renderer != null && combatDeckInspectionDetailPreviousBackground != null)
            {
                renderer.sprite = combatDeckInspectionDetailPreviousBackground;
                LayoutBackground(Camera.main);
            }
            combatDeckInspectionDetailPreviousBackground = null;
            RefreshEnemyVisual();
        }
        private void SetRuntimeUiVisibleForDeckDetail(bool visible)
        {
            if (runtimeUiRoot != null && runtimeUiRoot.gameObject.activeSelf != visible)
                runtimeUiRoot.gameObject.SetActive(visible);
        }

        private void OpenCombatDeckInspectionDetail(int index)
        {
            if (index < 0 || index >= combatDeckInspectionCards.Count) return;
            global::CombatCard card = combatDeckInspectionCards[index];
            if (card == null || card.Type == null) return;
            if (combatDeckInspectionDetailCard != null) Destroy(combatDeckInspectionDetailCard.gameObject);

            global::CardData data = card.Type.CreateRuntimeCardData();
            CardVisual visual = CardVisual.CreatePrefabInstance("Combat Deck Detail - " + data.Name);
            string colorKey = card.Color.ToString();
            Material attributeMaterial = GetTextureMaterial("CombatDetailAttribute_" + colorKey,
                "CardAssets/Attributes/Attribute" + colorKey, false);
            Material patternMaterial = GetTextureMaterial("CombatDetailPattern_" + data.RarityAssetKey,
                "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0);
            Material costMaterial = GetTextureMaterial("CombatDetailCost_" + card.Number,
                "CardAssets/Costs/Cost" + card.Number, true, 20);
            Material illustrationMaterial = GetTextureMaterial("CombatDetailImage_" + data.GetHashCode() + card.Type.name,
                card.Type.Image, true, 10);
            visual.BuildFromData(data, card.Color, attributeMaterial,
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                patternMaterial, illustrationMaterial, costMaterial, font, IsEnglishUi);
            visual.SetDisplayName(data.GetLocalizedName(IsEnglishUi));
            visual.SetDisplayDescription(data, data.GetLocalizedDescription(IsEnglishUi), IsEnglishUi,
                GetMineralMiningOddsLine(data));

            combatDeckInspectionDetailIndex = index;
            combatDeckInspectionDetailCard = visual;
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(false);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
            if (combatPlayerCharacter != null) combatPlayerCharacter.SetActive(false);
            SetRuntimeUiVisibleForDeckDetail(false);
            if (combatDeckInspectionUiRoot != null) combatDeckInspectionUiRoot.SetActive(false);
            // Use the original battle background in the legacy-style card detail view.
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(false);
            ShowCombatDeckInspectionLegacyScene();
            LayoutCombatDeckInspectionVisuals();
        }

        private void CloseCombatDeckInspectionDetail()
        {
            if (combatDeckInspectionDetailCard != null) Destroy(combatDeckInspectionDetailCard.gameObject);
            combatDeckInspectionDetailCard = null;
            combatDeckInspectionDetailIndex = -1;
            SetRuntimeUiVisibleForDeckDetail(true);
            if (combatDeckInspectionUiRoot != null) combatDeckInspectionUiRoot.SetActive(true);
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(true);
            RestoreCombatDeckInspectionScene();
            LayoutCombatDeckInspectionVisuals();
        }

        private void HandleCombatDeckInspectionDetailPointer(Vector2 screenPoint, Event inputEvent)
        {
            Camera camera = Camera.main;
            if (camera == null || combatDeckInspectionDetailCard == null) return;
            if (inputEvent.type == EventType.MouseDown)
            {
                if (deckInspectionReturnRoutine != null) StopCoroutine(deckInspectionReturnRoutine);
                deckInspectionReturnRoutine = null;
                deckInspectionReturning = false;
                deckInspectionDragging = true;
                deckInspectionHasDragged = false;
                deckInspectionPressOutside = !GetVisualScreenRect(combatDeckInspectionDetailCard.gameObject, camera).Contains(screenPoint);
                deckInspectionDragStart = screenPoint;
                deckInspectionStartRotation = combatDeckInspectionDetailCard.transform.rotation;
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseDrag && deckInspectionDragging)
            {
                Vector2 delta = screenPoint - deckInspectionDragStart;
                if (delta.sqrMagnitude >= 16f) deckInspectionHasDragged = true;
                if (deckInspectionHasDragged)
                {
                    combatDeckInspectionDetailCard.transform.rotation = Quaternion.Euler(-delta.y * 0.24f, delta.x * 0.28f, 0f)
                        * deckInspectionStartRotation;
                    bool faceUp = Vector3.Dot(combatDeckInspectionDetailCard.transform.forward, camera.transform.forward) >= 0f;
                    combatDeckInspectionDetailCard.SetFaceUp(faceUp);
                }
                inputEvent.Use();
                return;
            }
            if (inputEvent.type == EventType.MouseUp && deckInspectionDragging)
            {
                deckInspectionDragging = false;
                if (deckInspectionPressOutside && !deckInspectionHasDragged) CloseCombatDeckInspectionDetail();
                else if (deckInspectionHasDragged)
                    deckInspectionReturnRoutine = StartCoroutine(ReturnInspectedDeckCard(combatDeckInspectionDetailCard.gameObject));
                deckInspectionPressOutside = false;
                deckInspectionHasDragged = false;
                inputEvent.Use();
                return;
            }
            if (inputEvent.isMouse) inputEvent.Use();
        }
        private void LayoutCombatDeckInspectionVisuals()
        {
            if (!combatDeckInspectionVisible || Camera.main == null) return;
            Camera camera = Camera.main;
            GetUiLayout(out float uiScale, out float offsetX, out float offsetY);
            Rect view = new Rect(28f, 146f, UiReferenceWidth - 56f, UiReferenceHeight - 174f);
            int columns = IsPortraitUi ? 3 : 6;
            float cardHeight = IsPortraitUi ? 280f : 260f;
            float cardWidth = cardHeight * (1.82f / 3.28f);
            const float gapX = 22f;
            const float gapY = 26f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(combatDeckInspectionVisuals.Count / (float)columns));
            float contentHeight = gapY + rows * (cardHeight + gapY);
            float scrollLimit = Mathf.Max(0f, contentHeight - view.height);
            combatDeckInspectionSceneScrollY = Mathf.Clamp(combatDeckInspectionSceneScrollY, 0f, scrollLimit);
            if (combatDeckInspectionDetailCard != null)
            {
                for (int i = 0; i < combatDeckInspectionVisuals.Count; i++)
                    if (combatDeckInspectionVisuals[i] != null)
                        combatDeckInspectionVisuals[i].gameObject.SetActive(false);

                float screenHeightScale = Screen.height > 0 ? Screen.height / ReferenceHeight : 1f;
                float deckScale = screenHeightScale > 0f ? uiScale / screenHeightScale : 1f;
                float inspectionLayoutY = IsPortraitUi ? 610f + PortraitExtraHeight * 0.5f : 352.8f;
                float inspectionGuiX = offsetX + UiReferenceWidth * 0.5f * uiScale;
                float inspectionGuiY = offsetY + inspectionLayoutY * uiScale;
                float detailDepth = camera.WorldToScreenPoint(CardHome).z;
                combatDeckInspectionDetailCard.transform.position = camera.ScreenToWorldPoint(new Vector3(
                    inspectionGuiX, Screen.height - inspectionGuiY, detailDepth));
                combatDeckInspectionDetailCard.transform.localScale = Vector3.one
                    * ((IsPortraitUi ? 2.10f : 1.72f) * deckScale);
                if (!deckInspectionDragging && !deckInspectionReturning)
                    combatDeckInspectionDetailCard.transform.rotation = camera.transform.rotation;
                combatDeckInspectionDetailCard.gameObject.SetActive(true);
                LayoutDeckInspectionBackdrop(camera, detailDepth);
                return;
            }
            for (int i = 0; i < combatDeckInspectionVisuals.Count; i++)
            {
                RectTransform visual = combatDeckInspectionVisuals[i];
                int row = i / columns;
                int column = i % columns;
                int rowCount = Mathf.Min(columns, combatDeckInspectionVisuals.Count - row * columns);
                float rowWidth = rowCount * cardWidth + (rowCount - 1) * gapX;
                float x = UiReferenceWidth * 0.5f - rowWidth * 0.5f + column * (cardWidth + gapX);
                float y = view.y + gapY + row * (cardHeight + gapY) - combatDeckInspectionSceneScrollY;
                // Keep a card alive until it has completely left the screen. Culling at the
                // scroll viewport edge made a card pop out as soon as only its edge crossed it.
                bool visible = y + cardHeight >= 0f && y <= UiReferenceHeight;
                if (!visible)
                {
                    if (visual != null) Destroy(visual.gameObject);
                    combatDeckInspectionVisuals[i] = null;
                    continue;
                }
                if (visual == null)
                {
                    visual = CreateCombatDeckInspectionVisual(i);
                    combatDeckInspectionVisuals[i] = visual;
                }
                if (visual == null) continue;
                visual.gameObject.SetActive(true);
                visual.sizeDelta = new Vector2(cardWidth * uiScale, cardHeight * uiScale);
                visual.anchoredPosition = new Vector2(
                    offsetX + (x + cardWidth * 0.5f) * uiScale - Screen.width * 0.5f,
                    Screen.height * 0.5f - (offsetY + (y + cardHeight * 0.5f) * uiScale));
            }
            if (combatDeckInspectionToolbarRoot != null) combatDeckInspectionToolbarRoot.transform.SetAsLastSibling();
        }

        private float GetCombatDeckInspectionScrollLimit()
        {
            int columns = IsPortraitUi ? 3 : 6;
            float cardHeight = IsPortraitUi ? 280f : 260f;
            const float gapY = 26f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(combatDeckInspectionVisuals.Count / (float)columns));
            float contentHeight = gapY + rows * (cardHeight + gapY);
            return Mathf.Max(0f, contentHeight - (UiReferenceHeight - 174f));
        }

        private static void AddCombatDeckInspectionCard(List<global::CombatCard> output, StoredCard stored)
        {
            if (output == null || stored == null || stored.CombatType == null) return;
            output.Add(new global::CombatCard { Type = stored.CombatType, Color = stored.Color, Number = stored.Number });
        }

        private static string GetCombatDeckRarityAssetKey(global::CardRarity rarity)
        {
            switch (rarity)
            {
                case global::CardRarity.Uncommon: return "Rare";
                case global::CardRarity.Rare: return "Epic";
                case global::CardRarity.Epic:
                case global::CardRarity.Legendary: return "Legendary";
                default: return "Common";
            }
        }

        private void DrawCombatDeckInspectionOverlay(float scale, float offsetX, float offsetY)
        {
            EnsureCombatDeckInspectionToolbar();
            if (combatDeckInspectionDetailCard != null)
            {
                HandleCombatDeckInspectionDetailPointer(Event.current.mousePosition, Event.current);
                return;
            }
            Rect view = new Rect(28f, 146f, UiReferenceWidth - 56f, UiReferenceHeight - 174f);
            Vector2 referencePoint = ScreenToReferencePoint(Event.current.mousePosition);
            float tabInputWidth = IsPortraitUi ? 670f : 980f;
            float tabInputHeight = IsPortraitUi ? 84f : 104f;

            bool scrollChanged = false;
            if (Event.current.type == EventType.MouseDown && view.Contains(referencePoint))
            {
                combatDeckInspectionDragActive = true;
                combatDeckInspectionDragStartY = referencePoint.y;
                combatDeckInspectionDragStartPoint = referencePoint;
                combatDeckInspectionDragStartScrollY = combatDeckInspectionSceneScrollY;
                Event.current.Use();
                return;
            }
            if (Event.current.type == EventType.MouseDrag && combatDeckInspectionDragActive)
            {
                combatDeckInspectionSceneScrollY = Mathf.Clamp(combatDeckInspectionDragStartScrollY
                    + combatDeckInspectionDragStartY - referencePoint.y, 0f, GetCombatDeckInspectionScrollLimit());
                scrollChanged = true;
                Event.current.Use();
            }
            if (Event.current.type == EventType.MouseUp && combatDeckInspectionDragActive)
            {
                combatDeckInspectionDragActive = false;
                if ((referencePoint - combatDeckInspectionDragStartPoint).sqrMagnitude < 36f)
                {
                    Vector2 canvasPoint = new Vector2(Event.current.mousePosition.x, Screen.height - Event.current.mousePosition.y);
                    bool clickedCard = false;
                    for (int i = combatDeckInspectionVisuals.Count - 1; i >= 0; i--)
                    {
                        RectTransform card = combatDeckInspectionVisuals[i];
                        if (card == null || !card.gameObject.activeSelf
                            || !RectTransformUtility.RectangleContainsScreenPoint(card, canvasPoint)) continue;
                        clickedCard = true;
                        if (shopDeckRemovalSelectionActive) CompleteShopDeckRemoval(i);
                        else OpenCombatDeckInspectionDetail(i);
                        break;
                    }
                    if (!clickedCard) CloseCombatDeckInspection();
                }
                Event.current.Use();
                return;
            }
            if (Event.current.type == EventType.MouseDown && !IsPointerOverDeckInspectionToolbar(Event.current.mousePosition) && !view.Contains(referencePoint))
            {
                CloseCombatDeckInspection();
                Event.current.Use();
                return;
            }
            if (scrollChanged) LayoutCombatDeckInspectionVisuals();
        }
        private bool IsPointerOverDeckInspectionToolbar(Vector2 guiPoint)
        {
            if (combatDeckInspectionToolbarRoot == null || !combatDeckInspectionToolbarRoot.activeInHierarchy)
                return false;
            Vector2 screenPoint = new Vector2(guiPoint.x, Screen.height - guiPoint.y);
            foreach (Transform child in combatDeckInspectionToolbarRoot.transform)
            {
                RectTransform rect = child as RectTransform;
                if (rect != null && child.GetComponent<Button>() != null
                    && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint))
                    return true;
            }
            return false;
        }

        private float GetEnemyUiTopOffset()
        {
            // Keep the full enemy block around the middle of the battle area.
            return IsPortraitUi ? PortraitExtraHeight * 0.4f + 105f : 105f;
        }
        private float GetEnemyUiX(int enemyIndex)
        {
            int count = Mathf.Clamp(enemies.Count, 1, 3);
            enemyIndex = Mathf.Clamp(enemyIndex, 0, count - 1);
            if (IsPortraitUi)
            {
                if (count == 1) return 250f;
                if (count == 2) return enemyIndex == 0 ? 125f : 375f;
                return 5f + enemyIndex * 245f;
            }
            if (count == 1) return 520f;
            if (count == 2) return enemyIndex == 0 ? 380f : 660f;
            return 240f + enemyIndex * 280f;
        }
        private void LayoutEnemyVisualToCombatUi(float uiScale, float offsetX, float offsetY)
        {
            if (Camera.main == null || enemies.Count == 0) return;
            Camera camera = Camera.main;
            float topY = GetEnemyUiTopOffset();
            float imageWidth = IsPortraitUi ? 160f : 150f;
            const float imageHeight = 82f;
            int count = Mathf.Min(enemies.Count, enemyVisuals.Count);
            for (int i = 0; i < count; i++)
            {
                EnemyVisual visual = enemyVisuals[i];
                if (visual == null) continue;
                float imageX = GetEnemyUiX(i) + (IsPortraitUi ? 30f : 45f);
                float imageY = 232f + topY;
                float depth = camera.WorldToScreenPoint(visual.transform.position).z;
                Vector2 imageWorldSize = CombatUiSizeToWorld(camera, new Vector2(imageWidth, imageHeight),
                    uiScale, offsetX, offsetY, depth);
                // EnemyVisual artwork is 2.10 world units high at root scale 1.
                visual.transform.localScale = Vector3.one * (imageWorldSize.y / 2.10f * 1.60f);
                Vector3 position = camera.ScreenToWorldPoint(new Vector3(
                    offsetX + (imageX + imageWidth * 0.5f) * uiScale,
                    Screen.height - (offsetY + (imageY + imageHeight * 0.5f) * uiScale), depth));
                // Artwork is offset upward inside EnemyVisual, so align its visible center to the UI image rect.
                position.y -= 0.55f * visual.transform.localScale.y;
                visual.transform.position = position;
            }
        }
        private Vector3 CombatUiToWorld(Camera camera, Vector2 point, float uiScale, float offsetX, float offsetY, float depth)
        {
            return camera.ScreenToWorldPoint(new Vector3(offsetX + point.x * uiScale,
                Screen.height - (offsetY + point.y * uiScale), depth));
        }
        private Vector2 CombatUiSizeToWorld(Camera camera, Vector2 size, float uiScale, float offsetX, float offsetY, float depth)
        {
            Vector3 origin = CombatUiToWorld(camera, Vector2.zero, uiScale, offsetX, offsetY, depth);
            Vector3 end = CombatUiToWorld(camera, size, uiScale, offsetX, offsetY, depth);
            return new Vector2(Mathf.Abs(end.x - origin.x), Mathf.Abs(end.y - origin.y));
        }
        private void DrawEnemyHealth(float scale, float offsetX, float offsetY)
        {
            // Canvas now owns enemy combat status; keep this legacy world renderer disabled once ready.
            if (canvasEnemyHuds.Count > 0) return;
            // OnGUI runs for layout and input events too; rebuilding TMP meshes each time is costly.
            if (Event.current.type != EventType.Repaint) return;
            if (!combatVisualAssetsLoaded)
            {
                clockTexture = Resources.Load<Texture2D>("CardAssets/Content/clock");
                attackTexture = Resources.Load<Texture2D>("CardAssets/Content/attack");
                bleedingTexture = Resources.Load<Texture2D>("CardAssets/Content/Bleeding");
                healTexture = Resources.Load<Texture2D>("CardAssets/Content/heal");
                multiHealTexture = Resources.Load<Texture2D>("CardAssets/Content/multiheal");
                summonTexture = Resources.Load<Texture2D>("CardAssets/Content/Summon");
                combatVisualAssetsLoaded = true;
            }
            Camera camera = Camera.main;
            if (camera == null) return;
            int stateHash = Screen.width * 397 ^ Screen.height * 17 ^ uiLanguage ^ enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = enemies[i];
                stateHash = stateHash * 31 + enemy.Health;
                stateHash = stateHash * 31 + enemy.Shield;
                stateHash = stateHash * 31 + enemy.Burn;
                stateHash = stateHash * 31 + enemy.Scales;
                stateHash = stateHash * 31 + enemy.BleedingDurations.Count;
                stateHash = stateHash * 31 + enemy.ActionTurnsRemaining;
                stateHash = stateHash * 31 + enemy.ActionDamage;
                stateHash = stateHash * 31 + enemy.BleedingStacks;
                stateHash = stateHash * 31 + (enemy.IsDefeated ? 1 : 0);
            }
            if (stateHash == enemyVisualStatusHash) return;
            enemyVisualStatusHash = stateHash;
            LayoutEnemyVisualToCombatUi(scale, offsetX, offsetY);
            float topY = GetEnemyUiTopOffset();
            const float iconPixels = 48f;
            for (int i = 0; i < enemies.Count && i < enemyVisuals.Count; i++)
            {
                EnemyState enemy = enemies[i];
                EnemyVisual visual = enemyVisuals[i];
                if (visual == null) continue;
                float x = GetEnemyUiX(i);
                float width = IsPortraitUi ? 220f : 240f;
                float barWidth = IsPortraitUi ? 170f : 180f;
                float depth = camera.WorldToScreenPoint(visual.transform.position).z;
                float ratio = enemy.MaximumHealth > 0 ? (float)enemy.Health / enemy.MaximumHealth : 0f;
                GetEnemyActionUiPositions(enemy, x, width, out float countdownX, out float damageX, out float healX, out float bleedingX);
                int selfHeal = enemy.Definition != null
                    ? enemy.SelfHealAmount : 0;
                int allHeal = enemy.Definition != null
                    ? enemy.AllEnemyHealAmount : 0;
                bool hasDamage = enemy.ActionDamage > 0;
                int fallbackAmount = hasDamage ? enemy.ActionDamage : Mathf.Max(selfHeal, allHeal);
                Texture2D fallbackTexture = hasDamage ? attackTexture : (enemy.HasSummonAction ? summonTexture : (allHeal > 0 ? multiHealTexture : healTexture));
                float fallbackX = hasDamage ? damageX : healX;
                visual.UpdateCombatStatus(font, IsEnglishUi ? enemy.EnglishName : enemy.Name,
                    enemy.ActionTurnsRemaining.ToString(), fallbackAmount.ToString(), "0",
                    enemy.Health.ToString("N0") + " / " + enemy.MaximumHealth.ToString("N0"), ratio, enemy.IsDefeated,
                    clockTexture, fallbackTexture, bleedingTexture,
                    CombatUiToWorld(camera, new Vector2(x + width * 0.5f, 132f + topY), scale, offsetX, offsetY, depth),
                    CombatUiToWorld(camera, new Vector2(countdownX, 174f + topY), scale, offsetX, offsetY, depth),
                    CombatUiToWorld(camera, new Vector2(float.IsNaN(fallbackX) ? x + width * 0.5f : fallbackX, 174f + topY), scale, offsetX, offsetY, depth),
                    CombatUiToWorld(camera, new Vector2(float.IsNaN(bleedingX) ? x + width * 0.5f : bleedingX, 174f + topY), scale, offsetX, offsetY, depth),
                    CombatUiToWorld(camera, new Vector2(x + width * 0.5f, 299f + topY), scale, offsetX, offsetY, depth),
                    CombatUiSizeToWorld(camera, new Vector2(iconPixels, iconPixels), scale, offsetX, offsetY, depth),
                    CombatUiSizeToWorld(camera, new Vector2(barWidth, 22f), scale, offsetX, offsetY, depth),
                    CombatUiSizeToWorld(camera, new Vector2(14f, 14f), scale, offsetX, offsetY, depth).y);
            }
        }
        private void UpdateEnemyActionBuffVisuals()
        {
            if (canvasPlayerBuffList != null) return;
            GetUiLayout(out float scale, out float offsetX, out float offsetY);
            Camera camera = Camera.main;
            if (camera == null) return;
            int stateHash = Screen.width * 397 ^ Screen.height * 17 ^ uiLanguage
                ^ (stageSelectionVisible ? 1 : 0) ^ enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = enemies[i];
                stateHash = stateHash * 31 + (enemy != null && enemy.Definition != null
                    ? enemy.Definition.GetInstanceID() : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.ActionDamage : 0);
                stateHash = stateHash * 31 + GetEnemyActionBuffCount(enemy);
            }
            if (stateHash == enemyActionBuffVisualHash) return;
            enemyActionBuffVisualHash = stateHash;

            while (enemyActionBuffListVisuals.Count < enemies.Count)
            {
                GameObject root = new GameObject("Enemy Action Buff List " + enemyActionBuffListVisuals.Count);
                enemyActionBuffListVisuals.Add(root.AddComponent<CombatBuffListVisual>());
            }
            float topY = GetEnemyUiTopOffset();
            for (int i = 0; i < enemyActionBuffListVisuals.Count; i++)
            {
                CombatBuffListVisual visual = enemyActionBuffListVisuals[i];
                EnemyState enemy = i < enemies.Count ? enemies[i] : null;
                enemyActionBuffEntries.Clear();
                if (!stageSelectionVisible && enemy != null && !enemy.IsDefeated)
                    CollectEnemyActionBuffEntries(enemy, enemyActionBuffEntries);
                visual.gameObject.SetActive(enemyActionBuffEntries.Count > 0);
                if (enemyActionBuffEntries.Count == 0) continue;

                float x = GetEnemyUiX(i);
                float width = IsPortraitUi ? 220f : 240f;
                GetEnemyActionUiPositions(enemy, x, width, out _, out _, out _, out float buffStartX);
                float depth = camera.WorldToScreenPoint(CardHome).z;
                Vector3 first = CombatUiToWorld(camera, new Vector2(buffStartX, 174f + topY),
                    scale, offsetX, offsetY, depth);
                Vector3 next = CombatUiToWorld(camera, new Vector2(buffStartX + 64f, 174f + topY),
                    scale, offsetX, offsetY, depth);
                Vector2 iconSize = CombatUiSizeToWorld(camera, new Vector2(48f, 48f),
                    scale, offsetX, offsetY, depth);
                visual.UpdateEntries(font, enemyActionBuffEntries, first, iconSize, next - first, 1175);
            }
        }
        private void UpdateCombatBuffVisuals()
        {
            if (canvasPlayerBuffList != null) return;
            GetUiLayout(out float scale, out float offsetX, out float offsetY);
            Camera camera = Camera.main;
            if (camera == null) return;
            int stateHash = Screen.width * 397 ^ Screen.height * 17 ^ uiLanguage ^ (stageSelectionVisible ? 1 : 0) ^ enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = enemies[i];
                stateHash = stateHash * 31 + (enemy != null ? enemy.Shield : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.Burn : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.Wood : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.Regeneration : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.Stun : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.Scales : 0);
                stateHash = stateHash * 31 + (enemy != null ? enemy.BleedingDurations.Count : 0);
                if (enemy != null)
                    for (int stack = 0; stack < enemy.BleedingDurations.Count; stack++) stateHash = stateHash * 31 + enemy.BleedingDurations[stack];
            }
            stateHash = stateHash * 31 + playerShield;
            stateHash = stateHash * 31 + playerBurn;
            stateHash = stateHash * 31 + playerWood;
            stateHash = stateHash * 31 + playerRegeneration;
            stateHash = stateHash * 31 + playerStun;
            stateHash = stateHash * 31 + playerBindDuration;
            stateHash = stateHash * 31 + playerScales;
            for (int stack = 0; stack < playerBleedingStacks.Count; stack++) stateHash = stateHash * 31 + playerBleedingStacks[stack];
            if (stateHash == combatBuffVisualHash) return;
            combatBuffVisualHash = stateHash;
            while (enemyBuffListVisuals.Count < enemies.Count)
            {
                GameObject root = new GameObject("Enemy Buff List " + enemyBuffListVisuals.Count);
                enemyBuffListVisuals.Add(root.AddComponent<CombatBuffListVisual>());
            }
            float depth = camera.WorldToScreenPoint(CardHome).z;
            float topY = GetEnemyUiTopOffset();
            for (int i = 0; i < enemyBuffListVisuals.Count; i++)
            {
                CombatBuffListVisual visual = enemyBuffListVisuals[i];
                EnemyState enemy = i < enemies.Count ? enemies[i] : null;
                enemyBuffEntries.Clear();
                if (!stageSelectionVisible && enemy != null && !enemy.IsDefeated)
                {
                    if (enemy.Burn > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Burn"), enemy.Burn));
                    if (enemy.Wood > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Wood"), enemy.Wood));
                    if (enemy.Regeneration > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Regeneration"), enemy.Regeneration));
                    if (enemy.Stun > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Stun"), enemy.Stun));
                    if (enemy.Cleverness > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Cleverness"), enemy.Cleverness));
                    if (enemy.Scales > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Scales"), enemy.Scales));
                    for (int stack = 0; stack < enemy.BleedingDurations.Count; stack++)
                        enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Bleeding"), enemy.BleedingDurations[stack]));
                }
                visual.gameObject.SetActive(enemyBuffEntries.Count > 0);
                if (enemyBuffEntries.Count == 0) continue;
                float width = IsPortraitUi ? 220f : 240f;
                float panelWidth = IsPortraitUi ? 170f : 180f;
                float x = GetEnemyUiX(i) + (width - panelWidth) * 0.5f;
                Vector3 first = CombatUiToWorld(camera, new Vector2(x + 26f, 341f + topY), scale, offsetX, offsetY, depth);
                Vector3 next = CombatUiToWorld(camera, new Vector2(x + 78f, 341f + topY), scale, offsetX, offsetY, depth);
                Vector2 iconSize = CombatUiSizeToWorld(camera, new Vector2(44f, 40f), scale, offsetX, offsetY, depth);
                visual.UpdateEntries(font, enemyBuffEntries, first, iconSize, next - first, 1250);
            }
            playerBuffEntries.Clear();
            if (playerBurn > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Burn"), playerBurn));
            if (playerWood > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Wood"), playerWood));
            if (playerRegeneration > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Regeneration"), playerRegeneration));
            if (playerStun > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Stun"), playerStun));
            if (playerBindDuration > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Bind"), playerBindDuration));
            if (playerScales > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Scales"), playerScales));
            for (int stack = 0; stack < playerBleedingStacks.Count; stack++)
                playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Bleeding"), playerBleedingStacks[stack]));
            if (playerBuffListVisual == null)
            {
                GameObject root = new GameObject("Player Buff List");
                playerBuffListVisual = root.AddComponent<CombatBuffListVisual>();
            }
            playerBuffListVisual.gameObject.SetActive(playerBuffEntries.Count > 0);
            if (playerBuffEntries.Count == 0) return;
            Vector3 playerLayoutFirst = CombatUiToWorld(camera, new Vector2(299f, 386f), scale, offsetX, offsetY, depth);
            Vector2 playerIconSize = CombatUiSizeToWorld(camera, new Vector2(44f, 40f), scale, offsetX, offsetY, depth);
            Vector3 playerFirst = new Vector3(-6.7f, -2.4f, playerLayoutFirst.z);
            Vector3 playerNext = playerFirst + Vector3.right * playerIconSize.x * 1.2f;
            playerBuffListVisual.UpdateEntries(font, playerBuffEntries, playerFirst, playerIconSize, playerNext - playerFirst, 1850);
        }
        private void UpdateCombatRelicVisuals()
        {
            if (canvasRelicList != null) return;
            int stateHash = Screen.width * 397 ^ Screen.height * 17 ^ uiLanguage
                ^ (stageSelectionVisible ? 1 : 0) ^ relicDamageBonusThisTurn ^ gold;
            for (int i = 0; i < ownedRelics.Count; i++)
                stateHash = stateHash * 31 + (ownedRelics[i] != null ? ownedRelics[i].GetInstanceID() : 0);
            if (stateHash == combatRelicVisualHash) return;
            combatRelicVisualHash = stateHash;

            if (playerRelicListVisual == null)
            {
                GameObject root = new GameObject("Player Relic List");
                playerRelicListVisual = root.AddComponent<CombatRelicListVisual>();
            }
            playerRelicEntries.Clear();
            if (!stageSelectionVisible)
            {
                global::CombatRelicDefinition goldDefinition = GetGoldCurrencyDefinition();
                if (goldDefinition != null)
                    playerRelicEntries.Add(new CombatRelicListVisual.Entry(goldDefinition, gold));
                for (int i = 0; i < ownedRelics.Count; i++)
                {
                    global::CombatRelicDefinition relic = ownedRelics[i];
                    if (relic == null) continue;
                    int amount = relic.Effect == global::CombatRelicEffect.CardUseDamagePercent
                        ? relicDamageBonusThisTurn : relic.Amount;
                    playerRelicEntries.Add(new CombatRelicListVisual.Entry(relic, amount));
                }
            }
            playerRelicListVisual.gameObject.SetActive(playerRelicEntries.Count > 0);
            if (playerRelicEntries.Count == 0) return;

            GetUiLayout(out float scale, out float offsetX, out float offsetY);
            Camera camera = Camera.main;
            if (camera == null) return;
            float depth = camera.WorldToScreenPoint(CardHome).z;
            Vector2 firstUi = IsPortraitUi ? new Vector2(28f, 146f) : new Vector2(28f, 106f);
            Vector2 nextUi = firstUi + new Vector2(64f, 0f);
            Vector3 first = CombatUiToWorld(camera, firstUi, scale, offsetX, offsetY, depth);
            Vector3 next = CombatUiToWorld(camera, nextUi, scale, offsetX, offsetY, depth);
            Vector2 iconSize = CombatUiSizeToWorld(camera, new Vector2(56f, 52f), scale, offsetX, offsetY, depth);
            playerRelicListVisual.UpdateEntries(font, playerRelicEntries, first, iconSize, next - first, 1900);
        }
        private void EnsureCombatPlayerCharacter()
        {
            if (combatPlayerCharacter != null) return;
            Texture2D normalTexture = Resources.Load<Texture2D>("Textures/StageSelectionCharacterFront");
            Texture2D stunnedTexture = Resources.Load<Texture2D>("Textures/CombatPlayerStunned");
            if (normalTexture == null)
            {
                Debug.LogWarning("Textures/StageSelectionCharacterFront could not be loaded.");
                return;
            }
            combatPlayerNormalSprite = Sprite.Create(normalTexture,
                new Rect(0f, 0f, normalTexture.width, normalTexture.height), new Vector2(0.5f, 0.5f), 100f);
            if (stunnedTexture != null)
                combatPlayerStunnedSprite = Sprite.Create(stunnedTexture,
                    new Rect(0f, 0f, stunnedTexture.width, stunnedTexture.height), new Vector2(0.5f, 0.5f), 100f);
            else
                Debug.LogWarning("Textures/CombatPlayerStunned could not be loaded.");
            combatPlayerCharacter = new GameObject("Combat Player Character (Behind Health Bar)");
            combatPlayerCharacterRenderer = combatPlayerCharacter.AddComponent<SpriteRenderer>();
            combatPlayerCharacterRenderer.sprite = combatPlayerNormalSprite;
        }

        private void UpdateCombatPlayerCharacterSprite()
        {
            if (combatPlayerCharacterRenderer == null) return;
            Sprite desiredSprite = playerStun > 0 && combatPlayerStunnedSprite != null
                ? combatPlayerStunnedSprite : combatPlayerNormalSprite;
            if (desiredSprite != null && combatPlayerCharacterRenderer.sprite != desiredSprite)
                combatPlayerCharacterRenderer.sprite = desiredSprite;
        }

        private void ShowPlayerCharacterInCombat()
        {
            EnsureCombatPlayerCharacter();
            if (combatPlayerCharacter == null || combatPlayerCharacterRenderer == null) return;
            UpdateCombatPlayerCharacterSprite();
            combatPlayerCharacter.SetActive(true);
            combatPlayerCharacter.transform.position = new Vector3(-5.5f, -3.2f, 0.18f);
            combatPlayerCharacter.transform.rotation = Quaternion.identity;
            combatPlayerCharacter.transform.localScale = Vector3.one * 0.7f;
            // The character stays behind player status UI and all hand cards.
            combatPlayerCharacterRenderer.sortingOrder = 1500;
        }        private void DrawPlayerHealthAndBuffs(float scale, float offsetX, float offsetY)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (stageSelectionVisible || shopChoiceActive || inspectedDeckIndex >= 0 || combatDeckInspectionDetailCard != null || usedPileDetailCard != null)
            {
                if (combatPlayerCharacter != null) combatPlayerCharacter.SetActive(false);
            }
            else ShowPlayerCharacterInCombat();
            if (canvasPlayerHealthRoot != null) return;
            const float barX = 24f;
            const float barY = 420f;
            const float barWidth = 300f;
            const float barHeight = 26f;
            Camera camera = Camera.main;
            if (camera == null) return;
            EnsurePlayerCombatStatusVisual();
            playerCombatStatusVisual.gameObject.SetActive(true);
            int statusHash = Screen.width * 397 ^ Screen.height * 17 ^ uiLanguage ^ playerHealth;
            if (statusHash == playerStatusVisualHash) return;
            playerStatusVisualHash = statusHash;
            float healthRatio = PlayerMaximumHealth > 0 ? Mathf.Clamp01((float)playerHealth / PlayerMaximumHealth) : 0f;
            float depth = camera.WorldToScreenPoint(CardHome).z;
            playerCombatStatusVisual.UpdateStatus(font,
                playerHealth + " / " + PlayerMaximumHealth, healthRatio,
                CombatUiToWorld(camera, new Vector2(barX + barWidth * 0.5f, barY + barHeight * 0.5f), scale, offsetX, offsetY, depth)
                    + Vector3.down * 3f,
                CombatUiSizeToWorld(camera, new Vector2(barWidth, barHeight), scale, offsetX, offsetY, depth),
                CombatUiSizeToWorld(camera, new Vector2(14f, 14f), scale, offsetX, offsetY, depth).y);
        }
        private bool IsEnemyBuffPanelCoveredByHand(Rect panel, float scale, float offsetX, float offsetY)
        {
            if (!startingHandVisible || cards.Count == 0 || Camera.main == null) return false;
            Rect screenPanel = new Rect(offsetX + panel.x * scale, offsetY + panel.y * scale,
                panel.width * scale, panel.height * scale);
            for (int i = 0; i < cards.Count; i++)
            {
                CardVisual card = cards[i];
                if (card == null || !card.gameObject.activeSelf) continue;
                if (GetVisualScreenRect(card.gameObject, Camera.main).Overlaps(screenPanel)) return true;
            }
            return false;
        }
        private void DrawPackChoice(float scale, float offsetX, float offsetY)
        {
            if (inspectedPackChoice != null) return;
            Camera camera = Camera.main;
            if (camera == null || leftPackChoiceVisual == null || rightPackChoiceVisual == null) return;
            if (IsPortraitUi)
            {
                float choiceDepth = camera.WorldToScreenPoint(PackHome).z;
                float choiceScreenY = Screen.height - (offsetY + (550f + PortraitExtraHeight * 0.5f) * scale);
                leftPackChoiceVisual.transform.position = camera.ScreenToWorldPoint(
                    new Vector3(offsetX + 190f * scale, choiceScreenY, choiceDepth));
                rightPackChoiceVisual.transform.position = camera.ScreenToWorldPoint(
                    new Vector3(offsetX + 530f * scale, choiceScreenY, choiceDepth));
            }
            else
            {
                leftPackChoiceVisual.transform.position = new Vector3(-1.8f, 0.55f, -0.65f);
                rightPackChoiceVisual.transform.position = new Vector3(1.8f, 0.55f, -0.65f);
            }
            Rect leftRect = GetVisualScreenRect(leftPackChoiceVisual.gameObject, camera);
            Rect rightRect = GetVisualScreenRect(rightPackChoiceVisual.gameObject, camera);
            Vector2 mousePosition = Event.current.mousePosition;
            bool leftHovered = leftRect.Contains(mousePosition);
            bool rightHovered = rightRect.Contains(mousePosition);
            float leftScale = ResponsiveWorldScale(leftHovered ? 1.58f : 1.45f, leftHovered ? 1.25f : 1.18f);
            float rightScale = ResponsiveWorldScale(rightHovered ? 1.58f : 1.45f, rightHovered ? 1.25f : 1.18f);
            leftPackChoiceVisual.transform.localScale = Vector3.one * leftScale;
            rightPackChoiceVisual.transform.localScale = Vector3.one * rightScale;
            if (Event.current.type == EventType.MouseDown && leftHovered)
            {
                Event.current.Use();
                SelectPackChoice(leftPackChoice);
                return;
            }
            if (Event.current.type == EventType.MouseDown && rightHovered)
            {
                Event.current.Use();
                SelectPackChoice(rightPackChoice);
                return;
            }
            // Canvas owns the title and info controls. This method now only handles world-card input.
        }
        private void OpenPackContents(global::CardPackData packData)
        {
            if (packData == null) return;
            inspectedPackChoice = packData;
            packContentsScroll = Vector2.zero;
            packContentsPreviewIndex = 0;
            packContentsPackWasActive = pack != null && pack.gameObject.activeSelf;
            packContentsStackWasActive = cardStack != null && cardStack.gameObject.activeSelf;
            if (leftPackChoiceVisual != null) leftPackChoiceVisual.gameObject.SetActive(false);
            if (rightPackChoiceVisual != null) rightPackChoiceVisual.gameObject.SetActive(false);
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(false);
            BuildPackContentsPreviewCard();
        }
        private void ClosePackContents()
        {
            ClearPackContentsPreview();
            inspectedPackChoice = null;
            if (phase == RevealPhase.PackChoice)
            {
                if (leftPackChoiceVisual != null) leftPackChoiceVisual.gameObject.SetActive(true);
                if (rightPackChoiceVisual != null) rightPackChoiceVisual.gameObject.SetActive(true);
            }
            else
            {
                if (pack != null) pack.gameObject.SetActive(packContentsPackWasActive);
                if (cardStack != null) cardStack.gameObject.SetActive(packContentsStackWasActive);
            }
        }
        private int GetPackContentsCardCount()
        {
            if (inspectedPackChoice == null || inspectedPackChoice.IncludeCards == null) return 0;
            int count = 0;
            for (int i = 0; i < inspectedPackChoice.IncludeCards.Count; i++)
            {
                global::CardPackEntry entry = inspectedPackChoice.IncludeCards[i];
                if (entry != null && entry.Card != null) count++;
            }
            return count;
        }
        private global::CardPackEntry GetPackContentsEntry(int visibleIndex)
        {
            if (inspectedPackChoice == null || inspectedPackChoice.IncludeCards == null) return null;
            int current = 0;
            for (int i = 0; i < inspectedPackChoice.IncludeCards.Count; i++)
            {
                global::CardPackEntry entry = inspectedPackChoice.IncludeCards[i];
                if (entry == null || entry.Card == null) continue;
                if (current == visibleIndex) return entry;
                current++;
            }
            return null;
        }
        private static int GetPackContentsRarityNumber(global::CardRarity rarity)
        {
            switch (rarity)
            {
                case global::CardRarity.Uncommon: return 2;
                case global::CardRarity.Rare: return 3;
                case global::CardRarity.Epic: return 4;
                case global::CardRarity.Legendary: return 5;
                default: return 1;
            }
        }
        private void BuildPackContentsPreviewCard()
        {
            ClearPackContentsPreview();
            global::CardPackEntry entry = GetPackContentsEntry(packContentsPreviewIndex);
            if (entry == null || entry.Card == null) return;
            global::CardData data = entry.Card;
            int previewNumber = GetPackContentsRarityNumber(data.Rare);
            global::CardColor previewColor = global::CardColor.Black;
            string previewAttributeKey = previewColor.ToString();
            CardVisual visual = CardVisual.CreatePrefabInstance("Pack Contents Preview - " + data.Name);
            GameObject cardObject = visual.gameObject;
            Material attributeMaterial = GetTextureMaterial("Attribute_" + previewAttributeKey,
                "CardAssets/Attributes/Attribute" + previewAttributeKey, false);
            Material rarityPatternMaterial = GetTextureMaterial("Pattern_" + data.RarityAssetKey,
                "CardAssets/Rarities/Pattern" + data.RarityAssetKey, true, 0);
            string costAsset = "Cost" + previewNumber;
            Material costMaterial = GetTextureMaterial("Cost_" + previewNumber,
                "CardAssets/Costs/" + costAsset, true, 20);
            Material illustrationMaterial = GetTextureMaterial(
                "CardImage_" + data.GetHashCode(), data.Image, true, 10);
            visual.BuildFromData(data, previewColor, attributeMaterial,
                GetTextureMaterial("CardBack", "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                rarityPatternMaterial, illustrationMaterial, costMaterial, font, IsEnglishUi);
            visual.SetDisplayDescription(data, data.GetLocalizedDescription(IsEnglishUi), IsEnglishUi, GetMineralMiningOddsLine(data));
            visual.PrepareFaceUp(Vector3.zero, CurrentRevealedCardScale, 0f);
            visual.SetFaceDetailsVisible(true);
            SetStoredVisualShadowMode(cardObject);
            packContentsPreviewVisual = visual;
            LayoutPackContentsPreviewCard();
        }
        private void LayoutPackContentsPreviewCard()
        {
            if (packContentsPreviewVisual == null) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            GetUiLayout(out float uiScale, out float offsetX, out float offsetY);
            float referenceCenterY = IsPortraitUi ? 624f : 351f;
            float screenX = offsetX + UiReferenceWidth * 0.5f * uiScale;
            float screenY = Screen.height - (offsetY + referenceCenterY * uiScale);
            float depth = camera.WorldToScreenPoint(new Vector3(0f, 0.92f, -0.24f)).z;
            packContentsPreviewVisual.transform.position =
                camera.ScreenToWorldPoint(new Vector3(screenX, screenY, depth));
            packContentsPreviewVisual.transform.localScale = Vector3.one * CurrentRevealedCardScale;
        }
        private void ClearPackContentsPreview()
        {
            if (packContentsPreviewVisual == null) return;
            packContentsPreviewVisual.gameObject.SetActive(false);
            Destroy(packContentsPreviewVisual.gameObject);
            packContentsPreviewVisual = null;
        }
        private void ChangePackContentsPreview(int direction)
        {
            int count = GetPackContentsCardCount();
            if (count <= 0) return;
            packContentsPreviewIndex = (packContentsPreviewIndex + direction + count) % count;
            BuildPackContentsPreviewCard();
        }
        private void DrawActualPackContentsOverlay(float scale, float offsetX, float offsetY)
        {
            if (canvasPackContentsControlsRoot != null) return;
            EnsureDiscardStyles();
            if (packContentsTitleStyle == null)
            {
                packContentsTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 30,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                packContentsCardStyle = new GUIStyle(packContentsTitleStyle)
                {
                    fontSize = 22
                };
            }
            int count = GetPackContentsCardCount();
            int cardsPerPack = inspectedPackChoice != null
                ? Mathf.Max(1, inspectedPackChoice.CardsPerPack) : 0;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            string packContentsTitle = Ui("봉입 잎 (" + cardsPerPack + "장입)",
                "Included cards (" + cardsPerPack + (cardsPerPack == 1 ? " card)" : " cards)"));
            GUI.Label(UiRect(new Rect(390f, 28f, 500f, 52f), new Rect(110f, 28f, 500f, 52f)),
                packContentsTitle, packContentsTitleStyle);
            if (GUI.Button(UiRect(new Rect(1060f, 28f, 170f, 52f), new Rect(522f, 95f, 170f, 52f)), Ui("\uB2EB\uAE30", "Close"), discardButtonStyle))
            {
                GUI.matrix = previousMatrix;
                ClosePackContents();
                return;
            }
            if (count > 0)
            {
                if (GUI.Button(UiRect(new Rect(250f, 320f, 150f, 62f), new Rect(20f, 590f, 140f, 68f)), "\u25C0", discardButtonStyle))
                    ChangePackContentsPreview(-1);
                if (GUI.Button(UiRect(new Rect(880f, 320f, 150f, 62f), new Rect(560f, 590f, 140f, 68f)), "\u25B6", discardButtonStyle))
                    ChangePackContentsPreview(1);
                GUI.Label(UiRect(new Rect(490f, 642f, 300f, 42f), new Rect(210f, 1160f, 300f, 42f)),
                    (packContentsPreviewIndex + 1) + " / " + count, packContentsCardStyle);
            }
            else
            {
                GUI.Label(UiRect(new Rect(390f, 320f, 500f, 60f), new Rect(110f, 590f, 500f, 60f)),
                    Ui("\uD45C\uC2DC\uD560 \uCE74\uB4DC\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", "No cards to display."), packContentsCardStyle);
            }
            GUI.matrix = previousMatrix;
        }
        private bool DrawActivePackContentsButton(float scale, float offsetX, float offsetY)
        {
            if (canvasActivePackInfoButton != null) return false;
            EnsureDiscardStyles();
            Rect buttonRect = UiRect(new Rect(880f, 105f, 54f, 54f), new Rect(638f, 105f, 54f, 54f));
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            bool clicked = canvasActivePackInfoButton == null && GUI.Button(buttonRect, "?", discardButtonStyle);
            GUI.matrix = previousMatrix;
            if (clicked) OpenPackContents(activePackData);
            return clicked || Event.current.type == EventType.Used;
        }
        private void DrawRunEndOverlay(float scale, float offsetX, float offsetY)
        {
            if (canvasRunEndRoot != null) return;
            EnsureDiscardStyles();
            if (runEndTitleStyle == null)
            {
                runEndTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 44,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.84f, 0.3f) }
                };
                runEndTitleStyle.hover.textColor = Color.black;
                runEndTitleStyle.active.textColor = Color.black;
                runEndTitleStyle.focused.textColor = Color.black;
                runEndTitleStyle.onNormal.textColor = Color.black;
                runEndTitleStyle.onHover.textColor = Color.black;
                runEndTitleStyle.onActive.textColor = Color.black;
                runEndTitleStyle.onFocused.textColor = Color.black;
                runEndBodyStyle = new GUIStyle(runEndTitleStyle)
                {
                    fontSize = 25,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                runEndButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    font = font,
                    fontSize = 25,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                runEndButtonStyle.normal.textColor = Color.black;
                runEndButtonStyle.hover.textColor = Color.black;
                runEndButtonStyle.active.textColor = Color.black;
                runEndButtonStyle.focused.textColor = Color.black;
                runEndButtonStyle.border = new RectOffset(12, 12, 12, 12);
                runEndButtonStyle.normal.background = roundedDiscardTexture;
                runEndButtonStyle.hover.background = roundedDiscardTexture;
                runEndButtonStyle.active.background = roundedDiscardTexture;
                runEndButtonStyle.focused.background = roundedDiscardTexture;
                runEndBadgeStyle = new GUIStyle(runEndBodyStyle)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
                runEndStatLabelStyle = new GUIStyle(runEndBodyStyle)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter
                };
                runEndStatValueStyle = new GUIStyle(runEndBodyStyle)
                {
                    fontSize = 29,
                    alignment = TextAnchor.UpperCenter
                };
                runEndHintStyle = new GUIStyle(runEndBodyStyle)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            GUI.Box(UiRect(new Rect(270f, 45f, 740f, 485f), new Rect(50f, 270f, 620f, 650f)), GUIContent.none, discardPanelStyle);
            bool cleared = phase == RevealPhase.RunCleared;
            bool defeated = !sharedResultMode && playerHealth <= 0;
            string title = cleared ? Ui("런 클리어!", "RUN CLEARED!") : Ui("도전 실패", "CHALLENGE FAILED");
            string resultMessage = cleared
                ? Ui("당신의 꿈나무가 이야기 꼬리에 닿았습니다.", "Your Dream Tree has reached the end of the story.")
                : challengeAbandoned
                    ? Ui("도전을 포기하였습니다.", "The challenge was abandoned.")
                    : defeated
                        ? Ui("체력이 0이되어 패배 하였습니다.", "You were defeated because your health reached 0.")
                        : Ui("이번 라운드에서 목표 점수를 달성하지 못했습니다.", "The goal score was not reached this round.");
            if (sharedResultMode && cleared)
                GUI.Label(UiRect(new Rect(450f, 58f, 380f, 32f), new Rect(190f, 292f, 340f, 36f)),
                    Ui("공유받은 결과", "SHARED RESULT"), runEndBadgeStyle);
            GUI.Label(UiRect(new Rect(320f, 88f, 640f, 70f), new Rect(90f, 330f, 540f, 82f)), title, runEndTitleStyle);
            GUI.Label(UiRect(new Rect(340f, 154f, 600f, 42f), new Rect(90f, 410f, 540f, 48f)), resultMessage, runEndBodyStyle);
            Rect leftButtonRect = UiRect(new Rect(360f, 400f, 260f, 70f), new Rect(90f, 755f, 250f, 76f));
            Rect rightButtonRect = sharedResultMode ? UiRect(new Rect(660f, 400f, 260f, 70f), new Rect(380f, 755f, 250f, 76f)) : UiRect(new Rect(510f, 400f, 260f, 70f), new Rect(235f, 755f, 250f, 76f));
            if (sharedResultMode)
            {
                if (GUI.Button(leftButtonRect, Ui("\uD329 \uAE4C\uBCF4\uAE30", "Open a Pack"), runEndButtonStyle))
                {
                    GUI.matrix = previousMatrix;
                    BeginSharedPackPreview();
                    return;
                }
                if (GUI.Button(rightButtonRect, Ui("\uB3C4\uC804\uD558\uAE30", "Challenge"), runEndButtonStyle))
                    StartNewRun();
            }
            else
            {


                if (GUI.Button(rightButtonRect, Ui("다시 도전", "Try Again"), runEndButtonStyle))
                    StartNewRun();
            }
            if (!sharedResultMode && !string.IsNullOrEmpty(shareFeedback) && Time.unscaledTime < shareFeedbackUntil)
                GUI.Label(UiRect(new Rect(340f, 478f, 600f, 38f), new Rect(85f, 842f, 550f, 42f)), shareFeedback, runEndHintStyle);
            GUI.matrix = previousMatrix;
        }
        private void DrawDeckInspectionControls(float scale, float offsetX, float offsetY)
        {
            if (canvasDeckInspectionControlsRoot != null) return;
            EnsureDiscardStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            if (inspectedDeckIndex >= 0 && inspectedDeckIndex < deckCards.Count
                && deckCards[inspectedDeckIndex] != null)
            {
                StoredCard inspectedCard = deckCards[inspectedDeckIndex];
                GUI.color = GetRarityDisplayColor(inspectedCard.Rarity);
                GUI.Label(UiRect(new Rect(490f, 18f, 300f, 48f), new Rect(210f, 205f + PortraitExtraHeight * 0.5f, 300f, 52f)),
                    GetRarityDisplayName(inspectedCard.Rarity), deckRarityStyle);
                GUI.color = previousColor;
                string progressText = GetDeckProgressText(inspectedCard);
                if (!string.IsNullOrEmpty(progressText))
                {
                    EnsureDeckStatusStyles();
                    DrawStatusLabelWithShadow(UiRect(new Rect(855f, 270f, 390f, 120f), new Rect(150f, 950f + PortraitExtraHeight * 0.5f, 420f, 150f)),
                        progressText, deckInspectionStatusStyle, new Color(0.55f, 0.95f, 1f));
                }
            }
            if (IsDeckInspectionReadOnly())
            {
                discardConfirmationVisible = false;
            }
            else if (!discardConfirmationVisible)
            {
                if (GUI.Button(UiRect(new Rect(550f, 646f, 180f, 52f), new Rect(270f, 1115f + PortraitExtraHeight * 0.5f, 180f, 62f)), Ui("\uCE74\uB4DC \uBC84\uB9AC\uAE30", "Discard card"), discardButtonStyle))
                    discardConfirmationVisible = true;
            }
            else
            {
                Rect panelRect = UiRect(new Rect(430f, 252f, 420f, 206f), new Rect(70f, 470f, 580f, 300f));
                GUI.Box(panelRect, GUIContent.none, discardPanelStyle);
                GUI.Label(UiRect(new Rect(455f, 278f, 370f, 64f), new Rect(110f, 515f, 500f, 80f)), Ui("\uC774 \uCE74\uB4DC\uB97C \uBC84\uB9B4\uAE4C\uC694?", "Discard this card?"), discardMessageStyle);
                if (GUI.Button(UiRect(new Rect(480f, 370f, 140f, 52f), new Rect(130f, 650f, 190f, 64f)), Ui("\uBC84\uB9AC\uAE30", "Discard"), discardButtonStyle))
                    DiscardInspectedDeckCard();
                if (GUI.Button(UiRect(new Rect(660f, 370f, 140f, 52f), new Rect(400f, 650f, 190f, 64f)), Ui("\uCDE8\uC18C", "Cancel"), discardButtonStyle))
                    discardConfirmationVisible = false;
            }
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }
        private string GetDeckProgressText(StoredCard card, bool includeEquipment = true)
        {
            if (card == null || card.Data == null || card.Data.DeckAbilities == null) return string.Empty;
            List<string> statusLines = new List<string>();
            if (includeEquipment && card.EquippedMagic != null && card.EquippedMagic.Data != null)
                statusLines.Add(Ui("마법: ", "Magic: ")
                    + card.EquippedMagic.Data.GetLocalizedName(IsEnglishUi));
            if (includeEquipment && card.EquippedWeapon != null && card.EquippedWeapon.Data != null)
                statusLines.Add(Ui("무기: ", "Weapon: ")
                    + card.EquippedWeapon.Data.GetLocalizedName(IsEnglishUi));
            int effectiveCopies = GetEffectiveDeckCopyCount(card);
            for (int i = 0; i < card.Data.DeckAbilities.Count; i++)
            {
                global::CardDeckAbility ability = card.Data.DeckAbilities[i];
                if (ability == null) continue;
                if (ability.Effect == global::DeckAbilityEffect.TransformAfterPacks)
                {
                    card.PacksElapsedByAbility.TryGetValue(i, out int elapsedPacks);
                    statusLines.Add(elapsedPacks + "/" + Mathf.Max(1, ability.PacksToTransform));
                }
                if (ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusPerDraw
                    || ability.Effect == global::DeckAbilityEffect.AccumulatePercentAtStackThreshold
                    || ability.Effect == global::DeckAbilityEffect.AccumulateScoreBonusEfficiencyByNumber)
                {
                    card.AccumulatedPercentByAbility.TryGetValue(i, out float accumulatedPercent);
                    statusLines.Add(accumulatedPercent.ToString("0.#") + "%");
                }
                if (ability.Effect == global::DeckAbilityEffect.GrantTemporaryPercentForNextDraws)
                {
                    card.RemainingDrawsByAbility.TryGetValue(i, out int remainingDraws);
                    if (remainingDraws > 0)
                        statusLines.Add(remainingDraws + Ui("\uD68C", " uses"));
                }
                if (ability.Effect == global::DeckAbilityEffect.AccumulateFlatScorePerDraw)
                {
                    card.AccumulatedFlatScoreByAbility.TryGetValue(i, out int accumulatedScore);
                    statusLines.Add(accumulatedScore + Ui("\uC810", " pts"));
                }
                if (IsStackThresholdEffect(ability.Effect)
                    || ability.Effect == global::DeckAbilityEffect.AddScoreEveryOtherCardScoreEvents)
                {
                    int threshold = Mathf.Max(1, ability.StackThreshold);
                    card.StackByAbilityCopy.TryGetValue(GetAbilityCopyKey(i, 0), out int currentStacks);
                    string stackText = currentStacks + "/" + threshold;
                    if (effectiveCopies > 1) stackText += " \u00D7" + effectiveCopies;
                    statusLines.Add(stackText);
                    if ((ability.Effect == global::DeckAbilityEffect.AddSpecificCardAtStackThreshold
                        || ability.Effect == global::DeckAbilityEffect.AddMinedMineralCardAtStackThreshold)
                        && ability.MaxTriggersPerPack > 0)
                    {
                        card.PerPackTriggerCountByAbility.TryGetValue(i, out int usedThisPack);
                        statusLines.Add(usedThisPack + Ui("\uD68C", " uses"));
                    }
                }
                if (ability.Effect == global::DeckAbilityEffect.AddScorePercentPerPackStack
                    || ability.Effect == global::DeckAbilityEffect.AddScorePerDecayingStack)
                {
                    int currentStacks = GetTotalAbilityStacks(card, i, effectiveCopies);
                    statusLines.Add(currentStacks.ToString());
                }
            }
            for (int i = 0; i < card.InheritedRelics.Count; i++)
            {
                StoredCard relic = card.InheritedRelics[i];
                string relicProgress = GetDeckProgressText(relic, false);
                if (string.IsNullOrWhiteSpace(relicProgress)) continue;
                string relicName = GetInheritedRelicShortName(relic);
                statusLines.Add(relicName + " " + relicProgress.Replace("\n", " / "));
            }
            return string.Join("\n", statusLines);
        }
        private static int GetTotalAbilityStacks(StoredCard card, int abilityIndex, int effectiveCopies)
        {
            int total = 0;
            for (int copy = 0; copy < effectiveCopies; copy++)
            {
                card.StackByAbilityCopy.TryGetValue(
                    GetAbilityCopyKey(abilityIndex, copy), out int stacks);
                total += Mathf.Max(0, stacks);
            }
            return total;
        }
        private string GetInheritedRelicShortName(StoredCard relic)
        {
            if (relic == null || relic.Data == null) return Ui("조립 가지", "Relic");
            return relic.Data.GetLocalizedShortStatusName(IsEnglishUi);
        }
        private static void DrawStatusLabelWithShadow(Rect rect, string text, GUIStyle style, Color color)
        {
            GUIStyle drawStyle = style;
            if (!style.wordWrap && style.fontSize > 0 && rect.width > 4f)
            {
                float availableWidth = rect.width - 4f;
                float maxLineWidth = 0f;
                string[] lines = text.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    float lineWidth = style.CalcSize(new GUIContent(lines[i])).x;
                    maxLineWidth = Mathf.Max(maxLineWidth, lineWidth);
                }
                if (maxLineWidth > availableWidth)
                {
                    drawStyle = new GUIStyle(style);
                    drawStyle.fontSize = Mathf.Max(9,
                        Mathf.FloorToInt(style.fontSize * availableWidth / maxLineWidth) - 1);
                }
            }
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, drawStyle);
            GUI.color = color;
            GUI.Label(rect, text, drawStyle);
            GUI.color = previousColor;
        }
        private void EnsureDeckStatusStyles()
        {
            if (deckStatusStyle != null) return;
            deckStatusStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            deckInspectionStatusStyle = new GUIStyle(deckStatusStyle)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleLeft
            };
        }
        private string GetRarityDisplayName(global::CardRarity rarity)
        {
            switch (rarity)
            {
                case global::CardRarity.Uncommon: return Ui("\uACE0\uAE09", "Uncommon");
                case global::CardRarity.Rare: return Ui("\uD76C\uADC0", "Rare");
                case global::CardRarity.Epic: return Ui("\uC601\uC6C5", "Epic");
                case global::CardRarity.Legendary: return Ui("\uC804\uC124", "Legendary");
                default: return Ui("\uC77C\uBC18", "Common");
            }
        }
        private static Color GetRarityDisplayColor(global::CardRarity rarity)
        {
            switch (rarity)
            {
                case global::CardRarity.Uncommon: return new Color(0.45f, 1f, 0.72f);
                case global::CardRarity.Rare: return new Color(0.72f, 0.88f, 1f);
                case global::CardRarity.Epic: return new Color(0.72f, 0.30f, 1f);
                case global::CardRarity.Legendary: return new Color(1f, 0.72f, 0.20f);
                default: return Color.white;
            }
        }
        private void EnsureDiscardStyles()
        {
            if (discardButtonStyle != null) return;
            roundedDiscardTexture = CreateRoundedBorderTexture(40, 10f, 3f);
            discardButtonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(12, 12, 12, 12),
                normal = { background = roundedDiscardTexture, textColor = Color.black },
                hover = { background = roundedDiscardTexture, textColor = Color.black },
                active = { background = roundedDiscardTexture, textColor = Color.black }
            };
            discardPanelStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(12, 12, 12, 12),
                normal = { background = roundedDiscardTexture }
            };
            discardMessageStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.84f, 0.3f) }
            };
            deckRarityStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }
        private static Texture2D CreateRoundedBorderTexture(int size, float radius, float borderWidth)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Rounded Black White Button",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float outerX = Mathf.Max(Mathf.Abs(px) - (half - radius), 0f);
                float outerY = Mathf.Max(Mathf.Abs(py) - (half - radius), 0f);
                bool insideOuter = outerX * outerX + outerY * outerY <= radius * radius;
                float innerHalf = half - borderWidth;
                float innerRadius = Mathf.Max(1f, radius - borderWidth);
                float innerX = Mathf.Max(Mathf.Abs(px) - (innerHalf - innerRadius), 0f);
                float innerY = Mathf.Max(Mathf.Abs(py) - (innerHalf - innerRadius), 0f);
                bool insideInner = Mathf.Abs(px) <= innerHalf && Mathf.Abs(py) <= innerHalf
                    && innerX * innerX + innerY * innerY <= innerRadius * innerRadius;
                pixels[y * size + x] = !insideOuter ? Color.clear : insideInner ? Color.white : Color.black;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
        private static Texture2D CreateSimpleSettingsIconTexture()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Simple Settings Icon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float distance = offset.magnitude;
                float angle = Mathf.Atan2(offset.y, offset.x);
                float outerRadius = Mathf.Cos(angle * 8f) > 0.45f ? 23f : 19f;
                pixels[y * size + x] = distance >= 12f && distance <= outerRadius
                    ? new Color(0.03f, 0.03f, 0.03f, 1f) : Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
        private void DiscardInspectedDeckCard()
        {
            int index = inspectedDeckIndex;
            if (index < 0 || index >= deckCards.Count || index >= deckVisuals.Count) return;
            GameObject discardedVisual = deckVisuals[index];
            StoredCard discardedCard = deckCards[index];
            if (discardedCard != null)
            {
                discardedCard.IsStoredInDeck = false;
                discardedCard.DeckSlot = -1;
                if (discardedCard.EquippedMagic != null)
                {
                    discardedCard.EquippedMagic.IsStoredInDeck = false;
                    discardedCard.EquippedMagic.DeckSlot = -1;
                }
                if (discardedCard.EquippedWeapon != null)
                {
                    discardedCard.EquippedWeapon.IsStoredInDeck = false;
                    discardedCard.EquippedWeapon.DeckSlot = -1;
                }
            }
            deckCards.RemoveAt(index);
            deckVisuals.RemoveAt(index);
            if (discardedVisual != null) Destroy(discardedVisual);
            RefreshDeckCardDisplayNames();
            discardConfirmationVisible = false;
            CloseDeckInspection();
        }
        private void DrawDeck(float scale, float offsetX, float offsetY)
        {
            if (deckHeaderStyle == null)
            {
                deckHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = Color.white }
                };
            }
            if (startingHandVisible) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            bool resultScreen = phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared;
            Rect deckHeaderRect = resultScreen && !IsPortraitUi
                ? new Rect(470f, 545f, 260f, 34f)
                : UiRect(new Rect(24f, 516f, 260f, 34f), new Rect(75f, 975f + PortraitExtraHeight, 260f, 42f));
            GUI.Label(deckHeaderRect, Ui("\uB371  ", "Deck  ") + deckCards.Count + "/5", deckHeaderStyle);
            EnsureDeckStatusStyles();
            for (int i = 0; i < deckCards.Count; i++)
            {
                StoredCard card = deckCards[i];
                if (card == null) continue;
                string progressText = GetDeckProgressText(card, false);
                if (string.IsNullOrEmpty(progressText)) continue;
                int slot = Mathf.Clamp(card.DeckSlot, 0, 4);
                int progressLineCount = progressText.Split('\n').Length;
                float portraitStatusExtraHeight = Mathf.Max(0, progressLineCount - 1) * 22f;
                float landscapeStatusExtraHeight = Mathf.Max(0, progressLineCount - 1) * 22f;
                Rect statusRect = IsPortraitUi
                    ? new Rect(90f + slot * 110f,
                        1050f + PortraitExtraHeight - portraitStatusExtraHeight,
                        100f, 48f + portraitStatusExtraHeight)
                    : (resultScreen
                        ? new Rect(430f + slot * 85f, 660f - landscapeStatusExtraHeight,
                            80f, 40f + landscapeStatusExtraHeight)
                        : new Rect(14f + slot * 74.25f, 654f - landscapeStatusExtraHeight,
                            80f, 40f + landscapeStatusExtraHeight));
                DrawStatusLabelWithShadow(statusRect,
                    progressText, deckStatusStyle, new Color(0.55f, 0.95f, 1f));
            }
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }
        private void DrawUsedPileHeader(float scale, float offsetX, float offsetY)
        {
            if (deckHeaderStyle == null) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            Rect headerRect = IsPortraitUi
                ? new Rect(0f, 975f + PortraitExtraHeight, 190f, 42f)
                : new Rect(0f, 516f, 220f, 34f);
            GUI.Label(headerRect, Ui("사용한 잎 더미", "Used card pile"), deckHeaderStyle);
            GUI.matrix = previousMatrix;
        }
    }
}
