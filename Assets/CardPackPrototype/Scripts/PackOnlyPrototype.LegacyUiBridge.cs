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
        private static Vector2 ScreenToReferencePoint(Vector2 screenPoint)
        {
            GetUiLayout(out float scale, out float offsetX, out float offsetY);
            if (scale <= 0f) return Vector2.zero;
            return new Vector2((screenPoint.x - offsetX) / scale, (screenPoint.y - offsetY) / scale);
        }
        private void OnGUI()
        {
            EventType eventType = Event.current.type;
            if (eventType != EventType.Repaint && eventType != EventType.MouseDown
                && eventType != EventType.MouseDrag && eventType != EventType.MouseUp
                && eventType != EventType.Used) return;
            try
            {
                GetUiLayout(out float scale, out float offsetX, out float offsetY);
            Vector2 raw = Event.current.mousePosition;
            if (HandleRuntimeCanvasButtonPointer(raw, Event.current)) return;
            if (settingsOpen) return;
            if (!hasHandHoverPointer || (raw - lastHandHoverPointer).sqrMagnitude > 0.25f)
            {
                lastHandHoverPointer = raw;
                hasHandHoverPointer = true;
                handHoverPointerDirty = true;
            }
            if (inspectedDeckIndex >= 0)
            {
                HandleDeckPointer(raw, Event.current);
                return;
            }
            if (inspectedPackChoice != null)
            {
                HandleDeckPointer(raw, Event.current);
                return;
            }
            if (usedPileExpanded)
            {
                DrawUsedPileOverlay(scale, offsetX, offsetY);
                return;
            }
            if (combatDeckInspectionVisible)
            {
                DrawCombatDeckInspectionOverlay(scale, offsetX, offsetY);
                return;
            }
            if (stageSelectionVisible)
            {
                if (Event.current.type == EventType.Repaint) DrawEffectInfoPopup(raw);
                HandleStageSelectionPointer(raw, Event.current);
                return;
            }

            if (phase == RevealPhase.PackChoice)
            {
                DrawPackChoice(scale, offsetX, offsetY);
                HandleDeckPointer(raw, Event.current);
                return;
            }
            if (phase == RevealPhase.GameOver || phase == RevealPhase.RunCleared)
            {
                HandleDeckPointer(raw, Event.current);
                return;
            }

            if (startingHandVisible)
            {
                if (Event.current.type == EventType.Repaint) DrawEffectInfoPopup(raw);
                HandleStartingHandPointer(raw, Event.current);
                return;
            }
            if (HandleDeckPointer(raw, Event.current)) return;
            HandlePointer(new Vector2((raw.x - offsetX) / scale, (raw.y - offsetY) / scale), Event.current);
            }
            finally
            {
            }
        }
        private bool HandleRuntimeCanvasButtonPointer(Vector2 screenPoint, Event inputEvent)
        {
            if (inputEvent.type != EventType.MouseUp) return false;
            Vector2 point = ScreenToReferencePoint(screenPoint);
            if (canvasSettingsButton != null && canvasSettingsButton.gameObject.activeInHierarchy
                && new Rect(UiReferenceWidth - 144f, 28f, 120f, 54f).Contains(point))
            {
                OpenCanvasSettings();
                inputEvent.Use();
                return true;
            }
            if (canvasDeckButton != null && canvasDeckButton.gameObject.activeInHierarchy
                && new Rect(24f, 28f, 190f, 48f).Contains(point))
            {
                if (!combatDeckInspectionVisible && !stageDeckInspectionVisible)
                    OpenCanvasDeckInspection();
                inputEvent.Use();
                return true;
            }
            if (canvasEndTurnButton != null && canvasEndTurnButton.gameObject.activeInHierarchy)
            {
                float endTurnY = canvasEndTurnButton.GetComponent<RectTransform>().anchoredPosition.y;
                Rect endTurnRect = new Rect(UiReferenceWidth - 45f - 190f,
                    UiReferenceHeight - endTurnY - 48f, 190f, 48f);
                if (endTurnRect.Contains(point) && canvasEndTurnButton.interactable)
                {
                    EndPlayerTurn();
                    inputEvent.Use();
                    return true;
                }
            }
            return false;
        }

        private bool TryGetHoveredRelic(Vector2 screenPoint, out global::CombatRelicDefinition definition,
            out int amount, out Rect iconAnchor)
        {
            definition = null;
            amount = 0;
            iconAnchor = default;
            if (canvasRelicList == null || canvasRelicList.Root == null
                || !canvasRelicList.Root.activeInHierarchy) return false;

            Vector2 point = ScreenToReferencePoint(screenPoint);
            float y = IsPortraitUi ? 146f : 106f;
            for (int i = 0; i < playerRelicEntries.Count; i++)
            {
                CombatRelicListVisual.Entry entry = playerRelicEntries[i];
                if (entry.Definition == null) continue;
                Rect rect = new Rect(28f + i * 64f, y, 56f, 52f);
                if (!rect.Contains(point)) continue;
                definition = entry.Definition;
                amount = entry.Amount;
                iconAnchor = rect;
                return true;
            }
            return false;
        }
        private void DrawEffectInfoPopup(Vector2 screenPoint)
        {
            if (canvasEffectPopupRoot != null) canvasEffectPopupRoot.SetActive(false);
            if (draggedHandIndex >= 0 || pressedHandIndex >= 0) return;

            string title = null;
            Texture popupIcon = null;
            List<string> effectLines = new List<string>();
            Rect cardRect;
            Rect actionAnchor = default;
            bool fixedActionPopup = false;
            Rect buffAnchor = default;
            bool fixedBuffPopup = false;
            StoredCard hoveredCard = GetHoveredHandCard(screenPoint, out cardRect);

            if (TryGetHoveredEnemyBuff(screenPoint, out global::CombatBuffDefinition hoveredBuff, out buffAnchor)
                || TryGetHoveredPlayerBuff(screenPoint, out hoveredBuff, out buffAnchor))
            {
                fixedBuffPopup = true;
                title = hoveredBuff.GetLocalizedName(IsEnglishUi);
                popupIcon = hoveredBuff.Image;
                AddEffectPopupLine(effectLines, hoveredBuff, 0);
            }
            else if (TryGetHoveredRelic(screenPoint, out global::CombatRelicDefinition hoveredRelic,
                out _, out buffAnchor))
            {
                fixedBuffPopup = true;
                title = hoveredRelic.GetLocalizedName(IsEnglishUi);
                popupIcon = hoveredRelic.Image;
                string description = hoveredRelic.GetLocalizedDescription(IsEnglishUi);
                effectLines.Add(string.IsNullOrEmpty(description)
                    ? Ui("가지 효과가 적용 중입니다.", "This relic effect is active.") : description);
            }
            else if (TryGetHoveredEnemyAction(screenPoint, out EnemyState actionEnemy,
                out PlannedActionInfo actionInfo, out global::CombatBuffDefinition actionBuffDefinition, out actionAnchor))
            {
                fixedActionPopup = true;
                if (actionInfo == PlannedActionInfo.Countdown)
                {
                    if (clockTexture == null) clockTexture = Resources.Load<Texture2D>("CardAssets/Content/clock");
                    title = Ui("남은 시간", "Time Remaining");
                    popupIcon = clockTexture;
                    effectLines.Add(Ui(actionEnemy.ActionTurnsRemaining + "차례 후에 행동하여 우측의 효과를 가합니다.",
                        "Acts in " + actionEnemy.ActionTurnsRemaining + " turn(s), applying the effects shown to the right."));
                }
                else if (actionInfo == PlannedActionInfo.Buff)
                {
                    title = actionBuffDefinition != null
                        ? actionBuffDefinition.GetLocalizedName(IsEnglishUi) : Ui("효과", "Effect");
                    popupIcon = actionBuffDefinition != null ? actionBuffDefinition.Image : null;
                    AddEffectPopupLine(effectLines, actionBuffDefinition, 0);
                }
                else if (actionInfo == PlannedActionInfo.Damage)
                {
                    if (attackTexture == null) attackTexture = Resources.Load<Texture2D>("CardAssets/Content/attack");
                    title = IsEnglishUi ? actionEnemy.EnglishActionName : actionEnemy.ActionName;
                    popupIcon = attackTexture;
                    effectLines.Add(Ui("행동하면 " + actionEnemy.ActionDamage + "만큼의 피해를 줍니다.",
                        "Deals " + actionEnemy.ActionDamage + " damage when this action is performed."));
                }
                else if (actionInfo == PlannedActionInfo.Summon)
                {
                    if (summonTexture == null) summonTexture = Resources.Load<Texture2D>("CardAssets/Content/Summon");
                    title = Ui("화이리 소환", "Summon FireWolf");
                    popupIcon = summonTexture;
                    effectLines.Add(Ui("행동하면 화이리 1마리를 소환합니다.",
                        "Summons one FireWolf when this action is performed."));
                }
                else if (actionInfo == PlannedActionInfo.HealSelf || actionInfo == PlannedActionInfo.HealAllEnemies)
                {
                    bool healsAll = actionInfo == PlannedActionInfo.HealAllEnemies;
                    if (healsAll)
                    {
                        if (multiHealTexture == null) multiHealTexture = Resources.Load<Texture2D>("CardAssets/Content/multiheal");
                        popupIcon = multiHealTexture;
                    }
                    else
                    {
                        if (healTexture == null) healTexture = Resources.Load<Texture2D>("CardAssets/Content/heal");
                        popupIcon = healTexture;
                    }
                    int amount = healsAll ? actionEnemy.AllEnemyHealAmount : actionEnemy.SelfHealAmount;
                    title = healsAll ? Ui("전체 회복", "Heal All Enemies") : Ui("자신 회복", "Heal Self");
                    effectLines.Add(healsAll
                        ? Ui("행동하면 모든 적의 체력을 " + amount + " 회복합니다.",
                            "Restores " + amount + " HP to all enemies when this action is performed.")
                        : Ui("행동하면 자신의 체력을 " + amount + " 회복합니다.",
                            "Restores " + amount + " HP to itself when this action is performed."));
                }
                else
                {
                    global::CombatBuffDefinition bleeding = GetCombatBuffDefinition("Bleeding");
                    title = bleeding != null ? bleeding.GetLocalizedName(IsEnglishUi) : Ui("출혈", "Bleeding");
                    popupIcon = bleeding != null ? bleeding.Image : bleedingTexture;
                    AddEffectPopupLine(effectLines, bleeding, 0);
                }
            }
            else if (hoveredCard != null && hoveredCard.CombatType != null)
            {
                List<global::CombatCardAbility> abilities = hoveredCard.CombatType.Abilities;
                if (abilities != null)
                {
                    for (int i = 0; i < abilities.Count; i++)
                    {
                        global::CombatCardAbility ability = abilities[i];
                        if (ability == null || ability.RelatedBuff == null) continue;
                        if (string.IsNullOrEmpty(title))
                        {
                            title = ability.RelatedBuff.GetLocalizedName(IsEnglishUi);
                            popupIcon = ability.RelatedBuff.Image;
                        }
                        AddEffectPopupLine(effectLines, ability.RelatedBuff, 0);
                    }
                }
            }
            if (string.IsNullOrEmpty(title) || effectLines.Count == 0) return;
            EnsureEffectPopupStyles();
            string body = string.Join("\n\n", effectLines);
            GUIContent titleContent = new GUIContent(title);
            GUIContent bodyContent = new GUIContent(body);
            float titleWidth = effectPopupTitleStyle.CalcSize(titleContent).x + (popupIcon != null ? 94f : 48f);
            float bodyWidth = effectPopupBodyStyle.CalcSize(bodyContent).x + 44f;
            // Popup styles are authored in the 1920x1080 reference space. Convert the
            // resulting panel size to screen pixels once, matching runtimeUiRoot's scale.
            GetUiLayout(out float popupScale, out float popupOffsetX, out float popupOffsetY);
            if (popupScale <= 0f) return;
            float widthReference = Mathf.Clamp(Mathf.Max(420f, titleWidth, bodyWidth), 420f, 720f);
            float heightReference = 78f + effectPopupBodyStyle.CalcHeight(bodyContent, widthReference - 40f);
            heightReference = Mathf.Max(heightReference, 165f);
            float width = widthReference * popupScale;
            float height = heightReference * popupScale;
            bool isCardPopup = hoveredCard != null;
            float x;
            float y;
            if (isCardPopup)
            {
                x = cardRect.xMax + 18f;
                if (x + width > Screen.width - 12f) x = cardRect.xMin - width - 18f;
                y = cardRect.center.y - height * 0.5f;
            }
            else if (fixedActionPopup || fixedBuffPopup)
            {
                Rect fixedAnchor = fixedActionPopup ? actionAnchor : buffAnchor;
                x = popupOffsetX + (fixedAnchor.xMax + 12f) * popupScale;
                y = popupOffsetY + fixedAnchor.center.y * popupScale - height * 0.5f;
                if (x + width > Screen.width - 12f)
                    x = popupOffsetX + (fixedAnchor.xMin - 12f) * popupScale - width;
            }
            else
            {
                x = screenPoint.x + 22f;
                y = screenPoint.y + 18f;
            }
            x = Mathf.Clamp(x, 12f, Screen.width - width - 12f);
            y = Mathf.Clamp(y, 12f, Screen.height - height - 12f);
            Rect panel = new Rect(x, y, width, height);
            ShowCanvasEffectPopup(title, popupIcon, body, panel.x, panel.y, panel.width, panel.height);
            return;
        }
    }
}
