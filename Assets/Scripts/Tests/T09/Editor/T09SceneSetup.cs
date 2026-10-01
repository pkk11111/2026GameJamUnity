// Soap/T09 Editor-only：一次批量保存新Config/正式敌人Prefab/最小独测；旧T07与地图只读。
// UnityEditor/Runtime/Gameplay/Tests；禁止Play或未保存场景；交接Soap.handoff；规则AGENTS.md。
using System.IO;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using Regrowth.Tests.T07;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.Tests.T09.Editor
{
    public static class T09SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tests/T09/T09_Smoke.unity";
        [MenuItem("Tools/pawgatory/T09/Create Smoke Scene %#&9")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || File.Exists(ScenePath))
            {
                Debug.LogWarning("[T09 SETUP] Refused Play / unsaved scene / existing T09 scene.");
                return;
            }
            foreach (string path in new[] { "Assets/Configs/EnemyBasic", "Assets/Prefabs/EnemyBasic", "Assets/Prefabs/Tests/T09", "Assets/Scenes/Tests/T09" })
            {
                Directory.CreateDirectory(path);
            }
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var config = ScriptableObject.CreateInstance<EnemyBasicConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Configs/EnemyBasic/EnemyBasicConfig.asset");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tests/T07/T07SmokeRig.prefab"));
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            rig.name = "T09 Minimal Smoke Rig";
            var old = rig.GetComponentInChildren<T07SmokeDriver>();
            var oldFields = new SerializedObject(old);
            var status = (TMP_Text)oldFields.FindProperty("status").objectReferenceValue;
            GameObject ui = old.gameObject;
            Object.DestroyImmediate(old);
            // 保留真实输入/EventSystem/小平台；本轮自动验收，无逐字段手工点击。
            foreach (Button button in ui.GetComponentsInChildren<Button>(true))
            {
                Object.DestroyImmediate(button.gameObject);
            }
            foreach (T07DamageDummy dummy in rig.GetComponentsInChildren<T07DamageDummy>(true))
            {
                Object.DestroyImmediate(dummy.gameObject);
            }
            var state = rig.GetComponentInChildren<PlayerState>();
            var run = rig.GetComponentInChildren<RunController>();
            var body = state.GetComponent<Rigidbody2D>();
            state.gameObject.name = "T09 Real Player - Head then BodyCore";
            var probe = rig.AddComponent<T09RegistrationProbe>();
            var enemyRoot = new GameObject("EnemyBasic - stationary contact", typeof(EnemyBasic), typeof(BoxCollider2D), typeof(SpriteRenderer));
            var enemy = enemyRoot.GetComponent<EnemyBasic>();
            Bind(enemy, "config", config);
            var sprite = enemyRoot.GetComponent<SpriteRenderer>();
            sprite.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Configs/T06/GrayboxSquare.png");
            sprite.drawMode = SpriteDrawMode.Sliced; sprite.size = Vector2.one; sprite.color = new Color(0.55f, 0.45f, 0.4f);
            enemyRoot.GetComponent<BoxCollider2D>().size = Vector2.one;
            var area = new GameObject("Contact Attack Trigger", typeof(BoxCollider2D), typeof(EnemyContactAttack));
            area.transform.SetParent(enemyRoot.transform, false);
            var trigger = area.GetComponent<BoxCollider2D>(); trigger.isTrigger = true; trigger.size = new Vector2(1.5f, 1.2f);
            var contact = area.GetComponent<EnemyContactAttack>();
            Bind(contact, "owner", enemy); Bind(contact, "attackTrigger", trigger);
            var enemyFields = new SerializedObject(enemy);
            var deathColliders = enemyFields.FindProperty("collidersToDisableOnDeath");
            deathColliders.arraySize = 2;
            deathColliders.GetArrayElementAtIndex(0).objectReferenceValue = enemyRoot.GetComponent<BoxCollider2D>();
            deathColliders.GetArrayElementAtIndex(1).objectReferenceValue = trigger;
            enemyFields.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(enemyRoot, "Assets/Prefabs/EnemyBasic/EnemyBasic.prefab");
            Object.DestroyImmediate(enemyRoot);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(rig.transform, false); instance.transform.position = new Vector3(1.05f, 0f, 0f);
            enemy = instance.GetComponent<EnemyBasic>(); contact = instance.GetComponentInChildren<EnemyContactAttack>();
            Bind(enemy, "runContextSource", run); Bind(enemy, "registrationSource", probe);
            Bind(contact, "playerDamageableSource", state);
            var driver = ui.AddComponent<T09SmokeDriver>();
            Bind(driver, "enemy", enemy); Bind(driver, "enemyPrefab", prefab); Bind(driver, "config", config);
            Bind(driver, "registration", probe); Bind(driver, "state", state); Bind(driver, "run", run);
            Bind(driver, "body", body); Bind(driver, "input", rig.GetComponentInChildren<PlayerInputReader>());
            Bind(driver, "router", state.GetComponent<PlayerAttackRouter>()); Bind(driver, "bite", state.GetComponent<PlayerBiteAttack>());
            Bind(driver, "biteConfig", AssetDatabase.LoadAssetAtPath<BiteConfig>("Assets/Configs/Bite/BiteConfig.asset"));
            Bind(driver, "status", status); status.text = "T09 - concentrated real combat checks start on Play";
            PrefabUtility.SaveAsPrefabAssetAndConnect(rig, "Assets/Prefabs/Tests/T09/T09SmokeRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            // 重开磁盘资产检查接线持久化；不修改当前用户地图。
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[T09 SETUP] Saved and reloaded Config/EnemyBasic prefab/T09 rig/scene. Shared T07 and map unchanged.");
            PrepareManual();
        }
        [MenuItem("Tools/pawgatory/T09/Prepare Manual Play %#&0")]
        public static void PrepareManual()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || !File.Exists(ScenePath))
            {
                Debug.LogWarning("[T09 MANUAL SETUP] Refused Play / unsaved scene / missing T09 scene.");
                return;
            }
            const string path = "Assets/Prefabs/Tests/T09/T09SmokeRig.prefab";
            var rig = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var driver = rig.GetComponentInChildren<T09SmokeDriver>(true);
                var fields = new SerializedObject(driver);
                fields.FindProperty("autoRun").boolValue = false;
                fields.ApplyModifiedPropertiesWithoutUndo();
                var button = (Button)fields.FindProperty("autoChecksButton").objectReferenceValue;
                if (button == null)
                {
                    // 复制既有测试按钮视觉，不修改T07原资产/Input Actions/EventSystem。
                    var sourceRig = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tests/T07/T07SmokeRig.prefab");
                    var source = new SerializedObject(sourceRig.GetComponentInChildren<T07SmokeDriver>(true));
                    var template = (Button)source.FindProperty("checksButton").objectReferenceValue;
                    var copy = Object.Instantiate(template.gameObject, driver.transform, false);
                    copy.name = "Run T09 Auto Checks - fresh isolated session";
                    button = copy.GetComponent<Button>();
                    button.onClick = new Button.ButtonClickedEvent();
                    var rect = copy.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
                    rect.pivot = new Vector2(0.5f, 0f); rect.anchoredPosition = new Vector2(0f, 20f);
                    rect.sizeDelta = new Vector2(320f, 45f);
                }
                button.GetComponentInChildren<TMP_Text>(true).text = "Run T09 Auto Checks";
                Bind(driver, "autoChecksButton", button);
                Bind(driver, "testRigRoot", rig);
                var enemy = (EnemyBasic)fields.FindProperty("enemy").objectReferenceValue;
                enemy.transform.localPosition = new Vector3(3f, 0f, 0f); // 人工起点不自动接触扣血。
                var status = (TMP_Text)fields.FindProperty("status").objectReferenceValue;
                status.text = "T09 MANUAL | A/D move, Space jump, Enter / left click Attack\nAuto Checks only by explicit button; Stop/Play resets Manual fixture.";
                // 独立Auto资产避免自引用Prefab被Instantiate映射为已试玩的场景对象。
                fields.Update();
                fields.FindProperty("autoRun").boolValue = true;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Bind(driver, "testRigPrefab", null);
                var autoPrefab = PrefabUtility.SaveAsPrefabAsset(rig, "Assets/Prefabs/Tests/T09/T09AutoChecksRig.prefab");
                fields.Update();
                fields.FindProperty("autoRun").boolValue = false;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Bind(driver, "testRigPrefab", autoPrefab);
                PrefabUtility.SaveAsPrefabAsset(rig, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(rig);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[T09 MANUAL SETUP] Saved default Manual + explicit fresh Auto Checks button; reloaded T09 scene. Gameplay unchanged.");
        }
        private static void Bind(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new System.InvalidOperationException("Unknown serialized field: " + field);
            }
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
