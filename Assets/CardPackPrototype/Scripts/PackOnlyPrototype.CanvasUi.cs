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
        private void EnsureRuntimeUiCanvas()
        {
            if (runtimeUiCanvas != null)
            {
                if (runtimeUiCanvas.GetComponent<GraphicRaycaster>() == null)
                    runtimeUiCanvas.gameObject.AddComponent<GraphicRaycaster>();
                ConfigureRuntimeUiCanvasRenderLayer();
                return;
            }
            GameObject canvasObject = new GameObject("Runtime UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            runtimeUiCanvas = canvasObject.GetComponent<Canvas>();
            ConfigureRuntimeUiCanvasRenderLayer();
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            GameObject rootObject = new GameObject("Reference UI Root", typeof(RectTransform));
            rootObject.transform.SetParent(canvasObject.transform, false);
            runtimeUiRoot = rootObject.GetComponent<RectTransform>();
            runtimeUiRoot.anchorMin = new Vector2(0.5f, 0.5f);
            runtimeUiRoot.anchorMax = new Vector2(0.5f, 0.5f);
            runtimeUiRoot.pivot = new Vector2(0.5f, 0.5f);
            UpdateRuntimeUiRootLayout();
        }
        private void ConfigureRuntimeUiCanvasRenderLayer()
        {
            if (runtimeUiCanvas == null) return;
            // Standard cards use sorting orders 2000+, while the active hand card uses 3000.
            // Keeping this canvas between them lets only hover/drag cards render in front of UI.
            runtimeUiCanvas.sortingOrder = 2500;
            Camera camera = Camera.main;
            if (camera == null) return;
            runtimeUiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            runtimeUiCanvas.worldCamera = camera;
            runtimeUiCanvas.planeDistance = 10f;
        }
        private void UpdateRuntimeUiRootLayout()
        {
            if (runtimeUiRoot == null) return;
            GetUiLayout(out _, out _, out _);
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
                safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
            runtimeUiRoot.sizeDelta = new Vector2(UiReferenceWidth, UiReferenceHeight);
            runtimeUiRoot.localScale = Vector3.one;
            runtimeUiRoot.anchoredPosition = Vector2.zero;
        }
        private void PrewarmRuntimeUiFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return;
            HashSet<char> characters = new HashSet<char>();
            AppendUniqueCharacters(characters,
                "덱 확인 턴 종료 스테이지 선택 전투 보상 상점 상점 나가기 설정 언어 음량 닫기 도전 포기 취소 다음 팩을 선택하세요 봉입 카드 표시할 카드가 없습니다 카드 버리기 이 카드를 버릴까요 사용한 카드 더미 공격 피해량 방어력 출혈 보호막 골드 유물 행동 예정");
            global::CardData[] cardData = Resources.LoadAll<global::CardData>("Cards");
            for (int i = 0; i < cardData.Length; i++)
            {
                global::CardData data = cardData[i];
                if (data == null) continue;
                AppendUniqueCharacters(characters, data.Name);
                AppendUniqueCharacters(characters, data.Description);
                AppendUniqueCharacters(characters, data.EnglishName);
                AppendUniqueCharacters(characters, data.EnglishDescription);
            }
            if (characters.Count > 0) fontAsset.TryAddCharacters(new string(new List<char>(characters).ToArray()), out _);
        }

        private TMP_FontAsset GetRuntimeUiFont()
        {
            if (runtimeUiFont != null && runtimeUiSourceFont == font) return runtimeUiFont;
            runtimeUiSourceFont = font;
            runtimeUiFont = CombatTextOutline.GetSharedFontAsset(font);
            PrewarmRuntimeUiFont(runtimeUiFont);
            return runtimeUiFont;
        }
        private void ApplyContextOutlinedFont(TextMeshProUGUI value, bool bold)
        {
            if (value == null) return;
            value.font = GetRuntimeUiFont();
            if (value.font != null && value.font.material != null)
            {
                if (contextTextMaterial == null) contextTextMaterial = UnityEngine.Object.Instantiate(value.font.material);
                value.fontSharedMaterial = contextTextMaterial;
                value.material = contextTextMaterial;
                contextTextMaterial.EnableKeyword("OUTLINE_ON");
                contextTextMaterial.SetColor(ShaderUtilities.ID_FaceColor, Color.white);
                contextTextMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                contextTextMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
                contextTextMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.35f);
                contextTextMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
                contextTextMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
                contextTextMaterial.DisableKeyword("UNDERLAY_ON");
            }
            value.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            value.color = Color.white;
            value.outlineColor = Color.black;
            value.outlineWidth = 0.35f;
            value.extraPadding = true;
            value.UpdateMeshPadding();
        }
        private void ApplyOutlinedValueFont(TextMeshProUGUI value)
        {
            if (value == null) return;
            value.font = GetRuntimeUiFont();
            value.fontStyle = FontStyles.Bold;
            CombatTextOutline.ApplySharedWhiteOutline(value);
        }

        private Material actionTimerMaterial;
        private TMP_FontAsset actionTimerMaterialFont;
        private void ApplyPlainActionTimerFont(TextMeshProUGUI value)
        {
            if (value == null || value.font == null || value.font.material == null) return;
            if (actionTimerMaterial == null || actionTimerMaterialFont != value.font)
            {
                actionTimerMaterialFont = value.font;
                actionTimerMaterial = UnityEngine.Object.Instantiate(value.font.material);
                actionTimerMaterial.name = "Combat Action Timer Black Preset";
                actionTimerMaterial.DisableKeyword("OUTLINE_ON");
                actionTimerMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
                actionTimerMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            }
            value.fontSharedMaterial = actionTimerMaterial;
            value.fontStyle = FontStyles.Bold;
            value.color = Color.black;
            value.outlineColor = Color.black;
            value.outlineWidth = 0f;
            value.extraPadding = false;
            value.UpdateMeshPadding();
        }
        private TextMeshProUGUI CreateCanvasHudLabel(string name, Vector2 anchoredPosition,
            Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(runtimeUiRoot, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = GetRuntimeUiFont();
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.color = Color.white;
            label.outlineColor = Color.black;
            label.outlineWidth = CombatTextOutline.OutlineThickness;
            label.enableWordWrapping = true;
            label.raycastTarget = false;
            return label;
        }
        private Sprite GetRoundedCanvasButtonSprite()
        {
            if (roundedCanvasButtonSprite != null) return roundedCanvasButtonSprite;
            if (roundedDiscardTexture == null) roundedDiscardTexture = CreateRoundedBorderTexture(40, 10f, 3f);
            roundedCanvasButtonSprite = Sprite.Create(roundedDiscardTexture,
                new Rect(0f, 0f, roundedDiscardTexture.width, roundedDiscardTexture.height),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
                new Vector4(12f, 12f, 12f, 12f));
            roundedCanvasButtonSprite.name = "Rounded Canvas Button";
            return roundedCanvasButtonSprite;
        }
        private Button CreateCanvasButton(string name, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size, UnityEngine.Events.UnityAction action,
            out TextMeshProUGUI label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(runtimeUiRoot, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = GetRoundedCanvasButtonSprite();
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            Outline border = buttonObject.AddComponent<Outline>();
            border.effectColor = Color.black;
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = false;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            label = CreateCanvasHudLabel("Label", Vector2.zero, size, 22f, TextAlignmentOptions.Center);
            RectTransform labelRect = label.rectTransform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.enableWordWrapping = false;
            label.color = Color.black;
            label.outlineColor = Color.white;
            label.outlineWidth = 0.12f;
            return button;
        }
        private void EnsureCanvasCombatControls()
        {
            EnsureRuntimeUiCanvas();
            if (canvasDeckButton != null) return;
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("Runtime UI EventSystem", typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventSystemObject.transform.SetParent(transform, false);
            }
            else if (eventSystem.GetComponent<BaseInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }
            canvasDeckButton = CreateCanvasButton("Deck Button", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -28f), new Vector2(190f, 48f), OpenCanvasDeckInspection,
                out canvasDeckButtonLabel);
            Outline deckButtonOutline = canvasDeckButton.GetComponent<Outline>();
            if (deckButtonOutline != null) deckButtonOutline.enabled = false;
            canvasEndTurnButton = CreateCanvasButton("End Turn Button", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-45f, 250f), new Vector2(190f, 48f), EndPlayerTurn,
                out _);
            Outline endTurnButtonOutline = canvasEndTurnButton.GetComponent<Outline>();
            if (endTurnButtonOutline != null) endTurnButtonOutline.enabled = false;
            canvasEndTurnButton.GetComponentInChildren<TextMeshProUGUI>().text = Ui("턴 종료", "End Turn");
        }
        private void OpenCanvasDeckInspection()
        {
            if (stageSelectionVisible) OpenStageDeckInspection();
            else OpenCombatDeckInspection();
        }
        private void UpdateCanvasCombatControls()
        {
            EnsureCanvasCombatControls();
            SetChoiceCharacterVisible(shopChoiceActive || eventChoiceActive || (rewardChoiceActive && cards.Count >= 2));
            bool isOverlayOpen = settingsOpen || combatDeckInspectionVisible || stageDeckInspectionVisible
                || usedPileExpanded || inspectedDeckIndex >= 0 || inspectedPackChoice != null;
            bool runEnded = phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared;
            bool showDeck = !isOverlayOpen && !runEnded && !rewardChoiceActive && !shopChoiceActive && !eventChoiceActive && !shopRewardOpeningActive;
            canvasDeckButton.gameObject.SetActive(true);
            canvasDeckButton.interactable = true;
            if (canvasDeckButtonLabel != null)
                canvasDeckButtonLabel.text = Ui("덱 확인", "View Deck");
            bool showEndTurn = showDeck && !stageSelectionVisible && !restStageActive && !eventChoiceActive;
            canvasEndTurnButton.gameObject.SetActive(showEndTurn);
            SetCanvasEndTurnButtonHoverOffset(discardPileHovered);
            bool canEndTurn = startingHandVisible && phase == RevealPhase.CardFront && playerHealth > 0
                && enemyTurnRoutine == null;
            canvasEndTurnButton.interactable = canEndTurn;
        }
        private void SetCanvasEndTurnButtonHoverOffset(bool raised)
        {
            if (canvasEndTurnButton == null) return;
            RectTransform rect = canvasEndTurnButton.GetComponent<RectTransform>();
            Vector2 target = new Vector2(-45f, raised ? 320f : 250f);
            if ((rect.anchoredPosition - target).sqrMagnitude < 0.01f) return;
            if (canvasEndTurnMoveRoutine != null) StopCoroutine(canvasEndTurnMoveRoutine);
            canvasEndTurnMoveRoutine = StartCoroutine(AnimateCanvasEndTurnButton(rect, target));
        }

        private IEnumerator AnimateCanvasEndTurnButton(RectTransform rect, Vector2 target)
        {
            Vector2 start = rect.anchoredPosition;
            const float duration = 0.16f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                rect.anchoredPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            rect.anchoredPosition = target;
            canvasEndTurnMoveRoutine = null;
        }
        private void UpdateCanvasPlayerHealthHud()
        {
            EnsureRuntimeUiCanvas();
            if (canvasPlayerHealthRoot == null)
            {
                canvasPlayerHealthRoot = new GameObject("Player Health HUD", typeof(RectTransform));
                canvasPlayerHealthRoot.transform.SetParent(runtimeUiRoot, false);
                RectTransform rootRect = canvasPlayerHealthRoot.GetComponent<RectTransform>();
                rootRect.anchorMin = new Vector2(0f, 1f);
                rootRect.anchorMax = new Vector2(0f, 1f);
                rootRect.pivot = new Vector2(0f, 1f);
                rootRect.anchoredPosition = new Vector2(24f, -650f);
                rootRect.sizeDelta = new Vector2(300f, 54f);
                GameObject background = new GameObject("Bar Background", typeof(RectTransform), typeof(Image));
                background.transform.SetParent(rootRect, false);
                RectTransform backgroundRect = background.GetComponent<RectTransform>();
                backgroundRect.anchorMin = new Vector2(0f, 0f);
                backgroundRect.anchorMax = new Vector2(1f, 0f);
                backgroundRect.pivot = new Vector2(0.5f, 0f);
                backgroundRect.anchoredPosition = Vector2.zero;
                backgroundRect.sizeDelta = new Vector2(0f, 26f);
                background.GetComponent<Image>().color = new Color(0.12f, 0.04f, 0.06f, 0.9f);
                GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(backgroundRect, false);
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(1f, 1f);
                fillRect.offsetMin = new Vector2(2f, 2f);
                fillRect.offsetMax = new Vector2(-2f, -2f);
                canvasPlayerHealthFill = fill.GetComponent<Image>();
                canvasPlayerHealthFill.color = new Color(0.87f, 0.23f, 0.27f, 1f);
                canvasPlayerHealthLabel = CreateCanvasHudLabel("Value", Vector2.zero,
                    new Vector2(300f, 28f), 18f, TextAlignmentOptions.Center);
                ApplyOutlinedValueFont(canvasPlayerHealthLabel);
                RectTransform labelRect = canvasPlayerHealthLabel.rectTransform;
                labelRect.SetParent(rootRect, false);
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0f, 0f);
                labelRect.sizeDelta = new Vector2(0f, 28f);

                GameObject shieldObject = new GameObject("Shield Indicator", typeof(RectTransform), typeof(Image));
                shieldObject.transform.SetParent(rootRect, false);
                canvasPlayerShieldIcon = shieldObject.GetComponent<Image>();
                RectTransform shieldRect = canvasPlayerShieldIcon.rectTransform;
                shieldRect.anchorMin = Vector2.zero;
                shieldRect.anchorMax = Vector2.zero;
                shieldRect.pivot = new Vector2(1f, 0.5f);
                shieldRect.anchoredPosition = new Vector2(30f, 13f);
                shieldRect.sizeDelta = new Vector2(68f, 68f);
                canvasPlayerShieldIcon.raycastTarget = false;

                canvasPlayerShieldLabel = CreateCanvasHudLabel("Shield Value", Vector2.zero,
                    new Vector2(68f, 68f), 22f, TextAlignmentOptions.Center);
                ApplyOutlinedValueFont(canvasPlayerShieldLabel);
                RectTransform shieldLabelRect = canvasPlayerShieldLabel.rectTransform;
                shieldLabelRect.SetParent(shieldRect, false);
                shieldLabelRect.anchorMin = Vector2.zero;
                shieldLabelRect.anchorMax = Vector2.one;
                shieldLabelRect.offsetMin = Vector2.zero;
                shieldLabelRect.offsetMax = Vector2.zero;
            }
            bool visible = phase != RevealPhase.GameOver && phase != RevealPhase.RunCleared;
            canvasPlayerHealthRoot.SetActive(visible);
            if (!visible || stageSelectionVisible || restStageActive || eventChoiceActive)
            {
                if (combatPlayerCharacter != null) combatPlayerCharacter.SetActive(false);
                if (!visible) return;
            }
            else ShowPlayerCharacterInCombat();
            float ratio = PlayerMaximumHealth > 0 ? Mathf.Clamp01((float)playerHealth / PlayerMaximumHealth) : 0f;
            RectTransform healthFillRect = canvasPlayerHealthFill.rectTransform;
            healthFillRect.anchorMax = new Vector2(ratio, 1f);
            canvasPlayerHealthLabel.text = playerHealth + " / " + PlayerMaximumHealth;
            bool hasShield = playerShield > 0;
            Color healthFillColor = hasShield ? new Color(0.33f, 0.78f, 1f, 1f) : new Color(0.87f, 0.23f, 0.27f, 1f);
            if (canvasPlayerHealthFill.color != healthFillColor) canvasPlayerHealthFill.color = healthFillColor;
            if (canvasPlayerShieldIcon != null)
            {
                canvasPlayerShieldIcon.gameObject.SetActive(hasShield);
                if (hasShield)
                {
                    global::CombatBuffDefinition shield = GetCombatBuffDefinition("Shield");
                    Texture2D texture = shield != null && shield.Image != null ? shield.Image : Texture2D.whiteTexture;
                    if (canvasPlayerShieldIcon.sprite == null || canvasPlayerShieldIcon.sprite.texture != texture)
                        canvasPlayerShieldIcon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    canvasPlayerShieldIcon.color = Color.white;
                    canvasPlayerShieldLabel.text = playerShield.ToString();
                }
            }
        }
        private Image CreateCanvasEnemyActionIcon(Transform parent, string name, out TextMeshProUGUI amount)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            RectTransform rect = iconObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(48f, 48f);
            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            amount = CreateCanvasHudLabel("Amount", Vector2.zero, new Vector2(48f, 48f), 16f,
                TextAlignmentOptions.Center);
            amount.rectTransform.SetParent(rect, false);
            amount.rectTransform.anchorMin = Vector2.zero;
            amount.rectTransform.anchorMax = Vector2.one;
            amount.rectTransform.offsetMin = Vector2.zero;
            amount.rectTransform.offsetMax = Vector2.zero;
            // All planned-action values use the same high-contrast treatment as buffs and relics.
            amount.fontStyle = FontStyles.Bold;
            amount.color = Color.white;
            amount.outlineColor = Color.black;
            amount.outlineWidth = CombatTextOutline.OutlineThickness;
            amount.extraPadding = true;
            if (name != "Action Countdown")
            {
                amount.alignment = TextAlignmentOptions.BottomRight;
                amount.rectTransform.offsetMin = new Vector2(0f, 0f);
                amount.rectTransform.offsetMax = new Vector2(-3f, -3f);
            }
            ApplyOutlinedValueFont(amount);
            if (name == "Action Countdown")
            {
                ApplyPlainActionTimerFont(amount);
                // Stretched RectTransform: Bottom = 5, Top = 0.
                amount.rectTransform.offsetMin = new Vector2(0f, 5f);
                amount.rectTransform.offsetMax = Vector2.zero;
            }
            return icon;
        }

        private void UpdateCanvasEnemyActionIcon(Image icon, TextMeshProUGUI amount, Texture2D texture,
            string value, float localX, bool visible)
        {
            icon.gameObject.SetActive(visible);
            if (!visible) return;
            if (icon.sprite == null || icon.sprite.texture != texture)
                icon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            icon.rectTransform.anchoredPosition = new Vector2(localX - 24f, -60f);
            amount.text = value;
        }

        private CanvasEnemyHud CreateCanvasEnemyHud(int index)
        {
            GameObject root = new GameObject("Enemy HUD " + index, typeof(RectTransform));
            root.transform.SetParent(runtimeUiRoot, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(IsPortraitUi ? 220f : 240f, 250f);
            CanvasEnemyHud hud = new CanvasEnemyHud { Root = root };
            hud.Name = CreateCanvasHudLabel("Name", Vector2.zero, new Vector2(240f, 30f), 24f,
                TextAlignmentOptions.Center);
            hud.Name.rectTransform.SetParent(rootRect, false);
            hud.Name.rectTransform.anchorMin = new Vector2(0f, 1f);
            hud.Name.rectTransform.anchorMax = new Vector2(1f, 1f);
            hud.Name.rectTransform.pivot = new Vector2(0.5f, 1f);
            hud.Name.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            hud.Name.rectTransform.sizeDelta = new Vector2(0f, 30f);
            ApplyOutlinedValueFont(hud.Name);
            hud.Action = CreateCanvasHudLabel("Action", Vector2.zero, new Vector2(240f, 24f), 16f,
                TextAlignmentOptions.Center);
            hud.Action.rectTransform.SetParent(rootRect, false);
            hud.Action.rectTransform.anchorMin = new Vector2(0f, 1f);
            hud.Action.rectTransform.anchorMax = new Vector2(1f, 1f);
            hud.Action.rectTransform.pivot = new Vector2(0.5f, 1f);
            hud.Action.rectTransform.anchoredPosition = new Vector2(0f, -26f);
            hud.Action.rectTransform.sizeDelta = new Vector2(0f, 24f);
            hud.Action.gameObject.SetActive(false);
            hud.ActionCountdownIcon = CreateCanvasEnemyActionIcon(rootRect, "Action Countdown", out hud.ActionCountdownText);
            hud.ActionDamageIcon = CreateCanvasEnemyActionIcon(rootRect, "Action Damage", out hud.ActionDamageText);
            GameObject bar = new GameObject("Health Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(rootRect, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 1f);
            barRect.anchorMax = new Vector2(0.5f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -220f);
            barRect.sizeDelta = new Vector2(IsPortraitUi ? 170f : 180f, 22f);
            bar.GetComponent<Image>().color = new Color(0.13f, 0.04f, 0.05f, 0.9f);
            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(barRect, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            hud.HealthFill = fill.GetComponent<Image>();
            hud.HealthFill.color = new Color(0.86f, 0.19f, 0.25f, 1f);
            hud.Health = CreateCanvasHudLabel("Value", Vector2.zero, new Vector2(180f, 22f), 14f,
                TextAlignmentOptions.Center);
            hud.Health.rectTransform.SetParent(barRect, false);
            ApplyOutlinedValueFont(hud.Health);
            hud.Health.rectTransform.anchorMin = Vector2.zero;
            hud.Health.rectTransform.anchorMax = Vector2.one;
            hud.Health.rectTransform.offsetMin = Vector2.zero;
            hud.Health.rectTransform.offsetMax = Vector2.zero;

            GameObject shieldObject = new GameObject("Shield Indicator", typeof(RectTransform), typeof(Image));
            shieldObject.transform.SetParent(barRect, false);
            hud.ShieldIcon = shieldObject.GetComponent<Image>();
            RectTransform shieldRect = hud.ShieldIcon.rectTransform;
            shieldRect.anchorMin = new Vector2(0f, 0.5f);
            shieldRect.anchorMax = new Vector2(0f, 0.5f);
            shieldRect.pivot = new Vector2(1f, 0.5f);
            shieldRect.anchoredPosition = new Vector2(30f, 0f);
            shieldRect.sizeDelta = new Vector2(60f, 60f);
            hud.ShieldIcon.raycastTarget = false;
            hud.ShieldAmount = CreateCanvasHudLabel("Shield Value", Vector2.zero, new Vector2(60f, 60f), 20f,
                TextAlignmentOptions.Center);
            ApplyOutlinedValueFont(hud.ShieldAmount);
            RectTransform shieldLabelRect = hud.ShieldAmount.rectTransform;
            shieldLabelRect.SetParent(shieldRect, false);
            shieldLabelRect.anchorMin = Vector2.zero;
            shieldLabelRect.anchorMax = Vector2.one;
            shieldLabelRect.offsetMin = Vector2.zero;
            shieldLabelRect.offsetMax = Vector2.zero;
            return hud;
        }
        private void UpdateCanvasEnemyHud()
        {
            EnsureRuntimeUiCanvas();
            while (canvasEnemyHuds.Count < enemies.Count)
                canvasEnemyHuds.Add(CreateCanvasEnemyHud(canvasEnemyHuds.Count));
            float topY = GetEnemyUiTopOffset();
            for (int i = 0; i < canvasEnemyHuds.Count; i++)
            {
                CanvasEnemyHud hud = canvasEnemyHuds[i];
                EnemyState enemy = i < enemies.Count ? enemies[i] : null;
                bool visible = !stageSelectionVisible && enemy != null && !enemy.IsDefeated
                    && phase != RevealPhase.GameOver && phase != RevealPhase.RunCleared;
                hud.Root.SetActive(visible);
                if (!visible) continue;
                float width = IsPortraitUi ? 220f : 240f;
                hud.Root.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(GetEnemyUiX(i), -(topY + 100f));
                hud.Name.text = IsEnglishUi ? enemy.EnglishName : enemy.Name;
                if (clockTexture == null) clockTexture = Resources.Load<Texture2D>("CardAssets/Content/clock");
                if (attackTexture == null) attackTexture = Resources.Load<Texture2D>("CardAssets/Content/attack");
                GetEnemyActionUiPositions(enemy, GetEnemyUiX(i), width,
                    out float countdownX, out float damageX, out _);
                UpdateCanvasEnemyActionIcon(hud.ActionCountdownIcon, hud.ActionCountdownText, clockTexture,
                    enemy.ActionTurnsRemaining.ToString(), countdownX - GetEnemyUiX(i), clockTexture != null);
                bool hasDamage = enemy.ActionDamage > 0 && attackTexture != null;
                UpdateCanvasEnemyActionIcon(hud.ActionDamageIcon, hud.ActionDamageText, attackTexture,
                    enemy.ActionDamage.ToString(), damageX - GetEnemyUiX(i), hasDamage);
                float ratio = enemy.MaximumHealth > 0 ? Mathf.Clamp01((float)enemy.Health / enemy.MaximumHealth) : 0f;
                hud.HealthFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                hud.Health.text = enemy.Health.ToString("N0") + " / " + enemy.MaximumHealth.ToString("N0");
                bool hasShield = enemy.Shield > 0;
                Color healthFillColor = hasShield ? new Color(0.33f, 0.78f, 1f, 1f) : new Color(0.86f, 0.19f, 0.25f, 1f);
                if (hud.HealthFill.color != healthFillColor) hud.HealthFill.color = healthFillColor;
                hud.ShieldIcon.gameObject.SetActive(hasShield);
                if (hasShield)
                {
                    global::CombatBuffDefinition shield = GetCombatBuffDefinition("Shield");
                    Texture2D texture = shield != null && shield.Image != null ? shield.Image : Texture2D.whiteTexture;
                    if (hud.ShieldIcon.sprite == null || hud.ShieldIcon.sprite.texture != texture)
                        hud.ShieldIcon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    hud.ShieldIcon.color = Color.white;
                    hud.ShieldAmount.text = enemy.Shield.ToString();
                }
            }

            if (!stageSelectionVisible && enemies.Count > 0 && Camera.main != null)
            {
                GetUiLayout(out float scale, out float offsetX, out float offsetY);
                LayoutEnemyVisualToCombatUi(scale, offsetX, offsetY);
            }
        }        private CanvasIconList CreateCanvasIconList(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(runtimeUiRoot, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return new CanvasIconList { Root = root };
        }
        private CanvasIconSlot CreateCanvasIconSlot(CanvasIconList list, Vector2 size)
        {
            GameObject root = new GameObject("Icon", typeof(RectTransform));
            root.transform.SetParent(list.Root.transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            Image icon = root.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TextMeshProUGUI amount = CreateCanvasHudLabel("Amount", Vector2.zero, size, size.y * 0.42f,
                TextAlignmentOptions.BottomRight);
            amount.rectTransform.SetParent(rect, false);
            amount.rectTransform.anchorMin = Vector2.zero;
            amount.rectTransform.anchorMax = Vector2.one;
            amount.rectTransform.offsetMin = Vector2.zero;
            amount.rectTransform.offsetMax = Vector2.zero;
            amount.fontStyle = FontStyles.Bold;
            amount.color = Color.white;
            amount.outlineColor = Color.black;
            amount.outlineWidth = CombatTextOutline.OutlineThickness;
            amount.extraPadding = true;
            ApplyOutlinedValueFont(amount);
            return new CanvasIconSlot { Root = root, Icon = icon, Amount = amount };
        }
        private void UpdateCanvasIconList(CanvasIconList list, IList<CombatBuffListVisual.Entry> entries,
            Vector2 anchoredPosition, Vector2 iconSize, float spacing, bool visible)
        {
            list.Root.SetActive(visible);
            if (!visible) return;
            while (list.Slots.Count < entries.Count) list.Slots.Add(CreateCanvasIconSlot(list, iconSize));
            list.Root.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
            for (int i = 0; i < list.Slots.Count; i++)
            {
                CanvasIconSlot slot = list.Slots[i];
                bool active = i < entries.Count && entries[i].Definition != null && entries[i].Amount > 0;
                slot.Root.SetActive(active);
                if (!active) continue;
                CombatBuffListVisual.Entry entry = entries[i];
                Texture2D texture = entry.Definition.Image != null ? entry.Definition.Image : Texture2D.whiteTexture;
                if (slot.Icon.sprite == null || slot.Icon.sprite.texture != texture)
                    slot.Icon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f);
                slot.Root.GetComponent<RectTransform>().anchoredPosition = new Vector2(i * spacing, 0f);
                slot.Amount.text = entry.Amount.ToString();
            }
        }
        private void UpdateCanvasRelicList(CanvasIconList list, IList<CombatRelicListVisual.Entry> entries,
            Vector2 anchoredPosition, bool visible)
        {
            list.Root.SetActive(visible);
            if (!visible) return;
            Vector2 iconSize = new Vector2(56f, 52f);
            while (list.Slots.Count < entries.Count) list.Slots.Add(CreateCanvasIconSlot(list, iconSize));
            list.Root.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
            for (int i = 0; i < list.Slots.Count; i++)
            {
                CanvasIconSlot slot = list.Slots[i];
                bool active = i < entries.Count && entries[i].Definition != null;
                slot.Root.SetActive(active);
                if (!active) continue;
                CombatRelicListVisual.Entry entry = entries[i];
                Texture2D texture = entry.Definition.Image != null ? entry.Definition.Image : Texture2D.whiteTexture;
                if (slot.Icon.sprite == null || slot.Icon.sprite.texture != texture)
                    slot.Icon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f);
                slot.Root.GetComponent<RectTransform>().anchoredPosition = new Vector2(i * 64f, 0f);
                bool showAmount = entry.Definition.ShowAmountAsPercent
                    || entry.Definition.Effect == CombatRelicEffect.ShopCurrency || entry.Amount > 0;
                slot.Amount.gameObject.SetActive(showAmount);
                if (showAmount)
                    slot.Amount.text = entry.Definition.ShowAmountAsPercent ? "+" + entry.Amount + "%" : entry.Amount.ToString();
            }
        }
        private void UpdateCanvasCombatStatusIcons()
        {
            EnsureRuntimeUiCanvas();
            if (canvasPlayerBuffList == null)
            {
                canvasPlayerBuffList = CreateCanvasIconList("Canvas Player Buff List");
                canvasRelicList = CreateCanvasIconList("Canvas Relic List");
            }
            playerBuffEntries.Clear();
            if (playerBurn > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Burn"), playerBurn));
            if (playerScales > 0) playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Scales"), playerScales));
            for (int i = 0; i < playerBleedingStacks.Count; i++)
                playerBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Bleeding"), playerBleedingStacks[i]));
            UpdateCanvasIconList(canvasPlayerBuffList, playerBuffEntries, new Vector2(24f, -625f),
                new Vector2(44f, 40f), 53f, !stageSelectionVisible);
            playerRelicEntries.Clear();
                global::CombatRelicDefinition goldDefinition = GetGoldCurrencyDefinition();
                if (goldDefinition != null) playerRelicEntries.Add(new CombatRelicListVisual.Entry(goldDefinition, gold));
                for (int i = 0; i < ownedRelics.Count; i++)
                {
                    global::CombatRelicDefinition relic = ownedRelics[i];
                    if (relic == null) continue;
                    int amount = relic.Effect == CombatRelicEffect.CardUseDamagePercent ? relicDamagePercentThisTurn : relic.Amount;
                    playerRelicEntries.Add(new CombatRelicListVisual.Entry(relic, amount));
                }
            UpdateCanvasRelicList(canvasRelicList, playerRelicEntries,
                IsPortraitUi ? new Vector2(28f, -146f) : new Vector2(28f, -106f), true);
            while (canvasEnemyBuffLists.Count < enemies.Count)
            {
                canvasEnemyBuffLists.Add(CreateCanvasIconList("Canvas Enemy Buff List " + canvasEnemyBuffLists.Count));
                canvasEnemyActionBuffLists.Add(CreateCanvasIconList("Canvas Enemy Action Buff List " + canvasEnemyActionBuffLists.Count));
            }
            float topY = GetEnemyUiTopOffset();
            for (int i = 0; i < canvasEnemyBuffLists.Count; i++)
            {
                EnemyState enemy = i < enemies.Count ? enemies[i] : null;
                bool visible = !stageSelectionVisible && enemy != null && !enemy.IsDefeated;
                enemyBuffEntries.Clear();
                if (visible)
                {
                    if (enemy.Burn > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Burn"), enemy.Burn));
                    if (enemy.Scales > 0) enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Scales"), enemy.Scales));
                    for (int stack = 0; stack < enemy.BleedingDurations.Count; stack++)
                        enemyBuffEntries.Add(new CombatBuffListVisual.Entry(GetCombatBuffDefinition("Bleeding"), enemy.BleedingDurations[stack]));
                }
                float width = IsPortraitUi ? 220f : 240f;
                float panelWidth = IsPortraitUi ? 170f : 180f;
                float x = GetEnemyUiX(i) + (width - panelWidth) * 0.5f + 26f;
                UpdateCanvasIconList(canvasEnemyBuffLists[i], enemyBuffEntries,
                    new Vector2(x - 8f, -(topY + 349f)), new Vector2(44f, 40f), 52f, visible);
                enemyActionBuffEntries.Clear();
                if (visible) CollectEnemyActionBuffEntries(enemy, enemyActionBuffEntries);
                GetEnemyActionUiPositions(enemy, GetEnemyUiX(i), width, out _, out _, out float actionBuffX);
                UpdateCanvasIconList(canvasEnemyActionBuffLists[i], enemyActionBuffEntries,
                    new Vector2(actionBuffX - 24f, -(topY + 160f)), new Vector2(48f, 48f), 52f, visible);
            }
        }
        private void UpdateCanvasContextHud() // event choices
        {
            EnsureRuntimeUiCanvas();
            if (canvasContextTitle == null)
            {
                canvasContextTitle = CreateCanvasHudLabel("Context Title", Vector2.zero,
                    new Vector2(900f, 54f), 32f, TextAlignmentOptions.Center);
                ApplyContextOutlinedFont(canvasContextTitle, true);
                RectTransform titleRect = canvasContextTitle.rectTransform;
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 1f);
                titleRect.anchoredPosition = new Vector2(0f, -40f);
                canvasContextMessage = CreateCanvasHudLabel("Context Message", Vector2.zero,
                    new Vector2(1050f, 42f), 18f, TextAlignmentOptions.Center);
                ApplyContextOutlinedFont(canvasContextMessage, false);
                canvasContextMessage.fontStyle = FontStyles.Normal;
                RectTransform messageRect = canvasContextMessage.rectTransform;
                messageRect.anchorMin = new Vector2(0.5f, 1f);
                messageRect.anchorMax = new Vector2(0.5f, 1f);
                messageRect.pivot = new Vector2(0.5f, 1f);
                messageRect.anchoredPosition = new Vector2(0f, -92f);
                canvasLeaveShopButton = CreateCanvasButton("Leave Shop Button", new Vector2(1f, 0f),
                    new Vector2(1f, 0f), new Vector2(-45f, 250f), new Vector2(190f, 48f),
                    () => ResolveOffer(false), out TextMeshProUGUI leaveLabel);
                Outline leaveShopButtonOutline = canvasLeaveShopButton.GetComponent<Outline>();
                if (leaveShopButtonOutline != null) leaveShopButtonOutline.enabled = false;
                leaveLabel.text = Ui("상점 나가기", "Leave Shop");
            }
            ApplyContextOutlinedFont(canvasContextTitle, true);
            ApplyContextOutlinedFont(canvasContextMessage, false);
            bool show = stageSelectionVisible || restStageActive || eventChoiceActive || rewardChoiceActive || shopChoiceActive || shopRewardOpeningActive;
            canvasContextTitle.gameObject.SetActive(show);
            canvasContextMessage.gameObject.SetActive(show);
            canvasLeaveShopButton.gameObject.SetActive(shopChoiceActive && !discardPileHovered);
            if (!show) return;
            if (shopRewardOpeningActive)
            {
                bool openingPack = phase == RevealPhase.Pack || phase == RevealPhase.Animating;
                canvasContextTitle.text = openingPack ? Ui("상점 보상 팩", "Shop Reward Pack") : Ui("보상 선택", "Choose Reward");
                canvasContextMessage.text = openingPack
                    ? Ui("카드팩을 위쪽으로 드래그해 뜯으세요.", "Drag the pack upward to open it.")
                    : Ui("카드를 위로 끌어올려 보상을 선택하세요.", "Drag a card upward to choose your reward.");
            }
            if (!shopRewardOpeningActive && eventChoiceActive)
            {
                canvasContextTitle.text = activeEventId == 1 ? Ui("떨어진 카드를 발견했다", "You found a fallen card") : Ui("수상한 인물이 카드를 요구했다", "A suspicious figure asks for a card");
                canvasContextMessage.text = Ui("선택지 카드를 위로 드래그해 사용하세요.", "Drag a choice card upward to use it.");
            }
            if (!shopRewardOpeningActive && !eventChoiceActive && restStageActive)
            {
                canvasContextTitle.text = Ui("휴식", "Rest");
                canvasContextMessage.text = Ui("휴식 카드를 위로 드래그해 사용하세요.", "Drag the rest card upward to use it.");
            }
            if (!shopRewardOpeningActive && !eventChoiceActive && !restStageActive && stageSelectionVisible)
            {
                canvasContextTitle.text = Ui("스테이지 선택", "Choose a Stage");
                canvasContextMessage.text = Ui("카드를 위로 드래그해 스테이지를 선택하세요.",
                    "Drag a stage card upward to choose it.");
            }
            if (!shopRewardOpeningActive && !eventChoiceActive && !restStageActive && !stageSelectionVisible && rewardChoiceActive)
            {
                canvasContextTitle.text = string.IsNullOrEmpty(pendingRewardContextTitle) ? Ui("전투 보상", "Combat Reward") : pendingRewardContextTitle;
                canvasContextMessage.text = string.IsNullOrEmpty(pendingRewardContextMessage) ? Ui("카드를 위로 드래그해 보상을 받고, 버린 카드 더미로 드래그해 거절하세요.", "Drag the card upward to claim it, or drag it to the discard pile to decline.") : pendingRewardContextMessage;
            }
            if (!shopRewardOpeningActive && !eventChoiceActive && !restStageActive && !stageSelectionVisible && !rewardChoiceActive)
            {
                canvasContextTitle.text = Ui("상점", "Shop");
                canvasContextMessage.text = Ui("카드를 위로 드래그해 구매하세요.", "Drag a card upward to purchase it.");
            }
        }
        private TextMeshProUGUI CreateSettingsText(string name, Transform parent, Vector2 anchoredPosition,
            Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI label = CreateCanvasHudLabel(name, Vector2.zero, size, fontSize, alignment);
            RectTransform rect = label.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            return label;
        }
        private Button CreateSettingsButton(string name, Transform parent, Vector2 anchoredPosition,
            Vector2 size, UnityEngine.Events.UnityAction action, out TextMeshProUGUI label)
        {
            Button button = CreateCanvasButton(name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                Vector2.zero, size, action, out label);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            return button;
        }
        private void OpenCanvasSettings()
        {
            abandonConfirmationVisible = false;
            settingsOpen = true;
        }
        private void CloseCanvasSettings()
        {
            abandonConfirmationVisible = false;
            settingsOpen = false;
        }
        private void EnsureCanvasSettingsUi()
        {
            EnsureRuntimeUiCanvas();
            if (canvasSettingsRoot != null) return;
            canvasSettingsButton = CreateCanvasButton("Settings Button", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -28f), new Vector2(120f, 54f), OpenCanvasSettings, out TextMeshProUGUI settingsButtonLabel);
            Outline settingsButtonOutline = canvasSettingsButton.GetComponent<Outline>();
            if (settingsButtonOutline != null) settingsButtonOutline.enabled = false;
            settingsButtonLabel.text = Ui("설정", "Settings");
            settingsButtonLabel.fontSize = 22f;
            canvasSettingsRoot = new GameObject("Settings Overlay", typeof(RectTransform), typeof(Image));
            canvasSettingsRoot.transform.SetParent(runtimeUiRoot, false);
            RectTransform overlay = canvasSettingsRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            canvasSettingsRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(500f, 535f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 0.98f);
            canvasSettingsTitle = CreateSettingsText("Title", panel.transform, new Vector2(0f, -34f),
                new Vector2(420f, 58f), 38f, TextAlignmentOptions.Center);
            canvasSettingsLanguageLabel = CreateSettingsText("Language", panel.transform, new Vector2(-150f, -128f),
                new Vector2(200f, 42f), 23f, TextAlignmentOptions.Left);
            Button korean = CreateSettingsButton("Korean", panel.transform, new Vector2(-105f, -180f),
                new Vector2(170f, 52f), () => SetUiLanguage(0), out TextMeshProUGUI koreanLabel);
            Button english = CreateSettingsButton("English", panel.transform, new Vector2(105f, -180f),
                new Vector2(170f, 52f), () => SetUiLanguage(1), out TextMeshProUGUI englishLabel);
            koreanLabel.name = "Korean Label";
            englishLabel.name = "English Label";
            canvasSettingsVolumeLabel = CreateSettingsText("Volume", panel.transform, new Vector2(-110f, -262f),
                new Vector2(280f, 42f), 23f, TextAlignmentOptions.Left);
            GameObject sliderObject = new GameObject("Volume Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(panel.transform, false);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 1f);
            sliderRect.anchorMax = new Vector2(0.5f, 1f);
            sliderRect.pivot = new Vector2(0.5f, 1f);
            sliderRect.anchoredPosition = new Vector2(0f, -320f);
            sliderRect.sizeDelta = new Vector2(370f, 28f);
            Image sliderBackground = sliderObject.AddComponent<Image>();
            sliderBackground.color = new Color(0.2f, 0.24f, 0.34f, 1f);
            canvasSettingsVolumeSlider = sliderObject.GetComponent<Slider>();
            canvasSettingsVolumeSlider.minValue = 0f;
            canvasSettingsVolumeSlider.maxValue = 1f;
            canvasSettingsVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            CreateSettingsButton("Abandon", panel.transform, new Vector2(0f, -405f), new Vector2(220f, 46f),
                () => abandonConfirmationVisible = true, out TextMeshProUGUI abandonLabel).gameObject.name = "Abandon Button";
            abandonLabel.text = Ui("도전 포기", "Abandon Run");
            CreateSettingsButton("Close", panel.transform, new Vector2(0f, -465f), new Vector2(170f, 52f),
                CloseCanvasSettings, out TextMeshProUGUI closeLabel);
            closeLabel.text = Ui("닫기", "Close");
            canvasAbandonConfirmationRoot = new GameObject("Abandon Confirmation", typeof(RectTransform), typeof(Image));
            canvasAbandonConfirmationRoot.transform.SetParent(overlay, false);
            RectTransform confirmRect = canvasAbandonConfirmationRoot.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0.5f);
            confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
            confirmRect.pivot = new Vector2(0.5f, 0.5f);
            confirmRect.sizeDelta = new Vector2(500f, 280f);
            canvasAbandonConfirmationRoot.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.10f, 1f);
            TextMeshProUGUI confirmText = CreateSettingsText("Message", canvasAbandonConfirmationRoot.transform,
                new Vector2(0f, -36f), new Vector2(420f, 100f), 22f, TextAlignmentOptions.Center);
            confirmText.text = Ui("도전을 포기할까요?\n현재 결과 화면으로 이동합니다.",
                "Abandon this run?\nYou will move to the current result.");
            CreateSettingsButton("Confirm Abandon", canvasAbandonConfirmationRoot.transform, new Vector2(-110f, -175f),
                new Vector2(170f, 58f), AbandonChallengeToResults, out TextMeshProUGUI confirmLabel);
            confirmLabel.text = Ui("포기", "Abandon");
            CreateSettingsButton("Cancel Abandon", canvasAbandonConfirmationRoot.transform, new Vector2(110f, -175f),
                new Vector2(170f, 58f), () => abandonConfirmationVisible = false, out TextMeshProUGUI cancelLabel);
            cancelLabel.text = Ui("취소", "Cancel");
            canvasSettingsRoot.SetActive(false);
        }
        private void UpdateCanvasSettingsUi()
        {
            EnsureCanvasSettingsUi();
            bool canAbandonChallenge = phase != RevealPhase.GameOver && phase != RevealPhase.RunCleared;
            canvasSettingsButton.gameObject.SetActive(!settingsOpen && !combatDeckInspectionVisible && !stageDeckInspectionVisible);
            canvasSettingsRoot.SetActive(settingsOpen);
            if (!settingsOpen) return;
            canvasSettingsTitle.text = Ui("설정", "Settings");
            canvasSettingsLanguageLabel.text = Ui("언어", "Language");
            canvasSettingsVolumeLabel.text = Ui("음량  ", "Volume  ") + Mathf.RoundToInt(masterVolume * 100f) + "%";
            if (!Mathf.Approximately(canvasSettingsVolumeSlider.value, masterVolume)) canvasSettingsVolumeSlider.SetValueWithoutNotify(masterVolume);
            Transform abandon = canvasSettingsRoot.transform.Find("Panel/Abandon Button");
            if (abandon != null) abandon.gameObject.SetActive(canAbandonChallenge);
            canvasAbandonConfirmationRoot.SetActive(abandonConfirmationVisible && canAbandonChallenge);
        }
        private void EnsureCanvasRunEndUi()
        {
            EnsureRuntimeUiCanvas();
            if (canvasRunEndRoot != null) return;
            canvasRunEndRoot = new GameObject("Run End Overlay", typeof(RectTransform), typeof(Image));
            canvasRunEndRoot.transform.SetParent(runtimeUiRoot, false);
            RectTransform overlay = canvasRunEndRoot.GetComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            canvasRunEndRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(740f, 485f);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 0.98f);
            canvasRunEndTitle = CreateSettingsText("Title", panel.transform, new Vector2(0f, -42f),
                new Vector2(640f, 70f), 44f, TextAlignmentOptions.Center);
            canvasRunEndBody = CreateSettingsText("Body", panel.transform, new Vector2(0f, -124f),
                new Vector2(610f, 260f), 24f, TextAlignmentOptions.Center);
            canvasRunEndBody.enableWordWrapping = true;
            canvasRunEndLeftButton = CreateSettingsButton("Left Action", panel.transform, new Vector2(-150f, -405f),
                new Vector2(260f, 70f), HandleCanvasRunEndLeftAction, out _);
            canvasRunEndRightButton = CreateSettingsButton("Right Action", panel.transform, new Vector2(150f, -405f),
                new Vector2(260f, 70f), StartNewRun, out _);
            canvasRunEndRoot.SetActive(false);
        }
        private void HandleCanvasRunEndLeftAction()
        {
            ShareCurrentResult();
        }
        private void UpdateCanvasRunEndUi()
        {
            EnsureCanvasRunEndUi();
            bool visible = phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared;
            canvasRunEndRoot.SetActive(visible);
            if (!visible) return;
            bool cleared = phase == RevealPhase.RunCleared;
            int goalIndex = Mathf.Clamp(currentGoalIndex, 0, GoalScores.Length - 1);
            int targetScore = GoalScores[goalIndex];
            int reachedStage = cleared ? GoalScores.Length : Mathf.Clamp(currentGoalIndex + 1, 1, GoalScores.Length);
            canvasRunEndTitle.text = cleared ? Ui("런 클리어!", "RUN CLEARED!") : Ui("게임 오버", "GAME OVER");
            string resultMessage = cleared ? Ui("모든 목표를 달성했습니다.", "All goals cleared.")
                : Ui("목표 점수에 도달하지 못했습니다.", "Goal score not reached.");
            string roundValue = cleared ? Ui("완료", "CLEAR") : roundScore.ToString("N0") + " / " + targetScore.ToString("N0");
            canvasRunEndBody.text = (sharedResultMode ? Ui("공유받은 결과\n\n", "SHARED RESULT\n\n") : string.Empty)
                + resultMessage + "\n\n"
                + Ui("총점 ", "TOTAL SCORE ") + totalScore.ToString("N0") + "    "
                + Ui("도달 단계 ", "STAGE ") + reachedStage + " / " + GoalScores.Length + "    "
                + Ui("라운드 점수 ", "ROUND SCORE ") + roundValue + "\n\n"
                + Ui("아래 덱 카드를 눌러 상세히 볼 수 있어요.", "Select a deck card below to inspect it.");
            canvasRunEndLeftButton.GetComponentInChildren<TextMeshProUGUI>().text = Ui("공유", "Share");
            canvasRunEndRightButton.GetComponentInChildren<TextMeshProUGUI>().text = sharedResultMode
                ? Ui("도전하기", "Challenge") : Ui("다시 시작", "Restart");
        }
        private void EnsureCanvasPackChoiceUi()
        {
            EnsureRuntimeUiCanvas();
            if (canvasPackChoiceTitle != null) return;
            canvasPackChoiceTitle = CreateCanvasHudLabel("Pack Choice Title", Vector2.zero,
                new Vector2(500f, 52f), 32f, TextAlignmentOptions.Center);
            RectTransform titleRect = canvasPackChoiceTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -72f);
            canvasLeftPackInfoButton = CreateCanvasButton("Left Pack Info", new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(54f, 54f),
                () => OpenPackContents(leftPackChoice), out TextMeshProUGUI leftLabel);
            leftLabel.text = "?";
            canvasRightPackInfoButton = CreateCanvasButton("Right Pack Info", new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(54f, 54f),
                () => OpenPackContents(rightPackChoice), out TextMeshProUGUI rightLabel);
            rightLabel.text = "?";
            canvasActivePackInfoButton = CreateCanvasButton("Active Pack Info", new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(54f, 54f),
                () => OpenPackContents(activePackData), out TextMeshProUGUI activeLabel);
            activeLabel.text = "?";
        }
        private void UpdateCanvasPackChoiceUi()
        {
            EnsureCanvasPackChoiceUi();
            bool showChoice = phase == RevealPhase.PackChoice && inspectedPackChoice == null;
            canvasPackChoiceTitle.gameObject.SetActive(showChoice);
            canvasLeftPackInfoButton.gameObject.SetActive(showChoice && leftPackChoice != null);
            canvasRightPackInfoButton.gameObject.SetActive(showChoice && rightPackChoice != null);
            if (showChoice)
            {
                canvasPackChoiceTitle.text = Ui("다음 팩을 선택하세요", "Choose the next pack");
                PositionPackInfoButton(canvasLeftPackInfoButton, leftPackChoiceVisual);
                PositionPackInfoButton(canvasRightPackInfoButton, rightPackChoiceVisual);
            }
            bool showActiveInfo = phase == RevealPhase.Pack && activePackData != null && inspectedPackChoice == null;
            canvasActivePackInfoButton.gameObject.SetActive(showActiveInfo);
            if (showActiveInfo)
            {
                RectTransform activeRect = canvasActivePackInfoButton.GetComponent<RectTransform>();
                Vector2 point = IsPortraitUi ? new Vector2(638f, 105f) : new Vector2(880f, 105f);
                activeRect.anchoredPosition = new Vector2(point.x + 27f, -(point.y + 6f));
            }
        }
        private void PositionPackInfoButton(Button button, PackVisual packVisual)
        {
            if (button == null || packVisual == null || Camera.main == null) return;
            GetUiLayout(out float scale, out float offsetX, out float offsetY);
            if (scale <= 0f) return;
            Rect screenRect = GetVisualScreenRect(packVisual.gameObject, Camera.main);
            Vector2 reference = new Vector2((screenRect.center.x - offsetX) / scale,
                (screenRect.yMin - offsetY) / scale - 35f);
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(reference.x, -reference.y);
        }
        private void UpdateCanvasScorePopups()
        {
            EnsureRuntimeUiCanvas();
            canvasScorePopupsActive = scorePopups.Count > 0;
            for (int i = scorePopups.Count - 1; i >= 0; i--)
            {
                ScorePopup popup = scorePopups[i];
                float age = (Time.unscaledTime - popup.StartTime) * Mathf.Max(1f, popup.PlaybackSpeed);
                if (age >= 1.35f) scorePopups.RemoveAt(i);
            }
            while (canvasScorePopupLabels.Count < scorePopups.Count)
            {
                TextMeshProUGUI label = CreateCanvasHudLabel("Score Popup", Vector2.zero,
                    new Vector2(210f, 76f), 28f, TextAlignmentOptions.MidlineLeft);
                RectTransform rect = label.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                label.enableWordWrapping = true;
                canvasScorePopupLabels.Add(label);
            }
            for (int i = 0; i < canvasScorePopupLabels.Count; i++)
            {
                bool active = i < scorePopups.Count;
                TextMeshProUGUI label = canvasScorePopupLabels[i];
                label.gameObject.SetActive(active);
                if (!active) continue;
                ScorePopup popup = scorePopups[i];
                float age = (Time.unscaledTime - popup.StartTime) * Mathf.Max(1f, popup.PlaybackSpeed);
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.18f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1.35f, age));
                float x = Mathf.Lerp(IsPortraitUi ? 40f : 783f, IsPortraitUi ? 210f : 837f, enter);
                float y = (IsPortraitUi ? 350f + PortraitExtraHeight * 0.35f : 270f) + popup.Lane * 72f
                    - Mathf.Clamp01(age / 1.35f) * 24f;
                label.rectTransform.anchoredPosition = new Vector2(x, -y);
                label.rectTransform.sizeDelta = new Vector2(IsPortraitUi ? 176f : 210f, 76f);
                label.fontSize = IsPortraitUi ? 22f : 28f;
                label.text = popup.Text;
                label.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, fade);
            }
            canvasScorePopupsActive = scorePopups.Count > 0;
        }
        private void EnsureCanvasPackContentsControls()
        {
            EnsureRuntimeUiCanvas();
            if (canvasPackContentsControlsRoot != null) return;
            canvasPackContentsControlsRoot = new GameObject("Pack Contents Controls", typeof(RectTransform));
            canvasPackContentsControlsRoot.transform.SetParent(runtimeUiRoot, false);
            canvasPackContentsTitle = CreateSettingsText("Title", canvasPackContentsControlsRoot.transform,
                new Vector2(0f, -28f), new Vector2(500f, 52f), 30f, TextAlignmentOptions.Center);
            Button close = CreateCanvasButton("Pack Contents Close", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-50f, -28f), new Vector2(170f, 52f), ClosePackContents, out TextMeshProUGUI closeLabel);
            close.transform.SetParent(canvasPackContentsControlsRoot.transform, false);
            closeLabel.text = Ui("닫기", "Close");
            canvasPackContentsPreviousButton = CreateCanvasButton("Pack Contents Previous", new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), new Vector2(325f, -320f), new Vector2(150f, 62f),
                () => ChangePackContentsPreview(-1), out TextMeshProUGUI previousLabel);
            canvasPackContentsPreviousButton.transform.SetParent(canvasPackContentsControlsRoot.transform, false);
            previousLabel.text = "◀";
            canvasPackContentsNextButton = CreateCanvasButton("Pack Contents Next", new Vector2(0f, 1f),
                new Vector2(0.5f, 1f), new Vector2(955f, -320f), new Vector2(150f, 62f),
                () => ChangePackContentsPreview(1), out TextMeshProUGUI nextLabel);
            canvasPackContentsNextButton.transform.SetParent(canvasPackContentsControlsRoot.transform, false);
            nextLabel.text = "▶";
            canvasPackContentsCount = CreateCanvasHudLabel("Count", Vector2.zero,
                new Vector2(500f, 60f), 22f, TextAlignmentOptions.Center);
            RectTransform countRect = canvasPackContentsCount.rectTransform;
            countRect.SetParent(canvasPackContentsControlsRoot.transform, false);
            countRect.anchorMin = new Vector2(0.5f, 1f);
            countRect.anchorMax = new Vector2(0.5f, 1f);
            countRect.pivot = new Vector2(0.5f, 1f);
            countRect.anchoredPosition = new Vector2(0f, -642f);
            canvasPackContentsControlsRoot.SetActive(false);
        }
        private void UpdateCanvasPackContentsControls()
        {
            EnsureCanvasPackContentsControls();
            bool visible = inspectedPackChoice != null;
            canvasPackContentsControlsRoot.SetActive(visible);
            if (!visible) return;
            int count = GetPackContentsCardCount();
            int cardsPerPack = Mathf.Max(1, inspectedPackChoice.CardsPerPack);
            canvasPackContentsTitle.text = Ui("봉입 카드 (" + cardsPerPack + "장입)",
                "Included cards (" + cardsPerPack + (cardsPerPack == 1 ? " card)" : " cards)"));
            canvasPackContentsPreviousButton.gameObject.SetActive(count > 0);
            canvasPackContentsNextButton.gameObject.SetActive(count > 0);
            canvasPackContentsCount.text = count > 0 ? (packContentsPreviewIndex + 1) + " / " + count
                : Ui("표시할 카드가 없습니다.", "No cards to display.");
        }
        private void EnsureCanvasDeckInspectionControls()
        {
            EnsureRuntimeUiCanvas();
            if (canvasDeckInspectionControlsRoot != null) return;
            canvasDeckInspectionControlsRoot = new GameObject("Deck Inspection Controls", typeof(RectTransform));
            canvasDeckInspectionControlsRoot.transform.SetParent(runtimeUiRoot, false);
            canvasDeckInspectionRarity = CreateCanvasHudLabel("Rarity", Vector2.zero,
                new Vector2(300f, 48f), 24f, TextAlignmentOptions.Center);
            RectTransform rarityRect = canvasDeckInspectionRarity.rectTransform;
            rarityRect.SetParent(canvasDeckInspectionControlsRoot.transform, false);
            rarityRect.anchorMin = new Vector2(0.5f, 1f);
            rarityRect.anchorMax = new Vector2(0.5f, 1f);
            rarityRect.pivot = new Vector2(0.5f, 1f);
            rarityRect.anchoredPosition = new Vector2(0f, -18f);
            canvasDeckInspectionProgress = CreateCanvasHudLabel("Progress", Vector2.zero,
                new Vector2(390f, 120f), 20f, TextAlignmentOptions.TopLeft);
            RectTransform progressRect = canvasDeckInspectionProgress.rectTransform;
            progressRect.SetParent(canvasDeckInspectionControlsRoot.transform, false);
            progressRect.anchorMin = new Vector2(1f, 1f);
            progressRect.anchorMax = new Vector2(1f, 1f);
            progressRect.pivot = new Vector2(1f, 1f);
            progressRect.anchoredPosition = new Vector2(-35f, -270f);
            canvasDeckInspectionDiscardButton = CreateCanvasButton("Deck Discard", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(180f, 52f),
                () => discardConfirmationVisible = true, out TextMeshProUGUI discardLabel);
            discardLabel.text = Ui("카드 버리기", "Discard card");
            canvasDeckInspectionDiscardButton.transform.SetParent(canvasDeckInspectionControlsRoot.transform, false);
            canvasDeckInspectionConfirmation = new GameObject("Deck Discard Confirmation", typeof(RectTransform), typeof(Image));
            canvasDeckInspectionConfirmation.transform.SetParent(canvasDeckInspectionControlsRoot.transform, false);
            RectTransform confirmRect = canvasDeckInspectionConfirmation.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 0.5f);
            confirmRect.anchorMax = new Vector2(0.5f, 0.5f);
            confirmRect.pivot = new Vector2(0.5f, 0.5f);
            confirmRect.sizeDelta = new Vector2(420f, 206f);
            canvasDeckInspectionConfirmation.GetComponent<Image>().color = new Color(0.10f, 0.06f, 0.08f, 0.98f);
            TextMeshProUGUI message = CreateSettingsText("Message", canvasDeckInspectionConfirmation.transform,
                new Vector2(0f, -28f), new Vector2(370f, 64f), 22f, TextAlignmentOptions.Center);
            message.text = Ui("이 카드를 버릴까요?", "Discard this card?");
            CreateSettingsButton("Confirm", canvasDeckInspectionConfirmation.transform, new Vector2(-90f, -126f),
                new Vector2(140f, 52f), DiscardInspectedDeckCard, out TextMeshProUGUI confirmLabel);
            confirmLabel.text = Ui("버리기", "Discard");
            CreateSettingsButton("Cancel", canvasDeckInspectionConfirmation.transform, new Vector2(90f, -126f),
                new Vector2(140f, 52f), () => discardConfirmationVisible = false, out TextMeshProUGUI cancelLabel);
            cancelLabel.text = Ui("취소", "Cancel");
            canvasDeckInspectionControlsRoot.SetActive(false);
        }
        private void UpdateCanvasDeckInspectionControls()
        {
            EnsureCanvasDeckInspectionControls();
            bool visible = inspectedDeckIndex >= 0 && inspectedDeckIndex < deckCards.Count && deckCards[inspectedDeckIndex] != null;
            canvasDeckInspectionControlsRoot.SetActive(visible);
            if (!visible) return;
            StoredCard card = deckCards[inspectedDeckIndex];
            canvasDeckInspectionRarity.text = GetRarityDisplayName(card.Rarity);
            canvasDeckInspectionRarity.color = GetRarityDisplayColor(card.Rarity);
            canvasDeckInspectionProgress.text = GetDeckProgressText(card);
            canvasDeckInspectionProgress.gameObject.SetActive(!string.IsNullOrEmpty(canvasDeckInspectionProgress.text));
            bool canDiscard = !IsDeckInspectionReadOnly();
            canvasDeckInspectionDiscardButton.gameObject.SetActive(canDiscard && !discardConfirmationVisible);
            if (!canDiscard) discardConfirmationVisible = false;
            canvasDeckInspectionConfirmation.SetActive(canDiscard && discardConfirmationVisible);
        }
        private void UpdateCanvasUsedPileInspectionHud()
        {
            EnsureRuntimeUiCanvas();
            if (canvasUsedPileInspectionRarity == null)
            {
                canvasUsedPileInspectionRarity = CreateCanvasHudLabel("Used Pile Rarity", Vector2.zero,
                    new Vector2(300f, 48f), 24f, TextAlignmentOptions.Center);
                RectTransform rect = canvasUsedPileInspectionRarity.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -18f);
            }
            int index = usedPileDetailCard != null ? usedPileHistory.IndexOf(usedPileDetailCard) : -1;
            bool visible = index >= 0 && index < usedPileCardData.Count && usedPileCardData[index] != null;
            canvasUsedPileInspectionRarity.gameObject.SetActive(visible);
            if (!visible) return;
            canvasUsedPileInspectionRarity.text = GetRarityDisplayName(usedPileCardData[index].Rare);
            canvasUsedPileInspectionRarity.color = GetRarityDisplayColor(usedPileCardData[index].Rare);
        }
        private void EnsureCanvasEffectPopup()
        {
            EnsureRuntimeUiCanvas();
            if (canvasEffectPopupRoot != null) return;
            canvasEffectPopupRoot = new GameObject("Effect Info Popup", typeof(RectTransform), typeof(Image));
            canvasEffectPopupRoot.transform.SetParent(runtimeUiRoot, false);
            RectTransform rootRect = canvasEffectPopupRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            Image popupBackground = canvasEffectPopupRoot.GetComponent<Image>();
            popupBackground.color = new Color(0.04f, 0.05f, 0.08f, 0.97f);
            popupBackground.raycastTarget = false;
            GameObject icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(rootRect, false);
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(18f, -14f);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            canvasEffectPopupIcon = icon.GetComponent<Image>();
            canvasEffectPopupIcon.preserveAspect = true;
            canvasEffectPopupIcon.raycastTarget = false;
            canvasEffectPopupTitle = CreateCanvasHudLabel("Title", Vector2.zero,
                new Vector2(700f, 54f), 26f, TextAlignmentOptions.MidlineLeft);
            canvasEffectPopupTitle.rectTransform.SetParent(rootRect, false);
            canvasEffectPopupBody = CreateCanvasHudLabel("Body", Vector2.zero,
                new Vector2(700f, 200f), 20f, TextAlignmentOptions.TopLeft);
            canvasEffectPopupBody.enableWordWrapping = true;
            canvasEffectPopupBody.rectTransform.SetParent(rootRect, false);
        }
        private void ShowCanvasEffectPopup(string title, Texture icon, string body, float screenX, float screenY,
            float screenWidth, float screenHeight)
        {
            EnsureCanvasEffectPopup();
            GetUiLayout(out float scale, out float offsetX, out float offsetY);
            if (scale <= 0f) return;
            RectTransform rootRect = canvasEffectPopupRoot.GetComponent<RectTransform>();
            float width = screenWidth / scale;
            float height = screenHeight / scale;
            rootRect.anchoredPosition = new Vector2((screenX - offsetX) / scale, -(screenY - offsetY) / scale);
            rootRect.sizeDelta = new Vector2(width, height);
            canvasEffectPopupIcon.gameObject.SetActive(icon != null);
            if (icon != null)
            {
                Texture2D texture = icon as Texture2D;
                if (texture != null && (canvasEffectPopupIcon.sprite == null || canvasEffectPopupIcon.sprite.texture != texture))
                    canvasEffectPopupIcon.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f), 100f);
            }
            RectTransform titleRect = canvasEffectPopupTitle.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(icon != null ? 80f : 22f, -12f);
            titleRect.sizeDelta = new Vector2(-(icon != null ? 102f : 44f), 54f);
            RectTransform bodyRect = canvasEffectPopupBody.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 1f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0f, 1f);
            bodyRect.anchoredPosition = new Vector2(22f, -78f);
            bodyRect.sizeDelta = new Vector2(-44f, -92f);
            canvasEffectPopupTitle.text = title;
            canvasEffectPopupBody.text = body;
            canvasEffectPopupRoot.SetActive(true);
        }
        private void EnsurePlayerDamageFlashOverlay()
        {
            if (playerDamageFlashImage != null) return;
            EnsureRuntimeUiCanvas();
            GameObject imageObject = new GameObject("Player Damage Flash", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(runtimeUiRoot, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            playerDamageFlashImage = imageObject.GetComponent<Image>();
            playerDamageFlashImage.color = new Color(0.88f, 0.04f, 0.05f, 0f);
            playerDamageFlashImage.raycastTarget = false;
            imageObject.SetActive(false);
        }
        private void EnsureCombatEntryFadeOverlay()
        {
            if (combatEntryFadeImage != null) return;
            EnsureRuntimeUiCanvas();
            GameObject imageObject = new GameObject("Combat Entry Fade", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(runtimeUiRoot, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            combatEntryFadeImage = imageObject.GetComponent<Image>();
            combatEntryFadeImage.color = new Color(0f, 0f, 0f, combatEntryFade);
            combatEntryFadeImage.raycastTarget = false;
            combatEntryFadeImage.transform.SetAsLastSibling();
        }
        private void HandleStageSelectionPointer(Vector2 screenPoint, Event inputEvent)
        {
            if (combatEntryRoutine != null) return;
            Camera camera = Camera.main;
            if (camera == null) return;

            if (!hasStageHandHoverPointer || (screenPoint - lastStageHandHoverPointer).sqrMagnitude > 0.25f)
            {
                lastStageHandHoverPointer = screenPoint;
                hasStageHandHoverPointer = true;
                stageHandHoverPointerDirty = true;
            }

            if (inputEvent.type == EventType.MouseDown)
            {
                if (IsPointOverStageDiscardPile(screenPoint))
                {
                    OpenStageDeckInspection(StageDeckInspectionTarget.Discard);
                    inputEvent.Use();
                    return;
                }
                for (int i = stageHandVisuals.Count - 1; i >= 0; i--)
                {
                    CardVisual card = stageHandVisuals[i];
                    if (card == null || !card.gameObject.activeSelf
                        || !GetVisualScreenRect(card.gameObject, camera).Contains(screenPoint)) continue;
                    pressedStageHandIndex = i;
                    pressedStageHandScreenPosition = screenPoint;
                    inputEvent.Use();
                    return;
                }
                return;
            }

            if (inputEvent.type == EventType.MouseDrag && pressedStageHandIndex >= 0)
            {
                if ((screenPoint - pressedStageHandScreenPosition).sqrMagnitude < 25f)
                {
                    inputEvent.Use();
                    return;
                }
                int pressedIndex = pressedStageHandIndex;
                pressedStageHandIndex = -1;
                if (pressedIndex >= 0 && pressedIndex < stageHandVisuals.Count && stageHandVisuals[pressedIndex] != null)
                {
                    CardVisual pressedCard = stageHandVisuals[pressedIndex];
                    if (highlightedStageHandCard == pressedCard)
                    {
                        RestoreStageHandCard(pressedIndex);
                        highlightedStageHandCard = null;
                    }
                    draggedStageHandIndex = pressedIndex;
                    draggedStageHandStartPosition = pressedCard.transform.position;
                    pressedCard.transform.localScale = Vector3.one * 1.18f;
                    pressedCard.SetSortingOrder(1000);
                }
            }

            if (inputEvent.type == EventType.MouseDrag && draggedStageHandIndex >= 0
                && draggedStageHandIndex < stageHandVisuals.Count && stageHandVisuals[draggedStageHandIndex] != null)
            {
                Vector3 screenPosition = camera.WorldToScreenPoint(draggedStageHandStartPosition);
                Vector3 targetPosition = camera.ScreenToWorldPoint(new Vector3(
                    screenPoint.x, Screen.height - screenPoint.y, screenPosition.z));
                stageHandVisuals[draggedStageHandIndex].transform.position = new Vector3(
                    targetPosition.x, targetPosition.y, draggedStageHandStartPosition.z - 0.18f);
                inputEvent.Use();
                return;
            }

            if (inputEvent.type == EventType.MouseUp && pressedStageHandIndex >= 0)
            {
                int pressedIndex = pressedStageHandIndex;
                pressedStageHandIndex = -1;
                if (pressedIndex >= 0) RestoreStageHandCard(pressedIndex);
                inputEvent.Use();
                return;
            }

            if (inputEvent.type == EventType.MouseUp && draggedStageHandIndex >= 0)
            {
                int draggedIndex = draggedStageHandIndex;
                draggedStageHandIndex = -1;
                if (IsPointOverStageDiscardPile(screenPoint))
                {
                    DiscardStageHandCard(draggedIndex);
                    inputEvent.Use();
                    return;
                }
                bool canStart = IsStageCardRaisedForCast(draggedIndex)
                    && CanUseStageCard(draggedIndex, out _)
                    && draggedIndex >= 0 && draggedIndex < stageHand.Count
                    && stageHand[draggedIndex] != null
                    && (stageHand[draggedIndex].Kind == global::StageCardKind.Rest
                        || stageHand[draggedIndex].Encounters != null);
                if (canStart) StartSelectedStage(draggedIndex);
                else RestoreStageHandCard(draggedIndex);
                inputEvent.Use();
            }
        }
        private void OpenStageDeckInspection(StageDeckInspectionTarget target = StageDeckInspectionTarget.Deck)
        {
            stageDeckInspectionMode = true;
            stageDeckInspectionVisible = false;
            for (int i = 0; i < stageHandVisuals.Count; i++) if (stageHandVisuals[i] != null) stageHandVisuals[i].gameObject.SetActive(false);
            if (stageSelectionCharacter != null) stageSelectionCharacter.SetActive(false);
            OpenCombatDeckInspection((CombatDeckInspectionTarget)target);
        }
        private void DrawStageSelectionOverlay(float scale, float offsetX, float offsetY)
        {
            if (canvasContextTitle != null && canvasDeckButton != null) return;
            EnsureDiscardStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            if (canvasDeckButton == null && GUI.Button(UiRect(new Rect(0f, 28f, 190f, 48f), new Rect(0f, 64f, 190f, 48f)), Ui("덱 확인", "View Deck"), discardButtonStyle)) OpenStageDeckInspection();
            if (canvasContextTitle == null) GUI.Label(new Rect(0f, 40f, UiReferenceWidth, 54f), Ui("스테이지 선택", "Choose a Stage"), deckRarityStyle);
            if (canvasContextMessage == null) GUI.Label(new Rect(0f, 92f, UiReferenceWidth, 34f),
                Ui("카드를 위로 드래그해 스테이지를 선택하세요.", "Drag a stage card upward to choose it."), discardMessageStyle);
            GUI.matrix = previousMatrix;
        }
        // A new run starts directly with a five-card hand instead of a pack-opening step.
    }
}
