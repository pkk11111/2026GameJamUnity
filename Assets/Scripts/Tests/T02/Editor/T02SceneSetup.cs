// 职责：仅在Editor制作T02可替换HUD资产/独测接线，运行代码不动态搭建外观。
// 模块/维护：Soap / T02；依赖UnityEditor及现有uGUI/TMP/Runtime；保护脏场景/Play/已有资产。
// 交接docs/handoffs/Soap.handoff；规范根AGENTS.md。样式暂定值最终序列化到Prefab，允许美术直接替换。
using System.IO;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.UI;
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

namespace Regrowth.Tests.T02.Editor
{
    public static class T02SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tests/T02/T02_Smoke.unity";

        [MenuItem("Tools/GROWL AGAIN/T02/Create Smoke Scene")]
        public static void Create()
        {
            if (!Safe() || File.Exists(ScenePath))
            {
                Debug.LogWarning("[T02] Refusing creation: Play, dirty scene or existing T02 scene.");
                return;
            }
            Directory.CreateDirectory("Assets/Scenes/Tests/T02");
            Directory.CreateDirectory("Assets/Prefabs/Hud");
            Directory.CreateDirectory("Assets/Prefabs/Tests/T02");
            Directory.CreateDirectory("Assets/Configs/T02");
            AssetDatabase.Refresh();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = new GameObject("T02 Smoke Rig");
            var runtime = Child(rig.transform, "Runtime");
            var run = runtime.AddComponent<RunController>();
            var input = runtime.AddComponent<PlayerInputReader>();
            Bind(input, "inputActions", actions);
            var player = Child(rig.transform, "PlayerState - unique real state");
            var state = player.AddComponent<PlayerState>();
            var bootstrap = runtime.AddComponent<GameBootstrap>();
            Bind(bootstrap, "runController", run);
            Bind(bootstrap, "inputReader", input);
            Bind(bootstrap, "playerState", state);
            var cameraObject = Child(rig.transform, "Camera");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.GetUniversalAdditionalCameraData();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.13f, 0.13f);

            GameObject canvas = Canvas("Player HUD");
            Image paper = Image(canvas.transform, "Background - replace Sprite", new Vector2(0f, 260f), new Vector2(1500f, 300f), new Color(0.22f, 0.22f, 0.21f));
            TMP_Text hp = Text(paper.transform, font, "Health", "HP", new Vector2(-450f, 95f), new Vector2(500f, 65f), 38f);
            TMP_Text form = Text(paper.transform, font, "Form", "FORM", new Vector2(430f, 95f), new Vector2(500f, 65f), 30f);
            Image bar = Image(paper.transform, "Health Fill", new Vector2(-450f, 40f), new Vector2(470f, 18f), new Color(0.72f, 0.22f, 0.17f));
            // Unity UI默认白色Sprite足够占位；图标Sprite可空，后续配置映射，不依赖占位素材路径。
            HudSlotView[] slots = new HudSlotView[4];
            for (int i = 0; i < slots.Length; i++)
            {
                Image frame = Image(paper.transform, "Slot " + (i + 1), new Vector2(-540f + i * 360f, -55f), new Vector2(330f, 120f), new Color(0.1f, 0.1f, 0.095f));
                TMP_Text title = Text(frame.transform, font, "Title", "EMPTY", new Vector2(25f, 22f), new Vector2(270f, 45f), 25f);
                TMP_Text availability = Text(frame.transform, font, "Availability", "", new Vector2(0f, -28f), new Vector2(320f, 40f), 18f);
                Image icon = Image(frame.transform, "Icon - replace via Item Visuals", new Vector2(-130f, 22f), new Vector2(30f, 30f), Color.white);
                var empty = Text(frame.transform, font, "Empty Mark", "-", new Vector2(-130f, 22f), new Vector2(30f, 30f), 25f).gameObject;
                var unavailable = Image(frame.transform, "Unavailable Marker", new Vector2(0f, -53f), new Vector2(320f, 5f), new Color(0.55f, 0.52f, 0.47f)).gameObject;
                slots[i] = frame.gameObject.AddComponent<HudSlotView>();
                Bind(slots[i], "title", title);
                Bind(slots[i], "availability", availability);
                Bind(slots[i], "icon", icon);
                Bind(slots[i], "emptyView", empty);
                Bind(slots[i], "unavailableView", unavailable);
                unavailable.SetActive(false);
            }
            var hud = canvas.AddComponent<PlayerHud>();
            Bind(hud, "healthText", hp);
            Bind(hud, "formText", form);
            // Fill可选，此占位使用Filled Image需美术提供Sprite；目前只显示文字，不绑不工作的假血条。
            bar.gameObject.SetActive(false);
            Array(hud, "slots", slots);
            var serializedHud = new SerializedObject(hud);
            var visuals = serializedHud.FindProperty("itemVisuals");
            visuals.arraySize = 4;
            LoadoutItemId[] ids = { LoadoutItemId.Dash, LoadoutItemId.DoubleJump, LoadoutItemId.UprightForm, LoadoutItemId.Sword };
            string[] labels = { "DASH", "DOUBLE JUMP", "UPRIGHT", "SWORD" };
            for (int i = 0; i < ids.Length; i++)
            {
                visuals.GetArrayElementAtIndex(i).FindPropertyRelative("item").intValue = (int)ids[i];
                visuals.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue = labels[i];
            }
            serializedHud.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAssetAndConnect(canvas, "Assets/Prefabs/Hud/PlayerHud.prefab", InteractionMode.AutomatedAction);
            canvas.transform.SetParent(rig.transform, true);
            Bind(hud, "stateSource", state); // 场景/测试Rig绑定，HUD独立Prefab由总控注入唯一状态。

