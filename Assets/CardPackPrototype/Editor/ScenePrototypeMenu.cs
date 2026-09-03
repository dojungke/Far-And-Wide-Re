using CardOpen.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CardOpen.Editor
{
    /// <summary>Creates a persistent scene root for the runtime prototype.</summary>
    public static class ScenePrototypeMenu
    {
        [MenuItem("CardOpen/Create Scene-Editable Prototype", false, 1)]
        private static void CreateSceneEditablePrototype()
        {
            PackOnlyPrototype existing = Object.FindAnyObjectByType<PackOnlyPrototype>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                return;
            }

            GameObject root = new GameObject("Pack Only Prototype");
            Undo.RegisterCreatedObjectUndo(root, "Create Scene-Editable CardOpen Prototype");
            root.AddComponent<PackOnlyPrototype>();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }
    }
}