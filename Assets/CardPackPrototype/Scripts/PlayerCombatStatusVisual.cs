using TMPro;
using UnityEngine;

namespace CardOpen.Prototype
{
    /// <summary>World-space player health bar shown in the combat scene hierarchy.</summary>
    public sealed class PlayerCombatStatusVisual : MonoBehaviour
    {
        private const int StatusOrder = 1800;
        private SpriteRenderer outlineRenderer;
        private SpriteRenderer emptyRenderer;
        private SpriteRenderer fillRenderer;
        private TextMeshPro healthText;
        private TMP_FontAsset statusFontAsset;
        private Font sourceFont;

        public void UpdateStatus(Font font, string health, float healthRatio, Vector3 position,
            Vector2 size, float textSize)
        {
            SetSprite(ref outlineRenderer, "Health Outline", position, size, new Color(0.04f, 0.05f, 0.09f, 0.93f), StatusOrder);
            Vector2 innerSize = size - new Vector2(textSize * 0.60f, textSize * 0.30f);
            SetSprite(ref emptyRenderer, "Health Empty", position, innerSize, new Color(0.38f, 0.07f, 0.07f), StatusOrder + 1);

            float fillWidth = Mathf.Max(0.001f, innerSize.x * Mathf.Clamp01(healthRatio));
            Vector3 fillPosition = position + Vector3.left * (innerSize.x - fillWidth) * 0.5f;
            SetSprite(ref fillRenderer, "Health Fill", fillPosition, new Vector2(fillWidth, innerSize.y),
                healthRatio > 0.3f ? new Color(0.16f, 0.74f, 0.28f) : new Color(1f, 0.30f, 0.22f), StatusOrder + 2);
            SetText(font, health, position, textSize);
        }

        private void SetSprite(ref SpriteRenderer renderer, string name, Vector3 position, Vector2 size,
            Color color, int sortingOrder)
        {
            if (renderer == null)
            {
                GameObject item = new GameObject(name);
                item.transform.SetParent(transform, true);
                renderer = item.AddComponent<SpriteRenderer>();
                renderer.sprite = Sprite.Create(Texture2D.whiteTexture,
                    new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            }
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.position = position;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                size.x / Mathf.Max(0.0001f, spriteSize.x),
                size.y / Mathf.Max(0.0001f, spriteSize.y), 1f);
        }

        private void SetText(Font font, string value, Vector3 position, float textSize)
        {
            if (healthText == null)
            {
                GameObject item = new GameObject("Health Text");
                item.transform.SetParent(transform, true);
                healthText = item.AddComponent<TextMeshPro>();
                healthText.alignment = TextAlignmentOptions.Center;
                healthText.enableAutoSizing = true;
                healthText.overflowMode = TextOverflowModes.Overflow;
                healthText.fontStyle = FontStyles.Bold;
                healthText.outlineWidth = 0.18f;
                healthText.outlineColor = Color.black;
                healthText.extraPadding = true;
                healthText.GetComponent<MeshRenderer>().sortingOrder = StatusOrder + 10;
            }
            TMP_FontAsset fontAsset = GetFont(font);
            if (fontAsset == null) return;
            fontAsset.TryAddCharacters(value, out _);
            healthText.font = fontAsset;
            healthText.text = value;
            healthText.color = Color.white;
            CombatTextOutline.ApplyToWhiteText(healthText);
            healthText.fontSizeMin = 0.15f;
            healthText.fontSizeMax = Mathf.Clamp(textSize * 20f, 1.25f, 2.8f);
            healthText.fontSize = healthText.fontSizeMax;
            healthText.rectTransform.sizeDelta = new Vector2(3.5f, 0.55f);
            healthText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            healthText.transform.position = position;
            healthText.ForceMeshUpdate(true, true);
        }

        private TMP_FontAsset GetFont(Font font)
        {
            if (statusFontAsset != null && sourceFont == font) return statusFontAsset;
            sourceFont = font;
            if (font != null)
                statusFontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 6,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
            if (statusFontAsset == null) statusFontAsset = TMP_Settings.defaultFontAsset;
            return statusFontAsset;
        }
    }
}