            var ui = Canvas("T02 Tests UI");
            ui.transform.SetParent(rig.transform, true);
            TMP_Text status = Text(ui.transform, font, "Status", "T02 / Real PlayerState", new Vector2(0f, -30f), new Vector2(1450f, 200f), 28f);
            Button damage = Button(ui.transform, font, "TEST Damage 10", -470f);
            Button heal = Button(ui.transform, font, "TEST Heal 5", 0f);
            Button scenarios = Button(ui.transform, font, "TEST Scenario", 470f);
            var driver = ui.AddComponent<T02SmokeDriver>();
            Bind(driver, "state", state);
            Bind(driver, "hud", hud);
            Bind(driver, "healthText", hp);
            Bind(driver, "formText", form);
            Array(driver, "slots", slots);
            Bind(driver, "status", status);
            Bind(driver, "damageButton", damage);
            Bind(driver, "healButton", heal);
            Bind(driver, "scenarioButton", scenarios);
            Bind(driver, "probeA", Child(rig.transform, "TEST MaxHP Probe A").AddComponent<HudProbeState>());
            Bind(driver, "probeB", Child(rig.transform, "TEST MaxHP Probe B").AddComponent<HudProbeState>());
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
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, "Assets/Prefabs/Tests/T02/T02SmokeRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[T02] Saved scene, HUD and rig. Not added to Build Settings.");
        }

        [MenuItem("Tools/GROWL AGAIN/T02/Reload Smoke Scene")]
        public static void Reload()
        {
            if (Safe())
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[T02] Reloaded from disk.");
            }
        }
        private static bool Safe() => !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty;
        private static GameObject Child(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            return obj;
        }
        private static GameObject Canvas(string name)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            obj.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            return obj;
        }
        private static Image Image(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            var image = Image(parent, label, new Vector2(x, -280f), new Vector2(420f, 70f), new Color(0.28f, 0.28f, 0.27f));
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text(image.transform, font, "Label", label, Vector2.zero, new Vector2(400f, 65f), 28f);
            return button;
        }
        private static void Bind(Object obj, string field, Object value)
        {
            var serialized = new SerializedObject(obj);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Array(Object obj, string field, Object[] values)
        {
            var serialized = new SerializedObject(obj);
            var array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static InputActionReference Reference(InputActionAsset actions, string name)
        {
            const string path = "Assets/Configs/T02/T02UIReferences.asset";
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
