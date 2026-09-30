// 职责：仅编辑器的 T01 独测场景接线；实例化已制作的 Prefab，不在 C# 构建 UI 外观。
// 模块/维护：Soap / T01；依赖：UnityEditor、ChoicePanel、ChoiceMenuSmokeDriver。
// 保护：非 Play 且当前场景无未保存修改才能创建；不修改 Build Settings 或现有场景。
// 交接：docs/handoffs/Soap.handoff；规范：根目录 AGENTS.md。
using System.IO;
using Regrowth.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.Tests.T01.Editor
{
    public static class T01SceneSetup
    {
        /// <summary>仅整理本任务现有占位资产；从已有颜色计算灰阶，不提供正式美术参数。</summary>
        [MenuItem("Tools/GROWL AGAIN/T01/Normalize Placeholder Grayscale")]
        public static void NormalizePlaceholderGrayscale()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[T01] Stop Play and save current scene before editing test prefabs.");
                return;
            }
            string[] paths =
            {
                "Assets/Prefabs/Choice/ChoiceCard.prefab",
                "Assets/Prefabs/Choice/ChoiceMenu.prefab",
                "Assets/Prefabs/Tests/T01/T01SmokeRig.prefab"
            };
            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                    {
                        graphic.color = Grayscale(graphic.color);
                    }
                    foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(true))
                    {
                        ColorBlock colors = selectable.colors;
                        colors.normalColor = Grayscale(colors.normalColor);
                        colors.highlightedColor = Grayscale(colors.highlightedColor);
                        colors.pressedColor = Grayscale(colors.pressedColor);
                        colors.selectedColor = Grayscale(colors.selectedColor);
                        colors.disabledColor = Grayscale(colors.disabledColor);
                        selectable.colors = colors;
                    }
                    foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    {
                        camera.backgroundColor = Grayscale(camera.backgroundColor);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log("[T01] Existing placeholder colors saved as grayscale in three T01 prefabs.");
        }

        private static Color Grayscale(Color color)
        {
            float gray = color.grayscale;
            return new Color(gray, gray, gray, color.a);
        }

        /// <summary>从磁盘重新加载本任务场景；保护当前未保存场景和 Play 状态。</summary>
        [MenuItem("Tools/GROWL AGAIN/T01/Reload Smoke Scene")]
        public static void ReloadSmokeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[T01] Stop Play and save current scene before reloading smoke scene.");
                return;
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Tests/T01/T01_Smoke.unity", OpenSceneMode.Single);
            Debug.Log("[T01] Reloaded smoke scene from disk.");
        }

        [MenuItem("Tools/GROWL AGAIN/T01/Create Smoke Scene")]
        public static void CreateSmokeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogError("[T01] Stop Play and save current scene before creating smoke scene.");
                return;
            }
            const string scenePath = "Assets/Scenes/Tests/T01/T01_Smoke.unity";
            if (File.Exists(scenePath))
            {
                Debug.LogError("[T01] Smoke scene already exists; open it instead of overwriting.");
                return;
            }
            GameObject menuAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Choice/ChoiceMenu.prefab");
            GameObject rigAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tests/T01/T01SmokeRig.prefab");
            if (menuAsset == null || rigAsset == null)
            {
                Debug.LogError("[T01] Missing authored ChoiceMenu or T01SmokeRig prefab.");
                return;
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject menu = (GameObject)PrefabUtility.InstantiatePrefab(menuAsset, scene);
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, scene);
            ChoiceMenuSmokeDriver driver = rig.GetComponent<ChoiceMenuSmokeDriver>();
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("panel").objectReferenceValue = menu.GetComponent<ChoicePanel>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/Scenes/Tests/T01");
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[T01] Saved independent smoke scene with prefab instances: " + scenePath);
        }
    }
}
