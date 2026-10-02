// 职责：通过 Unity 原生 API 首次生成独立美术测试资产、切片和场景。
// 模块：player-visual-art-test；依赖：Sprite Editor、uGUI/TMP、Input System、本模块。
// 保护：显式按审核清单更新本模块资产；不改 Build Settings，没有自动运行钩子。
// 交接：docs/handoffs/Dada.handoff；规范：根 AGENTS.md。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.Tests.Art.Editor
{
    public static class PlayerVisualArtSetup
    {
        public const string ArtRoot = "Assets/Art/Characters/Player";
        public const string ScenePath = "Assets/Scenes/Tests/Art/PlayerVisual_ArtTest.unity";
        public const string LibraryPath = ArtRoot + "/Controllers/PlayerVisualLibrary.asset";
        private static TMP_FontAsset font;
        private static readonly Color Paper = new Color(0.91f, 0.89f, 0.83f);
        private static readonly Color Ink = new Color(0.13f, 0.13f, 0.12f);

        /// <summary>兼容原菜单入口，转交当前已审核清单同步。</summary>

        public static void Build()
        {
            PlayerVisualArtRefresh.Apply();
        }

        public static Sprite[] Slice(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (height != 500 || width % 500 != 0 || width > 4096)
            {
                throw new InvalidDataException("Invalid strip size: " + path);
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            var rects = new List<SpriteRect>();
            string sourceName = Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < width / 500; i++)
            {
                string name = sourceName + "_Frame" + (i + 1).ToString("00");
                rects.Add(new SpriteRect { name = name, rect = new Rect(i * 500, 0, 500, 500), alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f), spriteID = previous.TryGetValue(name, out SpriteRect old) ? old.spriteID : GUID.Generate() });
            }
            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.rect.x).ToArray();
            if (sprites.Length != width / 500)
            {
                throw new InvalidDataException("Slice count mismatch: " + path);
            }
            return sprites;
        }

        public static VisualAnimation MakeAnimation(string clipName, string sourceName, Sprite[] frames, VisualAction action, float fps, string root = ArtRoot)
        {
            string path = root + "/Animations/" + clipName + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) clip = new AnimationClip();
            clip.name = clipName;
            clip.frameRate = fps;
            foreach (EditorCurveBinding old in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, old, null);
            var keys = new List<ObjectReferenceKeyframe>();
            for (int i = 0; i < frames.Length; i++)
                keys.Add(new ObjectReferenceKeyframe { time = i / fps, value = frames[i] });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = action == VisualAction.Move;
            settings.startTime = 0;
            settings.stopTime = frames.Length / fps;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            if (created) AssetDatabase.CreateAsset(clip, path);
            else EditorUtility.SetDirty(clip);
            return new VisualAnimation(sourceName, clip, frames, action);
        }

        private static void BuildScene(PlayerVisualLibrary library)
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null)
            {
                throw new InvalidDataException("Existing TMP font is missing.");
            }
            Scene previous = SceneManager.GetActiveScene();
            bool additive = !Application.isBatchMode;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            // 单场景切换会卸载旧场景未引用的资产；切换后重新取得持久资产引用。
            library = AssetDatabase.LoadAssetAtPath<PlayerVisualLibrary>(LibraryPath);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            try
            {
                var root = new GameObject("PlayerVisualTestRoot");
                var visual = new GameObject("PlayerVisualRoot");
                visual.transform.SetParent(root.transform, false);
                var sprite = visual.AddComponent<SpriteRenderer>();
                var animator = visual.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                sprite.sprite = library.Families[0].Find(VisualAction.Idle).Frames[0];
                var camera = new GameObject("Art Preview Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.orthographic = true;
                camera.orthographicSize = 3.3f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.34f, 0.33f, 0.31f);
                camera.rect = new Rect(0.265f, 0.19f, 0.40f, 0.67f);
                var canvasObject = new GameObject("Debug Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1440, 900);
                scaler.matchWidthOrHeight = 0.5f;
                Transform ui = canvasObject.transform;
                Panel(ui, "Header", 0, 0, 1440, 104, Ink);
                Label(ui, "Title", "pawgatory / PLAYER ART TEST", 28, 18, 1000, 40, 32);
                Label(ui, "Subtitle", "Body combinations  /  animation timing  /  frame order", 30, 62, 1050, 27, 18);
                Panel(ui, "Body Panel", 16, 120, 345, 650, Ink);
                Label(ui, "Body Heading", "01 / BODY", 34, 136, 310, 32, 25);
                string[] labels = { "Head", "Body", "Arms + Sword", "Legs", "Normal Tail", "Flame Tail" };
                var toggles = new Toggle[6];
                for (int i = 0; i < toggles.Length; i++)
                {
                    toggles[i] = MakeToggle(ui, labels[i], 34, 180 + i * 43, i == 0);
                }
                Label(ui, "Body Help", "Head is permanent.\nArms / Legs / Tail need Body.\nTails are mutually exclusive.", 34, 452, 308, 88, 18);
                Label(ui, "Action Heading", "02 / ACTION", 34, 548, 310, 32, 25);
                var actions = new Button[6];
                for (int i = 0; i < actions.Length; i++)
                {
                    actions[i] = MakeButton(ui, ((VisualAction)i).ToString(), 34 + i % 2 * 155, 590 + i / 2 * 54, 144, 44);
                }
                Label(ui, "Preview Caption", "500 x 500  /  FULL CANVAS", 387, 128, 560, 32, 20);
                Label(ui, "Frame Help", "Left to right: Frame01 > FrameN\nPause + step to inspect the original order.", 389, 724, 548, 55, 18);
                Panel(ui, "Debug Panel", 978, 120, 446, 650, Ink);
                TMP_Text status = Label(ui, "Status", "OK", 996, 138, 408, 48, 32);
                TMP_Text debug = Label(ui, "Debug Info", "", 996, 198, 408, 550, 18);
                Panel(ui, "Timing Panel", 16, 786, 1408, 102, Ink);
                TMP_Text fpsText = Label(ui, "FPS Label", "FPS: 8", 34, 797, 310, 30, 21);
                Slider fps = MakeSlider(ui, "FPS Slider", 34, 841, 305, 2f, 16f, 8f);
                TMP_Text holdText = Label(ui, "Hold Label", "Hold: 0.150s", 383, 797, 310, 30, 21);
                Slider hold = MakeSlider(ui, "Hold Slider", 383, 841, 305, 0.05f, 0.5f, 0.15f);
                Button pause = MakeButton(ui, "Pause / Play", 724, 801, 132, 34);
                Button replay = MakeButton(ui, "Replay", 866, 801, 94, 34);
                Button prev = MakeButton(ui, "< Frame", 724, 843, 112, 32);
                Button next = MakeButton(ui, "Frame >", 846, 843, 114, 32);
                Button pass = MakeButton(ui, "PASS", 999, 800, 190, 34);
                Button fail = MakeButton(ui, "FAIL", 1200, 800, 199, 34);
                TMP_Text session = Label(ui, "Session Results", "", 999, 841, 399, 42, 13);
                var harness = root.AddComponent<PlayerVisualTestHarness>();
                var serialized = new SerializedObject(harness);
                void Bind(string field, UnityEngine.Object value)
                {
                    serialized.FindProperty(field).objectReferenceValue = value;
                }
                Bind("library", library); Bind("playerVisualRoot", visual.transform); Bind("spriteRenderer", sprite);
                Bind("previewAnimator", animator);
                string[] toggleFields = { "headToggle", "bodyToggle", "armsToggle", "legsToggle", "normalTailToggle", "flameTailToggle" };
                for (int i = 0; i < 6; i++)
                {
                    Bind(toggleFields[i], toggles[i]);
                }
                SerializedProperty buttons = serialized.FindProperty("actionButtons");
                buttons.arraySize = 6;
                for (int i = 0; i < 6; i++)
                {
                    buttons.GetArrayElementAtIndex(i).objectReferenceValue = actions[i];
                }
                Bind("fpsSlider", fps); Bind("holdSlider", hold); Bind("debugText", debug); Bind("statusText", status);
                Bind("fpsText", fpsText); Bind("holdText", holdText); Bind("sessionText", session);
                Bind("passButton", pass); Bind("failButton", fail); Bind("replayButton", replay); Bind("pauseButton", pause);
                Bind("previousButton", prev); Bind("nextButton", next);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ConfigureInput();
                PlayerVisualArtLayout.Apply(canvas, camera);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new IOException("Failed to save " + ScenePath);
                }
            }
            finally
            {
                if (additive)
                {
                    EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid())
                    {
                        SceneManager.SetActiveScene(previous);
                    }
                }
            }
        }

        private static void ConfigureInput()
        {
            var eventSystem = new GameObject("Art Test EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/Shared.inputactions");
            module.actionsAsset = actions;
            InputActionReference Reference(string actionName)
            {
                string path = ArtRoot + "/Controllers/ArtTestUI_" + actionName + ".asset";
                var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
                if (reference == null)
                {
                    reference = InputActionReference.Create(actions.FindAction("UI/" + actionName, true));
                    AssetDatabase.CreateAsset(reference, path);
                }
                return reference;
            }
            module.point = Reference("Point");
            module.leftClick = Reference("Click");
            module.scrollWheel = Reference("ScrollWheel");
            module.move = Reference("Navigate");
            module.submit = Reference("Submit");
            module.cancel = Reference("Cancel");
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static Image Panel(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        private static TMP_Text Label(Transform parent, string name, string text, float x, float y, float width, float height, float size)
        {
            var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = Paper;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private static Button MakeButton(Transform parent, string text, float x, float y, float width, float height)
        {
            Image bg = Panel(parent, text + " Button", x, y, width, height, Paper);
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            TMP_Text label = Label(bg.transform, "Label", text, 4, 0, width - 8, height, 19);
            label.color = Ink;
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static Toggle MakeToggle(Transform parent, string text, float x, float y, bool value)
        {
            var root = Rect(parent, text + " Toggle", x, y, 306, 37);
            Image bg = Panel(root, "Box", 0, 4, 28, 28, Paper);
            bg.raycastTarget = true;
            Image check = Panel(bg.transform, "Checkmark", 5, 5, 18, 18, Ink);
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = check;
            toggle.SetIsOnWithoutNotify(value);
            TMP_Text label = Label(root, "Label", text, 41, 1, 258, 36, 22);
            label.raycastTarget = true;
            return toggle;
        }
        private static Slider MakeSlider(Transform parent, string name, float x, float y, float width, float min, float max, float value)
        {
            var root = Rect(parent, name, x, y, width, 29);
            Image bg = Panel(root, "Track", 0, 9, width, 10, new Color(.4f, .4f, .38f));
            bg.raycastTarget = true;
            var area = Rect(root, "Handle Area", 10, 0, width - 20, 29);
            Image handle = Panel(area, "Handle", 0, 0, 20, 29, Paper);
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.raycastTarget = true;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            return slider;
        }
    }
}
