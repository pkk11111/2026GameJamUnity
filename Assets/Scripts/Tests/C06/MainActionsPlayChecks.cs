#if UNITY_EDITOR
// 职责：C06显式主图联动检查；仅UNITY_EDITOR条件编译，临时对象不保存到场景。
// 维护：controller；依赖实际Runtime/WhiteBox/Combat/Chest/Hud及InputSystem。
// 使用真实输入与卡片按钮；测试会移动本次Play中的箱/敌人并消耗状态，结束后重新Play。
// 规则：根AGENTS.md；交接：docs/handoffs/controller.handoff。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using Regrowth.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.Tests.C06
{
    public sealed class MainActionsPlayChecks : MonoBehaviour
    {
        public static MainActionsPlayChecks Current { get; private set; }
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public bool Finished { get; private set; }
        private readonly List<string> results = new List<string>();
        private Keyboard keyboard;
        private PlayerState player;
        private WhiteboxPlayer2D movement;
        private Rigidbody2D body;
        private ChoicePanel panel;
        private RunController run;

        [MenuItem("Tools/pawgatory/C06/Run Main Action Checks (Play)")]
        public static void StartChecks()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().path != "Assets/WhiteBox/Scenes/Level_Whitebox.unity"
                || Current != null)
            {
                throw new InvalidOperationException("C06需要主图全新Play；检查结束后重新Play才能手动试玩。");
            }
            var state = FindFirstObjectByType<PlayerState>();
            if (state == null || state.Items.Count != 0 || state.EverOwnedItems.Count != 0)
            {
                throw new InvalidOperationException("C06需要空槽、未领取过部件的全新玩家。");
            }
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var host = new GameObject("C06 Temporary Checks - DO NOT SAVE");
            host.hideFlags = HideFlags.DontSave;
            Current = host.AddComponent<MainActionsPlayChecks>();
            Current.player = state;
            Current.StartCoroutine(Current.Verify());
        }

        private IEnumerator Verify()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("C06 Test Keyboard");
            movement = player.GetComponent<WhiteboxPlayer2D>();
            body = player.GetComponent<Rigidbody2D>();
            panel = FindFirstObjectByType<ChoicePanel>();
            run = FindFirstObjectByType<RunController>();
            var bite = player.GetComponent<PlayerBiteAttack>();
            var sword = player.GetComponent<PlayerSwordAttack>();
            var facing = player.GetComponent<PlayerFacing2D>();
            var jaw = Read<SpriteRenderer>(player.GetComponent<PlayerBiteFlash>(), "visual");
            var slash = Read<SpriteRenderer>(sword, "slashVisual");
            var randomState = UnityEngine.Random.state;
            try
            {
                yield return Sample(.08f);
                Check(Application.isFocused, "Game view focused for device input");
                Check(run.IsGameplayActive && player.HasBodyCore && player.Items.Count == 0,
                    "main prototype starts with body and all three slots empty");
                Check(HudContains("EMPTY", 3), "HUD displays three empty slots");
                Check(FindObjectsByType<PlayerState>(FindObjectsSortMode.None).Length == 1
                    && FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None).Length == 1
                    && FindObjectsByType<PlayerAttackRouter>(FindObjectsSortMode.None).Length == 1
                    && FindObjectsByType<WhiteboxPlayer2D>(FindObjectsSortMode.None).Length == 1
                    && FindObjectsByType<PlayerFacing2D>(FindObjectsSortMode.None).Length == 1,
                    "unique state/input/attack router/locomotion");
                Check(body.constraints == RigidbodyConstraints2D.FreezeRotation && bite.IsWired && sword.IsWired
                    && ReferenceEquals(bite.Facing, facing) && ReferenceEquals(sword.Facing, facing)
                    && ReferenceEquals(Read<PlayerFacing2D>(movement, "facingSource"), facing),
                    "movement unfrozen and both actions explicitly wired");
                var scale = player.transform.localScale;
                float startX = body.position.x;
                yield return Sample(.2f, Key.D);
                Check(body.position.x > startX + .7f, "saved main spawn accepts real D movement");
                yield return Sample(.06f);

                var floor = new GameObject("C06 temporary isolated floor");
                floor.hideFlags = HideFlags.DontSave;
                floor.transform.position = new Vector3(1000, -1, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(60, 1);
                Check(movement.TryTeleportTo(new Vector2(1000, 1)), "sole movement writer accepts test relocation");
                yield return Sample(.6f);
                Check(movement.IsGrounded, "same main controller lands using ground rays");
                float groundY = body.position.y;
                yield return Sample(.06f, Key.Space);
                Check(body.position.y > groundY + .2f, "empty loadout retains first jump");
                yield return Sample(.15f);
                float before = body.linearVelocity.y;
                yield return Sample(.04f, Key.Space);
                Check(body.linearVelocity.y < before, "empty loadout rejects second jump");
                yield return Sample(1.5f);
                yield return Sample(.04f, Key.LeftShift);
                Check(!movement.IsDashing, "empty loadout rejects dash");
                yield return Sample(.06f);

                var enemies = FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None);
                Check(enemies.Length == 4 && Array.TrueForAll(enemies, e => e.IsAlive && e.CanAct),
                    "four main stationary enemies initialized");
                var enemy = enemies[0];
                var contact = enemy.GetComponentInChildren<EnemyContactAttack>();
                contact.enabled = false;
                enemy.transform.position = (Vector3)body.position + Vector3.right * 1.4f;
                Physics2D.SyncTransforms();
                int hp = enemy.CurrentHealth;
                yield return Sample(.06f, Key.Enter);
                Check(enemy.CurrentHealth == hp - player.BiteDamage && jaw.enabled && !slash.enabled,
                    "empty arms: Enter uses cyan bite and damages real enemy once");
                yield return Sample(.05f);
                hp = enemy.CurrentHealth;
                yield return Sample(.04f, Key.Enter);
                Check(enemy.CurrentHealth == hp, "rapid repeat obeys existing bite cooldown");
                yield return Sample(.65f);
                yield return Sample(.04f, Key.A);
                yield return Sample(.04f);
                enemy.transform.position = (Vector3)body.position + Vector3.left * 1.4f;
                Physics2D.SyncTransforms();
                hp = enemy.CurrentHealth;
                yield return Sample(.06f, Key.Enter);
                Check(facing.FacingSign < 0 && enemy.CurrentHealth == hp - player.BiteDamage,
                    "left input mirrors real bite hit origin");
                Check(player.transform.localScale == scale, "facing preserves root scale and collision shape");
                yield return Sample(.65f);

                UnityEngine.Random.InitState(1708);
                Chest chosen = null;
                int armsIndex = -1;
                var boxes = FindObjectsByType<Chest>(FindObjectsSortMode.None);
                Array.Sort(boxes, (a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (var box in boxes)
                {
                    if (!box.TryInteract(player.gameObject))
                    {
                        continue;
                    }
                    var request = Read<ChoiceRequest>(panel, "currentRequest");
                    var config = Read<ChestRewardConfig>(box, "rewardConfig");
                    for (int i = 0; i < request.Options.Count; i++)
                    {
                        foreach (var reward in config.Rewards)
                        {
                            if (reward.Id == request.Options[i].Id && reward.Kind == ChestRewardKind.Loadout
                                && reward.Item == LoadoutItemId.Arms)
                            {
                                armsIndex = i;
                            }
                        }
                    }
                    if (armsIndex >= 0)
                    {
                        chosen = box;
                        break;
                    }
                    panel.CancelCurrent();
                }
                Check(chosen != null && panel.IsOpen, "actual main reward pool offers Arms");
                if (chosen == null)
                {
                    yield break;
                }
                Check(player.Items.Count == 0 && !player.CanUseSword, "opening Arms card does not grant it");
                hp = enemy.CurrentHealth;
                bool refused = !bite.TryAttack() && !sword.TryAttack();
                yield return Sample(.06f);
                Check(refused && enemy.CurrentHealth == hp && !slash.enabled, "Choosing blocks both attack actions");
                panel.CancelCurrent();
                yield return Sample(.08f);
                Check(!chosen.IsClaimed && player.Items.Count == 0 && player.CanBite,
                    "cancel preserves empty slots and unclaimed chest");
                chosen.TryInteract(player.gameObject);
                var cards = Read<List<ChoiceCardView>>(panel, "cards");
                Read<Button>(cards[armsIndex], "selectButton").onClick.Invoke();
                yield return Sample(.08f);
                Check(chosen.IsClaimed && player.Items.Count == 1 && player.Contains(LoadoutItemId.Arms)
                    && player.CanUseSword && !player.CanBite && !player.Contains(LoadoutItemId.Legs),
                    "real card confirmation grants Arms, switches to sword without Legs");
                Check(HudContains("EMPTY", 2) && !HudContains("EMPTY", 3), "HUD changes exactly one slot");
                hp = enemy.CurrentHealth;
                yield return Sample(.06f, Key.Enter);
                Check(enemy.CurrentHealth == Mathf.Max(0, hp - player.SwordDamage) && slash.enabled && !jaw.enabled,
                    "Arms sword hits left and shows gold flash");
                yield return Sample(.65f);
                Check(player.TryRemoveLoadoutItem(LoadoutItemId.Arms) && player.CanBite && player.Items.Count == 0,
                    "losing Arms restores bite and frees slot");

                Check(player.TryAddLoadoutItem(LoadoutItemId.Legs), "test fixture grants Legs via unique state");
                movement.TryTeleportTo(new Vector2(1000, 1));
                yield return Sample(.7f);
                yield return Sample(.06f, Key.Space);
                yield return Sample(.15f);
                before = body.linearVelocity.y;
                yield return Sample(.04f, Key.Space);
                Check(body.linearVelocity.y > before, "Legs enables real second jump");
                yield return Sample(.08f);
                before = body.linearVelocity.y;
                yield return Sample(.04f, Key.Space);
                Check(body.linearVelocity.y < before, "third airborne jump rejected");
                yield return Sample(1.5f);
                Check(player.TryAddLoadoutItem(LoadoutItemId.Tail), "test fixture grants Tail via unique state");
                yield return Sample(.04f, Key.D);
                yield return Sample(.04f);
                startX = body.position.x;
                yield return Sample(.06f, Key.LeftShift);
                Check(movement.IsDashing && body.position.x > startX + .4f, "Tail enables real Shift dash");
                Check(run.TryPause(), "pause during dash accepted");
                var paused = body.position;
                yield return Sample(.12f, Key.D, Key.Enter);
                Check(Vector2.Distance(paused, body.position) < .01f && !jaw.enabled && !slash.enabled,
                    "pause freezes movement and suppresses attacks");
                Check(run.TryResume(), "resume accepted");
                yield return Sample(.7f);
                yield return Sample(.04f, Key.A);
                yield return Sample(.04f);
                startX = body.position.x;
                yield return Sample(.06f, Key.LeftShift);
                Check(movement.IsDashing && body.position.x < startX - .4f && facing.FacingSign < 0,
                    "left dash reads same facing as both attacks");
                yield return Sample(.7f);
                Check(player.TryRemoveLoadoutItem(LoadoutItemId.Tail), "remove Tail accepted");
                yield return Sample(.04f, Key.LeftShift);
                Check(!movement.IsDashing, "removed Tail rejects next dash");
                yield return Sample(.08f);

                enemy = Array.Find(enemies, e => e != enemy && e.IsAlive);
                enemy.transform.position = (Vector3)body.position + Vector3.right * 1.1f;
                Physics2D.SyncTransforms();
                int playerHp = player.CurrentHealth;
                yield return Sample(.1f);
                Check(player.CurrentHealth == playerHp - enemy.AttackDamage, "main enemy contact reaches unique player health");
                Check(enemy.TryTakeDamage(new DamageRequest(100000, DamageKind.Enemy, player.gameObject)),
                    "enemy death accepted");
                Check(!enemy.IsAlive && Array.TrueForAll(enemy.GetComponentsInChildren<Collider2D>(), c => !c.enabled)
                    && Array.TrueForAll(enemy.GetComponentsInChildren<SpriteRenderer>(), r => !r.enabled),
                    "dead main enemy disables collision and visuals");
                player.TryTakeDamage(new DamageRequest(100000, DamageKind.Enemy));
                yield return Sample(.08f, Key.Enter, Key.D);
                Check(run.Phase == RunPhase.Dead && !jaw.enabled && !slash.enabled, "death blocks both attack presentations");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                    keyboard = null;
                }
                Finished = true;
                Debug.Log("[C06 RESULT] " + Passed + " passed / " + Failed + " failed\n" + string.Join("\n", results), this);
            }
        }

        private IEnumerator Sample(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }

        private static T Read<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static bool HudContains(string text, int expected)
        {
            int count = 0;
            foreach (var slot in FindObjectsByType<HudSlotView>(FindObjectsSortMode.None))
            {
                if (Read<TMP_Text>(slot, "title").text == text)
                {
                    count++;
                }
            }
            return count == expected;
        }

        private void Check(bool ok, string message)
        {
            if (ok)
            {
                Passed++;
            }
            else
            {
                Failed++;
            }
            results.Add((ok ? "PASS " : "FAIL ") + message);
        }

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
