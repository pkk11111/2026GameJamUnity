// 职责：创建独立敌人美术预览，并验证原有切片、循环动画和保存重开后的引用。
// 模块/维护别名：Dada / Enemy Art；依赖：UnityEditor、Animator、TMP、URP。
// 状态归属：仅本工具的验收状态；场景只使用 Unity 内置表现组件，没有 EnemyBasic 或玩法绑定。
// 接线：编辑模式显式运行菜单；保护未保存场景，不修改源 PNG、Clip、主场景或 Build Settings。
// 交接：docs/handoffs/Dada.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Regrowth.Tests.Art.Editor
{
    [InitializeOnLoad]
    public static class EnemyVisualArtChecks
    {
        public const string ScenePath = "Assets/Scenes/Tests/Art/EnemyVisual_ArtTest.unity";
        private const string PreviewFolder = "Assets/Scenes/Tests/Art/EnemyPreview";
        private const string ArtRoot = "Assets/Art/Characters/Enemies/";
        private const string RunKey = "Regrowth.EnemyArt.Checks";
        private const string ReportFolder = "Logs/EnemyVisualArtTest";
        private static readonly string[] Names = { "Imp_Run", "Bat_Hover", "Satan_Move" };
        private static readonly string[] Guids =
        {
            "aab50c0bca6ca284d913f7984616365c",
            "c525022cd2cea694587cefd7c12a7d67",
            "52739f1a0515ef84197fbe582175e358"
        };
        private static readonly StringBuilder Report = new StringBuilder();
        private static readonly List<string> Errors = new List<string>();
        private static readonly List<int>[] Observed = { new List<int>(), new List<int>(), new List<int>() };
        private static Animator[] animators;
        private static Sprite[][] frames;
        private static float started;
        private static int passed;

        static EnemyVisualArtChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        /// <summary>主线程编辑模式入口：打开已保存预览；若不存在则首次创建，不覆盖已有场景或控制器。</summary>
        [MenuItem("Tools/pawgatory/Enemy Art/Open Preview")]
        public static void OpenPreview()
        {
            RequireCleanEditor();
            if (!File.Exists(ScenePath))
            {
                CreatePreview();
            }
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>显式验收入口：验证资产、保存重开独测、进入 Play；batch 模式写日志并退出。</summary>
        [MenuItem("Tools/pawgatory/Enemy Art/Run Checks (Play)")]
        public static void RunChecks()
        {
            try
            {
                OpenPreview();
                ResetReport();
                ValidateAssets();
                ValidateScene();
                Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()), "preview scene saved");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene(ScenePath);
                ValidateScene();
                Check(!EditorBuildSettings.scenes.Any(s => s.path == ScenePath), "preview excluded from Build Settings");
                Directory.CreateDirectory(ReportFolder);
                File.WriteAllText(ReportFolder + "/edit-checks.txt", passed + " passed / 0 failed\n" + Report);
                SessionState.SetBool(RunKey, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        }

        private static void RequireCleanEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                throw new InvalidOperationException("Please stop Play and save open scenes before opening the enemy preview.");
            }
        }

        private static Sprite[] LoadFrames(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ArtRoot + "Sprites/" + name + ".png")
                .OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
        }

        private static void CreatePreview()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (font == null || shader == null || AssetDatabase.IsValidFolder(PreviewFolder))
            {
                throw new InvalidOperationException("Missing preview font/shader, or partial preview assets already exist. No overwrite performed.");
            }
            for (int i = 0; i < Names.Length; i++)
            {
                if (LoadFrames(Names[i]).Length != 3 || AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "Animations/" + Names[i] + ".anim") == null)
                {
                    throw new InvalidOperationException("Existing enemy assets are incomplete: " + Names[i]);
                }
            }
            AssetDatabase.CreateFolder("Assets/Scenes/Tests/Art", "EnemyPreview");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Enemy Preview Camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 5.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .175f, .165f);
            Label("Title", "pawgatory / ENEMY ART PREVIEW", new Vector2(0f, 4.5f), 7f, new Vector2(18f, 1f), font);
            for (int i = 0; i < Names.Length; i++)
            {
                string name = Names[i];
                var visual = new GameObject(name);
                visual.transform.position = new Vector3((i - 1) * 6f, .6f, 0f);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = LoadFrames(name)[0];
                var material = new Material(shader);
                material.mainTexture = renderer.sprite.texture;
                AssetDatabase.CreateAsset(material, PreviewFolder + "/" + name + "_Preview.mat");
                renderer.sharedMaterial = material;
                var controller = AnimatorController.CreateAnimatorControllerAtPath(PreviewFolder + "/" + name + "_Preview.controller");
                AnimatorState state = controller.layers[0].stateMachine.AddState("Loop");
                state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "Animations/" + name + ".anim");
                state.writeDefaultValues = false;
                controller.layers[0].stateMachine.defaultState = state;
                var animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Label(name + " Label", name, new Vector2((i - 1) * 6f, -2.5f), 5f, new Vector2(5.5f, .8f), font);
                Label(name + " Info", "3 frames / 500 x 500 / 8 fps", new Vector2((i - 1) * 6f, -3.2f), 3f, new Vector2(5.5f, .6f), font);
            }
            Label("Footer", "Source canvas preserved  /  left to right  /  loop", new Vector2(0f, -4.5f), 3.5f, new Vector2(18f, .8f), font);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new IOException("Could not save enemy preview scene.");
            }
        }

        private static void Label(string name, string text, Vector2 position, float size, Vector2 dimensions, TMP_FontAsset font)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshPro>();
            label.text = text;
            label.font = font;
            label.fontSize = size;
            label.color = new Color(.9f, .88f, .82f);
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = dimensions;
            label.rectTransform.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        }

        private static void ValidateAssets()
        {
            for (int i = 0; i < Names.Length; i++)
            {
                string name = Names[i];
                string path = ArtRoot + "Sprites/" + name + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Sprite[] sprites = LoadFrames(name);
                Check(AssetDatabase.AssetPathToGUID(path) == Guids[i], name + " original PNG GUID retained");
                Check(importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple, name + " Sprite / Multiple");
                Check(importer.textureCompression == TextureImporterCompression.Uncompressed && importer.alphaIsTransparency, name + " uncompressed / alpha transparency");
                Check(texture.width == 1500 && texture.height == 500 && sprites.Length == 3, name + " imported full 1500 x 500, three frames");
                for (int frame = 0; frame < sprites.Length; frame++)
                {
                    Check(sprites[frame].rect == new Rect(frame * 500, 0, 500, 500), name + " untrimmed frame " + (frame + 1));
                    Check(sprites[frame].pixelsPerUnit == 100f && sprites[frame].pivot == new Vector2(250f, 250f), name + " frame scale/pivot " + (frame + 1));
                }
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "Animations/" + name + ".anim");
                Check(clip != null && clip.isLooping && AnimationUtility.GetAnimationClipSettings(clip).loopTime, name + " loop enabled");
                Check(clip.frameRate == 8f && Mathf.Abs(clip.length - .375f) < .001f, name + " 8 fps / 0.375 s");
                Check(AnimationUtility.GetAnimationEvents(clip).Length == 0 && AnimationUtility.GetCurveBindings(clip).Length == 0, name + " sprite-only animation, no gameplay events");
                EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Check(bindings.Length == 1 && bindings[0].type == typeof(SpriteRenderer) && bindings[0].path == "" && bindings[0].propertyName == "m_Sprite", name + " SpriteRenderer binding");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
                Check(keys.Length == sprites.Length, name + " three live Sprite keys");
                for (int frame = 0; frame < keys.Length; frame++)
                {
                    Check(keys[frame].value == sprites[frame] && Mathf.Abs(keys[frame].time - frame / clip.frameRate) < .001f, name + " current Sprite subresource / left-to-right key " + (frame + 1));
                }
            }
        }

        private static void ValidateScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Check(scene.path == ScenePath, "independent enemy preview scene active");
            foreach (Transform child in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, child.name + " no Missing Script");
                foreach (Component component in child.GetComponents<Component>())
                {
                    var property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        {
                            throw new InvalidDataException("Missing reference: " + child.name + "/" + property.propertyPath);
                        }
                    }
                }
            }
            foreach (string name in Names)
            {
                var visual = GameObject.Find(name);
                var animator = visual.GetComponent<Animator>();
                Check(visual.GetComponent<SpriteRenderer>().sprite == LoadFrames(name)[0], name + " saved first Sprite");
                Check(animator.runtimeAnimatorController.animationClips.Single() == AssetDatabase.LoadAssetAtPath<AnimationClip>(ArtRoot + "Animations/" + name + ".anim"), name + " saved original Clip reference");
                Check(visual.transform.localScale == Vector3.one && visual.GetComponents<Component>().Length == 3, name + " native presentation only / unchanged scale");
            }
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunKey, false))
            {
                SessionState.SetBool(RunKey, false);
                EditorApplication.delayCall += BeginPlayChecks;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= ObservePlayback;
                Application.logMessageReceived -= OnLog;
            }
        }

        private static void BeginPlayChecks()
        {
            ResetReport();
            Errors.Clear();
            Application.logMessageReceived += OnLog;
            try
            {
                animators = Names.Select(name => GameObject.Find(name).GetComponent<Animator>()).ToArray();
                frames = Names.Select(LoadFrames).ToArray();
                for (int i = 0; i < Names.Length; i++)
                {
                    Animator animator = animators[i];
                    for (int cycle = 0; cycle < 4; cycle++)
                    {
                        for (int frame = 0; frame < 3; frame++)
                        {
                            animator.Play("Loop", 0, cycle + (frame + .3f) / 3f);
                            animator.Update(0f);
                            Check(animator.GetComponent<SpriteRenderer>().sprite == frames[i][frame], Names[i] + " cycle " + cycle + " frame " + (frame + 1));
                        }
                    }
                    animator.Play("Loop", 0, 0f);
                    animator.Update(0f);
                    animator.speed = .5f;
                    animator.Update(.15f);
                    Check(animator.GetComponent<SpriteRenderer>().sprite == frames[i][0], Names[i] + " half speed holds frame 1");
                    animator.speed = 1f;
                    animator.Update(.15f);
                    Check(animator.GetComponent<SpriteRenderer>().sprite == frames[i][1], Names[i] + " speed change advances frame 2");
                    animator.Play("Loop", 0, 0f);
                    animator.Update(0f);
                    Observed[i].Clear();
                }
                started = Time.time;
                EditorApplication.update += ObservePlayback;
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        }

        private static void ObservePlayback()
        {
            try
            {
                for (int i = 0; i < Names.Length; i++)
                {
                    int frame = Array.IndexOf(frames[i], animators[i].GetComponent<SpriteRenderer>().sprite);
                    if (frame < 0)
                    {
                        throw new InvalidDataException(Names[i] + " Missing or unexpected Sprite during real playback.");
                    }
                    if (Observed[i].Count == 0 || Observed[i][Observed[i].Count - 1] != frame)
                    {
                        Observed[i].Add(frame);
                    }
                }
                if (Time.time - started < 2f)
                {
                    return;
                }
                for (int i = 0; i < Names.Length; i++)
                {
                    Check(Observed[i].Distinct().Count() == 3 && animators[i].GetCurrentAnimatorStateInfo(0).normalizedTime > 3f, Names[i] + " real Play covers all frames and multiple loops: " + string.Join(",", Observed[i].Select(f => f + 1)));
                    Check(animators[i].GetComponent<SpriteRenderer>().bounds.size.x > 0f && animators[i].GetComponent<SpriteRenderer>().bounds.size.x <= 5.001f, Names[i] + " rendered size within original 500 px canvas at 100 PPU");
                }
                if (Environment.GetCommandLineArgs().Contains("-enemyArtCapture"))
                {
                    Capture();
                }
                Check(Errors.Count == 0, "no new Console Error/Exception/Assert during Play: " + string.Join(" | ", Errors));
                Finish(null);
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        }

        private static void Capture()
        {
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(ReportFolder + "/preview.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                Errors.Add(message);
            }
        }

        private static void ResetReport()
        {
            passed = 0;
            Report.Clear();
        }

        private static void Check(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidDataException(description);
            }
            passed++;
            Report.AppendLine("PASS " + description);
        }

        private static void Finish(Exception failure)
        {
            EditorApplication.update -= ObservePlayback;
            Application.logMessageReceived -= OnLog;
            SessionState.SetBool(RunKey, false);
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/play-checks.txt", passed + " passed / " + (failure == null ? "0" : "1") + " failed\n" + Report + (failure == null ? "" : failure.ToString()));
            if (failure == null)
            {
                Debug.Log("[ENEMY ART CHECKS] " + passed + " Play checks passed, no new errors.");
            }
            else
            {
                Debug.LogException(failure);
            }
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(failure == null ? 0 : 1);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
