using System.Collections.Generic;
using UnityEngine;

namespace CardOpen.Prototype
{
    /// <summary>Keeps card-frame textures bright independently of world lighting.</summary>
    [DefaultExecutionOrder(1400)]
    public sealed class CardFrameBrightness : MonoBehaviour
    {
        private readonly Dictionary<Material, Material> brightFrames = new Dictionary<Material, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<CardFrameBrightness>() != null) return;
            new GameObject("Card Frame Brightness").AddComponent<CardFrameBrightness>();
        }

        private void Start()
        {
            // Card visuals use SpriteRenderer now. The legacy MeshRenderer conversion only needs one pass
            // for any old scene objects, never a full scene search every frame.
            CardVisual[] cards = FindObjectsByType<CardVisual>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int cardIndex = 0; cardIndex < cards.Length; cardIndex++)
            {
                MeshRenderer[] renderers = cards[cardIndex].GetComponentsInChildren<MeshRenderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    ReplaceFrameMaterials(renderers[rendererIndex]);
            }
            enabled = false;
        }

        private void ReplaceFrameMaterials(MeshRenderer renderer)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null || !source.name.StartsWith("Attribute_")) continue;
                if (!brightFrames.TryGetValue(source, out Material bright))
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Unlit/Texture");
                    bright = new Material(shader) { name = source.name + " Bright", mainTexture = source.mainTexture, color = Color.white };
                    if (bright.HasProperty("_BaseMap")) bright.SetTexture("_BaseMap", source.mainTexture);
                    if (bright.HasProperty("_BaseColor")) bright.SetColor("_BaseColor", Color.white);
                    bright.renderQueue = source.renderQueue;
                    brightFrames.Add(source, bright);
                }
                materials[i] = bright;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = materials;
        }
    }
}
