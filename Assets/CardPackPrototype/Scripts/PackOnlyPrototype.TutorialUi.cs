using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CardOpen.Prototype
{
    public sealed partial class PackOnlyPrototype
    {
        private enum TutorialFlowPhase
        {
            CardRules,
            StageSelection,
            CombatTarget,
            CombatEndTurn,
            CombatWaitEnemy,
            CombatRefillAttack,
            CombatRefillEndTurn,
            CombatFinish,
            Reward,
            ShopBuy,
            ShopRewardPack,
            ShopRewardChoice,
            ShopExit
        }

        private const int TutorialPracticeStepCount = 9;
        private TutorialFlowPhase tutorialFlowPhase;
        private global::CombatCardType tutorialAttackCardType;
        private int tutorialPracticeRequiredCardUses = 1;
        private GameObject canvasTutorialCompletionRoot;
        private TextMeshProUGUI canvasTutorialCompletionTitle;
        private TextMeshProUGUI canvasTutorialCompletionBody;
        private Button canvasTutorialCompletionButton;
        private bool tutorialCompletionVisible;
        private GameObject canvasTutorialReentryConfirmationRoot;
        private TextMeshProUGUI canvasTutorialReentryConfirmationTitle;
        private TextMeshProUGUI canvasTutorialReentryConfirmationBody;
        private bool tutorialReentryConfirmationVisible;


        private void RequestTutorialReentry()
        {
            if (!stageSelectionVisible || tutorialOpen || settingsOpen
                || sharedResultMode || completedPacks != 0 || deckCards.Count != 0) return;
            tutorialReentryConfirmationVisible = true;
            runtimeUiStateHash = int.MinValue;
        }

        private void ConfirmTutorialReentry()
        {
            tutorialReentryConfirmationVisible = false;
            TryOpenTutorial();
        }

        private void CancelTutorialReentry()
        {
            tutorialReentryConfirmationVisible = false;
            runtimeUiStateHash = int.MinValue;
        }

        private void TryOpenTutorial()
        {
            if (!stageSelectionVisible || completedPacks != 0 || deckCards.Count != 0) return;
            tutorialFlowPhase = TutorialFlowPhase.CardRules;
            tutorialPracticeStage = 0;
            tutorialOpen = true;
            ApplyTutorialStageDecorationVisibility();
            StartTutorialPractice();
            runtimeUiStateHash = int.MinValue;
        }

        private void ApplyTutorialStageDecorationVisibility()
        {
            if (!tutorialOpen) return;
            if (stageSelectionCharacter != null) stageSelectionCharacter.SetActive(false);
            if (stageDiscardPileRoot != null) stageDiscardPileRoot.gameObject.SetActive(false);
        }

        private void StartTutorialPractice()
        {
            stageSelectionVisible = false;
            ClearStageHand(true);
            ClearStageDiscardPileVisuals();
            restStageActive = false;
            eventChoiceActive = false;
            rewardChoiceActive = false;
            shopChoiceActive = false;
            startingHandVisible = true;
            tutorialReentryConfirmationVisible = false;
            phase = RevealPhase.CardFront;
            usedCastCount = 0;
            ClearCards();
            ClearUsedCardPile();
            if (cardStack != null)
            {
                cardStack.position = Vector3.zero;
                cardStack.rotation = Quaternion.identity;
                cardStack.localScale = Vector3.one;
                cardStack.gameObject.SetActive(true);
            }
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(true);
            PrepareTutorialPracticeStage(0);
        }

        private void PrepareTutorialPracticeStage(int stage)
        {
            tutorialPracticeStage = stage;
            tutorialPracticeRequiredCardUses = stage == 3 ? 2 : 1;
            ClearCards();
            ClearUsedCardPile();
            if (usedPileRoot != null) usedPileRoot.gameObject.SetActive(true);

            global::CardColor referenceColor;
            int referenceNumber;
            bool firstCardOfTurn;
            switch (stage)
            {
                case 0:
                    referenceColor = global::CardColor.Green;
                    referenceNumber = 1;
                    firstCardOfTurn = true;
                    AddTutorialPracticeCard(global::CardColor.Green, 4);
                    AddTutorialPracticeCard(global::CardColor.Red, 3);
                    AddTutorialPracticeCard(global::CardColor.Blue, 6);
                    break;
                case 1:
                    referenceColor = global::CardColor.Green;
                    referenceNumber = 4;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.Green, 5);
                    AddTutorialPracticeCard(global::CardColor.Green, 2);
                    AddTutorialPracticeCard(global::CardColor.Red, 3);
                    break;
                case 2:
                    referenceColor = global::CardColor.Green;
                    referenceNumber = 5;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.Blue, 5);
                    AddTutorialPracticeCard(global::CardColor.Red, 4);
                    AddTutorialPracticeCard(global::CardColor.White, 2);
                    break;
                case 3:
                    referenceColor = global::CardColor.Green;
                    referenceNumber = 1;
                    firstCardOfTurn = true;
                    AddTutorialPracticeCard(global::CardColor.Green, 4);
                    AddTutorialPracticeCard(global::CardColor.Green, 5);
                    AddTutorialPracticeCard(global::CardColor.Red, 2);
                    break;
                case 4:
                    referenceColor = global::CardColor.Red;
                    referenceNumber = 5;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.Blue, 5);
                    AddTutorialPracticeCard(global::CardColor.Green, 4);
                    AddTutorialPracticeCard(global::CardColor.White, 2);
                    break;
                case 5:
                    referenceColor = global::CardColor.Blue;
                    referenceNumber = 5;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.Blue, 4);
                    AddTutorialPracticeCard(global::CardColor.Red, 4);
                    AddTutorialPracticeCard(global::CardColor.Green, 2);
                    break;
                case 6:
                    referenceColor = global::CardColor.Blue;
                    referenceNumber = 1;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.Blue, 6);
                    AddTutorialPracticeCard(global::CardColor.Red, 6);
                    AddTutorialPracticeCard(global::CardColor.Green, 2);
                    break;
                case 7:
                    referenceColor = global::CardColor.Black;
                    referenceNumber = 3;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.White, 4);
                    AddTutorialPracticeCard(global::CardColor.Black, 1);
                    AddTutorialPracticeCard(global::CardColor.Red, 5);
                    break;
                default:
                    referenceColor = global::CardColor.Green;
                    referenceNumber = 5;
                    firstCardOfTurn = false;
                    AddTutorialPracticeCard(global::CardColor.Red, 2);
                    AddTutorialPracticeCard(global::CardColor.Blue, 3);
                    AddTutorialPracticeCard(global::CardColor.White, 6);
                    break;
            }
            ResetUsedPileReference(referenceColor, referenceNumber);
            hasPlayedCardThisTurn = !firstCardOfTurn;
            tutorialPracticeHandCount = cards.Count;
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            runtimeUiStateHash = int.MinValue;
        }

        private void AddTutorialPracticeCard(global::CardColor color, int number)
        {
            if (tutorialPracticeCardType == null)
            {
                global::CombatCardType source = Resources.Load<global::CombatCardType>("Combat/CardTypes/AccelerationMagic");
                tutorialPracticeCardType = ScriptableObject.CreateInstance<global::CombatCardType>();
                tutorialPracticeCardType.name = "TutorialPracticeCard";
                tutorialPracticeCardType.CardName = "연습 잎";
                tutorialPracticeCardType.Description = "카드 연결 규칙을 연습하는 잎입니다.";
                tutorialPracticeCardType.EnglishName = "Practice Card";
                tutorialPracticeCardType.EnglishDescription = "A card for practicing chain rules.";
                tutorialPracticeCardType.Rarity = global::CardRarity.Common;
                tutorialPracticeCardType.Image = source != null ? source.Image : null;
                tutorialPracticeCardType.RequiresEnemyTarget = false;
                tutorialPracticeCardType.Abilities.Clear();
            }
            AddStarterDeckCard(new global::CombatCard
            {
                Type = tutorialPracticeCardType,
                Color = color,
                Number = number
            });
        }

        private void PrepareTutorialStageSelection()
        {
            tutorialFlowPhase = TutorialFlowPhase.StageSelection;
            StartNewRun();

            ClearStageHand(true);
            ClearStageDiscardPileVisuals();
            stageDrawPile.Clear();
            string[] paths =
            {
                "Combat/Stages/일반전투",
                "Combat/Stages/휴식",
                "Combat/Stages/흰사건5"
            };
            for (int i = 0; i < paths.Length; i++)
            {
                global::StageCardType stage = Resources.Load<global::StageCardType>(paths[i]);
                if (stage == null) continue;
                stageHand.Add(stage);
                stageHandVisuals.Add(CreateStageCardVisual(stage));
            }

            stageChapterInitialized = true;
            completedStageCount = 0;
            firstStageChoiceBonusAvailable = true;
            stageSelectionVisible = true;
            startingHandVisible = false;
            CreateStageDiscardPile();
            LayoutStageSelectionHand();
            ApplyTutorialStageDecorationVisibility();
            RefreshStageCardInteractionStates();
            runtimeUiStateHash = int.MinValue;
        }

        private bool AllowTutorialStageSelection(int index, global::StageCardType stage)
        {
            if (!tutorialOpen || tutorialFlowPhase != TutorialFlowPhase.StageSelection
                || stage == null || stage.Kind == global::StageCardKind.Battle) return true;
            RestoreStageHandCard(index);
            AddScorePopup(Ui("튜토리얼\n일반 전투를 선택하세요.",
                "Tutorial\nChoose the normal battle."),
                new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            return false;
        }

        private bool AllowTutorialStageDiscard(int index)
        {
            if (!tutorialOpen || tutorialFlowPhase != TutorialFlowPhase.StageSelection) return true;
            RestoreStageHandCard(index);
            AddScorePopup(Ui("튜토리얼\n스테이지 잎을 버리지 말고 일반 전투를 선택하세요.",
                "Tutorial\nDo not discard a stage card; choose Battle."),
                new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            return false;
        }

        private void PrepareTutorialCombat()
        {
            tutorialFlowPhase = TutorialFlowPhase.CombatTarget;

            enemies.Clear();
            EnemyState enemy = CreateEnemyState(LoadDefaultEnemyDefinition());
            if (enemy != null)
            {
                // Three 7-damage tutorial attacks are intentional: first card, refill card, finisher.
                enemy.Health = 21;
                enemy.Shield = 0;
                enemy.ActionTurnsRemaining = 1;
                enemies.Add(enemy);
            }
            RefreshEnemyVisual();

            ClearCards();
            ClearUsedCardPile();
            starterDrawPile.Clear();
            ResetUsedPileReference(global::CardColor.Green, 1);
            hasPlayedCardThisTurn = false;
            AddTutorialAttackCard(global::CardColor.Green, 4);
            AddTutorialPracticeCard(global::CardColor.Red, 3);
            AddTutorialPracticeCard(global::CardColor.Blue, 6);
            AddTutorialPracticeCard(global::CardColor.Red, 1);
            AddTutorialPracticeCard(global::CardColor.White, 2);
            QueueTutorialCombatRefillCard(global::CardColor.Green, 4);
            QueueTutorialCombatRefillCard(global::CardColor.Green, 2);
            ConfigureTutorialCombatDeck();
            tutorialPracticeHandCount = cards.Count;
            startingHandVisible = true;
            phase = RevealPhase.CardFront;
            LayoutUsedCardPile();
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            runtimeUiStateHash = int.MinValue;
        }

        private void PrepareTutorialCombatFinisher()
        {
            // Keep the hand drawn during the tutorial turns so the finishing card is
            // presented as a normal start-of-turn refill, not as a replacement hand.
            tutorialFlowPhase = TutorialFlowPhase.CombatFinish;
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
            runtimeUiStateHash = int.MinValue;
        }

        private void QueueTutorialCombatRefillCard(global::CardColor color, int number)
        {
            global::CombatCardType type = tutorialAttackCardType != null
                ? tutorialAttackCardType : tutorialPracticeCardType;
            if (type == null) return;
            starterDrawPile.Enqueue(new global::CombatCard
            {
                Type = type,
                Color = color,
                Number = number
            });
        }

        private void ConfigureTutorialCombatDeck()
        {
            global::CombatCardType attackType = tutorialAttackCardType != null
                ? tutorialAttackCardType : tutorialPracticeCardType;
            if (attackType == null || tutorialPracticeCardType == null) return;
            runCombatDeck.Clear();
            runCombatDeckInitialized = true;
            AddTutorialCombatDeckCard(attackType, global::CardColor.Green, 4);
            AddTutorialCombatDeckCard(tutorialPracticeCardType, global::CardColor.Red, 3);
            AddTutorialCombatDeckCard(tutorialPracticeCardType, global::CardColor.Blue, 6);
            AddTutorialCombatDeckCard(tutorialPracticeCardType, global::CardColor.Red, 1);
            AddTutorialCombatDeckCard(tutorialPracticeCardType, global::CardColor.White, 2);
            AddTutorialCombatDeckCard(attackType, global::CardColor.Green, 4);
            AddTutorialCombatDeckCard(attackType, global::CardColor.Green, 2);
        }

        private void AddTutorialCombatDeckCard(global::CombatCardType type, global::CardColor color, int number)
        {
            runCombatDeck.Add(new global::CombatCard
            {
                Type = type,
                Color = color,
                Number = number
            });
        }

        private void AddTutorialAttackCard(global::CardColor color, int number)
        {
            if (tutorialAttackCardType == null)
                tutorialAttackCardType = Resources.Load<global::CombatCardType>("Combat/CardTypes/MagicBullet");
            if (tutorialAttackCardType == null)
            {
                Debug.LogError("Combat/CardTypes/MagicBullet is required for the combat tutorial.");
                AddTutorialPracticeCard(color, number);
                return;
            }
            AddStarterDeckCard(new global::CombatCard
            {
                Type = tutorialAttackCardType,
                Color = color,
                Number = number
            });
        }

        private void PrepareTutorialShop()
        {
            gold = Mathf.Max(gold, 1000);
            shopDeckRemovalPrice = 2000;
            BeginShopChoice();
            tutorialFlowPhase = TutorialFlowPhase.ShopBuy;
            runtimeUiStateHash = int.MinValue;
        }

        private bool AllowTutorialShopExit()
        {
            if (!tutorialOpen || tutorialFlowPhase != TutorialFlowPhase.ShopBuy) return true;
            AddScorePopup(Ui("튜토리얼\n먼저 상품 잎을 위로 드래그해 구매하세요.",
                "Tutorial\nFirst drag a product card upward to buy it."),
                new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            return false;
        }

        private bool AllowTutorialShopDiscard(int index)
        {
            if (!tutorialOpen || tutorialFlowPhase != TutorialFlowPhase.ShopBuy) return true;
            RestoreStartingHandCard(index);
            AddScorePopup(Ui("튜토리얼\n상품을 버리지 말고 위로 드래그해 구매하세요.",
                "Tutorial\nDo not discard the product; drag it upward to buy."),
                new Color(1f, 0.82f, 0.25f), Time.unscaledTime, scorePopups.Count, 0);
            return false;
        }

        private bool ShouldBlockTutorialDiscard()
        {
            if (!tutorialOpen) return false;
            if (tutorialFlowPhase == TutorialFlowPhase.CardRules)
                return tutorialPracticeStage < TutorialPracticeStepCount - 1;
            return tutorialFlowPhase == TutorialFlowPhase.CombatTarget
                || tutorialFlowPhase == TutorialFlowPhase.CombatEndTurn
                || tutorialFlowPhase == TutorialFlowPhase.CombatRefillAttack
                || tutorialFlowPhase == TutorialFlowPhase.CombatRefillEndTurn
                || tutorialFlowPhase == TutorialFlowPhase.CombatFinish;
        }

        private void UpdateTutorialPracticeProgress()
        {
            if (!tutorialOpen) return;
            ApplyTutorialStageDecorationVisibility();

            switch (tutorialFlowPhase)
            {
                case TutorialFlowPhase.CardRules:
                    int requiredRemainingCards = tutorialPracticeHandCount - tutorialPracticeRequiredCardUses;
                    if (cards.Count > requiredRemainingCards) return;
                    int nextStage = tutorialPracticeStage + 1;
                    if (nextStage < TutorialPracticeStepCount)
                        PrepareTutorialPracticeStage(nextStage);
                    else
                        PrepareTutorialStageSelection();
                    break;

                case TutorialFlowPhase.StageSelection:
                    if (!stageSelectionVisible && startingHandVisible
                        && combatEntryRoutine == null && enemies.Count > 0)
                        PrepareTutorialCombat();
                    break;

                case TutorialFlowPhase.CombatTarget:
                    if (cards.Count < tutorialPracticeHandCount)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.CombatEndTurn;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.CombatEndTurn:
                    if (!startingHandVisible)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.CombatWaitEnemy;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.CombatWaitEnemy:
                    if (startingHandVisible && enemyTurnRoutine == null)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.CombatRefillAttack;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.CombatRefillAttack:
                    if (cards.Count < tutorialPracticeHandCount)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.CombatRefillEndTurn;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.CombatRefillEndTurn:
                    if (startingHandVisible && enemyTurnRoutine == null)
                        PrepareTutorialCombatFinisher();
                    break;

                case TutorialFlowPhase.CombatFinish:
                    if (rewardChoiceActive)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.Reward;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.Reward:
                    if (shopChoiceActive)
                        PrepareTutorialShop();
                    break;

                case TutorialFlowPhase.ShopBuy:
                    if (shopRewardOpeningActive)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.ShopRewardPack;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.ShopRewardPack:
                    if (shopRewardOpeningActive && startingHandVisible
                        && phase == RevealPhase.CardFront)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.ShopRewardChoice;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.ShopRewardChoice:
                    if (!shopRewardOpeningActive && shopChoiceActive)
                    {
                        tutorialFlowPhase = TutorialFlowPhase.ShopExit;
                        runtimeUiStateHash = int.MinValue;
                    }
                    break;

                case TutorialFlowPhase.ShopExit:
                    if (!shopChoiceActive && stageSelectionVisible)
                        CompleteTutorial(true);
                    break;
            }
        }

        private void CompleteTutorial(bool showCompletion = false)
        {
            if (!tutorialOpen) return;
            tutorialOpen = false;
            tutorialCompletionVisible = showCompletion;
            runtimeUiStateHash = int.MinValue;
            StartNewRun();
        }

        private void DismissTutorialCompletion()
        {
            tutorialCompletionVisible = false;
            runtimeUiStateHash = int.MinValue;
        }
        private static readonly string[] TutorialPracticeTitles =
        {
            "첫 잎 사용",
            "같은 색 + 연속 숫자",
            "같은 숫자면 색 달라도 사용",
            "한 턴에 여러 장 사용",
            "유리한 색 강화",
            "숫자를 거꾸로 연결",
            "1과 6 연결",
            "검정과 하양",
            "손패에서 버리기"
        };
        private static readonly string[] TutorialPracticeBodies =
        {
            "차례마다 처음으로 사용하는 잎(카드)은 사용한 잎 더미의 잎과 같은 색상이나 숫자이면 사용할수있습니다..\n초록 4를 사용하세요. 어두운 잎은 사용할 수 없습니다.",
            "사용한 잎으로 사용한 잎 더미의 잎이 바뀌며두번째 잎 부터는 숫자가 같거나 같은 색상의 이어지는 숫자를 가진 잎만 사용할수있습니다..\n초록 5를 사용하세요. 초록 2는 같은 색이어도 사용할 수 없습니다.",
            "숫자가 같으면 색이 달라도 항상 사용할 수 있습니다.\n초록 5 위에 파랑 5를 사용하세요. 숫자가 이어지더라도 색이 다르면 사용할 수 없습니다.",
            "한 턴에 잎을 여러 장 연달아 사용할 수 있습니다.\n초록 4를 사용한 뒤 초록 5도 이어서 사용하세요.",
            "빨강(불) -> 파랑(물) -> 초록(풀) -> 빨강\n 유리한 색 순서로 연결하면 잎을 2번 발동합니다.\n파랑 5를 사용하세요.",
            "이어지는 숫자는 오름차순과 내림차순 모두 가능합니다.\n파랑 5 다음 파랑 4를 사용하세요.",
            "숫자 σ은 0과 6 모두로 취급하여 1, 5와 이어집니다.\n파랑 1 위에 파랑 6을 사용하세요.",
            "검정과 하양은 같은 색으로 취급되며 서로 이어서 사용할때 강화됩니다.\n검정 3 위에 하양 4를 사용하세요.",
            "필요 없는 잎은 오른쪽 사용한 잎 더미로 끌어 버릴 수 있습니다.\n카드를 버려도 사용한 잎 더미의 숫자는 바뀌지 않습니다. 어두운 잎 한 장을 버리세요."
        };
        private static readonly string[] TutorialPracticeEnglishTitles =
        {
            "First-card color",
            "Same color + adjacent",
            "Same number, any color",
            "Multiple cards in one turn",
            "Advantage enhancement",
            "Descending number",
            "Connect 1 and 6",
            "Black and white",
            "Discard from hand"
        };
        private static readonly string[] TutorialPracticeEnglishBodies =
        {
            "Your first card of the turn may ignore number adjacency only when its color matches the discard.\nPlay green 4. The two dim cards are unusable.",
            "After the first card, a color match also needs an adjacent number.\nPlay green 5. Green 2 is the same color but is unusable.",
            "Cards with the same number can be used even when their colors differ.\nPlay blue 5 on green 5. An adjacent number alone does not work when the color differs.",
            "You can use multiple cards consecutively in one turn.\nPlay green 4, then play green 5. Red 2 is unusable.",
            "An advantageous color sequence such as red to blue activates the effect twice.\nPlay blue 5.",
            "Adjacent numbers work in either direction.\nPlay blue 4 after blue 5.",
            "Numbers 1 and 6 are adjacent.\nPlay blue 6 on blue 1.",
            "Black and white connect as matching colors, and switching between them is enhanced.\nPlay white 4 on black 3.",
            "Drag an unwanted card to the discard pile on the right.\nDiscarding is not a cast, so it does not change the chain reference. Discard one dim card."
        };
        private static readonly string[] TutorialChapterTitles =
        {
            "스테이지 진행",
            "전투 1: 대상 지정 공격",
            "전투 2: 차례 종료",
            "전투 3: 손패 보충",
            "전투 4: 보충 후 한 번 공격",
            "전투 5: 차례 한 번 더 종료",
            "전투 6: 마무리 공격",
            "전투 보상",
            "상점 1: 상품 구매",
            "상점 2: 보상 팩 개봉",
            "상점 3: 보상 선택",
            "상점 4: 나가기"
        };

        private static readonly string[] TutorialChapterBodies =
        {
            "스테이지 잎은 같은 숫자 또는 검정·하양의 이웃 숫자로 연결해 사용합니다. 한 챕터에서 6곳을 진행하면 보스가 등장합니다.\n이번에는 잎을 위로 드래그해 일반 전투를 선택하세요.",
            "적의 체력과 행동 예정을 확인하세요.\n초록 4 ‘마법 총알’을 적에게 직접 드래그해 피해를 주세요. 어두운 잎은 현재 사용할 수 없습니다.",
            "차례 종료를 눌러도 손패의 남은 카드는 버려지지 않습니다.\n남은 카드는 다음 차례에도 그대로 유지됩니다. 오른쪽의 ‘차례 종료’를 누르세요.",
            "적의 차례가 끝나면 새 턴이 시작됩니다.\n현재 손패를 유지한 채 기본 손패 수인 5장까지 부족한 만큼 뽑습니다. 뽑을 더미가 비면 버린 잎을 섞어 다시 뽑습니다.",
            "보충된 손패에서 안내된 초록 4 ‘마법 총알’로 적을 한 번 공격하세요.\n이번 턴에는 카드 1장만 사용할 수 있습니다.",
            "한 장을 사용했으면 다시 오른쪽의 ‘차례 종료’를 누르세요.\n남은 손패도 버려지지 않은 채 다음 단계로 이어집니다.",
            "새 턴의 첫 잎도 버린 잎과 같은 색이면 사용할 수 있습니다.\n초록 2 ‘마법 총알’을 적에게 드래그해 전투를 끝내세요.",
            "전투가 끝나면 보상을 받습니다.\n보상 잎을 위로 드래그해 골드를 받고 상점으로 이동하세요.",
            "가격과 현재 골드를 비교하세요. 골드가 부족한 상품은 어둡게 표시됩니다. 덱 잎 제거는 덱의 카드 1장을 없애는 상품입니다.\n이번에는 다른 상품 잎 하나를 위로 드래그해 구매하세요.",
            "상품을 구매하면 카드나 가지를 고르는 보상 팩이 나옵니다.\n팩을 위쪽으로 드래그해 뜯으세요.",
            "팩에서 나온 보상 중 하나를 위로 드래그해 선택하세요.\n선택한 보상은 덱 또는 가지 목록에 추가됩니다.",
            "구매를 마치면 오른쪽의 ‘상점 나가기’를 누르세요.\n튜토리얼이 끝나고 새 게임의 스테이지 선택으로 돌아갑니다."
        };

        private static readonly string[] TutorialChapterEnglishTitles =
        {
            "Stage Progression",
            "Combat 1: Targeted attack",
            "Combat 2: End turn",
            "Combat 3: Refill hand",
            "Combat 4: One attack after refill",
            "Combat 5: End turn again",
            "Combat 6: Finishing attack",
            "Combat Reward",
            "Shop 1: Buy a product",
            "Shop 2: Open reward pack",
            "Shop 3: Choose reward",
            "Shop 4: Leave"
        };

        private static readonly string[] TutorialChapterEnglishBodies =
        {
            "Stage cards connect by matching numbers or adjacent black-white numbers. A boss appears after six locations in a chapter.\nDrag Battle upward this time.",
            "Check the enemy's health and action timer.\nDrag the green 4 Magic Bullet directly onto the enemy. Dim cards are currently unusable.",
            "Pressing End Turn does not automatically discard cards left in your hand.\nThey remain for the next turn. Press End Turn on the right.",
            "When the enemy turn ends, your next turn begins.\nKeep the current hand and draw until it reaches the base size of five. When the draw pile is empty, the discard pile is shuffled and reused.",
            "Use the instructed green 4 Magic Bullet from the refilled hand to hit the enemy once.\nOnly one card can be used in this turn.",
            "After using one card, press End Turn on the right again.\nThe remaining hand is kept and the tutorial moves to the finishing step.",
            "The first card of a new turn can match the discard color.\nDrag the green 2 Magic Bullet onto the enemy to finish combat.",
            "Combat grants a reward.\nDrag the reward card upward to receive gold and enter the shop.",
            "Compare prices with your gold. Unaffordable products are dim. Remove Deck Card permanently removes one card from the deck.\nDrag a different product upward to buy it this time.",
            "A purchase gives a reward pack containing a card or relic choice.\nDrag the pack upward to tear it open.",
            "Drag one reward from the pack upward.\nIt is added to your deck or relic list.",
            "Press Leave Shop on the right.\nThe tutorial ends and a fresh game starts at stage selection."
        };

        private int GetTutorialChapterTextIndex()
        {
            switch (tutorialFlowPhase)
            {
                case TutorialFlowPhase.StageSelection: return 0;
                case TutorialFlowPhase.CombatTarget: return 1;
                case TutorialFlowPhase.CombatEndTurn: return 2;
                case TutorialFlowPhase.CombatWaitEnemy: return 3;
                case TutorialFlowPhase.CombatRefillAttack: return 4;
                case TutorialFlowPhase.CombatRefillEndTurn: return 5;
                case TutorialFlowPhase.CombatFinish: return 6;
                case TutorialFlowPhase.Reward: return 7;
                case TutorialFlowPhase.ShopBuy: return 8;
                case TutorialFlowPhase.ShopRewardPack: return 9;
                case TutorialFlowPhase.ShopRewardChoice: return 10;
                default: return 11;
            }
        }

        private void EnsureCanvasTutorialUi()
        {
            EnsureRuntimeUiCanvas();
            if (canvasTutorialRoot != null) return;

            canvasTutorialRoot = new GameObject("Tutorial Overlay", typeof(RectTransform), typeof(Image));
            canvasTutorialRoot.transform.SetParent(runtimeUiRoot, false);
            RectTransform overlay = canvasTutorialRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            Image overlayImage = canvasTutorialRoot.GetComponent<Image>();
            overlayImage.color = Color.clear;
            overlayImage.raycastTarget = false;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(760f, 190f);
            panelRect.anchoredPosition = new Vector2(0f, -10f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, 0.96f);
            panelImage.raycastTarget = false;
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);

            canvasTutorialTitle = CreateSettingsText("Title", panel.transform, new Vector2(0f, -18f),
                new Vector2(690f, 46f), 30f, TextAlignmentOptions.Center);
            canvasTutorialBody = CreateSettingsText("Body", panel.transform, new Vector2(0f, -68f),
                new Vector2(690f, 72f), 19f, TextAlignmentOptions.Center);
            canvasTutorialProgress = CreateSettingsText("Progress", panel.transform, new Vector2(-130f, -145f),
                new Vector2(280f, 28f), 17f, TextAlignmentOptions.Center);
            canvasTutorialTitle.color = Color.black;
            canvasTutorialBody.color = Color.black;
            canvasTutorialProgress.color = Color.black;
            canvasTutorialProgress.gameObject.SetActive(false);
            canvasTutorialTitle.outlineColor = Color.white;
            canvasTutorialBody.outlineColor = Color.white;
            canvasTutorialProgress.outlineColor = Color.white;
            canvasTutorialTitle.outlineWidth = 0.12f;
            canvasTutorialBody.outlineWidth = 0.08f;
            canvasTutorialProgress.outlineWidth = 0.08f;

            canvasTutorialSkipButton = CreateSettingsButton("Skip", panel.transform, new Vector2(265f, -145f),
                new Vector2(160f, 44f), () => CompleteTutorial(), out TextMeshProUGUI skipLabel);
            Outline skipButtonOutline = canvasTutorialSkipButton.GetComponent<Outline>();
            if (skipButtonOutline != null) skipButtonOutline.enabled = false;
            skipLabel.text = Ui("건너뛰기", "Skip");
            canvasTutorialRoot.SetActive(false);
            canvasTutorialGuideButton = CreateCanvasButton("Tutorial Guide Button",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-160f, -28f), new Vector2(120f, 54f),
                RequestTutorialReentry, out TextMeshProUGUI guideLabel);
            Outline guideButtonOutline = canvasTutorialGuideButton.GetComponent<Outline>();
            if (guideButtonOutline != null) guideButtonOutline.enabled = false;
            guideLabel.text = Ui("안내", "Guide");
            EnsureTutorialCompletionPopup();
            EnsureTutorialReentryConfirmationPopup();
        }

        private void EnsureTutorialReentryConfirmationPopup()
        {
            if (canvasTutorialReentryConfirmationRoot != null) return;
            canvasTutorialReentryConfirmationRoot = new GameObject("Tutorial Reentry Confirmation", typeof(RectTransform), typeof(Image));
            canvasTutorialReentryConfirmationRoot.transform.SetParent(runtimeUiRoot, false);
            RectTransform overlay = canvasTutorialReentryConfirmationRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            Image overlayImage = canvasTutorialReentryConfirmationRoot.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
            overlayImage.raycastTarget = true;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(650f, 280f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = Color.white;
            panelImage.raycastTarget = true;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = Color.black;
            panelOutline.effectDistance = new Vector2(4f, -4f);

            canvasTutorialReentryConfirmationTitle = CreateSettingsText("Title", panel.transform, new Vector2(0f, -34f),
                new Vector2(580f, 54f), 32f, TextAlignmentOptions.Center);
            canvasTutorialReentryConfirmationBody = CreateSettingsText("Body", panel.transform, new Vector2(0f, -96f),
                new Vector2(560f, 96f), 21f, TextAlignmentOptions.Center);
            canvasTutorialReentryConfirmationTitle.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            canvasTutorialReentryConfirmationBody.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            canvasTutorialReentryConfirmationTitle.outlineColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            canvasTutorialReentryConfirmationBody.outlineColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            canvasTutorialReentryConfirmationTitle.outlineWidth = 0.10f;
            canvasTutorialReentryConfirmationBody.outlineWidth = 0.06f;

            CreateSettingsButton("Confirm Tutorial Reentry", panel.transform, new Vector2(-110f, -205f),
                new Vector2(170f, 58f), ConfirmTutorialReentry, out TextMeshProUGUI confirmLabel);
            confirmLabel.text = Ui("진입", "Enter");
            CreateSettingsButton("Cancel Tutorial Reentry", panel.transform, new Vector2(110f, -205f),
                new Vector2(170f, 58f), CancelTutorialReentry, out TextMeshProUGUI cancelLabel);
            cancelLabel.text = Ui("취소", "Cancel");
            canvasTutorialReentryConfirmationRoot.SetActive(false);
        }

        private void EnsureTutorialCompletionPopup()
        {
            if (canvasTutorialCompletionRoot != null) return;
            canvasTutorialCompletionRoot = new GameObject("Tutorial Completion Popup", typeof(RectTransform), typeof(Image));
            canvasTutorialCompletionRoot.transform.SetParent(runtimeUiRoot, false);
            RectTransform overlay = canvasTutorialCompletionRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            Image overlayImage = canvasTutorialCompletionRoot.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
            overlayImage.raycastTarget = true;

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 320f);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = Color.white;
            panelImage.raycastTarget = true;
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -4f);

            canvasTutorialCompletionTitle = CreateSettingsText("Title", panel.transform, new Vector2(0f, -30f),
                new Vector2(620f, 54f), 38f, TextAlignmentOptions.Center);
            canvasTutorialCompletionBody = CreateSettingsText("Body", panel.transform, new Vector2(0f, -95f),
                new Vector2(600f, 120f), 21f, TextAlignmentOptions.Center);
            canvasTutorialCompletionTitle.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            canvasTutorialCompletionBody.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            canvasTutorialCompletionTitle.outlineColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            canvasTutorialCompletionBody.outlineColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            canvasTutorialCompletionTitle.outlineWidth = 0.10f;
            canvasTutorialCompletionBody.outlineWidth = 0.06f;
            canvasTutorialCompletionButton = CreateSettingsButton("Start Game", panel.transform, new Vector2(0f, -252f),
                new Vector2(240f, 58f), DismissTutorialCompletion, out TextMeshProUGUI buttonLabel);
            buttonLabel.text = Ui("게임 시작", "Start Game");
            buttonLabel.color = Color.black;
            buttonLabel.outlineColor = Color.white;
            buttonLabel.outlineWidth = 0.10f;
            canvasTutorialCompletionRoot.SetActive(false);
        }

        private bool HandleTutorialReentryConfirmationPointer(Vector2 screenPoint, Event inputEvent)
        {
            if (!tutorialReentryConfirmationVisible) return false;
            if (inputEvent.type == EventType.MouseUp)
            {
                Vector2 point = ScreenToReferencePoint(screenPoint);
                if (new Rect(445f, 425f, 170f, 58f).Contains(point))
                    ConfirmTutorialReentry();
                else if (new Rect(665f, 425f, 170f, 58f).Contains(point))
                    CancelTutorialReentry();
            }
            if (inputEvent.type != EventType.Repaint) inputEvent.Use();
            return inputEvent.type != EventType.Repaint;
        }

        private bool HandleTutorialCompletionPointer(Vector2 screenPoint, Event inputEvent)
        {
            if (!tutorialCompletionVisible) return false;
            if (inputEvent.type == EventType.MouseUp)
            {
                Vector2 point = ScreenToReferencePoint(screenPoint);
                if (new Rect(515f, 445f, 250f, 70f).Contains(point))
                    DismissTutorialCompletion();
            }
            if (inputEvent.type != EventType.Repaint) inputEvent.Use();
            return inputEvent.type != EventType.Repaint;
        }

        private bool HandleTutorialPointer(Vector2 screenPoint, Event inputEvent)
        {
            if (!tutorialOpen || inputEvent.type != EventType.MouseUp) return false;
            Vector2 point = ScreenToReferencePoint(screenPoint);
            if (!new Rect(825f, 150f, 160f, 44f).Contains(point)) return false;
            CompleteTutorial();
            inputEvent.Use();
            return true;
        }

        private void UpdateCanvasTutorialCompletionUi()
        {
            EnsureTutorialCompletionPopup();
            canvasTutorialCompletionRoot.SetActive(tutorialCompletionVisible);
            if (!tutorialCompletionVisible) return;
            canvasTutorialCompletionRoot.transform.SetAsLastSibling();
            canvasTutorialCompletionTitle.text = Ui("튜토리얼 완료!", "Tutorial Complete!");
            canvasTutorialCompletionBody.text = Ui(
                "핵심 규칙과 카드 흐름을 모두 익혔습니다.\n‘게임 시작’을 눌러 실제 게임을 시작하세요.",
                "You have learned the core rules and card flow.\nPress Start Game to begin a real run.");
        }

        private void UpdateCanvasTutorialReentryUi()
        {
            EnsureTutorialReentryConfirmationPopup();
            canvasTutorialReentryConfirmationRoot.SetActive(tutorialReentryConfirmationVisible);
            if (!tutorialReentryConfirmationVisible) return;
            canvasTutorialReentryConfirmationRoot.transform.SetAsLastSibling();
            canvasTutorialReentryConfirmationTitle.text = Ui("튜토리얼에 다시 진입하시겠습니까?", "Enter the tutorial again?");
            canvasTutorialReentryConfirmationBody.text = Ui(
                "현재 화면을 초기화하고 튜토리얼을 시작합니다.",
                "The current screen will reset and the tutorial will begin.");
        }

        private void UpdateCanvasTutorialUi()
        {
            EnsureCanvasTutorialUi();
            UpdateCanvasTutorialCompletionUi();
            UpdateCanvasTutorialReentryUi();
            canvasTutorialRoot.SetActive(tutorialOpen && !tutorialReentryConfirmationVisible);
            if (canvasTutorialGuideButton != null)
                canvasTutorialGuideButton.gameObject.SetActive(!tutorialOpen && !tutorialReentryConfirmationVisible && !settingsOpen
                    && !sharedResultMode && stageSelectionVisible
                    && completedPacks == 0 && deckCards.Count == 0);
            if (!tutorialOpen) return;

            canvasTutorialRoot.transform.SetAsLastSibling();
            if (tutorialFlowPhase == TutorialFlowPhase.CardRules)
            {
                int index = Mathf.Clamp(tutorialPracticeStage, 0, TutorialPracticeTitles.Length - 1);
                canvasTutorialTitle.text = Ui(TutorialPracticeTitles[index],
                    TutorialPracticeEnglishTitles[index]);
                canvasTutorialBody.text = Ui(TutorialPracticeBodies[index],
                    TutorialPracticeEnglishBodies[index]);
                canvasTutorialProgress.text = Ui(
                    "카드 규칙 " + (index + 1) + " / " + TutorialPracticeStepCount,
                    "Card rules " + (index + 1) + " / " + TutorialPracticeStepCount);
                return;
            }

            int chapterIndex = GetTutorialChapterTextIndex();
            canvasTutorialTitle.text = Ui(TutorialChapterTitles[chapterIndex],
                TutorialChapterEnglishTitles[chapterIndex]);
            canvasTutorialBody.text = Ui(TutorialChapterBodies[chapterIndex],
                TutorialChapterEnglishBodies[chapterIndex]);
            canvasTutorialProgress.text = Ui(
                chapterIndex == 0 ? "스테이지 실습" : chapterIndex <= 5 ? "전투 실습" : "상점 실습",
                chapterIndex == 0 ? "Stage practice" : chapterIndex <= 5 ? "Combat practice" : "Shop practice");
        }
    }
}
