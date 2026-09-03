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
    public sealed partial class PackOnlyPrototype : MonoBehaviour
    {
        private sealed class ScorePopup
        {
            public string Text;
            public Color Color;
            public float StartTime;
            public int Lane;
            public int Score;
            public float PlaybackSpeed = 1f;
            public float AudioVolumeScale = 1f;
            public bool AddedToPendingScore;
            public bool SoundPlayed;
        }
        private enum PlannedActionInfo
        {
            Countdown,
            Damage,
            Bleeding
        }
        private sealed class EnemyState
        {
            public global::EnemyDefinition Definition;
            public int Health;
            public int Shield;
            public int Burn;
            public int Scales;
            // Each Bleeding application has its own remaining duration.
            public readonly List<int> BleedingDurations = new List<int>();
            public int ActionTurnsRemaining;
            public string Name => Definition != null ? Definition.EnemyName : string.Empty;
            public string EnglishName => Definition != null ? Definition.EnglishName : string.Empty;
            public int MaximumHealth => Definition != null ? Mathf.Max(1, Definition.MaximumHealth) : 1;
            public int ActionInterval => Definition != null ? Mathf.Max(1, Definition.ActionInterval) : 1;
            public int ActionDamage => Definition != null ? Definition.GetActionDamage() : 0;
            public int BleedingStacks => Definition != null ? Definition.GetActionBuffAmount("Bleeding") : 0;
            public string ActionName => Definition != null ? Definition.ActionName : string.Empty;
            public string EnglishActionName => Definition != null ? Definition.EnglishActionName : string.Empty;
            public bool IsDefeated => Health <= 0;
        }

        private sealed class CanvasEnemyHud
        {
            public GameObject Root;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Action;
            public Image ActionCountdownIcon;
            public TextMeshProUGUI ActionCountdownText;
            public Image ActionDamageIcon;
            public TextMeshProUGUI ActionDamageText;
            public TextMeshProUGUI Health;
            public Image HealthFill;
            public Image ShieldIcon;
            public TextMeshProUGUI ShieldAmount;
        }        private sealed class CanvasIconSlot
        {
            public GameObject Root;
            public Image Icon;
            public TextMeshProUGUI Amount;
        }
        private sealed class CanvasIconList
        {
            public GameObject Root;
            public readonly List<CanvasIconSlot> Slots = new List<CanvasIconSlot>();
        }        private sealed class StoredCard
        {
            public string Name;
            public global::CardData Data;
            public global::CombatCardType CombatType;
            public global::CardRarity Rarity;
            public global::CardColor Color;
            public int Number;
            public bool IsHolographic;
            public bool IsStoredInDeck;
            public int DeckSlot = -1;
            public int CombinedCopies = 1;
            public int CombinedHolographicCopies;
            public StoredCard EquippedMagic;
            public StoredCard EquippedWeapon;
            public readonly List<StoredCard> InheritedRelics = new List<StoredCard>();
            public readonly Dictionary<int, float> AccumulatedPercentByAbility =
                new Dictionary<int, float>();
            public readonly Dictionary<int, int> AccumulatedFlatScoreByAbility =
                new Dictionary<int, int>();
            public readonly Dictionary<int, int> RemainingDrawsByAbility =
                new Dictionary<int, int>();
            public readonly Dictionary<int, int> StackByAbilityCopy = new Dictionary<int, int>();
            public readonly Dictionary<int, int> TriggeredStackCountsThisDraw = new Dictionary<int, int>();
            public readonly HashSet<int> UsedOncePerPackAbilityCopies = new HashSet<int>();
            public readonly Dictionary<int, int> PerPackTriggerCountByAbility =
                new Dictionary<int, int>();
            public readonly Dictionary<int, int> PacksElapsedByAbility =
                new Dictionary<int, int>();
        }
        [Serializable]
        private sealed class SharedIntValue
        {
            public int Key;
            public int Value;
        }
        [Serializable]
        private sealed class SharedFloatValue
        {
            public int Key;
            public float Value;
        }
        [Serializable]
        private sealed class SharedCardData
        {
            public string ResourceName;
            public int Color;
            public int Number;
            public int Rarity;
            public int DeckSlot;
            public int CombinedCopies;
            public int CombinedHolographicCopies;
            public bool IsHolographic;
            public SharedCardData EquippedMagic;
            public SharedCardData EquippedWeapon;
            public SharedCardData[] InheritedRelics;
            public SharedIntValue[] AccumulatedFlatScore;
            public SharedIntValue[] RemainingDraws;
            public SharedIntValue[] Stacks;
            public SharedIntValue[] PerPackTriggers;
            public SharedIntValue[] PacksElapsed;
            public SharedFloatValue[] AccumulatedPercent;
        }
        [Serializable]
        private sealed class SharedResultData
        {
            public int Version = 1;
            public int TotalScore;
            public int RoundScore;
            public int GoalIndex;
            public int CompletedPacks;
            public bool Cleared;
            public SharedCardData[] Deck;
        }
        private enum RevealPhase { PackChoice, Pack, CardBack, CardFront, Animating, GameOver, RunCleared }
        private enum CombatDeckInspectionTarget { Deck, Discard, DrawPile }
        private enum StageDeckInspectionTarget { Deck, Discard, DrawPile }
        private const int FallbackCardsPerPack = 5;
        private const int PacksPerGoal = 3;
        private const int MaxSimultaneousEnemies = 3;
        private const int StartingHandSize = 5;
        private const int MaximumCombatHandSize = 10;
        private const int PlayerMaximumHealth = 100;
        private const int ScorePopupTrailCapacity = 5;
        private const string EditorShareBaseUrl = "https://dojungke.github.io/CardOpen/";
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;
        private const float PortraitWidth = 720f;
        private const float PortraitHeight = 1280f;
        private static readonly int[] GoalScores = { 3000, 10000, 25000, 40000, 60000 };
        private static readonly string[] EnemyNames =
        {
            "이끼 감시자", "심해 포식자", "화염 수호자", "폭풍 군주", "공허의 심장"
        };
        private static readonly string[] EnemyNamesEnglish =
        {
            "Moss Sentinel", "Abyss Devourer", "Ember Guardian", "Storm Sovereign", "Heart of the Void"
        };
        private const float RevealedCardScale = 1.5f;
        private static readonly Rect PackTearZone = new Rect(410f, 0f, 460f, 380f);
        private static readonly Rect CardGestureZone = new Rect(500f, 105f, 340f, 505f);
        private static readonly Vector3 PackHome = new Vector3(0f, 0.5f, -0.65f);
        private static readonly Vector3 CardHome = new Vector3(0f, 1.15f, -0.24f);
        private static readonly Vector3 PackedCardOffset = new Vector3(0f, -0.55f, 0f);
        private readonly List<CardVisual> cards = new List<CardVisual>();
        private readonly List<StoredCard> currentPackCards = new List<StoredCard>();
        // The authoritative deck for the current roguelike run. Draw and discard piles are rebuilt from this only when a new combat starts.
        private readonly List<global::CombatCard> runCombatDeck = new List<global::CombatCard>();
        private bool runCombatDeckInitialized;
        private readonly Queue<global::CombatCard> starterDrawPile =
            new Queue<global::CombatCard>();
        private readonly Queue<global::StageCardType> stageDrawPile = new Queue<global::StageCardType>();
        private readonly List<global::StageCardType> stageHand = new List<global::StageCardType>();
        private readonly List<global::StageCardType> stageDiscardPile = new List<global::StageCardType>();
        private readonly List<CardVisual> stageHandVisuals = new List<CardVisual>();
        private readonly List<CardVisual> stageDiscardPileVisuals = new List<CardVisual>();
        private Transform stageDiscardPileRoot;
        private CardVisual stageDiscardPilePlaceholder;
        private CardVisual stageDiscardPileTop;
        private global::CardData stageDiscardPilePlaceholderData;
        private TextMeshPro stageDiscardPileCountText;
        private GameObject stageSelectionCharacter;
        private SpriteRenderer stageSelectionCharacterRenderer;
        private GameObject restCharacter;
        private SpriteRenderer restCharacterRenderer;
        private GameObject combatPlayerCharacter;
        private SpriteRenderer combatPlayerCharacterRenderer;
        private GameObject choiceCharacter;
        private SpriteRenderer choiceCharacterRenderer;
        private readonly List<Vector3> stageHandHomePositions = new List<Vector3>();
        private readonly List<Quaternion> stageHandHomeRotations = new List<Quaternion>();
        private CardVisual highlightedStageHandCard;
        private int pressedStageHandIndex = -1;
        private int draggedStageHandIndex = -1;
        private Vector2 pressedStageHandScreenPosition;
        private Vector3 draggedStageHandStartPosition;
        private Vector2 lastStageHandHoverPointer;
        private bool hasStageHandHoverPointer;
        private bool stageHandHoverPointerDirty = true;
        private float stageHandHoverAnimationUntil;
        private global::StageCardType lastUsedStageCard;
        private bool eventChoiceActive;
        private int activeEventId;
        private string pendingRewardContextTitle;
        private string pendingRewardContextMessage;
        private bool stageChapterInitialized;
        private bool firstStageChoiceBonusAvailable = true;
        private bool finalBossStageSpawned;
        private bool stageSelectionVisible;
        private bool restStageActive;
        private readonly List<EnemyState> enemies = new List<EnemyState>();
        private int combatTurn;
        private int playerHealth = PlayerMaximumHealth;
        private int gold;
        private bool rewardChoiceActive;
        private bool shopChoiceActive;
        private int pendingOfferGold;
        private int shopDeckRemovalPrice = 50;
        private bool shopDeckRemovalSelectionActive;
        private bool shopOfferDrawPending = true;
        private sealed class ShopOffer
        {
            public int Price;
            public int Number;
            public int RemainingSalesPeriods;
            public int RarityTier;
            public int ChoiceCount;
            public global::CombatCard Card;
            public global::CombatRelicDefinition Relic;
            public bool IsRelic => Relic != null;
        }
        private readonly List<ShopOffer> shopOffers = new List<ShopOffer>();
        private sealed class ShopReward
        {
            public global::CombatCard Card;
            public global::CombatRelicDefinition Relic;
        }
        private bool shopRewardOpeningActive;
        private ShopOffer pendingShopRewardOffer;
        private readonly List<ShopReward> shopRewardCards = new List<ShopReward>();
        private int theFoolUseCount;
        private readonly List<int> playerBleedingStacks = new List<int>();
        private int playerShield;
        private int playerBurn;
        private int playerScales;
        private Texture2D clockTexture;
        private Texture2D attackTexture;
        private Texture2D bleedingTexture;
        private bool combatVisualAssetsLoaded;
        private int enemyVisualStatusHash = int.MinValue;
        private int enemyActionBuffVisualHash = int.MinValue;
        private global::CombatBuffDefinition shieldBuffDefinition;
        private global::CombatBuffDefinition burnBuffDefinition;
        private global::CombatBuffDefinition scalesBuffDefinition;
        private global::CombatBuffDefinition bleedingBuffDefinition;
        private global::CombatRelicDefinition goldCurrencyDefinition;
        private readonly List<global::CombatRelicDefinition> ownedRelics = new List<global::CombatRelicDefinition>();
        private int relicDamagePercentThisTurn;
        private readonly List<StoredCard> deckCards = new List<StoredCard>();
        private StoredCard previousRevealedCard;
        private StoredCard lastUsedCard;
        private int usedCastCount;
        private bool hasPlayedCardThisTurn;
        private StoredCard revealedMineralAbilityOwner;
        private readonly Dictionary<StoredCard, int> naturallyTriggeredNatureCounts = new Dictionary<StoredCard, int>();
        private readonly HashSet<StoredCard> pendingPackOpenNatureSources = new HashSet<StoredCard>();
        private bool natureAbilityChainActive;
        private int natureAbilityChainTriggerCount;
        private readonly List<GameObject> deckVisuals = new List<GameObject>();
        private readonly List<ScorePopup> scorePopups = new List<ScorePopup>();
        private int scorePopupBatchStartIndex;
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private PackVisual pack;
        private EnemyVisual enemyVisual;
        private readonly List<EnemyVisual> enemyVisuals = new List<EnemyVisual>();
        private PlayerCombatStatusVisual playerCombatStatusVisual;
        private int playerStatusVisualHash = int.MinValue;
        private CombatBuffListVisual playerBuffListVisual;
        private CombatRelicListVisual playerRelicListVisual;
        private readonly List<CombatBuffListVisual> enemyBuffListVisuals = new List<CombatBuffListVisual>();
        private readonly List<CombatBuffListVisual> enemyActionBuffListVisuals = new List<CombatBuffListVisual>();
        private readonly List<CombatBuffListVisual.Entry> playerBuffEntries = new List<CombatBuffListVisual.Entry>();
        private readonly List<CombatRelicListVisual.Entry> playerRelicEntries = new List<CombatRelicListVisual.Entry>();
        private readonly List<CombatBuffListVisual.Entry> enemyBuffEntries = new List<CombatBuffListVisual.Entry>();
        private readonly List<CombatBuffListVisual.Entry> enemyActionBuffEntries = new List<CombatBuffListVisual.Entry>();
        private int combatBuffVisualHash = int.MinValue;
        private int combatRelicVisualHash = int.MinValue;
        private readonly Dictionary<EnemyVisual, Coroutine> enemyDeathRoutines = new Dictionary<EnemyVisual, Coroutine>();
        private readonly Dictionary<EnemyVisual, Coroutine> enemyAttackRoutines = new Dictionary<EnemyVisual, Coroutine>();
        private Coroutine combatVictoryRoutine;
        private Coroutine combatEntryRoutine;
        private Coroutine enemyTurnRoutine;
        private float combatEntryFade;
        private Canvas runtimeUiCanvas;
        private RectTransform runtimeUiRoot;
        private TMP_FontAsset runtimeUiFont;
        private Font runtimeUiSourceFont;
        private Button canvasDeckButton;
        private TextMeshProUGUI canvasDeckButtonLabel;
        private Button canvasEndTurnButton;
        private Coroutine canvasEndTurnMoveRoutine;
        private GameObject canvasPlayerHealthRoot;
        private Image canvasPlayerHealthFill;
        private TextMeshProUGUI canvasPlayerHealthLabel;
        private Image canvasPlayerShieldIcon;
        private TextMeshProUGUI canvasPlayerShieldLabel;
        private readonly List<CanvasEnemyHud> canvasEnemyHuds = new List<CanvasEnemyHud>();
        private CanvasIconList canvasPlayerBuffList;
        private CanvasIconList canvasRelicList;
        private TextMeshProUGUI canvasContextTitle;
        private TextMeshProUGUI canvasContextMessage;
        private Material contextTextMaterial;
        private Button canvasLeaveShopButton;
        private Button canvasSettingsButton;
        private GameObject canvasSettingsRoot;
        private GameObject canvasAbandonConfirmationRoot;
        private TextMeshProUGUI canvasSettingsTitle;
        private TextMeshProUGUI canvasSettingsLanguageLabel;
        private TextMeshProUGUI canvasSettingsVolumeLabel;
        private Slider canvasSettingsVolumeSlider;
        private GameObject canvasRunEndRoot;
        private TextMeshProUGUI canvasRunEndTitle;
        private TextMeshProUGUI canvasRunEndBody;
        private Button canvasRunEndLeftButton;
        private Button canvasRunEndRightButton;
        private TextMeshProUGUI canvasPackChoiceTitle;
        private Button canvasLeftPackInfoButton;
        private Button canvasRightPackInfoButton;
        private Button canvasActivePackInfoButton;
        private readonly List<TextMeshProUGUI> canvasScorePopupLabels = new List<TextMeshProUGUI>();
        private bool canvasScorePopupsActive;
        private GameObject canvasPackContentsControlsRoot;
        private TextMeshProUGUI canvasPackContentsTitle;
        private TextMeshProUGUI canvasPackContentsCount;
        private Button canvasPackContentsPreviousButton;
        private Button canvasPackContentsNextButton;
        private GameObject canvasDeckInspectionControlsRoot;
        private TextMeshProUGUI canvasDeckInspectionRarity;
        private TextMeshProUGUI canvasDeckInspectionProgress;
        private Button canvasDeckInspectionDiscardButton;
        private GameObject canvasDeckInspectionConfirmation;
        private TextMeshProUGUI canvasUsedPileInspectionRarity;
        private GameObject canvasEffectPopupRoot;
        private Image canvasEffectPopupIcon;
        private TextMeshProUGUI canvasEffectPopupTitle;
        private TextMeshProUGUI canvasEffectPopupBody;
        private readonly List<CanvasIconList> canvasEnemyBuffLists = new List<CanvasIconList>();
        private readonly List<CanvasIconList> canvasEnemyActionBuffLists = new List<CanvasIconList>();
        private Image combatEntryFadeImage;
        private Image playerDamageFlashImage;
        private Coroutine playerDamageFlashRoutine;
        private global::EnemyDefinition defaultEnemyDefinition;
        private global::BattleEncounters normalBattleEncounters;
        private global::BattleEncounters selectedStageEncounters;
        private global::StageEnemyList currentStageEnemies;
        private PackTearVisual tearVisual;
        private Transform cardStack;
        private Transform deckRoot;
        private Transform usedPileRoot;
        private CardVisual usedPilePlaceholder;
        private CardVisual usedPileCard;
        private readonly List<CardVisual> usedPileHistory = new List<CardVisual>();
        private readonly List<global::CardData> usedPileCardData = new List<global::CardData>();
        private readonly List<StoredCard> usedPileStoredCards = new List<StoredCard>();
        private bool usedPileExpanded;
        private bool combatDeckInspectionVisible;
        private bool stageDeckInspectionVisible;
        private bool stageDeckInspectionMode;
        private readonly Dictionary<global::StageCardType, global::CombatCardType> stageInspectionTypes = new Dictionary<global::StageCardType, global::CombatCardType>();
        private StageDeckInspectionTarget stageDeckInspectionTarget;
        private CombatDeckInspectionTarget combatDeckInspectionTarget = CombatDeckInspectionTarget.Deck;
        private readonly List<global::CombatCard> combatDeckInspectionCards = new List<global::CombatCard>();
        private readonly List<RectTransform> combatDeckInspectionVisuals = new List<RectTransform>();
        private GameObject combatDeckInspectionUiRoot;
        private Image combatDeckInspectionUiBackdrop;
        private GameObject combatDeckInspectionToolbarRoot;
        private TextMeshProUGUI combatDeckInspectionEmptyLabel;
        private float combatDeckInspectionSceneScrollY;
        private bool combatDeckInspectionDragActive;
        private float combatDeckInspectionDragStartY;
        private float combatDeckInspectionDragStartScrollY;
        private Vector2 combatDeckInspectionDragStartPoint;
        private int combatDeckInspectionDetailIndex = -1;
        private CardVisual combatDeckInspectionDetailCard;
        private Sprite combatDeckInspectionDetailPreviousBackground;
        private Sprite combatDeckInspectionLegacyBackgroundSprite;
        private CardVisual usedPileDetailCard;
        private bool usedPileBackgroundDimmed;
        private Color usedPileBackgroundColor;
        private bool usedPileAnimating;
        private Coroutine usedPileRoutine;
        private global::CardData usedPilePlaceholderData;
        private TextMeshPro usedPileCountText;
        private TMP_FontAsset pileCountFontAsset;
        private Font pileCountSourceFont;
        private readonly List<GameObject> emptyDeckPlaceholders = new List<GameObject>();
        private GameObject background;
        private GameObject deckInspectionBackdrop;
        private int inspectedDeckIndex = -1;
        private bool inspectionPackWasActive;
        private bool inspectionStackWasActive;
        private bool deckInspectionDragging;
        private bool deckInspectionReturning;
        private bool deckInspectionPressOutside;
        private bool deckInspectionHasDragged;
        private Vector2 deckInspectionDragStart;
        private Quaternion deckInspectionStartRotation;
        private Coroutine deckInspectionReturnRoutine;
        private int pressedDeckIndex = -1;
        private bool deckCardDragActive;
        private Vector2 deckCardDragStart;
        [SerializeField] private global::CardPackData activePackData;
        [SerializeField] private global::CardPackPoolData packPoolData;
        [Header("Scene-Editable Layout")]
        [SerializeField, Range(1f, 20f)] private float pileCountTextFontSize = 20f;
        [SerializeField, Min(0.01f)] private float pileCountTextWorldScale = 0.15f;
        [SerializeField, Min(0.1f)] private float pileCountTextHeight = 2.30f;
        private global::CardPackData[] randomPackPool;
        private global::CardPackData leftPackChoice;
        private global::CardPackData rightPackChoice;
        private PackVisual leftPackChoiceVisual;
        private PackVisual rightPackChoiceVisual;
        private readonly List<Material> packChoiceMaterials = new List<Material>();
        private global::CardPackData inspectedPackChoice;
        private Vector2 packContentsScroll;
        private CardVisual packContentsPreviewVisual;
        private int packContentsPreviewIndex;
        private bool packContentsPackWasActive;
        private bool packContentsStackWasActive;
        private global::CardData[] fallbackCards;
        private global::CardData runtimeFallbackCard;
        private global::CardPackEntry runtimeFallbackEntry;
        private Font font;
        private AudioSource scorePopupAudioSource;
        private AudioClip scorePopupAudioClip;
        private AudioSource abilityEffectAudioSource;
        private AudioClip magicEquipAudioClip;
        private AudioClip runeResonanceAudioClip;
        private AudioSource packTearAudioSource;
        private AudioClip packTearAudioClip;
        private AudioSource cardRarityAudioSource;
        private readonly AudioClip[] cardRarityAudioClips = new AudioClip[5];
        private RevealPhase phase;
        private int cardIndex;
        private bool currentPackIsHolographic;
        private bool packTearInProgress;
        private bool runeResonanceWasActive;
        private bool gestureDragging;
        private bool inspectionDragging;
        private Vector2 dragStart;
        private Vector2 dragDelta;
        private Vector3 gestureStartPosition;
        private Quaternion gestureStartRotation;
        private bool startingHandVisible;
        private int draggedHandIndex = -1;
        private CardVisual highlightedHandCard;
        private bool discardPileHovered;
        private float discardPileHoverOffsetY;
        private int pressedHandIndex = -1;
        private Vector2 pressedHandScreenPosition;
        private bool draggedHandRaisedEnough;
        private Vector2 lastHandHoverPointer;
        private bool hasHandHoverPointer;
        private bool handHoverPointerDirty = true;
        private float handHoverAnimationUntil;
        private Vector3 draggedHandStartPosition;
        private Coroutine handLayoutRoutine;
        private int lastResponsiveLayoutWidth = -1;
        private int lastResponsiveLayoutHeight = -1;
        private int runtimeUiStateHash = int.MinValue;
        private readonly Vector3[] canvasHoverCorners = new Vector3[4];
        private readonly List<Vector3> startingHandHomePositions = new List<Vector3>();
        private readonly List<Quaternion> startingHandHomeRotations = new List<Quaternion>();
        private Transform inspectionTarget;
        private Quaternion inspectionStartRotation;
        private Vector3 inspectionPivotWorld;
        private Coroutine inspectionReturnRoutine;
        private CardVisual activeSlidingCard;
        private bool cardTransitionActive;
        private bool transitionDragActive;
        private bool transitionSwipeCommitted;
        private int queuedCardSwipes;
        private float queuedSwipeDirection;
        private int totalScore;
        private int roundScore;
        private int completedPacks;
        private int currentGoalIndex;
        private bool currentPackOpenedForGoal;
        private int pendingScore;
        private float pendingScoreCommitTime = -1f;
        private int scoreTransferAmount;
        private int scoreTransferApplied;
        private float scoreTransferStartTime = -1f;
        private GUIStyle effectPopupTitleStyle;
        private GUIStyle effectPopupBodyStyle;
        private readonly List<Vector2> enemyBuffScrollPositions = new List<Vector2>();
        private Vector2 playerBuffScrollPosition = Vector2.zero;
        private GUIStyle runEndTitleStyle;
        private GUIStyle runEndBodyStyle;
        private GUIStyle runEndButtonStyle;
        private GUIStyle runEndBadgeStyle;
        private GUIStyle runEndStatLabelStyle;
        private GUIStyle runEndStatValueStyle;
        private GUIStyle runEndHintStyle;
        private GUIStyle packContentsTitleStyle;
        private GUIStyle packContentsCardStyle;
        private GUIStyle scorePopupStyle;
        private GUIStyle deckHeaderStyle;
        private GUIStyle discardButtonStyle;
        private GUIStyle discardPanelStyle;
        private GUIStyle discardMessageStyle;
        private GUIStyle deckRarityStyle;
        private GUIStyle deckStatusStyle;
        private GUIStyle deckInspectionStatusStyle;
        private Texture2D roundedDiscardTexture;
        private Sprite roundedCanvasButtonSprite;
        private Texture2D settingsIconTexture;
        private bool discardConfirmationVisible;
        private bool settingsOpen;
        private bool abandonConfirmationVisible;
        private int uiLanguage;
        private float masterVolume = 1f;
        private GUIStyle settingsTitleStyle;
        private GUIStyle settingsLabelStyle;
        private bool sharedResultMode;
        private bool sharedPackPreviewActive;
        private string sharedResultSnapshotJson;
        private string shareFeedback;
        private float shareFeedbackUntil;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void CardOpenShareResult(string title, string text, string url);
        [DllImport("__Internal")]
        private static extern void CardOpenReportReady();
#endif
        private void ClearCards()
        {
            foreach (CardVisual card in cards)
            {
                if (card != null) Destroy(card.gameObject);
            }

            cards.Clear();
            currentPackCards.Clear();
        }
    }
}
