// Soap: idempotent formal-scene wiring; depends on Intro/Runtime and UnityEditor scene/prefab APIs.
// Never runs automatically on import or Play; no gameplay, input or Bootstrap creation.
// Handoff: docs/handoffs/Soap.handoff. Rules: root AGENTS.md.
using System.Collections.Generic;
using Regrowth.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Regrowth.UI.Intro.Editor
{
    public static class OpeningStoryIntroSetup
    {
        private const string ScenePath = "Assets/Scenes/Gameplay/MainLevel.unity";
        private const string PrefabPath = "Assets/Prefabs/UI/OpeningStoryIntro.prefab";

        /// <summary>Edit-mode manual repair/install. Rejects dirty scenes/duplicate dependencies; saves only Level_Whitebox.</summary>
        [MenuItem("Tools/pawgatory/Setup Opening Story Intro")]
        public static void SetupMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Opening Story Intro setup requires Edit mode.");
                return;
            }
            // Do not close, overwrite or silently save anyone's unsaved scene edits.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogWarning("Opening Story Intro setup stopped: save/review dirty scenes first.");
                    return;
                }
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<OpeningStoryIntro>() == null)
            {
                Debug.LogError("Opening Story Intro prefab is missing; restore the delivered asset first.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var intros = new List<OpeningStoryIntro>();
            var runs = new List<RunController>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                intros.AddRange(root.GetComponentsInChildren<OpeningStoryIntro>(true));
                runs.AddRange(root.GetComponentsInChildren<RunController>(true));
            }
            if (intros.Count > 1 || runs.Count != 1 || !runs[0].isActiveAndEnabled)
            {
                Debug.LogError("Opening Story Intro setup requires at most one intro and exactly one active RunController; no objects changed.");
                return;
            }

            OpeningStoryIntro intro;
            if (intros.Count == 0)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Install Opening Story Intro");
                intro = instance.GetComponent<OpeningStoryIntro>();
            }
            else
            {
                intro = intros[0];
            }

            var serialized = new SerializedObject(intro);
            serialized.FindProperty("runController").objectReferenceValue = runs[0];
            bool changed = serialized.ApplyModifiedProperties();
            if (changed)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(intro);
            }
            if (scene.isDirty)
            {
                if (!EditorSceneManager.SaveScene(scene))
                {
                    Debug.LogError("Opening Story Intro setup could not save Level_Whitebox; wiring is still unsaved.", intro);
                    return;
                }
            }
            Debug.Log("Opening Story Intro is wired in Level_Whitebox. Repeated setup reuses the same instance; verify the opening manually.", intro);
        }
    }
}
