#if UNITY_EDITOR
using UnityEditor;

namespace CardOpen.Prototype.Editor
{
    /// <summary>Keeps Unity's new-scene workflow in 2D mode for this project.</summary>
    [InitializeOnLoad]
    internal static class TwoDimensionalProjectDefaults
    {
        static TwoDimensionalProjectDefaults()
        {
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            if (EditorSettings.defaultBehaviorMode == EditorBehaviorMode.Mode2D) return;
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
