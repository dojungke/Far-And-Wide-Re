using CardOpen.Prototype;
using UnityEditor;
using UnityEngine;

namespace CardOpen.Editor
{
    [CustomEditor(typeof(PackOnlyPrototype))]
    public sealed class PackOnlyPrototypeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Combat Debug (Play Mode)", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use combat debug controls.", MessageType.Info);
                return;
            }

            PackOnlyPrototype prototype = (PackOnlyPrototype)target;
            using (new EditorGUI.DisabledScope(false))
            {
                if (GUILayout.Button("Add Wolf (Max 3)"))
                {
                    Undo.RecordObject(prototype, "Add Debug Enemy");
                    prototype.EditorDebugAddEnemy();
                    EditorUtility.SetDirty(prototype);
                }
                if (GUILayout.Button("Defeat All Enemies"))
                {
                    Undo.RecordObject(prototype, "Defeat Debug Enemies");
                    prototype.EditorDebugDefeatAllEnemies();
                    EditorUtility.SetDirty(prototype);
                }
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Relics", EditorStyles.boldLabel);
                DrawAddRelicButton(prototype, "Add Green Ring", "Combat/Relics/GreenRing");
                DrawAddRelicButton(prototype, "Add Magitech Engine", "Combat/Relics/MagitechEngine");
            }
        }

        private static void DrawAddRelicButton(PackOnlyPrototype prototype, string label, string resourcePath)
        {
            CombatRelicDefinition relic = Resources.Load<CombatRelicDefinition>(resourcePath);
            using (new EditorGUI.DisabledScope(relic == null))
            {
                if (!GUILayout.Button(label)) return;
                Undo.RecordObject(prototype, "Add Debug Relic");
                prototype.EditorDebugAddRelic(relic);
                EditorUtility.SetDirty(prototype);
            }
        }
    }
}