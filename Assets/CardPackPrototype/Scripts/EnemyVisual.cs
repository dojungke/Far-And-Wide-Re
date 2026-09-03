using UnityEngine;
using TMPro;

namespace CardOpen.Prototype
{
    /// <summary>Displays enemy artwork and combat status beneath the card render layer.</summary>
    public sealed class EnemyVisual : MonoBehaviour
    {
        private const int EnemyOrder = 1000;
        private const int StatusOrder = 1100;
        private Renderer fallbackRenderer;
        private SpriteRenderer artworkRenderer;
        private SpriteRenderer clockRenderer;
        private SpriteRenderer attackRenderer;
        private SpriteRenderer bleedingRenderer;
        private SpriteRenderer healthOutlineRenderer;
        private SpriteRenderer healthEmptyRenderer;
        private SpriteRenderer healthFillRenderer;
        private TextMeshPro nameText;
        private TextMeshPro turnText;
        private TextMeshPro damageText;
        private TextMeshPro bleedingText;
        private TextMeshPro healthText;
        private TMP_FontAsset statusFontAsset;
        private Font statusSourceFont;

        public void Build(Texture2D enemyTexture, Material fallbackMaterial)
        {
            SetAppearance(enemyTexture, fallbackMaterial);
        }

        /// <summary>Reuses the existing visual root when a new combat starts.</summary>
        public void SetAppearance(Texture2D enemyTexture, Material fallbackMaterial)
        {
            if (enemyTexture != null)
            {
                if (fallbackRenderer != null) fallbackRenderer.gameObject.SetActive(false);
                if (artworkRenderer == null)
                {
                    GameObject artwork = new GameObject("Artwork");
                    artwork.transform.SetParent(transform, false);
                    artwork.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                    artworkRenderer = artwork.AddComponent<SpriteRenderer>();
                    artworkRenderer.sortingOrder = EnemyOrder;
                }
                artworkRenderer.gameObject.SetActive(true);
                if (artworkRenderer.sprite == null || artworkRenderer.sprite.texture != enemyTexture)
                {
                    artworkRenderer.sprite = CreateSprite(enemyTexture);
                    float heightInWorldUnits = enemyTexture.height / 100f;
                    artworkRenderer.transform.localScale = Vector3.one * (2.10f / Mathf.Max(0.01f, heightInWorldUnits));
                }
                return;
            }

            if (artworkRenderer != null) artworkRenderer.gameObject.SetActive(false);
            if (fallbackRenderer == null)
            {
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Fallback Body";
                body.transform.SetParent(transform, false);
                body.transform.localPosition = new Vector3(0f, 0.48f, 0f);
                body.transform.localScale = new Vector3(0.72f, 0.88f, 0.54f);
                fallbackRenderer = body.GetComponent<Renderer>();
                fallbackRenderer.sortingOrder = EnemyOrder;
                Destroy(body.GetComponent<Collider>());
            }
            fallbackRenderer.gameObject.SetActive(true);
            fallbackRenderer.sharedMaterial = fallbackMaterial;
        }

        public void SetColor(Color color)
        {
            if (fallbackRenderer != null) fallbackRenderer.sharedMaterial.color = color;
            if (artworkRenderer != null) artworkRenderer.color = Color.white;
        }

