using TMPro;
using UnityEngine;

namespace CardOpen.Prototype
{
    /// <summary>Shared TMP font asset and material preset for non-card combat UI text.</summary>
    public static class CombatTextOutline
    {
        public const float OutlineThickness = 1f;
        private static TMP_FontAsset sharedFontAsset;
        private static Font sharedSourceFont;
        private static Material sharedOutlineMaterial;

        public static TMP_FontAsset GetSharedFontAsset(Font source)
        {
            if (sharedFontAsset != null && sharedSourceFont == source) return sharedFontAsset;
            sharedSourceFont = source;
            sharedOutlineMaterial = null;

            if (source != null)
                sharedFontAsset = TMP_FontAsset.CreateFontAsset(source, 64, 10,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048,
                    AtlasPopulationMode.Dynamic, true);
            if (sharedFontAsset == null) sharedFontAsset = TMP_Settings.defaultFontAsset;
            return sharedFontAsset;
        }

        public static void ApplySharedWhiteOutline(TMP_Text text)
        {
            if (text == null) return;
            Material material = GetSharedOutlineMaterial(text.font);
            if (material != null)
            {
                text.fontSharedMaterial = material;
                material.EnableKeyword("OUTLINE_ON");
                material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineThickness);
                material.SetFloat(ShaderUtilities.ID_FaceDilate, 1f);
            }
            text.color = Color.white;
            text.outlineColor = Color.black;
            text.outlineWidth = OutlineThickness;
            text.extraPadding = true;
            text.UpdateMeshPadding();
        }

        public static void ApplyToWhiteText(TextMeshPro text)
        {
            if (text == null || !IsWhite(text.color)) return;
            if (text.GetComponentInParent<CardVisual>(true) != null) return;
            ApplySharedWhiteOutline(text);
        }

        private static Material GetSharedOutlineMaterial(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null || fontAsset.material == null) return null;
            if (sharedOutlineMaterial != null) return sharedOutlineMaterial;
            sharedOutlineMaterial = Object.Instantiate(fontAsset.material);
            sharedOutlineMaterial.name = "Combat UI White Black Outline Preset";
            return sharedOutlineMaterial;
        }

        private static bool IsWhite(Color color)
        {
            return color.r >= 0.98f && color.g >= 0.98f && color.b >= 0.98f && color.a > 0f;
        }
    }
}