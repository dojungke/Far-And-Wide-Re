using UnityEngine;
using UnityEngine.Rendering;

namespace CardOpen.Prototype
{
    /// <summary>Displays the player paw as a topmost 2D sprite over the card hand.</summary>
    [DefaultExecutionOrder(1300)]
    public sealed class PlayerHandArmSpriteVisual : MonoBehaviour
    {
        private const float ArmHeight = 3.08f;
        private GameObject visual;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // The player hand overlay is intentionally disabled for the enemy encounter UI.
            GameObject existingVisual = GameObject.Find("Player Hand Arm Sprite");
            if (existingVisual != null) Destroy(existingVisual);
        }

        private void Awake()
        {
            // The hand overlay is intentionally disabled for the combat UI.
            GameObject previousLayer = GameObject.Find("Player Hand Arm Front Visual");
            if (previousLayer != null) previousLayer.SetActive(false);
            enabled = false;
        }

        private void CreateVisual()
        {
            Texture2D texture = Resources.Load<Texture2D>("UI/PlayerHandArmFront");
            if (texture == null || Camera.main == null) return;

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f);
            visual = new GameObject("Player Hand Arm Sprite");
            visual.transform.position = new Vector3(0f, -2.42f, -1.10f);
            visual.transform.localScale = Vector3.one * (ArmHeight / sprite.bounds.size.y);

            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = short.MaxValue;
        }

    }
}
