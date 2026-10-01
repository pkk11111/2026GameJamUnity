#if UNITY_EDITOR
// 职责：C09一次性主图敌人接线与短链路Play核验；仅显式菜单/工具调用，不自动运行。
// 维护controller；来源Soap AI；依赖真实EnemyBasic/Run/Player/WhiteBox和UnityEditor。
// 交接docs/handoffs/controller.handoff；规则AGENTS.md。测试临时对象不保存，不进入玩家构建。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Regrowth.Tests.C09
{
    public sealed class MainEnemyChecks : MonoBehaviour
    {
        public static MainEnemyChecks Current { get; private set; }
        public readonly List<string> Results = new List<string>();
        public bool Finished { get; private set; }
        public int Failed { get; private set; }
        public static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        private static bool Fits(Tilemap ground, float x, int floor, float size)
        {
            int left = Mathf.FloorToInt(x - size / 2 + .01f);
            int right = Mathf.FloorToInt(x + size / 2 - .01f);
            for (int col = left; col <= right; col++)
            {
                if (!ground.HasTile(new Vector3Int(col, floor - 1, 0))) return false;
                for (int row = floor; row < Mathf.CeilToInt(floor + size); row++)
                    if (ground.HasTile(new Vector3Int(col, row, 0))) return false;
            }
            return true;
        }
        [MenuItem("Tools/pawgatory/C09/Install Main Enemies (Edit)")]
        public static string Install()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/WhiteBox/Scenes/Level_Whitebox.unity")
                throw new InvalidOperationException("Non-Play main map required.");
            if (GameObject.Find("Enemies_C09") != null) throw new InvalidOperationException("Already installed; edit existing objects.");
            var maps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            var ground = maps.Single(t => t.name == "TM_Ground");
            var markers = maps.Single(t => t.name == "TM_Markers");
            var cells = new HashSet<Vector3Int>();
            foreach (var cell in markers.cellBounds.allPositionsWithin)
            {
                var tile = markers.GetTile(cell);
                if (tile != null && tile.name == "Tile_Enemy") cells.Add(cell);
            }
            var groups = new List<List<Vector3Int>>();
            while (cells.Count > 0)
            {
                var seed = cells.First(); var q = new Queue<Vector3Int>(); var group = new List<Vector3Int>();
                cells.Remove(seed); q.Enqueue(seed);
                while (q.Count > 0)
                {
                    var cell = q.Dequeue(); group.Add(cell);
                    foreach (var d in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down })
                        if (cells.Remove(cell + d)) q.Enqueue(cell + d);
                }
                groups.Add(group);
            }
            if (groups.Count != 12) throw new InvalidOperationException("Expected the 12 reviewed marker groups.");
            var state = FindFirstObjectByType<PlayerState>();
            var motor = state.GetComponent<WhiteboxPlayer2D>();
            var run = FindFirstObjectByType<RunController>();
            var service = FindFirstObjectByType<EnemyEnhancementService>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyBasic/EnemyBasicAI.prefab");
            var normal = AssetDatabase.LoadAssetAtPath<EnemyBasicConfig>("Assets/Configs/EnemyBasic/EnemyBasicConfig.asset");
            if (state == null || motor == null || run == null || service == null || prefab == null || normal == null)
                throw new InvalidOperationException("Missing real map dependency.");
            const string elitePath = "Assets/Configs/EnemyBasic/EnemyEliteConfig.asset";
            var elite = AssetDatabase.LoadAssetAtPath<EnemyBasicConfig>(elitePath);
            if (elite == null) { elite = Instantiate(normal); AssetDatabase.CreateAsset(elite, elitePath); }
            var settings = new SerializedObject(elite);
            settings.FindProperty("maximumHealth").intValue = 250;
            settings.FindProperty("contactDamage").intValue = 20;
            settings.FindProperty("chaseSpeedFactor").floatValue = 1;
            settings.FindProperty("aggroRadius").floatValue = 7;
            settings.FindProperty("activityLeftOffset").floatValue = 8;
            settings.FindProperty("activityRightOffset").floatValue = 8;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var plans = new List<Tuple<Vector3, float, float, float, bool>>();
            foreach (var group in groups.OrderBy(g => g.Min(c => c.y)))
            {
                int minY = group.Min(c => c.y);
                bool boss = group.Count == 36;
                bool bottom = group.Count == 25 && minY == -13;
                float size = boss ? 3f : group.Count <= 2 ? 1f : 1.5f;
                float desired = (group.Min(c => c.x) + group.Max(c => c.x) + 1) / 2f;
                foreach (float split in bottom ? new[] { -2f, 0f, 2f } : new[] { 0f })
                {
                    float best = float.MaxValue, x = 0; int floor = 0;
                    for (int down = 0; down <= 8; down++)
                        for (int step = -8; step <= 8; step++)
                        {
                            float candidate = desired + split + step * .5f;
                            float score = down * 1.5f + Mathf.Abs(step * .5f);
                            if (score < best && Fits(ground, candidate, minY - down, size))
                            { best = score; x = candidate; floor = minY - down; }
                        }
                    if (best == float.MaxValue) throw new InvalidOperationException("No safe platform at " + desired + "," + minY);
                    float left = x, right = x, reach = boss ? 8 : 6;
                    while (left - .25f >= x - reach + size / 2 && Fits(ground, left - .25f, floor, size)) left -= .25f;
                    while (right + .25f <= x + reach - size / 2 && Fits(ground, right + .25f, floor, size)) right += .25f;
                    if (right - left < .2f) throw new InvalidOperationException("No patrol room at " + x + "," + floor);
                    plans.Add(Tuple.Create(new Vector3(x, floor + size / 2 + .03f, 0), size,
                        left - size / 2, right + size / 2, boss));
                }
            }
            // Only after every planned location passes validation mutate scene actors.
            var old = FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None);
            if (old.Length != 4 || old.Any(e => !e.name.StartsWith("TrialEnemy_")))
                throw new InvalidOperationException("Unexpected existing enemies; no automatic replacement.");
            foreach (var enemy in old) Undo.DestroyObjectImmediate(enemy.gameObject);
            var parent = new GameObject("Enemies_C09");
            var routes = new GameObject("EnemyRoutes_C09");
            var spawned = new List<EnemyBasic>(); int number = 0;
            foreach (var plan in plans)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
                go.name = plan.Item5 ? "Elite_Exit" : "Enemy_" + (++number).ToString("00");
                go.transform.position = plan.Item1; go.transform.localScale = Vector3.one * plan.Item2;
                var owner = go.GetComponent<EnemyBasic>();
                Set(owner, "config", plan.Item5 ? elite : normal);
                Set(owner, "runContextSource", run); Set(owner, "registrationSource", service);
                Set(go.GetComponentInChildren<EnemyContactAttack>(), "playerDamageableSource", state);
                var ai = go.GetComponent<EnemyBasicAI>();
                Set(ai, "playerSource", state); Set(ai, "baseMoveSpeedProvider", motor);
                var route = new GameObject(go.name + "_Route"); route.transform.SetParent(routes.transform);
                var left = new GameObject("Left").transform; left.SetParent(route.transform); left.position = new Vector3(plan.Item3, plan.Item1.y, 0);
                var right = new GameObject("Right").transform; right.SetParent(route.transform); right.position = new Vector3(plan.Item4, plan.Item1.y, 0);
                Set(ai, "leftBoundary", left); Set(ai, "rightBoundary", right);
                var renderer = go.GetComponent<SpriteRenderer>(); renderer.color = new Color(1, .388111f, .361635f);
                renderer.sortingOrder = 5;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                spawned.Add(owner);
            }
            var registration = new SerializedObject(service); var list = registration.FindProperty("initialEnemies");
            list.arraySize = spawned.Count;
            for (int i = 0; i < spawned.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = spawned[i];
            registration.ApplyModifiedPropertiesWithoutUndo();
            // Tile.SetColor can be overwritten by Tile refresh after reload. A sprite-less marker persists.
            const string markerPath = "Assets/WhiteBox/Tiles/Tile_EnemySpawnMarker.asset";
            var hiddenMarker = AssetDatabase.LoadAssetAtPath<Tile>(markerPath);
            if (hiddenMarker == null)
            {
                hiddenMarker = ScriptableObject.CreateInstance<Tile>();
                hiddenMarker.name = "Tile_EnemySpawnMarker";
                hiddenMarker.color = Color.clear;
                hiddenMarker.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(hiddenMarker, markerPath);
            }
            foreach (var group in groups) foreach (var cell in group) markers.SetTile(cell, hiddenMarker);
            EditorUtility.SetDirty(markers); EditorUtility.SetDirty(service);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return string.Join("\n", spawned.Select(e => e.name + " " + e.transform.position + " size=" + e.transform.localScale.x));
        }
        private void Check(bool condition, string text)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + text);
            if (!condition) Failed++;
        }
        private static IEnumerator Steps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
            yield return null;
        }
        [MenuItem("Tools/pawgatory/C09/Check Main Enemies (Play)")]
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null) throw new InvalidOperationException("Fresh Play required.");
            Current = new GameObject("C09 temporary checks").AddComponent<MainEnemyChecks>();
            Current.gameObject.hideFlags = HideFlags.DontSave;
            Current.StartCoroutine(Current.Verify());
        }
        private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private void Claim(ChestRewardKind kind, PlayerState player)
        {
            var chest = FindObjectsByType<Chest>(FindObjectsSortMode.None).First(c => !c.IsClaimed);
            var config = (ChestRewardConfig)typeof(Chest).GetField("rewardConfig", Private).GetValue(chest);
            typeof(Chest).GetMethod("EnsureTransaction", Private).Invoke(chest, null);
            var transaction = typeof(Chest).GetField("transaction", Private).GetValue(chest);
            transaction.GetType().GetField("cached", Private).SetValue(transaction, config.Rewards.Where(r => r.Kind == ChestRewardKind.Heal || r.Kind == ChestRewardKind.MaximumHealth || r.Kind == ChestRewardKind.Attack).ToArray());
            Check(chest.TryInteract(player.gameObject), "real chest opens fixed test deck: " + kind);
            var panel = FindFirstObjectByType<Regrowth.UI.ChoicePanel>();
            var request = (ChoiceRequest)typeof(Regrowth.UI.ChoicePanel).GetField("currentRequest", Private).GetValue(panel);
            string id = config.Rewards.First(r => r.Kind == kind).Id;
            if (kind == ChestRewardKind.Attack)
                Check(request.Options.First(o => o.Id == id).Description.Contains("+10 damage"), "attack card displays fixed points");
            typeof(Regrowth.UI.ChoicePanel).GetMethod("Submit", Private).Invoke(panel, new object[] { id });
            Check(chest.IsClaimed && !panel.IsOpen, "real card confirms once and consumes chest: " + kind);
        }
        private IEnumerator Verify()
        {
            yield return Steps(2);
            var all = FindObjectsByType<EnemyBasicAI>(FindObjectsSortMode.None);
            var player = FindFirstObjectByType<PlayerState>(); var run = FindFirstObjectByType<RunController>();
            var motor = player.GetComponent<WhiteboxPlayer2D>(); var body = player.GetComponent<Rigidbody2D>();
            Check(all.Length == 14 && all.Count(e => e.name == "Elite_Exit") == 1, "13 normal + 1 elite");
            Check(all.All(e => e.IsConfigured), "all real AI dependencies initialized");
            foreach (var ai in all)
            {
                var hp = ai.GetComponent<EnemyBasic>(); bool elite = ai.name == "Elite_Exit";
                Check(hp.MaximumHealth == (elite ? 250 : 100) && hp.AttackDamage == (elite ? 20 : 10), ai.name + " health/contact values");
                Check(Mathf.Abs(ai.ChaseSpeed - (elite ? motor.BaseMoveSpeed : motor.BaseMoveSpeed * 5 / 7)) < .001f, ai.name + " real base speed");
            }
            var saved = all.ToDictionary(e => e, e => e.transform.position);
            yield return Steps(30);
            Check(all.All(e => Mathf.Abs(e.transform.position.y - saved[e].y) < .12f), "all platforms support actors; no initial fall");
            Check(all.All(e => e.GetComponent<Collider2D>().bounds.min.x >= e.LeftBound - .06f && e.GetComponent<Collider2D>().bounds.max.x <= e.RightBound + .06f), "all actors stay in authored bounds");
            Check(all.Count(e => Mathf.Abs(e.transform.position.x - saved[e].x) > .05f) >= 10, "actual physics patrol movement");
            // Use one real spacious platform; disable other contacts for this controlled verification only.
            foreach (var contact in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None)) contact.enabled = false;
            var target = all.Single(e => e.name == "Enemy_04");
            if (target.SpawnPosition.y > 10) target = all.OrderBy(e => Mathf.Abs(e.SpawnPosition.y - 2.78f)).First();
            body.constraints = RigidbodyConstraints2D.FreezeAll; body.linearVelocity = Vector2.zero;
            // Patrol may already be near the right boundary when this menu is invoked.
            // Keep the visibility probe inside the authored activity area on either side.
            float approachOffset = target.transform.position.x + 3f <= target.RightBound ? 3f : -3f;
            body.position = target.GetComponent<Rigidbody2D>().position + Vector2.right * approachOffset;
            Physics2D.SyncTransforms(); yield return Steps(3);
            Check(target.State == EnemyAIState.Chase, "in-range visible player acquires chase");
            run.TryPause(); var paused = target.transform.position;
            yield return new WaitForSecondsRealtime(.12f);
            Check(target.State == EnemyAIState.Chase && target.transform.position == paused, "pause freezes position and preserves aggro");
            run.TryResume(); yield return Steps(2);
            run.TryBeginChoosing(this); yield return null;
            Check(target.State == EnemyAIState.Chase, "choice menu preserves aggro"); run.TryEndChoosing(this);
            var health = target.GetComponent<EnemyBasic>(); health.TryTakeDamage(new DamageRequest(10, DamageKind.Enemy));
            int remaining = health.CurrentHealth;
            body.position = new Vector2(target.RightBound + 3, target.transform.position.y); Physics2D.SyncTransforms();
            yield return Steps(3);
            Check(target.State == EnemyAIState.Return && health.CurrentHealth == remaining, "out-of-bounds disengage without healing");
            // Place just short of original spawn, keeping the actual Return movement/state transition.
            target.GetComponent<Rigidbody2D>().position = target.SpawnPosition + Vector2.right * .1f;
            yield return Steps(12);
            Check(target.State == EnemyAIState.Patrol && health.CurrentHealth == remaining, "return to original spawn then patrol");
            var buffs = FindFirstObjectByType<EnemyEnhancementService>();
            bool strengthened = buffs.TryCommit(TeleportCostKind.EnemyHealth, 50, () => true); buffs.PublishCommitted();
            Check(strengthened && all.All(e => e.GetComponent<EnemyBasic>().MaximumHealth == (e.name == "Elite_Exit" ? 300 : 150)), "global HP cost includes all normal and elite");
            bool stronger = buffs.TryCommit(TeleportCostKind.EnemyAttack, 5, () => true); buffs.PublishCommitted();
            Check(stronger && all.All(e => e.GetComponent<EnemyBasic>().AttackDamage == (e.name == "Elite_Exit" ? 25 : 15)), "global attack cost includes all normal and elite");
            health.TryTakeDamage(new DamageRequest(health.CurrentHealth, DamageKind.Enemy));
            Check(!health.IsAlive && !target.GetComponent<Rigidbody2D>().simulated && !target.GetComponent<Collider2D>().enabled && !target.GetComponent<SpriteRenderer>().enabled, "death stops AI and removes collider/visual");
            Check(player.BiteDamage == 10 && player.SwordDamage == 20 && player.FireDamage == 8, "initial damage is 10/20/8 without multiplier");
            string reason;
            Check(!player.CanPayTeleportCost(TeleportCostKind.Attack, 10, 6, out reason), "baseline -10 rejected below 6");
            Claim(ChestRewardKind.Attack, player);
            Check(player.BiteDamage == 20 && player.SwordDamage == 30 && player.FireDamage == 18, "real attack card adds exactly 10 to all three attacks");
            Check(!player.TryCommitTeleportCost(TeleportCostKind.Attack, 10, 6, 0, () => false, null)
                && player.BiteDamage == 20 && player.FireDamage == 18, "failed world commit never deducts attack");
            Check(player.TryCommitTeleportCost(TeleportCostKind.Attack, 10, 6, 0, () => true, null)
                && player.BiteDamage == 10 && player.SwordDamage == 20 && player.FireDamage == 8, "legal -10 restores 10/20/8 atomically");
            bool called = false;
            Check(!player.TryCommitTeleportCost(TeleportCostKind.Attack, 10, 6, 0, () => { called = true; return true; }, null)
                && !called && player.BiteDamage == 10, "second -10 refused with no world callback or partial clamp");
            Check(!player.CanPayTeleportCost(TeleportCostKind.Attack, 3, 6, out reason), "fire minimum checked even when fire tail not held");
            Check(!player.TryApplyReward(new PlayerReward(attackIncrease: int.MaxValue)) && player.BiteDamage == 10, "attack overflow rejects whole reward");
            typeof(PlayerState).GetField("currentHealth", Private).SetValue(player, 80);
            Claim(ChestRewardKind.MaximumHealth, player);
            Check(player.MaximumHealth == 110 && player.CurrentHealth == 90, "health formula retained: 80/100 becomes 90/110");
            typeof(PlayerState).GetField("currentHealth", Private).SetValue(player, 80);
            Claim(ChestRewardKind.Heal, player);
            Check(player.CurrentHealth == 102, "health formula retained: heal ceil(110 x 20%) = 22");
            var live = all.First(e => e.GetComponent<EnemyBasic>().IsAlive && e.name != "Elite_Exit");
            body.position = (Vector2)live.transform.position + Vector2.right * (live.transform.localScale.x / 2 + .65f);
            Physics2D.SyncTransforms();
            var testedContact = live.GetComponentInChildren<EnemyContactAttack>(); testedContact.enabled = true;
            int beforeContact = player.CurrentHealth;
            yield return Steps(2); testedContact.enabled = false;
            Check(player.CurrentHealth == beforeContact - live.GetComponent<EnemyBasic>().AttackDamage, "real normal contact applies current enhanced damage once");
            Finished = true;
            Debug.Log("[C09] " + (Results.Count - Failed) + " passed / " + Failed + " failed\n" + string.Join("\n", Results));
        }
    }
}
#endif
