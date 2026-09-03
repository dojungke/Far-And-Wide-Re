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
        public bool DebugAddCardToDeck(global::CardData data, int number,
            global::CardColor color)
        {
            if (data == null) return false;
            StoredCard card = new StoredCard
            {
                Name = data.Name,
                Data = data,
                Rarity = data.Rare,
                Color = color,
                Number = Mathf.Clamp(number, 1, 6),
                CombinedCopies = 1
            };
            if (TryMergeCardIntoDeck(card) || TryAutoEquipMagic(card) || TryAutoEquipWeapon(card))
                return true;
            if (deckCards.Count >= 5) return false;
            card.IsStoredInDeck = true;
            card.DeckSlot = GetFirstEmptyDeckSlot();
            if (card.DeckSlot < 0) return false;
            deckCards.Add(card);
            deckVisuals.Add(BuildDeckVisualForStoredCard(card));
            TryAutoEquipStoredCardsToHost(card);
            TryFuseCardRecipe();
            RefreshDeckCardDisplayNames();
            LayoutDeckVisuals();
            return true;
        }
        private bool StoreCurrentCardInDeck(StoredCard card, int preferredSlot = -1)
        {
            if (card == null || card.IsStoredInDeck) return false;
            if (TryMergeCardIntoDeck(card)) return true;
            if (TryAutoEquipMagic(card) || TryAutoEquipWeapon(card)) return true;
            if (deckCards.Count >= 5) return false;
            int slot = preferredSlot >= 0 && preferredSlot < 5 && GetDeckIndexAtSlot(preferredSlot) < 0
                ? preferredSlot
                : GetFirstEmptyDeckSlot();
            if (slot < 0) return false;
            card.IsStoredInDeck = true;
            card.DeckSlot = slot;
            deckCards.Add(card);
            CreateStoredCardVisual();
            TryAutoEquipStoredCardsToHost(card);
            TryFuseCardRecipe();
            RefreshDeckCardDisplayNames();
            return true;
        }
        private int GetFirstEmptyDeckSlot()
        {
            for (int slot = 0; slot < 5; slot++)
                if (GetDeckIndexAtSlot(slot) < 0) return slot;
            return -1;
        }
        private int GetDeckIndexAtSlot(int slot)
        {
            for (int i = 0; i < deckCards.Count; i++)
                if (deckCards[i] != null && deckCards[i].DeckSlot == slot) return i;
            return -1;
        }
        private void CreateStoredCardVisual()
        {
            if (cardIndex < 0 || cardIndex >= cards.Count || deckRoot == null) return;
            GameObject copy = Instantiate(cards[cardIndex].gameObject, deckRoot);
            copy.name = "Stored " + cards[cardIndex].gameObject.name;
            copy.SetActive(true);
            Renderer[] storedRenderers = copy.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < storedRenderers.Length; i++)
            {
                storedRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                storedRenderers[i].receiveShadows = false;
            }
            deckVisuals.Add(copy);
            LayoutDeckVisuals();
        }
        private void CreateUsedCardPile()
        {
            if (usedPileRoot == null)
            {
                GameObject pileObject = new GameObject("Used Card Pile");
                usedPileRoot = pileObject.transform;
            }
            if (usedPilePlaceholder != null) Destroy(usedPilePlaceholder.gameObject);

            global::CardColor initialColor = (global::CardColor)UnityEngine.Random.Range(0, 3);
            if (currentPackCards.Count > 0)
            {
                StoredCard openingHandCard = currentPackCards[UnityEngine.Random.Range(0, currentPackCards.Count)];
                if (openingHandCard != null) initialColor = openingHandCard.Color;
            }
            int initialNumber = UnityEngine.Random.Range(1, 7);
            usedPilePlaceholderData = ScriptableObject.CreateInstance<global::CardData>();
            usedPilePlaceholderData.Name = "버린 카드 더미";
            usedPilePlaceholderData.Description = "전투 시작 카드";
            usedPilePlaceholderData.Rare = global::CardRarity.Common;
            usedPilePlaceholder = CardVisual.CreatePrefabInstance("Starting Discard Card", usedPileRoot);
            string colorKey = initialColor.ToString();
            Material attributeMaterial = GetTextureMaterial("UsedPileAttribute_" + colorKey,
                "CardAssets/Attributes/Attribute" + colorKey, false);
            Material patternMaterial = GetTextureMaterial("UsedPilePattern", "CardAssets/Rarities/PatternCommon", true, 0);
            Material costMaterial = GetTextureMaterial("UsedPileCost_" + initialNumber,
                "CardAssets/Costs/Cost" + initialNumber, true, 20);
            usedPilePlaceholder.BuildFromData(usedPilePlaceholderData, initialColor,
                attributeMaterial, GetTextureMaterial("CardBack",
                    "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                patternMaterial, GetTextureMaterial("UsedPileMana", "CardAssets/Content/Mana", true, 10),
                costMaterial, font, IsEnglishUi);
            usedPilePlaceholder.SetDisplayName("버린 카드 더미");
            usedPilePlaceholder.gameObject.SetActive(true);
            lastUsedCard = new StoredCard { Color = initialColor, Number = initialNumber };
        }
        private void ResetUsedPileReference(global::CardColor color, int number)
        {
            if (usedPileRoot == null)
            {
                GameObject pileObject = new GameObject("Used Card Pile");
                usedPileRoot = pileObject.transform;
            }
            if (usedPilePlaceholder != null) Destroy(usedPilePlaceholder.gameObject);
            usedPilePlaceholderData = ScriptableObject.CreateInstance<global::CardData>();
            usedPilePlaceholderData.Name = "버린 카드 더미";
            usedPilePlaceholderData.Description = "사용 가능한 카드를 만들기 위해 다시 지정된 카드";
            usedPilePlaceholderData.Rare = global::CardRarity.Common;
            usedPilePlaceholder = CardVisual.CreatePrefabInstance("Reset Discard Card", usedPileRoot);
            string colorKey = color.ToString();
            Material attributeMaterial = GetTextureMaterial("UsedPileAttribute_" + colorKey,
                "CardAssets/Attributes/Attribute" + colorKey, false);
            Material patternMaterial = GetTextureMaterial("UsedPilePattern", "CardAssets/Rarities/PatternCommon", true, 0);
            Material costMaterial = GetTextureMaterial("UsedPileCost_" + number,
                "CardAssets/Costs/Cost" + number, true, 20);
            usedPilePlaceholder.BuildFromData(usedPilePlaceholderData, color,
                attributeMaterial, GetTextureMaterial("CardBack",
                    "CardAssets/Attributes/AttributeBackRemasterPurple", false),
                patternMaterial, GetTextureMaterial("UsedPileMana", "CardAssets/Content/Mana", true, 10),
                costMaterial, font, IsEnglishUi);
            usedPilePlaceholder.SetDisplayName("버린 카드 더미");
            usedPilePlaceholder.SetInteractionState(true, false);
            usedPilePlaceholder.gameObject.SetActive(true);
            usedPileCard = null;
            lastUsedCard = new StoredCard { Color = color, Number = number };
        }

        private void EnsurePlayableCombatCardAtTurnStart()
        {
            if (cards.Count == 0 || currentPackCards.Count == 0) return;
            for (int i = 0; i < cards.Count; i++)
                if (CanUseCardAtIndex(i, out _)) return;

            List<StoredCard> candidates = new List<StoredCard>();
            for (int i = 0; i < currentPackCards.Count; i++)
                if (currentPackCards[i] != null) candidates.Add(currentPackCards[i]);
            if (candidates.Count == 0) return;

            StoredCard selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            ResetUsedPileReference(selected.Color, selected.Number);
            AddScorePopup(Ui("버린 카드 더미 재지정\n사용 가능한 카드가 생겼습니다.",
                "Discard pile reset\nA playable card is available."),
                new Color(1f, 0.82f, 0.3f), Time.unscaledTime, scorePopups.Count, 0);
        }
        private Vector3 GetUsedPileWorldPosition()
        {
            return GetResponsiveDiscardPileWorldPosition();
        }
        private TMP_FontAsset GetPileCountFontAsset()
        {
            if (pileCountFontAsset != null && pileCountSourceFont == font) return pileCountFontAsset;
            pileCountSourceFont = font;
            if (font != null)
            {
                pileCountFontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 6,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
            }
            if (pileCountFontAsset == null) pileCountFontAsset = TMP_Settings.defaultFontAsset;
            return pileCountFontAsset;
        }
        private void UpdateDiscardPileCountLabel(ref TextMeshPro label, Transform pileRoot, Vector3 pilePosition,
            float pileScale, int drawCount, int discardCount)
        {
            if (pileRoot == null) return;
            if (label == null)
            {
                GameObject labelObject = new GameObject("Pile Count Text", typeof(RectTransform), typeof(TextMeshPro));
                labelObject.transform.SetParent(pileRoot, false);
                label = labelObject.GetComponent<TextMeshPro>();
                label.font = GetPileCountFontAsset();
                label.fontStyle = FontStyles.Bold;
                label.fontWeight = FontWeight.Heavy;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSize = Mathf.Min(20f, pileCountTextFontSize);
                label.fontSizeMax = 20f;
                label.fontSizeMin = 0.1f;
                label.color = Color.white;
                CombatTextOutline.ApplyToWhiteText(label);
                label.extraPadding = true;
                label.rectTransform.sizeDelta = new Vector2(18f, 5.4f);
                MeshRenderer renderer = label.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sortingOrder = 1700;
            }
            Camera camera = Camera.main;
            if (camera == null) return;
            if ((pileRoot.position - pilePosition).sqrMagnitude > 0.000001f)
                pileRoot.position = pilePosition;

            bool shouldBeVisible = pileRoot.gameObject.activeInHierarchy && !usedPileExpanded;
            if (label.gameObject.activeSelf != shouldBeVisible) label.gameObject.SetActive(shouldBeVisible);

            TMP_FontAsset desiredFont = GetPileCountFontAsset();
            if (label.font != desiredFont) label.font = desiredFont;
            string displayText = drawCount + "/" + discardCount;
            if (label.text != displayText)
            {
                label.text = displayText;
                if (label.font != null) label.font.TryAddCharacters(displayText, out _);
            }

            Vector3 desiredLocalPosition = new Vector3(0f, 1.75f + discardPileHoverOffsetY, -0.02f);
            if ((label.transform.localPosition - desiredLocalPosition).sqrMagnitude > 0.000001f)
                label.transform.localPosition = desiredLocalPosition;
            if (Quaternion.Angle(label.transform.rotation, camera.transform.rotation) > 0.01f)
                label.transform.rotation = camera.transform.rotation;
            Vector3 desiredScale = Vector3.one * pileCountTextWorldScale;
            if ((label.transform.localScale - desiredScale).sqrMagnitude > 0.000001f)
                label.transform.localScale = desiredScale;
        }

        private void LayoutUsedCardPile()
        {
            if (usedPileAnimating) return;
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 pilePosition = GetUsedPileWorldPosition();
            // The discard pile keeps a stable size while turns change.
            float cardScale = CurrentHandCardScale * 0.9f;
            if (usedPileCountText != null) usedPileCountText.gameObject.SetActive(!shopChoiceActive);
            UpdateDiscardPileCountLabel(ref usedPileCountText, usedPileRoot, pilePosition, cardScale,
                starterDrawPile.Count, usedPileStoredCards.Count);
            bool hasUsedTopCard = usedPileCard != null;
            if (usedPilePlaceholder != null) usedPilePlaceholder.gameObject.SetActive(!hasUsedTopCard);
            if (!hasUsedTopCard)
            {
                for (int i = 0; i < usedPileHistory.Count; i++)
                    if (usedPileHistory[i] != null) usedPileHistory[i].gameObject.SetActive(false);
                if (usedPilePlaceholder == null) return;
                usedPilePlaceholder.transform.position = pilePosition;
                usedPilePlaceholder.transform.localScale = Vector3.one * cardScale;
                usedPilePlaceholder.transform.rotation = camera.transform.rotation;
                usedPilePlaceholder.SetSortingOrder(1600);
                return;
            }
            if (!usedPileExpanded)
            {
                for (int i = 0; i < usedPileHistory.Count; i++)
                {
                    CardVisual card = usedPileHistory[i];
                    if (card == null) continue;
                    bool isLastUsedCard = i == usedPileHistory.Count - 1;
                    card.gameObject.SetActive(isLastUsedCard);
                    if (!isLastUsedCard) continue;
                    card.transform.position = pilePosition;
                    card.transform.localScale = Vector3.one * cardScale;
                    card.transform.rotation = camera.transform.rotation;
                card.SetSortingOrder(1600);
                }
                return;
            }
            if (usedPileDetailCard != null)
            {
                float detailDepth = camera.WorldToScreenPoint(CardHome).z;
                GetUiLayout(out float uiScale, out float offsetX, out float offsetY);
                float screenHeightScale = Screen.height > 0 ? Screen.height / ReferenceHeight : 1f;
                float deckScale = screenHeightScale > 0f ? uiScale / screenHeightScale : 1f;
                float inspectionLayoutY = IsPortraitUi ? 610f + PortraitExtraHeight * 0.5f : 352.8f;
                float inspectionGuiX = offsetX + UiReferenceWidth * 0.5f * uiScale;
                float inspectionGuiY = offsetY + inspectionLayoutY * uiScale;
                LayoutDeckInspectionBackdrop(camera, detailDepth);
                for (int i = 0; i < usedPileHistory.Count; i++)
                {
                    CardVisual card = usedPileHistory[i];
                    if (card == null) continue;
                    bool isSelected = card == usedPileDetailCard;
                    card.gameObject.SetActive(isSelected);
                    if (!isSelected) continue;
                    card.transform.position = camera.ScreenToWorldPoint(new Vector3(
                        inspectionGuiX, Screen.height - inspectionGuiY, detailDepth));
                    card.transform.localScale = Vector3.one * ((IsPortraitUi ? 2.10f : 1.72f) * deckScale);
                    if (!deckInspectionDragging && !deckInspectionReturning)
                        card.transform.rotation = camera.transform.rotation;
                }
                return;
            }

            int columns = IsPortraitUi ? 3 : 5;
            int rows = Mathf.CeilToInt(usedPileHistory.Count / (float)columns);
            float cardSpacingX = Screen.width * (IsPortraitUi ? 0.29f : 0.17f);
            float cardSpacingY = Screen.height * (IsPortraitUi ? 0.30f : 0.43f);
            float startY = Screen.height * 0.56f + (rows - 1) * cardSpacingY * 0.5f;
            float depth = camera.WorldToScreenPoint(CardHome).z;
            for (int i = 0; i < usedPileHistory.Count; i++)
            {
                CardVisual card = usedPileHistory[i];
                if (card == null) continue;
                int row = i / columns;
                int column = i % columns;
                float rowCardCount = Mathf.Min(columns, usedPileHistory.Count - row * columns);
                float rowX = Screen.width * 0.5f - (rowCardCount - 1) * cardSpacingX * 0.5f;
                card.gameObject.SetActive(true);
                card.transform.position = camera.ScreenToWorldPoint(new Vector3(
                    rowX + column * cardSpacingX, startY - row * cardSpacingY, depth));
                card.transform.localScale = Vector3.one * ResponsiveWorldScale(0.84f, 1.02f);
                card.transform.rotation = camera.transform.rotation;
            }
        }
        private void CreateEmptyDeckPlaceholder()
        {
            if (deckRoot == null) return;
            Material blackMaterial = GetMaterial("EmptyDeckCard", Color.black, 0.08f);
            for (int i = 0; i < 5; i++)
            {
                CardVisual placeholderVisual = CardVisual.CreatePrefabInstance("Empty Deck Card " + (i + 1), deckRoot);
                GameObject placeholder = placeholderVisual.gameObject;
                placeholderVisual.Build(default(CardData), blackMaterial, blackMaterial, null, null, font);
                SetStoredVisualShadowMode(placeholder);
                emptyDeckPlaceholders.Add(placeholder);
            }
        }
        private void LayoutDeckVisuals()
        {
            if (startingHandVisible)
            {
                for (int i = 0; i < emptyDeckPlaceholders.Count; i++)
                    if (emptyDeckPlaceholders[i] != null) emptyDeckPlaceholders[i].SetActive(false);
                for (int i = 0; i < deckVisuals.Count; i++)
                    if (deckVisuals[i] != null) deckVisuals[i].SetActive(false);
                return;
            }
            Camera camera = Camera.main;
            if (camera == null) return;
            float depth = camera.WorldToScreenPoint(CardHome).z;
            GetUiLayout(out float uiScale, out float offsetX, out float offsetY);
            float screenHeightScale = Screen.height > 0 ? Screen.height / ReferenceHeight : 1f;
            float deckScale = screenHeightScale > 0f ? uiScale / screenHeightScale : 1f;
            bool resultScreen = phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared;
            float deckLayoutY = IsPortraitUi ? 1165f + PortraitExtraHeight : (resultScreen ? 635f : 622.8f);
            float inspectionLayoutY = IsPortraitUi ? 610f + PortraitExtraHeight * 0.5f : 352.8f;
            float deckGuiY = offsetY + deckLayoutY * uiScale;
            float inspectionGuiY = offsetY + inspectionLayoutY * uiScale;
            float deckCardScale = IsPortraitUi ? 0.66f : 0.43f;
            float deckStartX = IsPortraitUi ? 140f : (resultScreen ? 470f : 53.76f);
            bool isInspecting = inspectedDeckIndex >= 0 && inspectedDeckIndex < deckVisuals.Count;
            int liftedDeckSlot = deckCardDragActive && pressedDeckIndex >= 0 && pressedDeckIndex < deckCards.Count
                && deckCards[pressedDeckIndex] != null
                ? deckCards[pressedDeckIndex].DeckSlot
                : -1;
            for (int i = 0; i < emptyDeckPlaceholders.Count; i++)
            {
                GameObject placeholder = emptyDeckPlaceholders[i];
                if (placeholder == null) continue;
                bool showPlaceholder = (GetDeckIndexAtSlot(i) < 0 || i == liftedDeckSlot) && !isInspecting;
                placeholder.SetActive(showPlaceholder);
                if (!showPlaceholder) continue;
                float deckSpacing = IsPortraitUi ? 110f : (resultScreen ? 85f : 74.24f);
                float deckGuiX = offsetX + (deckStartX + i * deckSpacing) * uiScale;
                placeholder.transform.position =
                    camera.ScreenToWorldPoint(new Vector3(deckGuiX, Screen.height - deckGuiY, depth));
                placeholder.transform.localScale = Vector3.one * (deckCardScale * deckScale);
                placeholder.transform.rotation = camera.transform.rotation;
            }
            for (int i = 0; i < deckVisuals.Count; i++)
            {
                GameObject visual = deckVisuals[i];
                if (visual == null) continue;
                bool selected = isInspecting && i == inspectedDeckIndex;
                visual.SetActive(!isInspecting || selected);
                if (!visual.activeSelf) continue;
                if (!isInspecting && deckCardDragActive && i == pressedDeckIndex) continue;
                if (selected)
                {
                    float inspectionGuiX = offsetX + UiReferenceWidth * 0.5f * uiScale;
                    visual.transform.position = camera.ScreenToWorldPoint(
                        new Vector3(inspectionGuiX, Screen.height - inspectionGuiY, depth));
                    visual.transform.localScale = Vector3.one * ((IsPortraitUi ? 2.10f : 1.72f) * deckScale);
                }
                else
                {
                    int slot = i < deckCards.Count && deckCards[i] != null ? deckCards[i].DeckSlot : i;
                    float deckSpacing = IsPortraitUi ? 110f : (resultScreen ? 85f : 74.24f);
                    float deckGuiX = offsetX + (deckStartX + Mathf.Clamp(slot, 0, 4) * deckSpacing) * uiScale;
                    visual.transform.position = camera.ScreenToWorldPoint(
                        new Vector3(deckGuiX, Screen.height - deckGuiY, depth));
                    visual.transform.localScale = Vector3.one * (deckCardScale * deckScale);
                }
                if (!selected || (!deckInspectionDragging && !deckInspectionReturning))
                    visual.transform.rotation = camera.transform.rotation;
            }
            LayoutDeckInspectionBackdrop(camera, depth);
        }
        private void CreateDeckInspectionBackdrop()
        {
            deckInspectionBackdrop = CreateQuadObject("Deck Inspection Backdrop");
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null)
                throw new InvalidOperationException("CardOpen could not load the inspection backdrop shader. Check Always Included Shaders.");
            Material material = new Material(shader)
            {
                name = "Deck Inspection Black",
                color = new Color(0f, 0f, 0f, 0.84f)
            };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.84f));
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = 3000;
            MeshRenderer renderer = deckInspectionBackdrop.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 2500;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            deckInspectionBackdrop.SetActive(false);
        }
        private void LayoutDeckInspectionBackdrop(Camera camera, float cardDepth)
        {
            if (deckInspectionBackdrop == null || (inspectedDeckIndex < 0 && usedPileDetailCard == null && !combatDeckInspectionVisible)) return;
            float backdropDepth = cardDepth + 3.2f;
            float height = 2f * backdropDepth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            deckInspectionBackdrop.transform.position = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, backdropDepth));
            deckInspectionBackdrop.transform.rotation = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);
            deckInspectionBackdrop.transform.localScale = new Vector3(height * camera.aspect * 1.08f, height * 1.08f, 1f);
        }
        private bool IsDeckInspectionReadOnly()
        {
            return sharedResultMode || phase == RevealPhase.PackChoice
                || phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared;
        }
        private bool HandleDeckPointer(Vector2 screenPoint, Event inputEvent)
        {
            bool isInspecting = inspectedDeckIndex >= 0;
            bool readOnly = IsDeckInspectionReadOnly();
            Camera camera = Camera.main;
            if (camera == null) return isInspecting;
            if (isInspecting)
            {
                if (discardConfirmationVisible)
                {
                    if (inputEvent.isMouse) inputEvent.Use();
                    return true;
                }
                GameObject selected = inspectedDeckIndex < deckVisuals.Count ? deckVisuals[inspectedDeckIndex] : null;
                if (inputEvent.type == EventType.MouseDown)
                {
                    if (deckInspectionReturnRoutine != null) StopCoroutine(deckInspectionReturnRoutine);
                    deckInspectionReturnRoutine = null;
                    deckInspectionReturning = false;
                    deckInspectionDragging = true;
                    deckInspectionHasDragged = false;
                    deckInspectionPressOutside = selected == null || !GetVisualScreenRect(selected, camera).Contains(screenPoint);
                    deckInspectionDragStart = screenPoint;
                    if (selected != null) deckInspectionStartRotation = selected.transform.rotation;
                    inputEvent.Use();
                    return true;
                }
                if (inputEvent.type == EventType.MouseDrag && deckInspectionDragging)
                {
                    Vector2 delta = screenPoint - deckInspectionDragStart;
                    if (delta.sqrMagnitude >= 16f) deckInspectionHasDragged = true;
                    if (selected != null && deckInspectionHasDragged)
                    {
                        selected.transform.rotation = Quaternion.Euler(-delta.y * 0.24f, delta.x * 0.28f, 0f)
                            * deckInspectionStartRotation;
                    }
                    inputEvent.Use();
                    return true;
                }
                if (inputEvent.type == EventType.MouseUp && deckInspectionDragging)
                {
                    deckInspectionDragging = false;
                    if (deckInspectionPressOutside && !deckInspectionHasDragged)
                        CloseDeckInspection();
                    else if (selected != null && deckInspectionHasDragged)
                        deckInspectionReturnRoutine = StartCoroutine(ReturnInspectedDeckCard(selected));
                    deckInspectionPressOutside = false;
                    deckInspectionHasDragged = false;
                    inputEvent.Use();
                    return true;
                }
                if (inputEvent.isMouse) inputEvent.Use();
                return true;
            }
            if (inputEvent.type == EventType.MouseUp && gestureDragging && phase == RevealPhase.CardFront
                && IsPointInDeckRow(screenPoint))
            {
                gestureDragging = false;
                TryDropCurrentCardIntoDeck(screenPoint);
                inputEvent.Use();
                return true;
            }
            if (inputEvent.type == EventType.MouseDown)
            {
                for (int i = deckVisuals.Count - 1; i >= 0; i--)
                {
                    GameObject visual = deckVisuals[i];
                    if (visual == null || !visual.activeSelf || !GetVisualScreenRect(visual, camera).Contains(screenPoint)) continue;
                    pressedDeckIndex = i;
                    deckCardDragStart = screenPoint;
                    deckCardDragActive = false;
                    inputEvent.Use();
                    return true;
                }
                return false;
            }
            if (inputEvent.type == EventType.MouseDrag && pressedDeckIndex >= 0)
            {
                Vector2 delta = screenPoint - deckCardDragStart;
                if (delta.sqrMagnitude >= 25f) deckCardDragActive = true;
                if (deckCardDragActive && !readOnly && pressedDeckIndex < deckVisuals.Count)
                {
                    GameObject dragged = deckVisuals[pressedDeckIndex];
                    float depth = camera.WorldToScreenPoint(CardHome).z - 0.45f;
                    dragged.transform.position = camera.ScreenToWorldPoint(
                        new Vector3(screenPoint.x, Screen.height - screenPoint.y, depth));
                    dragged.transform.rotation = camera.transform.rotation;
                    GetUiLayout(out float uiScale, out _, out _);
                    float heightScale = Screen.height > 0 ? Screen.height / ReferenceHeight : 1f;
                    float dragScale = heightScale > 0f ? uiScale / heightScale : 1f;
                    dragged.transform.localScale = Vector3.one * (0.52f * dragScale);
                }
                inputEvent.Use();
                return true;
            }
            if (inputEvent.type == EventType.MouseUp && pressedDeckIndex >= 0)
            {
                int sourceIndex = pressedDeckIndex;
                bool wasDragged = deckCardDragActive;
                pressedDeckIndex = -1;
                deckCardDragActive = false;
                if (!wasDragged)
                {
                    OpenDeckInspection(sourceIndex);
                }
                else if (readOnly)
                {
                    // Result and pack-choice decks can be inspected but not edited.
                }
                else if (IsPointOverCurrentCard(screenPoint, camera))
                {
                    SwapDeckCardWithCurrent(sourceIndex);
                }
                else
                {
                    int targetSlot = GetDeckSlotAtPoint(screenPoint);
                    int targetIndex = GetDeckIndexAtSlot(targetSlot);
                    if (targetIndex >= 0 && targetIndex != sourceIndex
                        && (TryEquipDeckMagic(sourceIndex, targetIndex)
                            || TryEquipDeckWeapon(sourceIndex, targetIndex)))
                    {
                        RefreshDeckCardDisplayNames();
                    }
                    else if (targetSlot >= 0)
                    {
                        MoveDeckCardToSlot(sourceIndex, targetSlot);
                    }
                }
                LayoutDeckVisuals();
                inputEvent.Use();
                return true;
            }
            return pressedDeckIndex >= 0;
        }
        private bool IsPointOverCurrentCard(Vector2 screenPoint, Camera camera)
        {
            return phase == RevealPhase.CardFront && cardIndex >= 0 && cardIndex < cards.Count
                && cards[cardIndex] != null
                && GetVisualScreenRect(cards[cardIndex].gameObject, camera).Contains(screenPoint);
        }
        private static bool IsPointInDeckRow(Vector2 screenPoint)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return false;
            Vector2 referencePoint = ScreenToReferencePoint(screenPoint);
            Rect deckRow = IsPortraitUi
                ? new Rect(0f, 1035f + PortraitExtraHeight, PortraitWidth, 245f)
                : new Rect(0f, 518.4f, 460.8f, 201.6f);
            return deckRow.Contains(referencePoint);
        }
        private static int GetDeckSlotAtPoint(Vector2 screenPoint)
        {
            if (!IsPointInDeckRow(screenPoint)) return -1;
            Vector2 referencePoint = ScreenToReferencePoint(screenPoint);
            float startX = IsPortraitUi ? 150f : 53.76f;
            float spacing = IsPortraitUi ? 105f : 74.24f;
            int slot = Mathf.RoundToInt((referencePoint.x - startX) / spacing);
            return Mathf.Clamp(slot, 0, 4);
        }
        private bool TryEquipDeckMagic(int sourceIndex, int hostIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= deckCards.Count || hostIndex < 0
                || hostIndex >= deckCards.Count || sourceIndex == hostIndex) return false;
            StoredCard magic = deckCards[sourceIndex];
            StoredCard host = deckCards[hostIndex];
            if (magic == null || magic.Data == null || !magic.Data.HasTag(global::CardTag.Magic)
                || IsStackableCardData(magic.Data)
                || host == null || host.Data == null || !host.Data.CanEquipMagic) return false;
            if (host.EquippedMagic != null)
            {
                host.EquippedMagic.IsStoredInDeck = false;
                host.EquippedMagic.DeckSlot = -1;
            }
            GameObject magicVisual = sourceIndex < deckVisuals.Count ? deckVisuals[sourceIndex] : null;
            deckCards.RemoveAt(sourceIndex);
            if (sourceIndex < deckVisuals.Count) deckVisuals.RemoveAt(sourceIndex);
            if (magicVisual != null) Destroy(magicVisual);
            magic.IsStoredInDeck = true;
            magic.DeckSlot = -1;
            host.EquippedMagic = magic;
            PlayMagicEquipSound();
            return true;
        }
        private bool TryEquipCurrentMagic(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= deckCards.Count || cardIndex < 0
                || cardIndex >= currentPackCards.Count) return false;
            StoredCard host = deckCards[deckIndex];
            StoredCard magic = currentPackCards[cardIndex];
            if (host == null || host.Data == null || !host.Data.CanEquipMagic
                || magic == null || magic.Data == null || magic.IsStoredInDeck
                || IsStackableCardData(magic.Data)
                || !magic.Data.HasTag(global::CardTag.Magic)) return false;
            if (host.EquippedMagic != null)
            {
                host.EquippedMagic.IsStoredInDeck = false;
                host.EquippedMagic.DeckSlot = -1;
            }
            magic.IsStoredInDeck = true;
            magic.DeckSlot = -1;
            host.EquippedMagic = magic;
            PlayMagicEquipSound();
            RefreshDeckCardDisplayNames();
            return true;
        }
        private bool TryEquipDeckWeapon(int sourceIndex, int hostIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= deckCards.Count || hostIndex < 0
                || hostIndex >= deckCards.Count || sourceIndex == hostIndex) return false;
            StoredCard weapon = deckCards[sourceIndex];
            StoredCard host = deckCards[hostIndex];
            if (weapon == null || weapon.Data == null || !weapon.Data.HasTag(global::CardTag.Weapon)
                || IsStackableCardData(weapon.Data)
                || host == null || host.Data == null || !host.Data.CanEquipWeapon) return false;
            if (host.EquippedWeapon != null)
            {
                host.EquippedWeapon.IsStoredInDeck = false;
                host.EquippedWeapon.DeckSlot = -1;
            }
            GameObject weaponVisual = sourceIndex < deckVisuals.Count ? deckVisuals[sourceIndex] : null;
            deckCards.RemoveAt(sourceIndex);
            if (sourceIndex < deckVisuals.Count) deckVisuals.RemoveAt(sourceIndex);
            if (weaponVisual != null) Destroy(weaponVisual);
            weapon.IsStoredInDeck = true;
            weapon.DeckSlot = -1;
            host.EquippedWeapon = weapon;
            PlayMagicEquipSound();
            return true;
        }
        private bool TryEquipCurrentWeapon(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= deckCards.Count || cardIndex < 0
                || cardIndex >= currentPackCards.Count) return false;
            StoredCard host = deckCards[deckIndex];
            StoredCard weapon = currentPackCards[cardIndex];
            if (host == null || host.Data == null || !host.Data.CanEquipWeapon
                || weapon == null || weapon.Data == null || weapon.IsStoredInDeck
                || IsStackableCardData(weapon.Data)
                || !weapon.Data.HasTag(global::CardTag.Weapon)) return false;
            if (host.EquippedWeapon != null)
            {
                host.EquippedWeapon.IsStoredInDeck = false;
                host.EquippedWeapon.DeckSlot = -1;
            }
            weapon.IsStoredInDeck = true;
            weapon.DeckSlot = -1;
            host.EquippedWeapon = weapon;
            PlayMagicEquipSound();
            RefreshDeckCardDisplayNames();
            return true;
        }
        private void TryDropCurrentCardIntoDeck(Vector2 screenPoint)
        {
            if (cardIndex < 0 || cardIndex >= cards.Count || cardIndex >= currentPackCards.Count) return;
            int slot = GetDeckSlotAtPoint(screenPoint);
            if (slot < 0) return;
            int occupiedDeckIndex = GetDeckIndexAtSlot(slot);
            if (occupiedDeckIndex >= 0)
            {
                if (TryEquipCurrentMagic(occupiedDeckIndex)
                    || TryEquipCurrentWeapon(occupiedDeckIndex))
                {
                    StartCoroutine(AdvanceAfterDeckDrop());
                    LayoutDeckVisuals();
                    return;
                }
                SwapDeckCardWithCurrent(occupiedDeckIndex);
                LayoutDeckVisuals();
                return;
            }
            if (!StoreCurrentCardInDeck(currentPackCards[cardIndex], slot)) return;
            StartCoroutine(AdvanceAfterDeckDrop());
            LayoutDeckVisuals();
        }
        private IEnumerator AdvanceAfterDeckDrop()
        {
            CommitPendingScoreImmediately();
            phase = RevealPhase.Animating;
            cardTransitionActive = true;
            CardVisual current = cards[cardIndex];
            if (cardIndex + 1 < cards.Count)
            {
                CardVisual next = cards[cardIndex + 1];
                next.gameObject.SetActive(true);
                next.PrepareFaceUp(CardHome + new Vector3(0f, 0.035f, 0.035f), CurrentRevealedCardScale, 0f);
                next.SetFaceDetailsVisible(true);
            }
            if (current != null) current.gameObject.SetActive(false);
            cardIndex++;
            if (cardIndex >= cards.Count)
            {
                cardTransitionActive = false;
                yield return new WaitForSeconds(0.35f);
                CompletePackAndBeginNextSequence();
                yield break;
            }
            yield return cards[cardIndex].MoveToFront(CardHome, CurrentRevealedCardScale, 0f);
            yield return RestoreCardStackRotation();
            PlayCardRarityRevealSound(currentPackCards[cardIndex].Rarity);
            AwardCurrentCardScore();
            cardTransitionActive = false;
            phase = RevealPhase.CardFront;
        }
        private bool SwapDeckCardWithCurrent(int deckIndex)
        {
            if (phase != RevealPhase.CardFront || cardIndex < 0 || cardIndex >= cards.Count
                || cardIndex >= currentPackCards.Count || deckIndex < 0 || deckIndex >= deckCards.Count) return false;
            StoredCard currentData = currentPackCards[cardIndex];
            int existingCurrentSlot = deckCards.IndexOf(currentData);
            if (existingCurrentSlot == deckIndex) return false;
            if (existingCurrentSlot >= 0)
            {
                GameObject duplicateVisual = deckVisuals[existingCurrentSlot];
                deckCards.RemoveAt(existingCurrentSlot);
                deckVisuals.RemoveAt(existingCurrentSlot);
                if (duplicateVisual != null) Destroy(duplicateVisual);
                currentData.IsStoredInDeck = false;
                if (existingCurrentSlot < deckIndex) deckIndex--;
            }
            if (deckIndex < 0 || deckIndex >= deckCards.Count) return false;
            StoredCard deckData = deckCards[deckIndex];
            GameObject deckObject = deckVisuals[deckIndex];
            CardVisual incomingVisual = deckObject != null ? deckObject.GetComponent<CardVisual>() : null;
            CardVisual outgoingVisual = cards[cardIndex];
            if (deckData == null || incomingVisual == null || outgoingVisual == null) return false;
            TransferOrSwapCompatibleEquipment(deckData, currentData);
            currentData.IsStoredInDeck = true;
            currentData.DeckSlot = deckData.DeckSlot;
            deckData.IsStoredInDeck = false;
            deckData.DeckSlot = -1;
            deckCards[deckIndex] = currentData;
            currentPackCards[cardIndex] = deckData;
            GameObject outgoingObject = outgoingVisual.gameObject;
            outgoingObject.transform.SetParent(deckRoot, true);
            outgoingObject.SetActive(true);
            SetStoredVisualShadowMode(outgoingObject);
            deckVisuals[deckIndex] = outgoingObject;
            deckObject.transform.SetParent(cardStack, false);
            deckObject.SetActive(true);
            incomingVisual.PrepareFaceUp(CardHome, CurrentRevealedCardScale, 0f);
            incomingVisual.SetFaceDetailsVisible(true);
            cards[cardIndex] = incomingVisual;
            incomingVisual.SetDisplayName(GetStoredCardDisplayName(deckData));
            incomingVisual.SetDisplayDescription(deckData.Data, GetStoredCardDisplayDescription(deckData), IsEnglishUi,
                GetMineralMiningOddsLine(deckData.Data));
            RefreshDeckCardDisplayNames();
            LayoutDeckVisuals();
            return true;
        }
        private void TransferOrSwapCompatibleEquipment(StoredCard source, StoredCard target)
        {
            if (source == null || source.Data == null || target == null || target.Data == null) return;
            bool changed = false;
            if (source.EquippedMagic != null && target.Data.CanEquipMagic)
            {
                StoredCard targetMagic = target.EquippedMagic;
                target.EquippedMagic = source.EquippedMagic;
                source.EquippedMagic = targetMagic;
                changed = true;
            }
            if (source.EquippedWeapon != null && target.Data.CanEquipWeapon)
            {
                StoredCard targetWeapon = target.EquippedWeapon;
                target.EquippedWeapon = source.EquippedWeapon;
                source.EquippedWeapon = targetWeapon;
                changed = true;
            }
            if (changed) PlayMagicEquipSound();
        }
        private void MoveDeckCardToSlot(int sourceIndex, int targetSlot)
        {
            if (sourceIndex < 0 || sourceIndex >= deckCards.Count || targetSlot < 0 || targetSlot >= 5) return;
            StoredCard source = deckCards[sourceIndex];
            if (source == null || source.DeckSlot == targetSlot) return;
            int sourceSlot = source.DeckSlot;
            int targetIndex = GetDeckIndexAtSlot(targetSlot);
            if (targetIndex >= 0 && targetIndex != sourceIndex && deckCards[targetIndex] != null)
                deckCards[targetIndex].DeckSlot = sourceSlot;
            source.DeckSlot = targetSlot;
        }
        private static void SetStoredVisualShadowMode(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
        }
        private IEnumerator ReturnInspectedDeckCard(GameObject selected)
        {
            if (selected == null)
            {
                deckInspectionReturnRoutine = null;
                yield break;
            }
            deckInspectionReturning = true;
            Quaternion startRotation = selected.transform.rotation;
            const float duration = 0.38f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                Camera camera = Camera.main;
                if (camera == null) break;
                float u = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                selected.transform.rotation = Quaternion.Slerp(startRotation, camera.transform.rotation, u);
                yield return null;
            }
            Camera finalCamera = Camera.main;
            if (selected != null && finalCamera != null) selected.transform.rotation = finalCamera.transform.rotation;
            CardVisual returnedCard = selected != null ? selected.GetComponent<CardVisual>() : null;
            if (returnedCard != null) returnedCard.SetFaceUp(true);
            deckInspectionReturning = false;
            deckInspectionReturnRoutine = null;
        }
        private void OpenDeckInspection(int index)
        {
            if (index < 0 || index >= deckVisuals.Count) return;
            inspectedDeckIndex = index;
            discardConfirmationVisible = false;
            deckInspectionDragging = false;
            deckInspectionReturning = false;
            deckInspectionPressOutside = false;
            deckInspectionHasDragged = false;
            inspectionPackWasActive = pack != null && pack.gameObject.activeSelf;
            inspectionStackWasActive = cardStack != null && cardStack.gameObject.activeSelf;
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(false);
            if (phase == RevealPhase.PackChoice)
            {
                if (leftPackChoiceVisual != null) leftPackChoiceVisual.gameObject.SetActive(false);
                if (rightPackChoiceVisual != null) rightPackChoiceVisual.gameObject.SetActive(false);
            }
            if (packContentsPreviewVisual != null)
                packContentsPreviewVisual.gameObject.SetActive(false);
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(true);
            LayoutDeckVisuals();
        }
        private void CloseDeckInspection()
        {
            if (deckInspectionReturnRoutine != null) StopCoroutine(deckInspectionReturnRoutine);
            deckInspectionReturnRoutine = null;
            deckInspectionDragging = false;
            deckInspectionReturning = false;
            deckInspectionPressOutside = false;
            deckInspectionHasDragged = false;
            discardConfirmationVisible = false;
            inspectedDeckIndex = -1;
            if (pack != null) pack.gameObject.SetActive(inspectionPackWasActive);
            if (cardStack != null) cardStack.gameObject.SetActive(inspectionStackWasActive);
            if (phase == RevealPhase.PackChoice && inspectedPackChoice == null)
            {
                if (leftPackChoiceVisual != null) leftPackChoiceVisual.gameObject.SetActive(true);
                if (rightPackChoiceVisual != null) rightPackChoiceVisual.gameObject.SetActive(true);
            }
            if (deckInspectionBackdrop != null) deckInspectionBackdrop.SetActive(false);
            if (inspectedPackChoice != null && packContentsPreviewVisual != null)
                packContentsPreviewVisual.gameObject.SetActive(true);
            for (int i = 0; i < deckVisuals.Count; i++)
                if (deckVisuals[i] != null) deckVisuals[i].SetActive(true);
            LayoutDeckVisuals();
        }
        private static Rect GetVisualScreenRect(GameObject visual, Camera camera)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Rect.zero;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 screen = camera.WorldToScreenPoint(center + Vector3.Scale(extents, new Vector3(x, y, z)));
                float guiY = Screen.height - screen.y;
                minX = Mathf.Min(minX, screen.x);
                maxX = Mathf.Max(maxX, screen.x);
                minY = Mathf.Min(minY, guiY);
                maxY = Mathf.Max(maxY, guiY);
            }
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
        private void DrawScorePopups(float scale, float offsetX, float offsetY)
        {
            if (canvasScorePopupLabels != null) return;
            if (scorePopupStyle == null)
            {
                scorePopupStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 25,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Overflow
                };
                scorePopupStyle.normal.textColor = Color.white;
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            const float portraitPopupStartX = 558f;
            const float portraitPopupEndX = 532f;
            const float portraitPopupWidth = 176f;
            TextAnchor previousAlignment = scorePopupStyle.alignment;
            TextClipping previousClipping = scorePopupStyle.clipping;
            bool previousWordWrap = scorePopupStyle.wordWrap;
            int previousFontSize = scorePopupStyle.fontSize;
            if (IsPortraitUi)
            {
                scorePopupStyle.alignment = TextAnchor.MiddleLeft;
                scorePopupStyle.clipping = TextClipping.Clip;
                scorePopupStyle.wordWrap = false;
                scorePopupStyle.fontSize = 22;
            }
            for (int i = scorePopups.Count - 1; i >= 0; i--)
            {
                ScorePopup popup = scorePopups[i];
                float age = (Time.unscaledTime - popup.StartTime) * Mathf.Max(1f, popup.PlaybackSpeed);
                if (age < 0f) continue;
                if (age >= 1.35f)
                {
                    scorePopups.RemoveAt(i);
                    continue;
                }
                float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.18f));
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1.35f, age));
                float x = Mathf.Lerp(IsPortraitUi ? portraitPopupStartX : 783f,
                    IsPortraitUi ? portraitPopupEndX : 837f, enter);
                float y = (IsPortraitUi ? 350f + PortraitExtraHeight * 0.35f : 270f) + popup.Lane * 72f - Mathf.Clamp01(age / 1.35f) * 24f;
                GUI.color = new Color(popup.Color.r, popup.Color.g, popup.Color.b, fade);
                if (IsPortraitUi)
                {
                    const int maximumPortraitFontSize = 22;
                    scorePopupStyle.fontSize = maximumPortraitFontSize;
                    string[] popupLines = popup.Text.Split('\n');
                    float widestLine = 0f;
                    for (int lineIndex = 0; lineIndex < popupLines.Length; lineIndex++)
                        widestLine = Mathf.Max(widestLine,
                            scorePopupStyle.CalcSize(new GUIContent(popupLines[lineIndex])).x);
                    if (widestLine > portraitPopupWidth)
                        scorePopupStyle.fontSize = Mathf.Max(1, Mathf.FloorToInt(
                            maximumPortraitFontSize * portraitPopupWidth / widestLine));
                }
                GUI.Label(new Rect(x, y, IsPortraitUi ? portraitPopupWidth : 210f, 76f), popup.Text, scorePopupStyle);
            }
            scorePopupStyle.alignment = previousAlignment;
            scorePopupStyle.clipping = previousClipping;
            scorePopupStyle.wordWrap = previousWordWrap;
            scorePopupStyle.fontSize = previousFontSize;
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }
        private bool DrawSettingsButton(float scale, float offsetX, float offsetY)
        {
            if (canvasSettingsButton != null) return false;
            EnsureDiscardStyles();
            if (settingsIconTexture == null) settingsIconTexture = CreateSimpleSettingsIconTexture();
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            Rect settingsButtonRect = UiRect(new Rect(1060f, 28f, 120f, 54f), new Rect(572f, 4f, 120f, 54f));
            bool clicked = canvasSettingsButton == null && GUI.Button(settingsButtonRect, GUIContent.none, discardButtonStyle);
            GUI.DrawTexture(new Rect(settingsButtonRect.x + 39f, settingsButtonRect.y + 6f,
                42f, 42f), settingsIconTexture, ScaleMode.ScaleToFit, true);
            bool consumed = clicked || Event.current.type == EventType.Used;
            GUI.matrix = previousMatrix;
            if (clicked)
            {
                abandonConfirmationVisible = false;
                settingsOpen = true;
            }
            return consumed;
        }
        private void DrawSettingsOverlay(float scale, float offsetX, float offsetY)
        {
            if (canvasSettingsRoot != null) return;
            EnsureDiscardStyles();
            if (settingsTitleStyle == null)
            {
                settingsTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    font = font,
                    fontSize = 38,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.84f, 0.3f) }
                };
                settingsLabelStyle = new GUIStyle(settingsTitleStyle)
                {
                    fontSize = 23,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(0f, 0f, 0f, 0.68f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0f), Quaternion.identity,
                new Vector3(scale, scale, 1f));
            if (abandonConfirmationVisible)
            {
                GUI.Box(UiRect(new Rect(390f, 220f, 500f, 280f), new Rect(70f, 430f, 580f, 360f)),
                    GUIContent.none, discardPanelStyle);
                GUI.Label(UiRect(new Rect(430f, 245f, 420f, 100f), new Rect(110f, 475f, 500f, 120f)),
                    Ui("\uB3C4\uC804\uC744 \uD3EC\uAE30\uD560\uAE4C\uC694?\n\uD604\uC7AC \uACB0\uACFC \uD654\uBA74\uC73C\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.",
                        "Abandon this run?\nYou will move to the current result."),
                    discardMessageStyle);
                if (GUI.Button(UiRect(new Rect(445f, 390f, 170f, 58f), new Rect(120f, 650f, 210f, 68f)),
                    Ui("\uD3EC\uAE30", "Abandon"), discardButtonStyle))
                {
                    GUI.matrix = previousMatrix;
                    AbandonChallengeToResults();
                    return;
                }
                if (GUI.Button(UiRect(new Rect(665f, 390f, 170f, 58f), new Rect(390f, 650f, 210f, 68f)),
                    Ui("\uCDE8\uC18C", "Cancel"), discardButtonStyle))
                    abandonConfirmationVisible = false;
                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
                return;
            }
            bool canAbandonChallenge = phase != RevealPhase.GameOver && phase != RevealPhase.RunCleared;
            Rect settingsPanelRect = canAbandonChallenge
                ? UiRect(new Rect(390f, 115f, 500f, 535f), new Rect(60f, 250f, 600f, 750f))
                : UiRect(new Rect(390f, 145f, 500f, 430f), new Rect(60f, 300f, 600f, 620f));
            GUI.Box(settingsPanelRect, GUIContent.none, discardPanelStyle);
            GUI.Label(canAbandonChallenge
                    ? UiRect(new Rect(440f, 140f, 400f, 58f), new Rect(110f, 285f, 500f, 70f))
                    : UiRect(new Rect(440f, 170f, 400f, 58f), new Rect(110f, 335f, 500f, 70f)),
                Ui("\uC124\uC815", "Settings"), settingsTitleStyle);
            GUI.Label(UiRect(new Rect(455f, 250f, 180f, 44f), new Rect(105f, 445f, 220f, 50f)), Ui("\uC5B8\uC5B4", "Language"), settingsLabelStyle);
            if (GUI.Button(UiRect(new Rect(455f, 300f, 170f, 52f), new Rect(105f, 510f, 230f, 62f)),
                (uiLanguage == 0 ? "\u25CF " : string.Empty) + "\uD55C\uAD6D\uC5B4", discardButtonStyle))
                SetUiLanguage(0);
            if (GUI.Button(UiRect(new Rect(655f, 300f, 170f, 52f), new Rect(385f, 510f, 230f, 62f)),
                (uiLanguage == 1 ? "\u25CF " : string.Empty) + "English", discardButtonStyle))
                SetUiLanguage(1);
            GUI.Label(UiRect(new Rect(455f, 380f, 260f, 44f), new Rect(105f, 635f, 320f, 50f)),
                Ui("\uC74C\uB7C9  ", "Volume  ") + Mathf.RoundToInt(masterVolume * 100f) + "%", settingsLabelStyle);
            float changedVolume = GUI.HorizontalSlider(UiRect(new Rect(455f, 438f, 370f, 28f), new Rect(105f, 710f, 510f, 34f)), masterVolume, 0f, 1f);
            if (!Mathf.Approximately(changedVolume, masterVolume)) SetMasterVolume(changedVolume);
