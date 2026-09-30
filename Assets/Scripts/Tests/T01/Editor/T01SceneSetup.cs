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

namespace Regrowth.Tests.T01.Editor
{
    public static class T01SceneSetup
    {
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
