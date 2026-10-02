#if UNITY_EDITOR
// 职责：正式关卡Play从主菜单开始，登记构建入口；独立测试场景仍直接播放。
// 维护Dada；仅UnityEditor入口设置，不重定向运行中的场景或写玩家状态。
// 交接docs/handoffs/Dada.handoff；用户授权主界面作为游戏开始入口。
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Regrowth.UI.Art.Editor
{
    [InitializeOnLoad]
    public static class GameplayEntry
    {
        public const string MainMenu = "Assets/Scenes/Frontend/MainMenu.unity";
        public const string Level = "Assets/Scenes/Gameplay/MainLevel.unity";
        private const string DirectCheck = "PAWGATORY.UI.DirectSceneCheck";

        static GameplayEntry()
        {
            EditorApplication.delayCall += Refresh;
            EditorSceneManager.activeSceneChangedInEditMode += (_, __) => Refresh();
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredEditMode) { Refresh(); }
            };
        }

        public static void SetDirectSceneCheck(bool direct)
        {
            SessionState.SetBool(DirectCheck, direct);
            if (direct) { EditorSceneManager.playModeStartScene = null; }
            else { Refresh(); }
        }

        public static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(DirectCheck, false)) { return; }
            string active = SceneManager.GetActiveScene().path;
            bool gameplay = active == MainMenu || active == Level;
            if (gameplay)
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenu);
            }
            else if (EditorSceneManager.playModeStartScene &&
                     AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == MainMenu)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        // Explicit migration only: preserve other registered scenes and configuration.
        public static void RegisterBuildEntry()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainMenu, true),
                new EditorBuildSettingsScene(Level, true)
            };
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.path != MainMenu && scene.path != Level) { scenes.Add(scene); }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Refresh();
        }
    }
}
#endif
