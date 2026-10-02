// 职责：Editor复用已保存T03灰盒/正式T01/T02，创建本任务独立场景与资产。
// Soap / T12；只编辑本任务实例/资产，不改来源Prefab；运行时不搭建UI。
// 保护Play/脏场景/已有T12；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System.IO;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using Regrowth.Tests.T03;
using Regrowth.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Regrowth.Tests.T12.Editor
{
    public static class T12SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tests/T12/T12_Smoke.unity";
        private static bool Safe => !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty;
        [MenuItem("Tools/GROWL AGAIN/T12/Create Smoke Scene")]
        public static void Create()
        {
            if (!Safe || File.Exists(ScenePath))
            {
                Debug.LogWarning("[T12] Refusing creation: Play / dirty scene / existing asset.");
                return;
            }
            Directory.CreateDirectory("Assets/Configs/Chest");
            Directory.CreateDirectory("Assets/Configs/Tests/Chest");
            Directory.CreateDirectory("Assets/Prefabs/Chest");
            Directory.CreateDirectory("Assets/Prefabs/Tests/T12");
            Directory.CreateDirectory("Assets/Scenes/Tests/T12");
            AssetDatabase.Refresh();
            var config = Config("Assets/Configs/Chest/LegacyRewardPool.asset", true, false);
            var healConfig = Config("Assets/Configs/Tests/Chest/T12_HealPool.asset", false, true);
            var replacement = Config("Assets/Configs/Tests/Chest/T12_ReplacementFixture.asset", false, false, false, true);
            var wide = Config("Assets/Configs/Tests/Chest/T12_WidePool.asset", false, false, true);
            var limited = Config("Assets/Configs/Tests/Chest/T12_LimitedPool.asset", false, false);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene会卸载尚无场景引用的资产；切场景后重新取得持久资产，不能保存已卸载实例。
            config = AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Chest/LegacyRewardPool.asset");
            healConfig = AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Tests/Chest/T12_HealPool.asset");
            replacement = AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Tests/Chest/T12_ReplacementFixture.asset");
            wide = AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Tests/Chest/T12_WidePool.asset");
            limited = AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Tests/Chest/T12_LimitedPool.asset");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tests/T03/T03SmokeRig.prefab"));
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rig.name = "T12 Smoke Rig";
            BindUiActions(rig);
            // 只移除本任务实例的T03世界物件/驱动，不触碰原Prefab。
            foreach (var value in rig.GetComponentsInChildren<WorldSwitch>(true))
            {
                Object.DestroyImmediate(value.gameObject);
            }
            foreach (var value in rig.GetComponentsInChildren<WorldDoor>(true))
            {
                Object.DestroyImmediate(value.gameObject);
            }
            var oldDriver = rig.GetComponentInChildren<T03SmokeDriver>(true);
            var oldSerialized = new SerializedObject(oldDriver);
            var status = (TMP_Text)oldSerialized.FindProperty("status").objectReferenceValue;
            var checks = (Button)oldSerialized.FindProperty("checksButton").objectReferenceValue;
            var damage = (Button)oldSerialized.FindProperty("pauseButton").objectReferenceValue;
            var unused = (Button)oldSerialized.FindProperty("choosingButton").objectReferenceValue;
            var testsUI = oldDriver.gameObject;
            unused.gameObject.SetActive(false);
            Object.DestroyImmediate(oldDriver);
            checks.GetComponentInChildren<TMP_Text>().text = "Run T12 Checks";
            damage.GetComponentInChildren<TMP_Text>().text = "TEST Damage 30";
            status.text = "T12 / E opens nearby Chest";
            var statusRect = status.rectTransform;
            statusRect.anchoredPosition = new Vector2(0f, -170f);
            statusRect.sizeDelta = new Vector2(1500f, 170f);
            var state = rig.GetComponentInChildren<PlayerState>(true);
            var run = rig.GetComponentInChildren<RunController>(true);
            var input = rig.GetComponentInChildren<PlayerInputReader>(true);
            var interactor = rig.GetComponentInChildren<PlayerInteractor>(true);
            var bootstrap = rig.GetComponentInChildren<GameBootstrap>(true);
            var flow = run.gameObject.AddComponent<ChoiceCoordinator>();
            var menu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Choice/ChoiceMenu.prefab"), rig.transform);
            var panel = menu.GetComponentInChildren<ChoicePanel>(true);
            foreach (var canvas in menu.GetComponentsInChildren<Canvas>(true))
            {
                canvas.sortingOrder = 20;
            }
            Bind(flow, "presenter", panel);
            Bind(bootstrap, "choiceCoordinator", flow);
            Bind(bootstrap, "choicePresenter", panel);
            var hudObject = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hud/PlayerHud.prefab"), rig.transform);
            var hud = hudObject.GetComponent<PlayerHud>();
            Bind(hud, "stateSource", state);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environment/Primitives/SwitchSquare.png");
            var chestObject = new GameObject("Chest");
            chestObject.transform.SetParent(rig.transform, false);
            chestObject.transform.position = state.transform.position + Vector3.right;
            var collider = chestObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.2f, 1f);
            var closed = View(chestObject.transform, sprite, "Closed - replace Sprite Animator", new Vector2(1.2f, 0.8f), new Color(0.2f, 0.18f, 0.17f));
            var claimed = View(chestObject.transform, sprite, "Claimed - replace Sprite Animator", new Vector2(1.2f, 0.3f), new Color(0.5f, 0.48f, 0.43f));
            claimed.SetActive(false);
            var chest = chestObject.AddComponent<Chest>();
            SetString(chest, "interactionId", "t12-chest-main");
            Bind(chest, "rewardConfig", config);
            Bind(chest, "closedView", closed);
            Bind(chest, "claimedView", claimed);
            var bridge = chestObject.AddComponent<InteractionTarget>();
            Bind(bridge, "interactionSource", chest);
            Bind(bridge, "interactionPoint", chestObject.transform);
            var chestPrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(chestObject, "Assets/Prefabs/Chest/Chest.prefab", InteractionMode.AutomatedAction);
            var chests = new Chest[6];
            chests[0] = chest;
            for (int i = 1; i < chests.Length; i++)
            {
                chests[i] = ((GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, rig.transform)).GetComponent<Chest>();
                chests[i].name = "TEST Chest Fixture " + i;
                chests[i].transform.position = chest.transform.position;
                SetString(chests[i], "interactionId", "t12-chest-fixture-" + i);
                Bind(chests[i], "rewardConfig", i == 1 || i == 2 ? healConfig : i == 3 ? wide : i == 5 ? limited : config);
                chests[i].gameObject.SetActive(false);
            }
            foreach (var value in chests)
            {
                Bind(value, "stateSource", state);
                Bind(value, "runSource", run);
                Bind(value, "choiceFlowSource", flow);
            }
            var driver = testsUI.AddComponent<T12SmokeDriver>();
            Bind(driver, "run", run);
            Bind(driver, "input", input);
            Bind(driver, "state", state);
            Bind(driver, "interactor", interactor);
            Bind(driver, "flow", flow);
            Bind(driver, "panel", panel);
            Bind(driver, "hud", hud);
            Bind(driver, "replacementConfig", replacement);
            Bind(driver, "status", status);
            Bind(driver, "checksButton", checks);
            Bind(driver, "damageButton", damage);
            var serialized = new SerializedObject(driver);
            var array = serialized.FindProperty("chests");
            array.arraySize = chests.Length;
            for (int i = 0; i < chests.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = chests[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, "Assets/Prefabs/Tests/T12/T12SmokeRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[T12] Saved Chest Config/Prefab/Smoke. Not in Build Settings.");
        }
        [MenuItem("Tools/GROWL AGAIN/T12/Reload Smoke Scene")]
        public static void Reload()
        {
            if (Safe)
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("[T12] Reloaded from disk.");
            }
        }
        [MenuItem("Tools/GROWL AGAIN/T12/Repair Saved Config Bindings")]
        public static void RepairConfigBindings()
        {
            if (!Safe || SceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogWarning("[T12] Config repair requires clean non-Play T12 scene.");
                return;
            }
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                var driver = root.GetComponentInChildren<T12SmokeDriver>(true);
                if (driver == null)
                {
                    continue;
                }
                var serialized = new SerializedObject(driver);
                var chestArray = serialized.FindProperty("chests");
                for (int i = 0; i < chestArray.arraySize; i++)
                {
                    var chest = (Chest)chestArray.GetArrayElementAtIndex(i).objectReferenceValue;
                    string file = i == 1 || i == 2 ? "T12_HealPool" : i == 3 ? "T12_WidePool" : i == 5 ? "T12_LimitedPool" : "LegacyRewardPool";
                    var config = AssetDatabase.LoadAssetAtPath<ChestRewardConfig>((file == "LegacyRewardPool" ? "Assets/Configs/Chest/" : "Assets/Configs/Tests/Chest/") + file + ".asset");
                    if (config == null || !config.IsValid)
                    {
                        Debug.LogWarning("[T12] Invalid persisted config: " + file, chest);
                        return;
                    }
                    Bind(chest, "rewardConfig", config);
                }
                Bind(driver, "replacementConfig", AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Tests/Chest/T12_ReplacementFixture.asset"));
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            }
            var contents = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Chest/Chest.prefab");
            try
            {
                Bind(contents.GetComponent<Chest>(), "rewardConfig", AssetDatabase.LoadAssetAtPath<ChestRewardConfig>("Assets/Configs/Chest/LegacyRewardPool.asset"));
                PrefabUtility.SaveAsPrefabAsset(contents, "Assets/Prefabs/Chest/Chest.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[T12] Rebound persisted configs and saved owned scene/prefabs.");
        }
        [MenuItem("Tools/GROWL AGAIN/T12/Repair UI Action Bindings")]
        public static void RepairUiActions()
        {
            if (!Safe || SceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogWarning("[T12] UI repair requires clean non-Play T12 scene.");
                return;
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.GetComponentInChildren<T12SmokeDriver>(true) == null)
                    continue;
                BindUiActions(root);
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[T12] Saved ten UI action references on owned Smoke Rig.");
        }

        private static void BindUiActions(GameObject rig)
        {
            // 复用已有持久引用，仍指唯一InputSystem_Actions；不创建第二份输入资产。
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Input/Shared.inputactions");
            var references = AssetDatabase.LoadAllAssetsAtPath("Assets/Input/UIActionReferences.asset");
            var module = rig.GetComponentInChildren<InputSystemUIInputModule>(true);
            var serialized = new SerializedObject(module);
            serialized.FindProperty("m_ActionsAsset").objectReferenceValue = asset;
            string[] fields = { "m_PointAction", "m_LeftClickAction", "m_RightClickAction", "m_MiddleClickAction",
                "m_ScrollWheelAction", "m_MoveAction", "m_SubmitAction", "m_CancelAction",
                "m_TrackedDevicePositionAction", "m_TrackedDeviceOrientationAction" };
            string[] names = { "Point", "Click", "RightClick", "MiddleClick", "ScrollWheel", "Navigate",
                "Submit", "Cancel", "TrackedDevicePosition", "TrackedDeviceOrientation" };
            for (int i = 0; i < fields.Length; i++)
            {
                var action = asset.FindAction("UI/" + names[i], true);
                InputActionReference found = null;
                foreach (var reference in references)
                    if (reference is InputActionReference candidate && candidate.action != null
                        && candidate.action.id == action.id && candidate.asset == asset)
                        found = candidate;
                if (found == null)
                    throw new System.InvalidOperationException("Missing persisted UI reference: " + names[i]);
                serialized.FindProperty(fields[i]).objectReferenceValue = found;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ChestRewardConfig Config(string path, bool includeDash, bool healOnly, bool wide = false, bool replacement = false)
        {
            var config = ScriptableObject.CreateInstance<ChestRewardConfig>();
            var serialized = new SerializedObject(config);
            var rewards = serialized.FindProperty("rewards");
            string[] ids = replacement ? new[] { "sword", "heal", "heal-b" }
                : wide ? new[] { "sword", "upright", "heal", "dash", "double" }
                : healOnly ? new[] { "heal", "heal-b", "heal-c" }
                : includeDash ? new[] { "sword", "upright", "heal", "dash" } : new[] { "sword", "upright", "heal" };
            rewards.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++)
            {
                var reward = rewards.GetArrayElementAtIndex(i);
                reward.FindPropertyRelative("id").stringValue = ids[i];
                bool isHeal = ids[i].StartsWith("heal", System.StringComparison.Ordinal);
                reward.FindPropertyRelative("title").stringValue = isHeal ? "Heal " + (healOnly ? 7 + i * 4 : 20)
                    : ids[i] == "sword" ? "Sword" : ids[i] == "upright" ? "Upright form" : ids[i] == "double" ? "Double jump" : "Dash";
                reward.FindPropertyRelative("description").stringValue = isHeal ? "Restore HP. May be selected at full health."
                    : "Keep this capability. Takes one loadout slot.";
                reward.FindPropertyRelative("kind").intValue = (int)(isHeal ? ChestRewardKind.Heal : ChestRewardKind.Loadout);
                reward.FindPropertyRelative("item").intValue = ids[i] == "sword" ? (int)LoadoutItemId.Sword
                    : ids[i] == "upright" ? (int)LoadoutItemId.UprightForm : ids[i] == "double" ? (int)LoadoutItemId.DoubleJump : (int)LoadoutItemId.Dash;
                reward.FindPropertyRelative("healAmount").intValue = healOnly ? 7 + i * 4 : 20;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(config, path);
            return config;
        }
        private static GameObject View(Transform parent, Sprite sprite, string name, Vector2 size, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            return obj;
        }
        private static void Bind(Object obj, string field, Object value)
        {
            var serialized = new SerializedObject(obj);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetString(Object obj, string field, string value)
        {
            var serialized = new SerializedObject(obj);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
