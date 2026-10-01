// 职责：本模块切片/Clip 验证与独立场景 Play 验收；不执行正式 gameplay 测试。
// 模块：player-visual-art-test；依赖：本模块、UnityEditor、uGUI；仅测试时使用反射读取绑定。
// 生命周期：菜单显式启动；SessionState 仅跨 Play 域重载保留本次运行标志。
// 交接：docs/handoffs/Dada.handoff；规范：根 AGENTS.md。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Regrowth.Tests.Art.Editor
{
    [InitializeOnLoad]
    public static class PlayerVisualArtChecks
    {
        private const string RunKey = "Regrowth.ArtTests.Run";
        private const string BatchKey = "Regrowth.ArtTests.Batch";
        private static int passed;
        private static StringBuilder report;
        // 显式 batch 入口：先保存本测试场景布局，再进入播放验收。
        public static void ValidateFittedScene()
        {
            PlayerVisualArtLayout.FitTestPreview();
            RunPlayChecks();
        }
        static PlayerVisualArtChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }
        private static void Check(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException("[ART TEST FAIL] " + description);
            }
            passed++;
            report.AppendLine("PASS " + description);
        }

        [MenuItem("Tools/pawgatory/Art Test/Validate Imported Assets")]
        public static void ValidateAssets()
        {
            passed = 0;
            report = new StringBuilder();
            PlayerVisualLibrary library = AssetDatabase.LoadAssetAtPath<PlayerVisualLibrary>(PlayerVisualArtSetup.LibraryPath);
            Check(library != null && library.Families.Length == 13 && library.UnassignedVariants.Length == 0, "13 mapped combinations / no removed variants");
            var all = library.Families.SelectMany(f => f.Animations).Concat(library.UnassignedVariants).ToArray();
            Check(all.Length == 52, "39 current player clips + 13 temporary Idle clips");
            foreach (VisualAnimation item in all)
            {
                Check(item.IsReady, item.Clip.name + " all references ready");
                Check(AnimationUtility.GetAnimationEvents(item.Clip).Length == 0, item.Clip.name + " no animation events");
                Check(AnimationUtility.GetAnimationClipSettings(item.Clip).loopTime == item.Loop, item.Clip.name + " loop flag");
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(item.Clip).Single();
                var keys = AnimationUtility.GetObjectReferenceCurve(item.Clip, binding);
                Check(binding.type == typeof(SpriteRenderer) && binding.path == "" && binding.propertyName == "m_Sprite", item.Clip.name + " sprite-only curve");
                Check(keys.Length == item.FrameCount, item.Clip.name + " exactly one key per source frame");
                for (int i = 0; i < item.FrameCount; i++)
                {
                    Sprite sprite = item.Frames[i];
                    Check(sprite.rect == new Rect(i * 500, 0, 500, 500) || (item.Action == VisualAction.Idle && sprite.rect == new Rect(0, 0, 500, 500)), item.Clip.name + " rect " + i);
                    Check(keys[i].value == sprite && Mathf.Abs(keys[i].time - i / item.Clip.frameRate) < 0.00001f, item.Clip.name + " frame order/time " + i);
                    Check(sprite.pivot == new Vector2(250, 250), item.Clip.name + " canvas pivot " + i);
                }
                string texturePath = AssetDatabase.GetAssetPath(item.Frames[0].texture);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                Check(importer.spritePixelsPerUnit == 100f && importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple && importer.textureCompression == TextureImporterCompression.Uncompressed && importer.maxTextureSize == 4096 && importer.alphaIsTransparency && !importer.mipmapEnabled && importer.npotScale == TextureImporterNPOTScale.None, texturePath + " importer contract");
                if (item.Action != VisualAction.Idle)
                {
                    Check(Mathf.Abs(item.Clip.length - item.FrameCount / item.Clip.frameRate) < 0.001f, item.Clip.name + " full duration");
                }
            }
            for (int bits = 0; bits < 64; bits++)
            {
                bool head = (bits & 1) != 0, body = (bits & 2) != 0, arms = (bits & 4) != 0, legs = (bits & 8) != 0, normal = (bits & 16) != 0, flame = (bits & 32) != 0;
                var state = new VisualCombination(head, body, arms, legs, normal, flame);
                foreach (VisualAction action in Enum.GetValues(typeof(VisualAction)))
                {
                    bool invalid = !head || (normal && flame) || (!body && (arms || legs || normal || flame));
                    bool unavailable = action == VisualAction.Bite && (!body || arms) || action == VisualAction.Attack && (!body || !arms) || action == VisualAction.Fire && (!body || !flame);
                    bool missingBody = false;
                    bool missingAction = body && flame && ((action == VisualAction.Bite && !arms) || (action == VisualAction.Fire && arms && legs));
                    VisualStatus expected = invalid ? VisualStatus.INVALID : unavailable ? VisualStatus.UNAVAILABLE : missingBody || missingAction ? VisualStatus.MISSING : VisualStatus.OK;
                    VisualStatus actual = library.Resolve(state, action, out _, out _, out _);
                    Check(actual == expected, $"combination {bits} / {action}: {expected}");
                }
            }
            foreach (PlayerVisualArtRefresh.Strip strip in PlayerVisualArtRefresh.ReadManifest().strips)
            {
                Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(strip.path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
                Check(sprites.Length == strip.frames, strip.name + " current manifest frame count");
                string root = PlayerVisualArtSetup.ArtRoot;
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(root + "/Animations/" + strip.name + ".anim");
                Check(clip != null && Mathf.Abs(clip.frameRate - strip.fps) < .001f, strip.name + " clip and fps");
            }
            Check(!EditorBuildSettings.scenes.Any(scene => scene.path == PlayerVisualArtSetup.ScenePath), "test scene excluded from production Build Settings");
            Directory.CreateDirectory("Logs/PlayerVisualArtTest");
            File.WriteAllText("Logs/PlayerVisualArtTest/asset-checks.txt", $"{passed} passed / 0 failed\n" + report);
            Debug.Log($"[ART ASSET CHECKS] {passed} passed / 0 failed");
        }

        [MenuItem("Tools/pawgatory/Art Test/Open Test Scene")]
        public static void OpenTestScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                throw new InvalidOperationException("Stop Play and save current scenes before opening the art test.");
            }
            EditorSceneManager.OpenScene(PlayerVisualArtSetup.ScenePath);
        }

        /// <summary>显式打开本模块场景，保护未保存场景；batch 模式验收后退出编辑器。</summary>
        [MenuItem("Tools/pawgatory/Art Test/Run Play Checks")]
        public static void RunPlayChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                throw new InvalidOperationException("Stop Play and save scenes before running art checks.");
            }
            ValidateAssets();
            EditorSceneManager.OpenScene(PlayerVisualArtSetup.ScenePath);
            ValidateSceneReferences();
            // 只保存独立 Art Test；卸载再打开，验证磁盘上的持久引用。
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                throw new InvalidOperationException("Cannot save the independent art test scene.");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.OpenScene(PlayerVisualArtSetup.ScenePath);
            ValidateSceneReferences();
            Debug.Log("[ART RELOAD CHECKS] Saved, closed and reopened: no missing scripts or broken object references.");
            SessionState.SetBool(RunKey, true);
            SessionState.SetBool(BatchKey, Application.isBatchMode);
            EditorApplication.EnterPlaymode();
        }
        // 检查测试场景的全部持久组件引用；可选且从未绑定的空字段不是丢失引用。
        private static void ValidateSceneReferences()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    {
                        throw new InvalidOperationException("Missing Script: " + child.name);
                    }
                    foreach (Component component in child.GetComponents<Component>())
                    {
                        var serialized = new SerializedObject(component);
                        SerializedProperty property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                                property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                            {
                                throw new InvalidOperationException("Broken reference: " + child.name + "/" + property.propertyPath);
                            }
                        }
                    }
                }
            }
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunKey, false))
            {
                EditorApplication.delayCall += RunInPlay;
            }
        }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void RunInPlay()
        {
            if (!SessionState.GetBool(RunKey, false))
            {
                return;
            }
            SessionState.SetBool(RunKey, false);
            passed = 0;
            report = new StringBuilder();
            int exitCode = 0;
            try
            {
                PlayerVisualTestHarness harness = UnityEngine.Object.FindFirstObjectByType<PlayerVisualTestHarness>();
                Check(harness != null && harness.enabled, "saved scene harness enabled with all bindings");
                string[] fields = { "headToggle", "bodyToggle", "armsToggle", "legsToggle", "normalTailToggle", "flameTailToggle" };
                var toggles = fields.Select(name => Field<Toggle>(harness, name)).ToArray();
                Button[] buttons = Field<Button[]>(harness, "actionButtons");
                SpriteRenderer renderer = Field<SpriteRenderer>(harness, "spriteRenderer");
                Transform visual = Field<Transform>(harness, "playerVisualRoot");
                PlayerVisualLibrary library = Field<PlayerVisualLibrary>(harness, "library");
                Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                Camera previewCamera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(camera => camera.targetTexture != null);
                Check(canvas.GetComponent<CanvasScaler>().screenMatchMode == CanvasScaler.ScreenMatchMode.Expand, "UI fits wide and tall Game view ratios");
                RectTransform board = canvas.transform.Find("ArtBoard") as RectTransform;
                Check(board != null && board.sizeDelta == new Vector2(1440, 900) && board.anchorMin == new Vector2(.5f, .5f), "centered art board preserves all controls");
                Check(previewCamera.targetTexture != null && board.GetComponentInChildren<RawImage>().texture == previewCamera.targetTexture, "sprite preview has an independent render target");
                Check(previewCamera.GetComponent<UniversalAdditionalCameraData>() != null, "preview camera has URP integration component");
                void Set(int bits)
                {
                    for (int i = 0; i < toggles.Length; i++)
                    {
                        toggles[i].SetIsOnWithoutNotify((bits & (1 << i)) != 0);
                    }
                }
                Check(toggles[0].isOn && toggles.Skip(1).All(toggle => !toggle.isOn), "head-only scene defaults");
                // 真实 Toggle 事件检查；组合穷举另用静默设置构造双尾等非法状态。
                for (int i = 0; i < toggles.Length; i++)
                {
                    Set(3);
                    harness.Request(VisualAction.Idle);
                    toggles[i].isOn = !toggles[i].isOn;
                    VisualStatus expectedToggle = library.Resolve(harness.Combination, VisualAction.Idle, out _, out VisualAnimation toggleAnimation, out _);
                    Check(harness.Status == expectedToggle && renderer.sprite == (expectedToggle == VisualStatus.OK ? toggleAnimation.Frames[0] : null), "body Toggle event " + fields[i]);
                }
                for (int bits = 0; bits < 64; bits++)
                {
                    Set(bits);
                    foreach (VisualAction action in Enum.GetValues(typeof(VisualAction)))
                    {
                        buttons[(int)action].onClick.Invoke();
                        VisualStatus expected = library.Resolve(harness.Combination, action, out _, out VisualAnimation animation, out _);
                        Check(harness.Status == expected && harness.RequestedAction == action, $"saved UI wiring {bits} / {action}");
                        if (expected != VisualStatus.OK)
                        {
                            Check(renderer.sprite == null, "no stale visual after invalid/unavailable/missing request");
                            continue;
                        }
                        Check(renderer.sprite == animation.Frames[0], animation.Clip.name + " first frame sampled");
                        if (action == VisualAction.Idle)
                        {
                            continue;
                        }
                        for (int i = 1; i < animation.FrameCount; i++)
                        {
                            harness.Tick(1f / harness.Fps + 0.00001f);
                            Check(renderer.sprite == animation.Frames[i], animation.Clip.name + " runtime ordered frame " + i);
                        }
                        harness.Tick(animation.FrameCount == 1 ? harness.HoldSeconds + 0.001f : 1f / harness.Fps + 0.001f);
                        Check(harness.Finished == !animation.Loop, animation.Clip.name + " looping / one-shot completion");
                    }
                }
                Set(3);
                toggles[4].isOn = true;
                toggles[5].isOn = true;
                Check(!toggles[4].isOn && toggles[5].isOn, "Flame Tail excludes Normal Tail");
                toggles[4].isOn = true;
                Check(toggles[4].isOn && !toggles[5].isOn, "Normal Tail excludes Flame Tail");
                Set(3);
                harness.Request(VisualAction.Move);
                Slider fps = Field<Slider>(harness, "fpsSlider");
                fps.value = 2f;
                harness.Tick(0.26f);
                Check(harness.DisplayedFrame == 1, "2fps holds first frame for 0.5 seconds");
                fps.value = 16f;
                harness.Tick(0.04f);
                Check(harness.DisplayedFrame == 2, "16fps immediately changes current playback without resetting phase");
                harness.Request(VisualAction.Bite);
                Slider hold = Field<Slider>(harness, "holdSlider");
                hold.value = 0.3f;
                harness.Tick(0.29f);
                Check(!harness.Finished && renderer.sprite == harness.CurrentAnimation.Frames[0], "single frame holds for selected duration");
                harness.Tick(0.02f);
                Check(harness.Finished && renderer.sprite == library.Families[1].Find(VisualAction.Idle).Frames[0], "one-shot returns to explicit rest pose after hold");
                hold.value = 0.12f;
                Check(!harness.Finished, "changing hold restarts one-shot for comparison");
                harness.Request(VisualAction.Idle);
                Vector3 scale = visual.localScale;
                Sprite idle = renderer.sprite;
                harness.Tick(0.25f);
                Check(renderer.sprite == idle && visual.localScale.x > scale.x && visual.localScale.x <= scale.x * 1.0101f, "static Idle with 0.99-1.01 root breathing");
                harness.Request(VisualAction.Move);
                Check(visual.localScale == scale, "leaving Idle restores visual root transform");
                Field<Button>(harness, "pauseButton").onClick.Invoke();
                harness.Tick(5f);
                Check(harness.DisplayedFrame == 1, "pause freezes animation");
                Field<Button>(harness, "nextButton").onClick.Invoke();
                Check(harness.DisplayedFrame == 2, "next frame control");
                Field<Button>(harness, "previousButton").onClick.Invoke();
                Check(harness.DisplayedFrame == 1, "previous frame control");
                Field<Button>(harness, "passButton").onClick.Invoke();
                Field<Button>(harness, "failButton").onClick.Invoke();
                Check(harness.SessionResults.Count == 2, "PASS / FAIL session and Console records");
                harness.enabled = false;
                harness.enabled = true;
                Field<Button>(harness, "passButton").onClick.Invoke();
                Check(harness.SessionResults.Count == 3, "disable/enable does not duplicate listeners");
                Check(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).All(component => component == null || !new[] { "PlayerState", "WhiteboxPlayer2D", "GameBootstrap", "RunController" }.Contains(component.GetType().Name)), "scene has no gameplay state or control components");
                Set(31);
                harness.Request(VisualAction.Move);
                if (Environment.GetCommandLineArgs().Contains("-artCapture"))
                {
                    Capture(canvas, previewCamera, "Logs/PlayerVisualArtTest/preview-ok.png");
                    Set(35);
                    harness.Request(VisualAction.Bite);
                    Capture(canvas, previewCamera, "Logs/PlayerVisualArtTest/preview-missing.png");
                    Set(5);
                    harness.Request(VisualAction.Idle);
                    Capture(canvas, previewCamera, "Logs/PlayerVisualArtTest/preview-invalid.png");
                    Set(1);
                    harness.Request(VisualAction.Attack);
                    Capture(canvas, previewCamera, "Logs/PlayerVisualArtTest/preview-unavailable.png");
                }
                report.Insert(0, $"{passed} passed / 0 failed\n");
                Debug.Log($"[ART PLAY CHECKS] {passed} passed / 0 failed");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                report.AppendLine("FAILED " + exception);
                Debug.LogException(exception);
            }
            Directory.CreateDirectory("Logs/PlayerVisualArtTest");
            File.WriteAllText("Logs/PlayerVisualArtTest/play-checks.txt", report.ToString());
            if (SessionState.GetBool(BatchKey, false))
            {
                EditorApplication.Exit(exitCode);
            }
        }
        // 显式 -artCapture 验证使用：渲染本测试 Canvas，不截取桌面或其他窗口。
        private static void Capture(Canvas canvas, Camera previewCamera, string path)
        {
            var captureObject = new GameObject("Temporary Art Capture Camera");
            var camera = captureObject.AddComponent<Camera>();
            captureObject.AddComponent<UniversalAdditionalCameraData>();
            var target = new RenderTexture(1440, 900, 24);
            var image = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            var children = canvas.GetComponentsInChildren<Transform>(true);
            int[] layers = children.Select(child => child.gameObject.layer).ToArray();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            RenderTexture oldTarget = RenderTexture.active;
            try
            {
                foreach (Transform child in children)
                {
                    child.gameObject.layer = 5;
                }
                camera.cullingMask = 1 << 5;
                camera.backgroundColor = new Color(.08f, .08f, .075f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.targetTexture = target;
                camera.transform.position = new Vector3(1000, 1000, -10);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                previewCamera.Render();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = oldTarget;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                for (int i = 0; i < children.Length; i++)
                {
                    children[i].gameObject.layer = layers[i];
                }
                UnityEngine.Object.DestroyImmediate(captureObject);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
