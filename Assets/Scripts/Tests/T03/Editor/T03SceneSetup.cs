// 职责：Editor制作T03开关/门Prefab和灰盒独测接线；不在运行时动态建外观。
// 模块/维护：Soap / T03；依赖现有UnityEditor/uGUI/TMP/Runtime/T06；不编辑T06原资产。
// 保护当前Play/脏场景/已有T03场景；所有制作默认值保存为Inspector可替换资产。
// 交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
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

namespace Regrowth.Tests.T03.Editor
{
    public static class T03SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tests/T03/T03_Smoke.unity";

        [MenuItem("Tools/GROWL AGAIN/T03/Create Smoke Scene")]
        public static void Create()
        {
            if (!Safe() || File.Exists(ScenePath))
            {
                Debug.LogWarning("[T03] Refusing creation: Play, dirty scene or existing T03 scene.");
                return;
            }
            Directory.CreateDirectory("Assets/Scenes/Tests/T03");
            Directory.CreateDirectory("Assets/Prefabs/SwitchDoor");
            Directory.CreateDirectory("Assets/Prefabs/Tests/T03");
            Directory.CreateDirectory("Assets/Configs/T03");
            AssetDatabase.Refresh();
            Sprite sprite = MakeSprite();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = new GameObject("T03 Smoke Rig");
            var runtime = Child(rig.transform, "Runtime");
            var run = runtime.AddComponent<RunController>();
            var input = runtime.AddComponent<PlayerInputReader>();
            Bind(input, "inputActions", actions);
            var player = Child(rig.transform, "Graybox Player");
            player.transform.position = new Vector3(-3f, 0.02f, 0f);
            var state = player.AddComponent<PlayerState>();
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 2.5f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            player.AddComponent<BoxCollider2D>();
            var visual = SpriteView(player.transform, "Visual - replace Sprite / Animator", sprite, Vector2.one, new Color(0.91f, 0.9f, 0.86f));
            visual.GetComponent<SpriteRenderer>().sortingOrder = 2;
            var feet = Child(player.transform, "Ground Ray Origin");
            feet.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            var motor = player.AddComponent<PlayerLocomotion>();
            Bind(motor, "inputSource", input);
            Bind(motor, "runSource", run);
            Bind(motor, "healthSource", state);
            Bind(motor, "body", body);
            Bind(motor, "groundOrigin", feet.transform);
            var sm = new SerializedObject(motor);
            sm.FindProperty("groundLayers").intValue = 1;
            sm.ApplyModifiedPropertiesWithoutUndo();
            var interactor = player.AddComponent<PlayerInteractor>();
            Bind(interactor, "actor", player);
            var bootstrap = runtime.AddComponent<GameBootstrap>();
            Bind(bootstrap, "runController", run);
            Bind(bootstrap, "inputReader", input);
            Bind(bootstrap, "playerState", state);
            Bind(bootstrap, "playerInteractor", interactor);
            var world = Child(rig.transform, "Test World");
            var floor = SpriteView(world.transform, "Floor", sprite, new Vector2(26f, 1f), new Color(0.38f, 0.38f, 0.36f));
            floor.transform.position = new Vector3(0f, -1f, 0f);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(26f, 1f);
            WorldDoor doorA = MakeDoor(world.transform, sprite);
            doorA.transform.position = new Vector3(0f, 1f, 0f);
            WorldSwitch switchA = MakeSwitch(world.transform, sprite);
            switchA.transform.position = new Vector3(-4f, 0.2f, 0f);
            var doorPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(doorA.gameObject, "Assets/Prefabs/SwitchDoor/WorldDoor.prefab", InteractionMode.AutomatedAction);
            var switchPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(switchA.gameObject, "Assets/Prefabs/SwitchDoor/WorldSwitch.prefab", InteractionMode.AutomatedAction);
            var doorB = ((GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, world.transform)).GetComponent<WorldDoor>();
            doorB.name = "Door B";
            doorB.transform.position = new Vector3(8f, 1f, 0f);
            var switchB = ((GameObject)PrefabUtility.InstantiatePrefab(switchPrefab, world.transform)).GetComponent<WorldSwitch>();
            switchB.name = "Switch B";
            switchB.transform.position = new Vector3(6f, 0.2f, 0f);
            Bind(switchA, "runSource", run);
            Bind(switchA, "door", doorA);
            Bind(switchB, "runSource", run);
            Bind(switchB, "door", doorB);
            var sb = new SerializedObject(switchB);
            sb.FindProperty("interactionId").stringValue = "t03-switch-b";
            sb.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = Child(rig.transform, "Camera");
            cameraObject.transform.position = new Vector3(0f, 2f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            var ui = new GameObject("T03 Tests UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            ui.transform.SetParent(rig.transform, false);
            ui.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = ui.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            TMP_Text status = Text(ui.transform, font, "Status", "T03 / A/D / Space / E", new Vector2(0f, 335f), new Vector2(1500f, 190f), 25f);
            Button pause = Button(ui.transform, font, "Pause / Resume", -470f);
            Button choosing = Button(ui.transform, font, "Choosing / End", 0f);
            Button checks = Button(ui.transform, font, "Run T03 Checks", 470f);
            var driver = ui.AddComponent<T03SmokeDriver>();
            Bind(driver, "run", run);
            Bind(driver, "input", input);
            Bind(driver, "state", state);
            Bind(driver, "interactor", interactor);
            Bind(driver, "body", body);
            Bind(driver, "switchA", switchA);
            Bind(driver, "switchB", switchB);
            Bind(driver, "doorA", doorA);
            Bind(driver, "doorB", doorB);
            Bind(driver, "status", status);
            Bind(driver, "pauseButton", pause);
            Bind(driver, "choosingButton", choosing);
            Bind(driver, "checksButton", checks);
            var events = Child(rig.transform, "EventSystem");
            events.AddComponent<EventSystem>();
            var module = events.AddComponent<InputSystemUIInputModule>();
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
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, "Assets/Prefabs/Tests/T03/T03SmokeRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[T03] Saved scene, switch/door prefabs and rig. Not in Build Settings.");
        }

        [MenuItem("Tools/GROWL AGAIN/T03/Reload Smoke Scene")]
        public static void Reload()
        {
            if (Safe())
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[T03] Reloaded from disk.");
            }
        }
        private static WorldDoor MakeDoor(Transform parent, Sprite sprite)
        {
            var obj = Child(parent, "Door A");
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 3f);
            var closed = SpriteView(obj.transform, "Closed View", sprite, collider.size, new Color(0.05f, 0.05f, 0.045f));
            var open = SpriteView(obj.transform, "Open View - passable", sprite, new Vector2(1f, 0.15f), new Color(0.55f, 0.53f, 0.48f));
            open.transform.localPosition = new Vector3(0f, -1.4f, 0f);
            open.SetActive(false);
            var door = obj.AddComponent<WorldDoor>();
            var serialized = new SerializedObject(door);
            var colliders = serialized.FindProperty("blockingColliders");
            colliders.arraySize = 1;
            colliders.GetArrayElementAtIndex(0).objectReferenceValue = collider;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Bind(door, "closedView", closed);
            Bind(door, "openView", open);
            return door;
        }
        private static WorldSwitch MakeSwitch(Transform parent, Sprite sprite)
        {
            var obj = Child(parent, "Switch A");
            var rootCollider = obj.AddComponent<BoxCollider2D>();
            rootCollider.isTrigger = true;
            rootCollider.size = new Vector2(0.8f, 0.8f);
            var extra = Child(obj.transform, "Second Interaction Collider").AddComponent<CircleCollider2D>();
            extra.radius = 0.5f;
            extra.isTrigger = true;
            var idle = SpriteView(obj.transform, "Idle View", sprite, new Vector2(0.5f, 1f), new Color(0.68f, 0.66f, 0.6f));
            var active = SpriteView(obj.transform, "Activated View", sprite, new Vector2(0.7f, 0.4f), new Color(0.35f, 0.35f, 0.32f));
            active.SetActive(false);
            var target = obj.AddComponent<WorldSwitch>();
            var serialized = new SerializedObject(target);
            serialized.FindProperty("interactionId").stringValue = "t03-switch-a";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Bind(target, "idleView", idle);
            Bind(target, "activatedView", active);
            var bridge = obj.AddComponent<InteractionTarget>();
            Bind(bridge, "interactionSource", target);
            Bind(bridge, "interactionPoint", obj.transform);
            return target;
        }
        private static GameObject SpriteView(Transform parent, string name, Sprite sprite, Vector2 size, Color color)
        {
            var obj = Child(parent, name);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            return obj;
        }
        private static Sprite MakeSprite()
        {
            const string path = "Assets/Configs/T03/GrayboxSquare.png";
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
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static bool Safe() => !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty;
        private static GameObject Child(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj;
        }
        private static TMP_Text Text(Transform parent, TMP_FontAsset font, string name, string value, Vector2 pos, Vector2 size, float fontSize)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.text = value;
            text.color = new Color(0.91f, 0.9f, 0.86f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
        private static Button Button(Transform parent, TMP_FontAsset font, string label, float x)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(x, -380f);
            rect.sizeDelta = new Vector2(420f, 65f);
            var image = obj.GetComponent<Image>();
            image.color = new Color(0.28f, 0.28f, 0.27f);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            Text(obj.transform, font, "Label", label, Vector2.zero, new Vector2(410f, 60f), 25f);
            return button;
        }
        private static void Bind(Object obj, string field, Object value)
        {
            var serialized = new SerializedObject(obj);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static InputActionReference Reference(InputActionAsset actions, string name)
        {
            const string path = "Assets/Configs/T03/T03UIReferences.asset";
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
