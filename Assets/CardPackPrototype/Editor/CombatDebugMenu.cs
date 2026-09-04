#if UNITY_EDITOR
using CardOpen.Prototype;
using UnityEditor;
using UnityEngine;

namespace CardOpen.Editor
{
    internal static class CombatDebugMenu
    {
        [MenuItem("CardOpen/Debug/적 추가 (최대 3)", false, 20)]
        private static void AddEnemy()
        {
            PackOnlyPrototype game = FindGame();
            if (game == null) return;
            game.EditorDebugAddEnemy();
        }

        [MenuItem("CardOpen/Debug/적 추가 (최대 3)", true)]
        private static bool ValidateAddEnemy()
        {
            return Application.isPlaying && FindGame() != null;
        }

        [MenuItem("CardOpen/Debug/적 전체 즉사", false, 21)]
        private static void DefeatAllEnemies()
        {
            PackOnlyPrototype game = FindGame();
            if (game == null) return;
            game.EditorDebugDefeatAllEnemies();
        }

        [MenuItem("CardOpen/Debug/적 전체 즉사", true)]
        private static bool ValidateDefeatAllEnemies()
        {
            return Application.isPlaying && FindGame() != null;
        }

        [MenuItem("CardOpen/Debug/잎 1장 드로우", false, 22)]
        private static void DrawCard()
        {
            PackOnlyPrototype game = FindGame();
            if (game == null) return;
            game.EditorDebugDrawCard();
        }

        [MenuItem("CardOpen/Debug/잎 1장 드로우", true)]
        private static bool ValidateDrawCard()
        {
            return Application.isPlaying && FindGame() != null;
        }

        [MenuItem("CardOpen/Debug/가지/초록 반지 추가", false, 30)]
        private static void AddGreenRing()
        {
            AddRelic("Combat/Relics/GreenRing");
        }

        [MenuItem("CardOpen/Debug/가지/초록 반지 추가", true)]
        private static bool ValidateAddGreenRing()
        {
            return CanAddRelic("Combat/Relics/GreenRing");
        }

                [MenuItem("CardOpen/Debug/가지/파란파란 추가", false, 36)]
        private static void AddBlueBlue()
        {
            AddRelic("Combat/Relics/BlueBlue");
        }

        [MenuItem("CardOpen/Debug/가지/파란파란 추가", true, 36)]
        private static bool ValidateAddBlueBlue()
        {
            return CanAddRelic("Combat/Relics/BlueBlue");
        }

        [MenuItem("CardOpen/Debug/가지/불타는 검 추가", false, 35)]
        private static void AddFlamingSword()
        {
            AddRelic("Combat/Relics/FlamingSword");
        }

        [MenuItem("CardOpen/Debug/가지/불타는 검 추가", true, 35)]
        private static bool ValidateAddFlamingSword()
        {
            return CanAddRelic("Combat/Relics/FlamingSword");
        }

[MenuItem("CardOpen/Debug/가지/녹색 주사위 추가", false, 34)]
        private static void AddGreenDice()
        {
            AddRelic("Combat/Relics/GreenDice");
        }

        [MenuItem("CardOpen/Debug/가지/녹색 주사위 추가", true, 34)]
        private static bool ValidateAddGreenDice()
        {
            return CanAddRelic("Combat/Relics/GreenDice");
        }

        [MenuItem("CardOpen/Debug/가지/날카로운 검 추가", false, 33)]
        private static void AddSharpSword()
        {
            AddRelic("Combat/Relics/SharpSword");
        }

        [MenuItem("CardOpen/Debug/가지/날카로운 검 추가", true, 33)]
        private static bool ValidateAddSharpSword()
        {
            return CanAddRelic("Combat/Relics/SharpSword");
        }

        [MenuItem("CardOpen/Debug/가지/마법장갑 추가", false, 32)]
        private static void AddMagicGlove()
        {
            AddRelic("Combat/Relics/MagicGlove");
        }

        [MenuItem("CardOpen/Debug/가지/마법장갑 추가", true, 32)]
        private static bool ValidateAddMagicGlove()
        {
            return CanAddRelic("Combat/Relics/MagicGlove");
        }

        [MenuItem("CardOpen/Debug/가지/마법공학 엔진 추가", false, 31)]
        private static void AddMagitechEngine()
        {
            AddRelic("Combat/Relics/MagitechEngine");
        }

        [MenuItem("CardOpen/Debug/가지/마법공학 엔진 추가", true)]
        private static bool ValidateAddMagitechEngine()
        {
            return CanAddRelic("Combat/Relics/MagitechEngine");
        }

        private static bool CanAddRelic(string resourcePath)
        {
            return Application.isPlaying && FindGame() != null
                && Resources.Load<CombatRelicDefinition>(resourcePath) != null;
        }

        private static void AddRelic(string resourcePath)
        {
            PackOnlyPrototype game = FindGame();
            CombatRelicDefinition relic = Resources.Load<CombatRelicDefinition>(resourcePath);
            if (game == null || relic == null) return;
            if (game.EditorDebugAddRelic(relic))
                Debug.Log("Debug relic added: " + relic.GetLocalizedName(false));
            else
                Debug.Log("Debug relic was already held: " + relic.GetLocalizedName(false));
        }

        private static PackOnlyPrototype FindGame()
        {
            return Object.FindAnyObjectByType<PackOnlyPrototype>();
        }
    }
}
#endif