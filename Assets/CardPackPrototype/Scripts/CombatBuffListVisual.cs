using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CardOpen.Prototype
{
    /// <summary>World-space combat buff list. Entries are regular scene objects, not IMGUI controls.</summary>
    public sealed class CombatBuffListVisual : MonoBehaviour
    {
        public struct Entry
        {
            public CombatBuffDefinition Definition;
            public int Amount;
            public Entry(CombatBuffDefinition definition, int amount)
            {
                Definition = definition;
                Amount = amount;
            }
        }

        private sealed class Slot
        {
            public GameObject Root;
            public SpriteRenderer Icon;
            public TextMeshPro Amount;
            public int LastAmount;
            public float LastFontSize;
            public bool HasText;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private TMP_FontAsset fontAsset;
        private Font sourceFont;

        public void UpdateEntries(Font font, IList<Entry> entries, Vector3 anchor, Vector2 iconSize,
            Vector3 step, int sortingOrder)
        {
            int count = entries != null ? entries.Count : 0;
            EnsureSlotCount(count, font);
            for (int i = 0; i < slots.Count; i++)
            {
                bool active = i < count && entries[i].Definition != null && entries[i].Amount > 0;
                Slot slot = slots[i];
                if (slot.Root.activeSelf != active) slot.Root.SetActive(active);
                if (!active) continue;

                Entry entry = entries[i];
                Texture2D texture = entry.Definition.Image;
                if (slot.Icon.sprite == null || slot.Icon.sprite.texture != texture)
                    slot.Icon.sprite = CreateSprite(texture != null ? texture : Texture2D.whiteTexture);
                slot.Icon.color = Color.white;
                slot.Icon.sortingOrder = sortingOrder;
                slot.Root.transform.position = anchor + step * i;
                Vector2 spriteSize = slot.Icon.sprite.bounds.size;
                slot.Icon.transform.localScale = new Vector3(iconSize.x / Mathf.Max(0.001f, spriteSize.x),
                    iconSize.y / Mathf.Max(0.001f, spriteSize.y), 1f);
                float fontSize = Mathf.Clamp(iconSize.y * 85f, 1.25f, 3f);
                if (!slot.HasText || slot.LastAmount != entry.Amount || !Mathf.Approximately(slot.LastFontSize, fontSize))
                {
                    slot.Amount.text = entry.Amount.ToString();
                    slot.Amount.fontSize = fontSize;
                    ApplyOutline(slot.Amount);
                    slot.Amount.ForceMeshUpdate(true, true);
                    slot.LastAmount = entry.Amount;
                    slot.LastFontSize = fontSize;
                    slot.HasText = true;
                }
                slot.Amount.GetComponent<MeshRenderer>().sortingOrder = sortingOrder + 2;
            }
        }

        private void EnsureSlotCount(int count, Font font)
        {
            TMP_FontAsset nextFont = GetFont(font);
            while (slots.Count < count)
            {
                GameObject root = new GameObject("Buff Entry");
                root.transform.SetParent(transform, true);
                GameObject iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(root.transform, false);
                SpriteRenderer icon = iconObject.AddComponent<SpriteRenderer>();
                GameObject textObject = new GameObject("Amount");
                textObject.transform.SetParent(root.transform, false);
                TextMeshPro amount = textObject.AddComponent<TextMeshPro>();
                amount.alignment = TextAlignmentOptions.Center;
                amount.enableAutoSizing = true;
                amount.overflowMode = TextOverflowModes.Overflow;
                amount.fontStyle = FontStyles.Bold;
                amount.color = Color.white;
                amount.outlineWidth = CombatTextOutline.OutlineThickness;
                amount.outlineColor = Color.black;
                amount.extraPadding = false;
                amount.rectTransform.sizeDelta = new Vector2(0.7f, 0.5f);
                amount.rectTransform.localPosition = new Vector3(0.20f, -0.13f, -0.01f);
                slots.Add(new Slot { Root = root, Icon = icon, Amount = amount });
            }
            for (int i = 0; i < slots.Count; i++)
            {
                TextMeshPro amount = slots[i].Amount;
                if (amount.font != nextFont)
                {
                    amount.font = nextFont;
                    slots[i].HasText = false;
                }
                amount.enableAutoSizing = true;
                amount.fontSizeMin = 0f;
                amount.fontSizeMax = 3f;
                amount.outlineColor = Color.black;
                amount.outlineWidth = CombatTextOutline.OutlineThickness;
            }
        }

        private static void ApplyOutline(TextMeshPro text)
        {
            if (text == null) return;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0f;
            text.fontSizeMax = 3f;
            CombatTextOutline.ApplyToWhiteText(text);
        }
        private TMP_FontAsset GetFont(Font font)
        {
            if (fontAsset != null && sourceFont == font) return fontAsset;
            sourceFont = font;
            if (font != null)
                fontAsset = CombatTextOutline.GetSharedFontAsset(font);
            return fontAsset;
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
        }
    }
}