        public void UpdateCombatStatus(Font font, string enemyName, string turns, string damage, string bleeding,
            string health, float healthRatio, bool defeated, Texture2D clock, Texture2D attack, Texture2D bleed,
            Vector3 namePosition, Vector3 clockPosition, Vector3 attackPosition, Vector3 bleedingPosition,
            Vector3 healthPosition, Vector2 iconSize, Vector2 healthSize, float textSize)
        {
            Color tint = defeated ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
            SetSprite(ref clockRenderer, "Action Clock", clock, clockPosition, iconSize, tint);
            bool hasDamage = !string.IsNullOrEmpty(damage) && damage != "0";
            if (hasDamage)
                SetSprite(ref attackRenderer, "Action Attack", attack, attackPosition, iconSize, tint);
            else if (attackRenderer != null) attackRenderer.gameObject.SetActive(false);
            bool hasBleeding = !string.IsNullOrEmpty(bleeding) && bleeding != "0";
            if (hasBleeding)
                SetSprite(ref bleedingRenderer, "Action Bleeding", bleed, bleedingPosition, iconSize, tint);
            else if (bleedingRenderer != null) bleedingRenderer.gameObject.SetActive(false);

            SetSprite(ref healthOutlineRenderer, "Health Outline", Texture2D.whiteTexture, healthPosition,
                healthSize, new Color(0.04f, 0.05f, 0.09f, 0.93f));
            Vector2 healthInnerSize = healthSize - new Vector2(textSize * 0.60f, textSize * 0.30f);
            SetSprite(ref healthEmptyRenderer, "Health Empty", Texture2D.whiteTexture, healthPosition,
                healthInnerSize, new Color(0.09f, 0.11f, 0.15f));
            float fillWidth = Mathf.Max(0.001f, healthInnerSize.x * Mathf.Clamp01(healthRatio));
            Vector3 fillPosition = healthPosition + Vector3.left * (healthInnerSize.x - fillWidth) * 0.5f;
            SetSprite(ref healthFillRenderer, "Health Fill", Texture2D.whiteTexture, fillPosition,
                new Vector2(fillWidth, healthInnerSize.y),
                defeated ? new Color(0.32f, 0.32f, 0.36f) : healthRatio > 0.3f ? new Color(0.28f, 0.92f, 0.42f) : new Color(1f, 0.30f, 0.22f));
            healthOutlineRenderer.sortingOrder = StatusOrder;
            healthEmptyRenderer.sortingOrder = StatusOrder + 1;
            healthFillRenderer.sortingOrder = StatusOrder + 2;

            SetText(ref nameText, "Enemy Name", font, enemyName, namePosition, textSize * 1.25f, TextAnchor.MiddleCenter, tint);
            SetText(ref turnText, "Action Turns", font, turns, clockPosition + Vector3.up * 0.05f, textSize, TextAnchor.MiddleCenter, Color.black);
            if (hasDamage)
                SetText(ref damageText, "Action Damage", font, damage, attackPosition + Vector3.right * iconSize.x * 0.43f - Vector3.up * iconSize.y * 0.30f, textSize, TextAnchor.MiddleCenter, Color.white);
            else if (damageText != null) damageText.gameObject.SetActive(false);
            if (hasBleeding)
                SetText(ref bleedingText, "Action Bleeding", font, bleeding, bleedingPosition + Vector3.right * iconSize.x * 0.43f - Vector3.up * iconSize.y * 0.30f, textSize, TextAnchor.MiddleCenter, Color.white);
            else if (bleedingText != null) bleedingText.gameObject.SetActive(false);
            SetText(ref healthText, "Health Text", font, health, healthPosition, textSize, TextAnchor.MiddleCenter, Color.white);
        }

        private void SetSprite(ref SpriteRenderer renderer, string name, Texture2D texture, Vector3 position,
            Vector2 size, Color color)
        {
            if (renderer == null)
            {
                GameObject item = new GameObject(name);
                item.transform.SetParent(transform, true);
                renderer = item.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = StatusOrder;
            }
            if (renderer.sprite == null || renderer.sprite.texture != texture) renderer.sprite = CreateSprite(texture);
            renderer.gameObject.SetActive(true);
            renderer.color = color;
            renderer.transform.position = position;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            Vector3 parentScale = transform.lossyScale;
            renderer.transform.localScale = new Vector3(size.x / spriteSize.x / parentScale.x,
                size.y / spriteSize.y / parentScale.y, 1f);
        }

        private void SetText(ref TextMeshPro text, string name, Font font, string value, Vector3 position,
            float size, TextAnchor anchor, Color color)
        {
            if (text == null)
            {
                GameObject item = new GameObject(name);
                item.transform.SetParent(transform, true);
                text = item.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.enableAutoSizing = true;
                text.overflowMode = TextOverflowModes.Overflow;
                text.fontStyle = FontStyles.Bold;
                text.outlineWidth = 0.18f;
                text.outlineColor = Color.black;
                text.extraPadding = true;
                text.GetComponent<MeshRenderer>().sortingOrder = StatusOrder + 10;
            }
            TMP_FontAsset fontAsset = GetStatusFont(font);
            if (fontAsset == null) return;
            fontAsset.TryAddCharacters(value, out string missingCharacters);
            text.gameObject.SetActive(true);
            text.font = fontAsset;
            text.text = value;
            text.color = color;
            CombatTextOutline.ApplyToWhiteText(text);
            text.fontSizeMax = Mathf.Clamp(size * 20f, 1.25f, 2.8f);
            text.fontSizeMin = 0.15f;
            text.fontSize = text.fontSizeMax;
            text.rectTransform.sizeDelta = new Vector2(2.2f, 0.55f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.transform.position = position;
            text.transform.localScale = Vector3.one;
            text.ForceMeshUpdate(true, true);
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            renderer.sortingOrder = StatusOrder + 10;
        }

        private TMP_FontAsset GetStatusFont(Font font)
        {
            if (statusFontAsset != null && statusSourceFont == font) return statusFontAsset;
            statusSourceFont = font;
            if (font != null)
                statusFontAsset = TMP_FontAsset.CreateFontAsset(font, 64, 6,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
            if (statusFontAsset == null) statusFontAsset = TMP_Settings.defaultFontAsset;
            return statusFontAsset;
        }
        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}