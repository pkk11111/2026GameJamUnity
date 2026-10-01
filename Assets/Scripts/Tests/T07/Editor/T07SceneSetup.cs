// Soap/T07 Editor-only：复用T06装配的副本，保存T07资产；不改共享Prefab/输入/旧场景。
// 所有外观存入Prefab/Inspector；拒绝覆盖已有T07场景或未保存场景。
using System.IO;
using System.Linq;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using Regrowth.Tests.T06;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.Tests.T07.Editor
{
    public static class T07SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tests/T07/T07_Smoke.unity";
        private const string ConfigPath = "Assets/Configs/Bite/BiteConfig.asset";
        [MenuItem("Tools/GROWL AGAIN/T07/Create Smoke Scene")]
        public static void Create()
        {
            if (!Safe() || File.Exists(ScenePath)) { Debug.LogWarning("[T07] Creation refused: existing scene / Play / unsaved scene."); return; }
            foreach (string path in new[] { "Assets/Scenes/Tests/T07", "Assets/Prefabs/Tests/T07", "Assets/Prefabs/Bite", "Assets/Configs/Bite" }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 切场景后再加载资产，避免NewScene卸载未引用的Config。
            var config = AssetDatabase.LoadAssetAtPath<BiteConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<BiteConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tests/T06/T06SmokeRig.prefab"));
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rig.name = "T07 Smoke Rig";
            var old = rig.GetComponentInChildren<T06SmokeDriver>();
            var oldFields = new SerializedObject(old);
            var status = (TMP_Text)oldFields.FindProperty("status").objectReferenceValue;
            var pause = (Button)oldFields.FindProperty("pauseButton").objectReferenceValue;
            var permission = (Button)oldFields.FindProperty("choosingButton").objectReferenceValue;
            var death = (Button)oldFields.FindProperty("deathButton").objectReferenceValue;
            var checks = (Button)oldFields.FindProperty("checksButton").objectReferenceValue;
            GameObject ui = old.gameObject;
            Object.DestroyImmediate(old);
            ui.name = "T07 Test UI";
            permission.GetComponentInChildren<TMP_Text>().text = "Current Form Toggle";
            checks.GetComponentInChildren<TMP_Text>().text = "Run T07 Checks";
            var body = rig.GetComponentInChildren<Rigidbody2D>();
            var player = body.gameObject;
            player.name = "T07 Graybox Player";
            player.transform.position = Vector3.zero;
            var state = player.GetComponent<PlayerState>();
            var stateFields = new SerializedObject(state);
            stateFields.FindProperty("initialBiteDamage").intValue = 13; // 真实PlayerState Inspector数值，非Bite伤害副本。
            stateFields.ApplyModifiedPropertiesWithoutUndo();
            var input = rig.GetComponentInChildren<PlayerInputReader>();
            var run = rig.GetComponentInChildren<RunController>();
            var motor = player.GetComponent<PlayerLocomotion>();
            var origin = new GameObject("Bite Origin - Presentation may adjust");
            origin.transform.SetParent(player.transform, false);
            origin.transform.localPosition = new Vector3(0.65f, 0f, 0f);
            var attack = player.AddComponent<PlayerBiteAttack>();
            Bind(attack, "inputSource", input); Bind(attack, "combatStateSource", state);
            Bind(attack, "biteOrigin", origin.transform); Bind(attack, "config", config);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Configs/T06/GrayboxSquare.png");
            var floor = new GameObject("T07 Floor", typeof(SpriteRenderer), typeof(BoxCollider2D));
            floor.transform.SetParent(rig.transform, false);
            floor.transform.position = new Vector3(0f, -1f, 0f);
            floor.GetComponent<BoxCollider2D>().size = new Vector2(20f, 1f);
            var floorVisual = floor.GetComponent<SpriteRenderer>();
            floorVisual.sprite = sprite; floorVisual.drawMode = SpriteDrawMode.Sliced;
            floorVisual.size = new Vector2(20f, 1f); floorVisual.color = new Color(0.3f, 0.3f, 0.3f);
            var dummyObject = new GameObject("T07 Damage Dummy (two colliders)", typeof(T07DamageDummy), typeof(BoxCollider2D), typeof(SpriteRenderer));
            dummyObject.transform.SetParent(rig.transform, false); dummyObject.transform.position = new Vector3(1f, 0f, 0f);
            dummyObject.GetComponent<BoxCollider2D>().isTrigger = true;
            dummyObject.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 1f);
            dummyObject.GetComponent<SpriteRenderer>().sprite = sprite;
            dummyObject.GetComponent<SpriteRenderer>().color = new Color(0.55f, 0.49f, 0.46f);
            var extra = new GameObject("Collider B", typeof(CircleCollider2D));
            extra.transform.SetParent(dummyObject.transform, false);
            extra.GetComponent<CircleCollider2D>().isTrigger = true;
            extra.GetComponent<CircleCollider2D>().radius = 0.3f;
            var dummy = dummyObject.GetComponent<T07DamageDummy>();
            var driver = ui.AddComponent<T07SmokeDriver>();
            Bind(driver, "attack", attack); Bind(driver, "config", config); Bind(driver, "body", body);
            Bind(driver, "motor", motor); Bind(driver, "state", state); Bind(driver, "input", input);
            Bind(driver, "run", run); Bind(driver, "dummy", dummy); Bind(driver, "status", status);
            Bind(driver, "pauseButton", pause); Bind(driver, "permissionButton", permission);
            Bind(driver, "deathButton", death); Bind(driver, "checksButton", checks);
            var camera = rig.GetComponentInChildren<Camera>();
            camera.GetUniversalAdditionalCameraData();
            camera.name = "T07 Camera";
            camera.transform.position = new Vector3(0f, 2f, -10f); camera.orthographicSize = 5f;
            var module = rig.GetComponentInChildren<InputSystemUIInputModule>();
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var references = AssetDatabase.LoadAllAssetsAtPath("Assets/Configs/T03/T03UIReferences.asset").OfType<InputActionReference>().ToArray();
            string[] fields = { "m_PointAction", "m_LeftClickAction", "m_RightClickAction", "m_MiddleClickAction", "m_ScrollWheelAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction", "m_TrackedDevicePositionAction", "m_TrackedDeviceOrientationAction" };
            string[] actions = { "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "Navigate", "Submit", "Cancel", "TrackedDevicePosition", "TrackedDeviceOrientation" };
            for (int i = 0; i < fields.Length; i++)
                Bind(module, fields[i], references.First(r => r.action.id == module.actionsAsset.FindAction("UI/" + actions[i], true).id));
            // 集成用挂点Prefab保留本地锚点/配置；跨场景端口必须由总控显式绑定。
            var mount = new GameObject("Bite Mount - bind input/combat on player root");
            var mountOrigin = new GameObject("Bite Origin"); mountOrigin.transform.SetParent(mount.transform, false);
            mountOrigin.transform.localPosition = origin.transform.localPosition;
            var mountAttack = mount.AddComponent<PlayerBiteAttack>();
            Bind(mountAttack, "biteOrigin", mountOrigin.transform); Bind(mountAttack, "config", config);
            PrefabUtility.SaveAsPrefabAsset(mount, "Assets/Prefabs/Bite/BiteMount.prefab");
            Object.DestroyImmediate(mount);
            PrefabUtility.SaveAsPrefabAsset(dummyObject, "Assets/Prefabs/Tests/T07/T07DamageDummy.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, "Assets/Prefabs/Tests/T07/T07SmokeRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Debug.Log("[T07] Saved Scene/Config/Prefabs with real C01/C02/T06 and all UI references; not in Build Settings.");
        }
        [MenuItem("Tools/GROWL AGAIN/T07/Reload Smoke Scene")]
        public static void Reload() { if (Safe()) { EditorSceneManager.OpenScene(ScenePath); Debug.Log("[T07] Reloaded saved smoke scene from disk."); } }
        [MenuItem("Tools/GROWL AGAIN/T07/Repair Saved Camera")]
        public static void RepairCamera()
        {
            if (!Safe() || SceneManager.GetActiveScene().path != ScenePath) return;
            const string path = "Assets/Prefabs/Tests/T07/T07SmokeRig.prefab";
            var rig = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var camera = rig.GetComponentInChildren<Camera>();
                camera.GetUniversalAdditionalCameraData(); camera.name = "T07 Camera";
                rig.GetComponentInChildren<T07SmokeDriver>().GetComponentInChildren<TMP_Text>().text = "T07 - manual or Run T07 Checks";
                PrefabUtility.SaveAsPrefabAsset(rig, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[T07] Saved T07 camera data; reloaded scene. Shared T06 unchanged.");
        }
        private static bool Safe() => !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty;
        private static void Bind(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException("Unknown actual serialized field: " + field);
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
