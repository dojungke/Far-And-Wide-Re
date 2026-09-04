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
        private void Awake()
        {
            QualitySettings.shadows = ShadowQuality.Disable;
            LoadUserSettings();
            SetupScene();
            StartNewRun();
            TryLoadSharedResultFromUrl();
#if UNITY_WEBGL && !UNITY_EDITOR
            CardOpenReportReady();
#endif
        }
        private bool IsEnglishUi { get { return uiLanguage == 1; } }
        private string Ui(string korean, string english)
        {
            return IsEnglishUi ? english : korean;
        }
        private void LoadUserSettings()
        {
            uiLanguage = Mathf.Clamp(PlayerPrefs.GetInt("CardOpen.UiLanguage", 0), 0, 1);
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("CardOpen.MasterVolume", 1f));
            AudioListener.volume = masterVolume;
        }
        private void SaveUserSettings()
        {
            PlayerPrefs.SetInt("CardOpen.UiLanguage", uiLanguage);
            PlayerPrefs.SetFloat("CardOpen.MasterVolume", masterVolume);
            PlayerPrefs.Save();
        }
        private void SetUiLanguage(int language)
        {
            int clamped = Mathf.Clamp(language, 0, 1);
            if (uiLanguage == clamped) return;
            uiLanguage = clamped;
            SaveUserSettings();
            RefreshLocalizedCardDisplays();
        }
        private void SetMasterVolume(float volume)
        {
            float clamped = Mathf.Clamp01(volume);
            if (Mathf.Approximately(masterVolume, clamped)) return;
            masterVolume = clamped;
            AudioListener.volume = masterVolume;
            SaveUserSettings();
        }
        private void ShareCurrentResult()
        {
            string url = BuildSharedResultUrl();
            if (string.IsNullOrEmpty(url))
            {
                shareFeedback = Ui("WebGL \uBE4C\uB4DC\uC5D0\uC11C \uACF5\uC720\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", "Sharing is available in the WebGL build.");
                shareFeedbackUntil = Time.unscaledTime + 3f;
                return;
            }
            string title = Ui("\uCE74\uB4DC\uD329 \uACB0\uACFC", "Card Pack Result");
            string message = Ui("총점 ", "Total score ") + totalScore.ToString("N0");
#if UNITY_WEBGL && !UNITY_EDITOR
            CardOpenShareResult(title, message, url);
            shareFeedback = Ui("\uACF5\uC720 \uCC3D\uC744 \uC5F4\uC5C8\uC2B5\uB2C8\uB2E4.", "Share dialog opened.");
#else
            GUIUtility.systemCopyBuffer = url;
            shareFeedback = Ui("\uACF5\uC720 \uB9C1\uD06C\uB97C \uBCF5\uC0AC\uD588\uC2B5\uB2C8\uB2E4.", "Share link copied.");
#endif
            shareFeedbackUntil = Time.unscaledTime + 3f;
        }
        private string BuildSharedResultUrl()
        {
            string baseUrl = Application.absoluteURL;
#if UNITY_EDITOR
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = EditorShareBaseUrl;
#endif
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;
            int hashIndex = baseUrl.IndexOf('#');
            if (hashIndex >= 0) baseUrl = baseUrl.Substring(0, hashIndex);
            int queryIndex = baseUrl.IndexOf('?');
            if (queryIndex >= 0) baseUrl = baseUrl.Substring(0, queryIndex);
            SharedResultData result = new SharedResultData
            {
                TotalScore = totalScore,
                RoundScore = roundScore,
                GoalIndex = currentGoalIndex,
                CompletedPacks = completedPacks,
                Cleared = phase == RevealPhase.RunCleared,
                Deck = new SharedCardData[deckCards.Count]
            };
            for (int i = 0; i < deckCards.Count; i++) result.Deck[i] = CaptureSharedCard(deckCards[i]);
            string payload = EncodeSharedResultBinary(result);
            return baseUrl + "?r=" + payload;
        }
        private static string EncodeSharedResult(string json)
        {
            byte[] source = Encoding.UTF8.GetBytes(json);
            using (MemoryStream output = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
                    gzip.Write(source, 0, source.Length);
                return "z." + ToBase64Url(output.ToArray());
            }
        }
        private static string DecodeSharedResult(string payload)
        {
            string decodedPayload = Uri.UnescapeDataString(payload);
            bool compressed = decodedPayload.StartsWith("z.", StringComparison.Ordinal);
            if (compressed) decodedPayload = decodedPayload.Substring(2);
            byte[] source = FromBase64Url(decodedPayload);
            if (!compressed) return Encoding.UTF8.GetString(source);
            using (MemoryStream input = new MemoryStream(source))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int total = 0;
                int read;
                while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += read;
                    if (total > 262144) throw new InvalidDataException("Shared result is too large.");
                    output.Write(buffer, 0, read);
                }
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
        private static string ToBase64Url(byte[] value)
        {
            return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
        private static byte[] FromBase64Url(string value)
        {
            string normalized = value.Replace('-', '+').Replace('_', '/');
            switch (normalized.Length % 4)
            {
                case 2: normalized += "=="; break;
                case 3: normalized += "="; break;
                case 1: throw new FormatException("Invalid shared result encoding.");
            }
            return Convert.FromBase64String(normalized);
        }
        private SharedCardData CaptureSharedCard(StoredCard card)
        {
            if (card == null || card.Data == null) return null;
            SharedCardData data = new SharedCardData
            {
                ResourceName = card.Data.name,
                Color = (int)card.Color,
                Number = card.Number,
                Rarity = (int)card.Rarity,
                DeckSlot = card.DeckSlot,
                CombinedCopies = card.CombinedCopies,
                CombinedHolographicCopies = card.CombinedHolographicCopies,
                IsHolographic = card.IsHolographic,
                GreenDiceDamageMultiplier = card.GreenDiceDamageMultiplier,
                EquippedMagic = CaptureSharedCard(card.EquippedMagic),
                EquippedWeapon = CaptureSharedCard(card.EquippedWeapon),
                AccumulatedFlatScore = CaptureIntValues(card.AccumulatedFlatScoreByAbility),
                RemainingDraws = CaptureIntValues(card.RemainingDrawsByAbility),
                Stacks = CaptureIntValues(card.StackByAbilityCopy),
                PerPackTriggers = CaptureIntValues(card.PerPackTriggerCountByAbility),
                PacksElapsed = CaptureIntValues(card.PacksElapsedByAbility),
                AccumulatedPercent = CaptureFloatValues(card.AccumulatedPercentByAbility)
            };
            data.InheritedRelics = new SharedCardData[card.InheritedRelics.Count];
            for (int i = 0; i < card.InheritedRelics.Count; i++)
                data.InheritedRelics[i] = CaptureSharedCard(card.InheritedRelics[i]);
            return data;
        }
        private static SharedIntValue[] CaptureIntValues(Dictionary<int, int> source)
        {
            SharedIntValue[] values = new SharedIntValue[source.Count];
            int index = 0;
            foreach (KeyValuePair<int, int> pair in source)
                values[index++] = new SharedIntValue { Key = pair.Key, Value = pair.Value };
            return values;
        }
        private static SharedFloatValue[] CaptureFloatValues(Dictionary<int, float> source)
        {
            SharedFloatValue[] values = new SharedFloatValue[source.Count];
            int index = 0;
            foreach (KeyValuePair<int, float> pair in source)
                values[index++] = new SharedFloatValue { Key = pair.Key, Value = pair.Value };
            return values;
        }
        private bool TryLoadSharedResultFromUrl()
        {
            string compactPayload = GetQueryValue(Application.absoluteURL, "r");
            string legacyPayload = GetQueryValue(Application.absoluteURL, "cardopenResult");
            if (string.IsNullOrEmpty(compactPayload) && string.IsNullOrEmpty(legacyPayload)) return false;
            try
            {
                SharedResultData result;
                string json;
                if (!string.IsNullOrEmpty(compactPayload))
                {
                    result = DecodeSharedResultBinary(compactPayload);
                    json = JsonUtility.ToJson(result);
                }
                else
                {
                    json = DecodeSharedResult(legacyPayload);
                    result = JsonUtility.FromJson<SharedResultData>(json);
                }
                if (result == null || result.Version != 1) return false;
                sharedResultSnapshotJson = json;
                RestoreSharedResult(result);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not load shared card result: " + exception.Message);
                return false;
            }
        }
        private static string GetQueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url)) return null;
            int queryIndex = url.IndexOf('?');
            if (queryIndex < 0 || queryIndex + 1 >= url.Length) return null;
            int fragmentIndex = url.IndexOf('#', queryIndex + 1);
            string query = fragmentIndex >= 0
                ? url.Substring(queryIndex + 1, fragmentIndex - queryIndex - 1)
                : url.Substring(queryIndex + 1);
            string[] entries = query.Split('&');
            for (int i = 0; i < entries.Length; i++)
            {
                int equalsIndex = entries[i].IndexOf('=');
                string entryKey = equalsIndex >= 0 ? entries[i].Substring(0, equalsIndex) : entries[i];
                if (!string.Equals(entryKey, key, StringComparison.Ordinal)) continue;
                return equalsIndex >= 0 ? entries[i].Substring(equalsIndex + 1) : string.Empty;
            }
            return null;
        }
        private void RestoreSharedResult(SharedResultData result)
        {
            CloseDeckInspection();
            ClearPackChoiceVisuals();
            ClearCards();
            for (int i = 0; i < deckVisuals.Count; i++)
                if (deckVisuals[i] != null) Destroy(deckVisuals[i]);
            deckVisuals.Clear();
            deckCards.Clear();
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(false);
            totalScore = Mathf.Max(0, result.TotalScore);
            roundScore = Mathf.Max(0, result.RoundScore);
            currentGoalIndex = Mathf.Clamp(result.GoalIndex, 0, GoalScores.Length);
            completedPacks = Mathf.Max(0, result.CompletedPacks);
            pendingScore = 0;
            pendingScoreCommitTime = -1f;
            scoreTransferAmount = 0;
            scoreTransferApplied = 0;
            scoreTransferStartTime = -1f;
            scorePopups.Clear();
            global::CardData[] resources = Resources.LoadAll<global::CardData>(string.Empty);
            Dictionary<string, global::CardData> lookup = new Dictionary<string, global::CardData>();
            for (int i = 0; i < resources.Length; i++)
                if (resources[i] != null && !lookup.ContainsKey(resources[i].name))
                    lookup.Add(resources[i].name, resources[i]);
            if (result.Deck != null)
            {
                for (int i = 0; i < result.Deck.Length && deckCards.Count < 5; i++)
                {
                    StoredCard card = RestoreSharedCard(result.Deck[i], lookup);
                    if (card == null) continue;
                    card.IsStoredInDeck = true;
                    deckCards.Add(card);
                    deckVisuals.Add(BuildDeckVisualForStoredCard(card));
                }
            }
            sharedResultMode = true;
            phase = result.Cleared ? RevealPhase.RunCleared : RevealPhase.GameOver;
            RefreshEnemyVisual();
            RefreshDeckCardDisplayNames();
            LayoutDeckVisuals();
        }
        private StoredCard RestoreSharedCard(SharedCardData source,
            Dictionary<string, global::CardData> lookup)
        {
            if (source == null || string.IsNullOrEmpty(source.ResourceName)
                || !lookup.TryGetValue(source.ResourceName, out global::CardData data)) return null;
            StoredCard card = new StoredCard
            {
                Name = data.Name,
                Data = data,
                Rarity = (global::CardRarity)source.Rarity,
                Color = (global::CardColor)source.Color,
                Number = source.Number,
                DeckSlot = source.DeckSlot,
                CombinedCopies = Mathf.Max(1, source.CombinedCopies),
                CombinedHolographicCopies = Mathf.Max(0, source.CombinedHolographicCopies),
                IsHolographic = source.IsHolographic,
                GreenDiceDamageMultiplier = source.GreenDiceDamageMultiplier > 0f ? source.GreenDiceDamageMultiplier : 1f,
                IsStoredInDeck = true
            };
            card.EquippedMagic = RestoreSharedCard(source.EquippedMagic, lookup);
            card.EquippedWeapon = RestoreSharedCard(source.EquippedWeapon, lookup);
            if (source.InheritedRelics != null)
                for (int i = 0; i < source.InheritedRelics.Length; i++)
                {
                    StoredCard relic = RestoreSharedCard(source.InheritedRelics[i], lookup);
                    if (relic != null) card.InheritedRelics.Add(relic);
                }
            RestoreIntValues(source.AccumulatedFlatScore, card.AccumulatedFlatScoreByAbility);
            RestoreIntValues(source.RemainingDraws, card.RemainingDrawsByAbility);
            RestoreIntValues(source.Stacks, card.StackByAbilityCopy);
            RestoreIntValues(source.PerPackTriggers, card.PerPackTriggerCountByAbility);
            RestoreIntValues(source.PacksElapsed, card.PacksElapsedByAbility);
            RestoreFloatValues(source.AccumulatedPercent, card.AccumulatedPercentByAbility);
            RemoveLegacySatelliteRelics(card);
            return card;
        }
        private static void RemoveLegacySatelliteRelics(StoredCard card)
        {
            if (card == null || card.Data == null || !card.Data.ClearInheritedRelicsOnLoad)
                return;
            card.InheritedRelics.Clear();
        }
        private static void RestoreIntValues(SharedIntValue[] source, Dictionary<int, int> target)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null) target[source[i].Key] = source[i].Value;
        }
        private static void RestoreFloatValues(SharedFloatValue[] source, Dictionary<int, float> target)
        {
            if (source == null) return;
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null) target[source[i].Key] = source[i].Value;
        }
        private void LateUpdate()
        {
            UpdatePendingScore();
            if (Screen.width != lastResponsiveLayoutWidth || Screen.height != lastResponsiveLayoutHeight)
            {
                lastResponsiveLayoutWidth = Screen.width;
                lastResponsiveLayoutHeight = Screen.height;
                if (startingHandVisible && draggedHandIndex < 0 && pressedHandIndex < 0)
                    LayoutStartingHand();
                if (combatDeckInspectionVisible)
                    LayoutCombatDeckInspectionVisuals();
                if (stageSelectionVisible)
                {
                    LayoutStageSelectionHand();
                    LayoutStageDiscardPile();
                    LayoutStageSelectionCharacter();
                }
                if (packContentsPreviewVisual != null)
                    LayoutPackContentsPreviewCard();
                if (background != null && Camera.main != null)
                    LayoutBackground(Camera.main);
                if (deckRoot != null) LayoutDeckVisuals();
                if (usedPileRoot != null) LayoutUsedCardPile();
                enemyVisualStatusHash = int.MinValue;
                runtimeUiStateHash = int.MinValue;
                UpdateRuntimeUiRootLayout();
            }
            if (pack != null && phase == RevealPhase.Pack)
            {
                Vector3 targetScale = Vector3.one * ResponsiveWorldScale(1.95f, 1.50f);
                if (pack.transform.localScale != targetScale) pack.transform.localScale = targetScale;
                if (!gestureDragging && !inspectionDragging && pack.transform.position != CurrentPackHome)
                    pack.transform.position = CurrentPackHome;
            }
            if (packTearInProgress)
            {
                if (pack != null)
                {
                    Vector3 targetScale = Vector3.one * ResponsiveWorldScale(1.95f, 1.50f);
                    if (pack.transform.position != CurrentPackHome) pack.transform.position = CurrentPackHome;
                    if (pack.transform.localScale != targetScale) pack.transform.localScale = targetScale;
                }
                if (cardStack != null)
                {
                    Vector3 tearCardOffset = IsPortraitUi ? Vector3.zero : PackedCardOffset;
                    Vector3 targetPosition = CardHome + tearCardOffset - cardStack.rotation * CardHome;
                    if (cardStack.position != targetPosition) cardStack.position = targetPosition;
                }
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null && cards[i].gameObject.activeSelf)
                        cards[i].transform.localScale = Vector3.one * CurrentRevealedCardScale;
            }
            if (!startingHandVisible && enemyTurnRoutine == null && (phase == RevealPhase.CardBack || phase == RevealPhase.CardFront))
            {
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null && cards[i].gameObject.activeSelf)
                        cards[i].transform.localScale = Vector3.one * CurrentRevealedCardScale;
            }
            bool blockCardHover = settingsOpen || abandonConfirmationVisible || phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared;
            if (blockCardHover)
            {
                ResetHandHoverVisuals();
                discardPileHovered = false;
            }
            else
            {
                UpdateHandCardHover();
                UpdateStageHandHover();
                UpdateDiscardPileHover();
            }
            RefreshRuntimeUiOnStateChange();
            if (scorePopups.Count > 0 || canvasScorePopupsActive) UpdateCanvasScorePopups(); // Time-based animation only.
        }
        private void RefreshRuntimeUiOnStateChange()
        {
            int hash = 17;
            hash = hash * 31 + Screen.width;
            hash = hash * 31 + Screen.height;
            hash = hash * 31 + uiLanguage;
            hash = hash * 31 + Mathf.RoundToInt(masterVolume * 1000f);
            hash = hash * 31 + (int)phase;
            hash = hash * 31 + (stageSelectionVisible ? 1 : 0);
            hash = hash * 31 + (startingHandVisible ? 1 : 0);
            hash = hash * 31 + (settingsOpen ? 1 : 0);
            hash = hash * 31 + (abandonConfirmationVisible ? 1 : 0);
            hash = hash * 31 + (combatDeckInspectionVisible ? 1 : 0);
            hash = hash * 31 + (stageDeckInspectionVisible ? 1 : 0);
            hash = hash * 31 + (usedPileExpanded ? 1 : 0);
            hash = hash * 31 + (discardPileHovered ? 1 : 0);
            hash = hash * 31 + (inspectedDeckIndex + 1);
            hash = hash * 31 + playerHealth;
            hash = hash * 31 + playerShield;
            hash = hash * 31 + lightStoryUseCount;
            hash = hash * 31 + playerBurn;
            hash = hash * 31 + playerWood;
            hash = hash * 31 + playerRegeneration;
            hash = hash * 31 + playerStun;
            hash = hash * 31 + playerBindDuration;
            hash = hash * 31 + playerScales;
            hash = hash * 31 + playerBleedingStacks.Count;
            hash = hash * 31 + gold;
            hash = hash * 31 + totalScore;
            hash = hash * 31 + roundScore;
            hash = hash * 31 + completedPacks;
            hash = hash * 31 + currentGoalIndex;
            hash = hash * 31 + deckCards.Count;
            hash = hash * 31 + ownedRelics.Count;
            hash = hash * 31 + (rewardChoiceActive ? 1 : 0);
            hash = hash * 31 + (shopChoiceActive ? 1 : 0);
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyState enemy = enemies[i];
                hash = hash * 31 + (enemy != null ? enemy.Health : 0);
                hash = hash * 31 + (enemy != null ? enemy.ActionTurnsRemaining : 0);
                hash = hash * 31 + (enemy != null ? enemy.ActionDamage : 0);
                hash = hash * 31 + (enemy != null ? enemy.Shield : 0);
                hash = hash * 31 + (enemy != null ? enemy.Burn : 0);
                hash = hash * 31 + (enemy != null ? enemy.Wood : 0);
                hash = hash * 31 + (enemy != null ? enemy.Regeneration : 0);
                hash = hash * 31 + (enemy != null ? enemy.Stun : 0);
                hash = hash * 31 + (enemy != null ? enemy.Scales : 0);
                hash = hash * 31 + (enemy != null ? enemy.BleedingDurations.Count : 0);
            }
            if (hash == runtimeUiStateHash) return;
            runtimeUiStateHash = hash;

            UpdateCanvasCombatStatusIcons();
            UpdateCombatBuffVisuals();
            UpdateEnemyActionBuffVisuals();
            UpdateCombatRelicVisuals();
            UpdateCanvasCombatControls();
            UpdateCanvasPlayerHealthHud();
            UpdateCanvasEnemyHud();
            UpdateCanvasContextHud();
            UpdateCombatDeckInspectionToolbar();
            UpdateCanvasSettingsUi();
            UpdateCanvasRunEndUi();
            UpdateCanvasDeckInspectionControls();
            UpdateCanvasUsedPileInspectionHud();
            if (canvasEffectPopupRoot != null && !startingHandVisible) canvasEffectPopupRoot.SetActive(false);
        }
        private static void AppendUniqueCharacters(HashSet<char> characters, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            for (int i = 0; i < value.Length; i++) characters.Add(value[i]);
        }
        private void PrewarmCardTextCharacters()
        {
            if (font == null) return;
            global::CardData[] cardData = Resources.LoadAll<global::CardData>("Cards");
            HashSet<char> nameCharacters = new HashSet<char>();
            HashSet<char> descriptionCharacters = new HashSet<char>();
            for (int i = 0; i < cardData.Length; i++)
            {
                global::CardData data = cardData[i];
                if (data == null) continue;
                AppendUniqueCharacters(nameCharacters, data.Name);
                AppendUniqueCharacters(nameCharacters, data.EnglishName);
                AppendUniqueCharacters(descriptionCharacters, data.Description);
                AppendUniqueCharacters(descriptionCharacters, data.EnglishDescription);
                AppendUniqueCharacters(descriptionCharacters, data.Name);
                AppendUniqueCharacters(descriptionCharacters, data.EnglishName);
            }
            AppendUniqueCharacters(descriptionCharacters,
                "[자연, 마법, 룬, 무기, 마공학, 전사, 마법사, 스택, 광물, 채굴] 장착됨 현재 수준 등장 확률 점 회 팩 종료 초기화");
            CardVisual.PrewarmCardText(font,
                new string(new List<char>(nameCharacters).ToArray()),
                new string(new List<char>(descriptionCharacters).ToArray()));
        }
    }
}