if (canAbandonChallenge
                && GUI.Button(UiRect(new Rect(530f, 525f, 220f, 46f), new Rect(220f, 810f, 280f, 68f)),
                    Ui("\uB3C4\uC804 \uD3EC\uAE30", "Abandon Run"), discardButtonStyle))
                abandonConfirmationVisible = true;
            Rect closeRect = canAbandonChallenge
                ? UiRect(new Rect(555f, 575f, 170f, 52f), new Rect(245f, 900f, 230f, 64f))
                : UiRect(new Rect(555f, 500f, 170f, 52f), new Rect(245f, 810f, 230f, 64f));
            if (GUI.Button(closeRect, Ui("\uB2EB\uAE30", "Close"), discardButtonStyle))
            {
                abandonConfirmationVisible = false;
                settingsOpen = false;
            }
            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }
        private void AbandonChallengeToResults()
        {
            CommitPendingScoreImmediately();
            StopAllCoroutines();
            settingsOpen = false;
            abandonConfirmationVisible = false;
            if (sharedPackPreviewActive)
            {
                ReturnToSharedResultAfterPackPreview();
                return;
            }
            sharedResultMode = false;
            shareFeedback = null;
            scorePopups.Clear();
            packTearInProgress = false;
            gestureDragging = false;
            inspectionDragging = false;
            transitionDragActive = false;
            transitionSwipeCommitted = false;
            queuedCardSwipes = 0;
            cardTransitionActive = false;
            activeSlidingCard = null;
            ClearPackChoiceVisuals();
            ClearCards();
            if (pack != null) pack.gameObject.SetActive(false);
            if (cardStack != null) cardStack.gameObject.SetActive(false);
            phase = RevealPhase.GameOver;
            LayoutDeckVisuals();
        }
        public void EditorDebugDrawCard()
        {
            if (!startingHandVisible || stageSelectionVisible || phase != RevealPhase.CardFront) return;
            if (!DrawStarterCardToHand()) return;
            LayoutStartingHand();
            RefreshHandCardInteractionStates();
        }
        public void EditorDebugAddEnemy()
        {
            if (enemies.Count >= 3) return;
            EnemyState enemy = CreateEnemyState(LoadDefaultEnemyDefinition());
            if (enemy == null)
            {
                Debug.LogError("Combat/Enemies/Wolf asset is missing.");
                return;
            }
            enemies.Add(enemy);
            RefreshEnemyVisual();
        }
        public void EditorDebugDefeatAllEnemies()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                enemies[i].Shield = 0;
                enemies[i].Health = 0;
                PlayEnemyDefeatEffect(i);
            }
            RefreshEnemyVisual();
            if (enemies.Count > 0)
            {
                startingHandVisible = false;
                BeginCombatVictoryAfterDefeatDelay();
            }
        }
    }
}
