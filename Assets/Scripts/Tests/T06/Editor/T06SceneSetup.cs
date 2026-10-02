// 职责：Unity 编辑器内制作/保存本任务灰盒场景及 Prefab；不是运行时造UI或正式资源加载器。
// 模块/维护：Soap / T06；依赖：UnityEditor、正式C01/C02、Locomotion、T06SmokeDriver。
// 接线：仅生成T06目录资产，拒绝脏场景/Play/覆盖已存在场景；外观最终存为可编辑Unity资产。
// 交接：docs/handoffs/Soap.handoff；规范：根AGENTS.md。
using System.IO;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.Tests.T06.Editor
{
    public static class T06SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tests/T06/T06_Smoke.unity";

        [MenuItem("Tools/GROWL AGAIN/T06/Create Smoke Scene")]
        public static void CreateSmokeScene()
        {
            if (!Safe() || File.Exists(ScenePath))
            {
                Debug.LogWarning("[T06] Refusing creation: scene already exists or current editor state is not safe.");
                return;
            }
            Directory.CreateDirectory("Assets/Scenes/Tests/T06");
            Directory.CreateDirectory("Assets/Prefabs/Tests/T06");
            Directory.CreateDirectory("Assets/Input/Tests");
            Directory.CreateDirectory("Assets/Art/Environment/Primitives");
            Directory.CreateDirectory("Assets/Configs/Physics");
            AssetDatabase.Refresh();
            Sprite sprite = MakeSprite();
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/Shared.inputactions");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var runtime = new GameObject("T06 Runtime");
            var run = runtime.AddComponent<RunController>();
            var input = runtime.AddComponent<PlayerInputReader>();
            Bind(input, "inputActions", actions);

            var player = new GameObject("T06 Graybox Player");
            player.transform.position = new Vector3(-10f, 0.1f, 0f);
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.5f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = player.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            var material = new PhysicsMaterial2D("T06 Zero Friction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, "Assets/Configs/Physics/ZeroFriction.physicsMaterial2D");
            collider.sharedMaterial = material;
            var state = player.AddComponent<PlayerState>();
            var feet = new GameObject("Ground Ray Origin");
            feet.transform.SetParent(player.transform, false);
            feet.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            var visual = new GameObject("Visual - replace Sprite or add Animator here");
            visual.transform.SetParent(player.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.91f, 0.9f, 0.86f);
            renderer.sortingOrder = 1;
            var motor = player.AddComponent<PlayerLocomotion>();
            Bind(motor, "inputSource", input);
            Bind(motor, "runSource", run);
            Bind(motor, "healthSource", state);
            Bind(motor, "body", body);
            Bind(motor, "groundOrigin", feet.transform);
            var serializedMotor = new SerializedObject(motor);
            serializedMotor.FindProperty("groundLayers").intValue = 1; // 现有Default层；不修改全局Layer。
            serializedMotor.ApplyModifiedPropertiesWithoutUndo();

            var bootstrap = runtime.AddComponent<GameBootstrap>();
            Bind(bootstrap, "runController", run);
            Bind(bootstrap, "inputReader", input);
            Bind(bootstrap, "playerState", state);
            Platform("Floor", sprite, new Vector2(0f, -1f), new Vector2(26f, 1f));
            Platform("Edge Platform", sprite, new Vector2(0f, 2f), new Vector2(3f, 0.5f));
            Platform("Left Platform", sprite, new Vector2(-5f, 1.1f), new Vector2(2.5f, 0.5f));
            Platform("Right Platform", sprite, new Vector2(4f, 3.2f), new Vector2(3f, 0.5f));
            Platform("Wall", sprite, new Vector2(10f, 2f), new Vector2(1f, 5f));
            GameObject trigger = Platform("Ignored Trigger", sprite, new Vector2(-8f, 1.5f), new Vector2(2f, 0.15f));
            trigger.GetComponent<BoxCollider2D>().isTrigger = true;
            trigger.GetComponent<SpriteRenderer>().color = new Color(0.35f, 0.35f, 0.35f, 0.4f);
            var cameraObject = new GameObject("T06 Camera");
            cameraObject.transform.position = new Vector3(0f, 3f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.075f, 0.075f);

            var ui = new GameObject("T06 Test UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            TMP_Text status = Text(ui.transform, font, "Status", "T06 - A/D Move, Space Jump", new Vector2(0f, 355f), new Vector2(1500f, 150f), 25f);
            Button pause = Button(ui.transform, font, "Pause / Resume", -540f);
            Button choosing = Button(ui.transform, font, "Choosing / End", -180f);
            Button death = Button(ui.transform, font, "TEST Die", 180f);
            Button checks = Button(ui.transform, font, "Run T06 Checks", 540f);
            var driver = ui.AddComponent<T06SmokeDriver>();
            Bind(driver, "motor", motor);
            Bind(driver, "body", body);
            Bind(driver, "run", run);
            Bind(driver, "input", input);
            Bind(driver, "state", state);
            Bind(driver, "status", status);
            Bind(driver, "pauseButton", pause);
            Bind(driver, "choosingButton", choosing);
            Bind(driver, "deathButton", death);
            Bind(driver, "checksButton", checks);
            var events = new GameObject("T06 EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = events.GetComponent<InputSystemUIInputModule>();
            module.actionsAsset = actions;
            module.point = Reference(actions, "Point");
            module.leftClick = Reference(actions, "Click");
            module.rightClick = Reference(actions, "RightClick");
            module.middleClick = Reference(actions, "MiddleClick");
            module.scrollWheel = Reference(actions, "ScrollWheel");
            module.move = Reference(actions, "Navigate");
            module.submit = Reference(actions, "Submit");
            module.cancel = Reference(actions, "Cancel");
            module.trackedDevicePosition = Reference(actions, "TrackedDevicePosition");
            module.trackedDeviceOrientation = Reference(actions, "TrackedDeviceOrientation");

            // 用完整测试装配Prefab保存跨对象接线，不把引用指向另一个场景。
            var rig = new GameObject("T06 Smoke Rig");
            runtime.transform.SetParent(rig.transform, true);
            player.transform.SetParent(rig.transform, true);
            ui.transform.SetParent(rig.transform, true);
            events.transform.SetParent(rig.transform, true);
            cameraObject.transform.SetParent(rig.transform, true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, "Assets/Prefabs/Tests/T06/T06SmokeRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[T06] Saved smoke scene and rig; not added to Build Settings.");
        }

        [MenuItem("Tools/GROWL AGAIN/T06/Reload Smoke Scene")]
        public static void ReloadSmokeScene()
        {
            if (Safe())
            {
                ConfigureSprite((TextureImporter)AssetImporter.GetAtPath("Assets/Art/Environment/Primitives/PlayerSquare.png"));
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[T06] Reloaded smoke scene from disk.");
            }
        }

        private static bool Safe() => !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty;

        private static void Bind(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite MakeSprite()
        {
            const string path = "Assets/Art/Environment/Primitives/PlayerSquare.png";
            var texture = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 4f;
            importer.filterMode = FilterMode.Point;
            ConfigureSprite(importer);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void ConfigureSprite(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static GameObject Platform(string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.position = position;
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = new Color(0.46f, 0.46f, 0.46f);
            obj.AddComponent<BoxCollider2D>().size = size;
            return obj;
        }

        private static TMP_Text Text(Transform parent, TMP_FontAsset font, string name, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var tmp = obj.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.text = text;
            tmp.color = new Color(0.91f, 0.9f, 0.86f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button Button(Transform parent, TMP_FontAsset font, string title, float x)
        {
            var obj = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -370f);
            rect.sizeDelta = new Vector2(320f, 70f);
            obj.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f);
            Text(obj.transform, font, "Label", title, Vector2.zero, rect.sizeDelta, 25f);
            return obj.GetComponent<Button>();
        }

        private static InputActionReference Reference(InputActionAsset actions, string name)
        {
            const string path = "Assets/Input/Tests/MovementUIReferences.asset";
            var reference = InputActionReference.Create(actions.FindAction("UI/" + name, true));
            if (!File.Exists(path))
            {
                AssetDatabase.CreateAsset(reference, path);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(reference, path);
            }
            return reference;
        }
    }
}
