#if UNITY_EDITOR
// 职责：C08主图真实输入/菜单/代价/物理迁移/朝向与正式动画回归；临时夹具不保存进场景。
// 维护controller；反射仅设边界夹具与读私有UI，不属于生产接点；交接portal-rules.handoff；规范AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Presentation;
using Regrowth.Runtime;
using Regrowth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.C08
{
    public sealed class PortalAndFacingChecks : MonoBehaviour
    {
        public static PortalAndFacingChecks Current { get; private set; }
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public bool Finished { get; private set; }
        public readonly List<string> Results = new List<string>();
        private PlayerState state;
        private WhiteboxPlayer2D motor;
        private ChoicePanel panel;
        private ChoiceCoordinator flow;
        private Keyboard keyboard;

        [MenuItem("Tools/pawgatory/C08/Run Portal And Facing Checks (Play)")]
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null)
            {
                throw new InvalidOperationException("Fresh main scene Play required.");
            }
            var state = FindFirstObjectByType<PlayerState>();
            if (state.Items.Count != 0 || state.BiteDamage != 10 || state.SwordDamage != 20 || state.FireDamage != 8)
            {
                throw new InvalidOperationException("Fresh empty state required.");
            }
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Current = new GameObject("C08 temporary checks - DO NOT SAVE").AddComponent<PortalAndFacingChecks>();
            Current.gameObject.hideFlags = HideFlags.DontSave;
            Current.state = state;
            Current.motor = state.GetComponent<WhiteboxPlayer2D>();
            Current.panel = FindFirstObjectByType<ChoicePanel>();
            Current.flow = FindFirstObjectByType<ChoiceCoordinator>();
            Current.StartCoroutine(Current.Verify());
        }

        private IEnumerator Verify()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("C08 Keyboard");
            GameObject floor = null, target = null, wall = null, future = null;
            var random = UnityEngine.Random.state;
            try
            {
                yield return Sample(.1f);
                foreach (var contact in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None))
                {
                    contact.enabled = false;
                }
                var portals = FindObjectsByType<PrototypePortal2D>(FindObjectsSortMode.None).OrderBy(p => p.name).ToArray();
                var enemies = FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None);
                var buffs = FindFirstObjectByType<EnemyEnhancementService>();
                Check(portals.Length == 4 && portals.All(p => p.IsWired), "four real map portals wired to cost config and shared choice flow");
                foreach (var portal in portals)
                {
                    var destination = Read<Transform>(portal, "destination");
                    Check(motor.CanLandAt(destination.position), portal.name + " actual destination is safe");
                    var interactionPoint = Read<Transform>(portal, "interactionPoint");
                    motor.TryTeleportTo(interactionPoint != null ? interactionPoint.position : portal.transform.position);
                    yield return Sample(.1f);
                    int hp = state.CurrentHealth;
                    Vector2 position = motor.Position;
                    yield return Sample(.06f, Key.E);
                    Check(panel.IsOpen && flow.RequestId == portal.InteractionId && Time.timeScale == 0f, portal.name + " real E opens paused three-card menu");
                    if (!panel.IsOpen)
                    {
                        continue;
                    }
                    var request = Request;
                    Check(request.Options.Count == 3 && request.Options.Select(o => o.Id).Distinct().Count() == 3
                        && request.Options.All(o => o.Id.StartsWith("COST_")), portal.name + " three unique cost cards, not rewards");
                    string ids = string.Join(",", request.Options.Select(o => o.Id));
                    yield return Sample(.05f);
                    yield return Sample(.05f, Key.Escape);
                    Check(!panel.IsOpen && state.CurrentHealth == hp && Vector2.Distance(position, motor.Position) < .25f, portal.name + " Escape cancels without payment or teleport");
                    yield return Sample(.08f);
                    yield return Sample(.06f, Key.E);
                    Check(panel.IsOpen && string.Join(",", Request.Options.Select(o => o.Id)) == ids, portal.name + " reopening keeps card IDs/order");
                    if (!panel.IsOpen)
                    {
                        continue;
                    }
                    var chosen = Request.Options.First(o => o.IsEnabled);
                    var savedButton = ButtonFor(chosen.Id);
                    savedButton.onClick.Invoke();
                    savedButton.onClick.Invoke();
                    yield return Sample(.08f);
                    Check(!panel.IsOpen && Vector2.Distance(motor.Position, destination.position) < .3f
                        && Read<PortalCostDefinition[]>(portal, "cached") == null, portal.name + " one accepted payment moves to paired exit and consumes deck");
                    Check(state.HasDamageProtection && !state.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)), portal.name + " arrival protection rejects enemy damage");
                    yield return Sample(.55f);
                }

                floor = new GameObject("C08 temporary floor");
                floor.transform.position = new Vector3(1005, 99, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(50, 1);
                target = new GameObject("C08 temporary destination");
                target.transform.position = new Vector3(1010, 100.3f, 0);
                var testPortal = portals[0];
                testPortal.transform.position = new Vector3(1000, 100.3f, 0);
                Write(testPortal, "destination", target.transform);
                var config = Read<PortalCostConfig>(testPortal, "costConfig");
                var baseEnemy = enemies[0];
                baseEnemy.TrySetRuntimeStats(150, 75, 10);
                foreach (var cost in config.Costs)
                {
                    motor.TryTeleportTo(testPortal.transform.position);
                    yield return Sample(.4f);
                    Write(state, "currentHealth", 80); Write(state, "maximumHealth", 100); Write(state, "biteDamage", 10); Write(state, "swordDamage", 20); Write(state, "fireDamage", 8);
                    if (cost.Kind == TeleportCostKind.Attack) state.TryApplyReward(new PlayerReward(attackIncrease: 10));
                    var held = PlayerState.CostItem(cost.Kind);
                    if (held.HasValue && !state.Contains(held.Value))
                    {
                        if (held.Value == LoadoutItemId.FlameTail && state.Contains(LoadoutItemId.Tail))
                        {
                            state.TryRemoveLoadoutItem(LoadoutItemId.Tail);
                        }
                        state.TryAddLoadoutItem(held.Value);
                    }
                    ForceDeck(testPortal, config, cost.Kind);
                    int enemyMax = baseEnemy.MaximumHealth, enemyHp = baseEnemy.CurrentHealth, enemyAttack = baseEnemy.AttackDamage;
                    Check(testPortal.TryInteract(state.gameObject), cost.Id + " opens via real portal transaction");
                    if (!panel.IsOpen)
                    {
                        continue;
                    }
                    ButtonFor(cost.Id).onClick.Invoke();
                    yield return Sample(.08f);
                    bool effect = held.HasValue ? !state.Contains(held.Value)
                        : cost.Kind == TeleportCostKind.CurrentHealth ? state.CurrentHealth == 64
                        : cost.Kind == TeleportCostKind.MaximumHealth ? state.MaximumHealth == 90 && state.CurrentHealth == 80
                        : cost.Kind == TeleportCostKind.Attack ? state.BiteDamage == 10 && state.SwordDamage == 20 && state.FireDamage == 8
                        : cost.Kind == TeleportCostKind.EnemyHealth ? baseEnemy.MaximumHealth == enemyMax + 50 && baseEnemy.CurrentHealth == (int)(((long)enemyHp * (enemyMax + 50) + enemyMax - 1) / enemyMax)
                        : baseEnemy.AttackDamage == enemyAttack + 5;
                    Check(effect && Vector2.Distance(motor.Position, target.transform.position) < .3f, cost.Id + " real effect and movement commit once");
                }

                // A future instance uses the same explicit registration binding, including accumulated costs.
                future = Instantiate(enemies[1].gameObject);
                yield return Sample(.05f);
                var spawned = future.GetComponent<EnemyBasic>();
                Check(spawned.MaximumHealth == spawned.Config.MaximumHealth + buffs.HealthBonus
                    && spawned.AttackDamage == spawned.Config.ContactDamage + buffs.AttackBonus, "future enemy inherits all accumulated enhancements");
                spawned.TryTakeDamage(new DamageRequest(int.MaxValue, DamageKind.Enemy));
                int deadMax = spawned.MaximumHealth;
                Check(buffs.TryCommit(TeleportCostKind.EnemyHealth, 50, () => true), "enemy service accepts another validated enhancement");
                buffs.PublishCommitted();
                Check(!spawned.IsAlive && spawned.MaximumHealth == deadMax, "dead enemies do not revive or receive enhancements");

                motor.TryTeleportTo(testPortal.transform.position);
                yield return Sample(.4f);
                foreach (var item in state.Items.ToArray())
                {
                    state.TryRemoveLoadoutItem(item);
                }
                Write(state, "currentHealth", 1); Write(state, "maximumHealth", 1); Write(state, "biteDamage", 10); Write(state, "swordDamage", 20); Write(state, "fireDamage", 8);
                Write(testPortal, "cached", new[] { config.Costs.First(c => c.Kind == TeleportCostKind.CurrentHealth), config.Costs.First(c => c.Kind == TeleportCostKind.MaximumHealth), config.Costs.First(c => c.Kind == TeleportCostKind.Attack) });
                Check(testPortal.TryInteract(state.gameObject), "all-grey boundary repairs one slot and opens");
                Check(Request.Options.Count(o => o.IsEnabled) == 1 && Request.Options.Count(o => !o.IsEnabled) == 2, "all-grey repair preserves two grey slots");
                var grey = Request.Options.First(o => !o.IsEnabled);
                var disabled = ButtonFor(grey.Id);
                Check(!disabled.interactable, "grey cost cannot be selected by navigation");
                disabled.onClick.Invoke();
                Check(panel.IsOpen && state.CurrentHealth == 1 && !motor.HasPendingRelocation, "programmatic click also rejects disabled cost");
                string repaired = string.Join(",", Request.Options.Select(o => o.Id));
                panel.CancelCurrent();
                Check(testPortal.TryInteract(state.gameObject) && repaired == string.Join(",", Request.Options.Select(o => o.Id)), "cancel does not reroll repaired deck");
                panel.CancelCurrent();
                Check(!state.CanPayTeleportCost(TeleportCostKind.CurrentHealth, 20, 6, out _)
                    && !state.CanPayTeleportCost(TeleportCostKind.MaximumHealth, 10, 6, out _)
                    && !state.CanPayTeleportCost(TeleportCostKind.Attack, 10, 6, out _), "service independently rejects lethal health and attack below 6 damage");
                Write(state, "currentHealth", 2); Write(state, "maximumHealth", 100);
                Check(state.CanPayTeleportCost(TeleportCostKind.CurrentHealth, 20, 6, out _), "2 HP can legally pay one HP");

                Write(state, "currentHealth", 80); Write(state, "biteDamage", 10); Write(state, "swordDamage", 20); Write(state, "fireDamage", 8);
                ForceDeck(testPortal, config, TeleportCostKind.CurrentHealth);
                testPortal.TryInteract(state.gameObject);
                wall = new GameObject("C08 temporary invalid landing");
                wall.transform.position = target.transform.position;
                wall.AddComponent<BoxCollider2D>().size = Vector2.one * 3;
                Physics2D.SyncTransforms();
                ButtonFor("COST_CURRENT_HP").onClick.Invoke();
                Check(panel.IsOpen && state.CurrentHealth == 80 && !motor.HasPendingRelocation, "invalid landing on confirmation keeps menu and charges nothing");
                panel.CancelCurrent();
                Check(!testPortal.TryInteract(state.gameObject), "invalid landing also rejects initial opening");
                Destroy(wall); wall = null;
                yield return Sample(.05f);
                ForceDeck(testPortal, config, TeleportCostKind.CurrentHealth);
                testPortal.TryInteract(state.gameObject);
                ButtonFor("COST_CURRENT_HP").onClick.Invoke();
                wall = new GameObject("C08 landing invalidated after click");
                wall.transform.position = target.transform.position;
                wall.AddComponent<BoxCollider2D>().size = Vector2.one * 3;
                yield return Sample(.08f);
                Check(state.CurrentHealth == 80 && Vector2.Distance(motor.Position, testPortal.transform.position) < .3f
                    && Read<PortalCostDefinition[]>(testPortal, "cached") != null, "physics revalidation refuses late blocked landing without fee or consuming deck");
                Destroy(wall); wall = null;

                var art = state.GetComponent<PlayerCharacterPresentation>();
                var bite = state.GetComponent<PlayerBiteAttack>();
                var sword = state.GetComponent<PlayerSwordAttack>();
                var fire = state.GetComponent<PlayerFireAttack>();
                var facing = state.GetComponent<PlayerFacing2D>();
                var gate = state.GetComponent<PlayerActionGate>();
                Check(state.GetComponent<PlayerBiteFlash>() == null && Read<SpriteRenderer>(sword, "slashVisual") == null
                    && state.transform.Find("Bite Cyan Flash") == null && state.transform.Find("Sword Gold Flash") == null, "cyan/gold debug components and objects removed, attack logic retained");
                yield return Sample(.7f);
                Check(bite.TryAttack() && art.CurrentAction == PlayerVisualAction.Bite && !art.MissingAction, "real bite starts assigned bite artwork");
                yield return Sample(.7f);
                state.TryAddLoadoutItem(LoadoutItemId.Arms);
                Check(sword.TryAttack() && art.CurrentAction == PlayerVisualAction.Attack && !art.MissingAction, "real sword attack starts assigned sword artwork");
                yield return Sample(.7f);
                state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
                state.TryAddLoadoutItem(LoadoutItemId.FlameTail);
                for (int i = 0; i < 12; i++)
                {
                    fire.Cancel(); Write(fire, "nextStart", 0d);
                    yield return Sample(.03f);
                    Key move = i % 2 == 0 ? Key.A : Key.D;
                    yield return Sample(.06f, move, Key.Q);
                    int expected = move == Key.A ? -1 : 1;
                    Check(fire.IsFiring && Read<int>(fire, "direction") == expected, "moving + Q direction iteration " + i);
                }
                fire.Cancel(); Write(fire, "nextStart", 0d);
                yield return Sample(.05f, Key.A);
                gate.TryBegin(bite, 1f);
                yield return Sample(.05f, Key.D);
                Check(facing.FacingSign == -1, "fixture reproduces stale facing while previous action owns lock");
                Write(gate, "until", Time.timeAsDouble - .001);
                Check(fire.TryAttack() && Read<int>(fire, "direction") == 1 && facing.FacingSign == 1, "expiry before next Update refreshes input before acquiring fire lock");
                fire.Cancel();
            }
            finally
            {
                if (panel != null)
                {
                    panel.CancelCurrent();
                }
                if (floor != null)
                {
                    Destroy(floor);
                }
                if (target != null)
                {
                    Destroy(target);
                }
                if (wall != null)
                {
                    Destroy(wall);
                }
                if (future != null)
                {
                    Destroy(future);
                }
                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                }
                keyboard = null;
                UnityEngine.Random.state = random;
                Finished = true;
                Debug.Log("[C08 RESULT] " + Passed + " passed / " + Failed + " failed\n" + string.Join("\n", Results), this);
            }
        }

        private ChoiceRequest Request => Read<ChoiceRequest>(panel, "currentRequest");
        private Button ButtonFor(string id)
        {
            int index = Request.Options.ToList().FindIndex(o => o.Id == id);
            return Read<Button>(Read<List<ChoiceCardView>>(panel, "cards")[index], "selectButton");
        }
        private static void ForceDeck(PrototypePortal2D portal, PortalCostConfig config, TeleportCostKind first)
        {
            var selected = new List<PortalCostDefinition> { config.Costs.First(c => c.Kind == first) };
            selected.AddRange(config.Costs.Where(c => c.Kind != first && (c.Kind == TeleportCostKind.EnemyAttack || c.Kind == TeleportCostKind.EnemyHealth || c.Kind == TeleportCostKind.CurrentHealth)).Take(2));
            Write(portal, "cached", selected.ToArray());
        }
        private IEnumerator Sample(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }
        private void Check(bool success, string text)
        {
            if (success)
            {
                Passed++;
            }
            else
            {
                Failed++;
            }
            Results.Add((success ? "PASS " : "FAIL ") + text);
        }
        private static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void Write(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private void OnDestroy()
        {
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
#endif
