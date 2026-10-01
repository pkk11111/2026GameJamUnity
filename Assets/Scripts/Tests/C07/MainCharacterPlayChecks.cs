#if UNITY_EDITOR
// 职责：主图 C07 临时 Play 验收；真实卡片/输入/角色/攻击/敌人，场景不持久保存测试改动。
// 维护 controller；依赖已接线模块；规范 AGENTS.md；测试结束 Stop/Play 还原。
using System;
using System.Collections;
using System.Collections.Generic;
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
namespace Regrowth.Tests.C07
{
    public sealed class MainCharacterPlayChecks : MonoBehaviour
    {
        public static MainCharacterPlayChecks Current
        {
            get;
            private set;
        }
        public int Passed
        {
            get;
            private set;
        }
        public int Failed
        {
            get;
            private set;
        }
        public bool Finished
        {
            get;
            private set;
        }
        public readonly List<string> Results = new List<string>();
        private Keyboard keyboard;
        private PlayerState player;
        private ChoicePanel panel;
        [MenuItem("Tools/pawgatory/C07/Run Main Character Checks (Play)")]
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/WhiteBox/Scenes/Level_Whitebox.unity")
            {
                throw new InvalidOperationException("C07 requires fresh main scene Play.");
            }
            var state = FindFirstObjectByType<PlayerState>();
            if (state.Items.Count != 0 || state.EverOwnedItems.Count != 0)
            {
                throw new InvalidOperationException("Fresh empty slots required.");
            }
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Current = new GameObject("C07 temporary checks - DO NOT SAVE").AddComponent<MainCharacterPlayChecks>();
            Current.gameObject.hideFlags = HideFlags.DontSave;
            Current.player = state;
            Current.StartCoroutine(Current.Verify());
        }
        private IEnumerator Verify()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("C07 Keyboard");
            panel = FindFirstObjectByType<ChoicePanel>();
            var run = FindFirstObjectByType<RunController>();
            var art = player.GetComponent<PlayerCharacterPresentation>();
            var fire = player.GetComponent<PlayerFireAttack>();
            var bite = player.GetComponent<PlayerBiteAttack>();
            var sword = player.GetComponent<PlayerSwordAttack>();
            var facing = player.GetComponent<PlayerFacing2D>();
            var motor = player.GetComponent<WhiteboxPlayer2D>();
            var body = player.GetComponent<Rigidbody2D>();
            var collider = player.GetComponent<BoxCollider2D>();
            var originalSize = collider.size;
            var originalScale = player.transform.localScale;
            var randomState = UnityEngine.Random.state;
            GameObject floor = null, wall = null;
            try
            {
                yield return Sample(.1f);
                Check(Application.isFocused, "Game view has focus for real Input System requests");
                Check(art.IsWired && fire.IsWired && art.CurrentFamily == "Dog0" && player.Items.Count == 0, "body prototype / empty slots / real character wired");
                Check(!player.GetComponent<SpriteRenderer>().enabled && !fire.TryAttack(), "white block hidden and no free flame skill");
                foreach (var e in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None))
                {
                    e.enabled = false;
                }
                floor = new GameObject("C07 temporary floor");
                floor.transform.position = new Vector3(1000, 99, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(30, 1);
                motor.TryTeleportTo(new Vector2(1000, 100.3f));
                yield return Sample(.4f);
                var enemy = FindFirstObjectByType<EnemyBasic>();
                enemy.TrySetRuntimeStats(1000, 1000, 7);
                enemy.transform.position = (Vector3)body.position + Vector3.right * 1.5f;
                Physics2D.SyncTransforms();
                int hp = enemy.CurrentHealth;
                yield return Sample(.06f, Key.Enter);
                Check(enemy.CurrentHealth == hp - player.BiteDamage && art.CurrentAction == PlayerVisualAction.Bite, "Enter starts real bite animation and one damage submission: hp=" + enemy.CurrentHealth + "/" + (hp-player.BiteDamage) + " action=" + art.CurrentAction + " hit=" + bite.HitCenter + " enemy=" + enemy.transform.position);
                yield return Sample(.65f);
                float before = body.position.x;
                yield return Sample(.12f, Key.A);
                Check(body.position.x < before - .3f && facing.FacingSign == -1 && art.CurrentAction == PlayerVisualAction.Move, "left movement and character facing");
                yield return Sample(.04f);
                yield return Sample(.08f, Key.Space);
                Check(body.linearVelocity.y > 0 && art.CurrentAction == PlayerVisualAction.Jump, "jump motion and jump artwork");
                yield return Sample(.08f);
                motor.TryTeleportTo(new Vector2(1000, 100.3f));
                yield return Sample(.4f, Key.D);
                yield return Sample(.08f);
                UnityEngine.Random.InitState(701);
                Check(Claim(LoadoutItemId.Arms, true), "real chest offers Arms; cancel keeps empty slots; confirm grants once");
                yield return Sample(.08f);
                Check(art.CurrentFamily == "Dog0_Sword" && player.CanUseSword && !player.CanBite, "Arms card changes body and sword permission together");
                enemy.transform.position = (Vector3)body.position + Vector3.right * 1.5f;
                Physics2D.SyncTransforms();
                hp = enemy.CurrentHealth;
                yield return Sample(.06f, Key.Enter);
                Check(enemy.CurrentHealth == hp - player.SwordDamage && art.CurrentAction == PlayerVisualAction.Attack, "real sword damage and Dada attack frames: hp=" + enemy.CurrentHealth + "/" + (hp-player.SwordDamage) + " action=" + art.CurrentAction);
                yield return Sample(.65f);
                Check(Claim(LoadoutItemId.FlameTail, false), "real chest/card grants FlameTail from the shared pool");
                yield return Sample(.08f);
                Check(art.CurrentFamily == "Dog0_FlameTailSword" && player.CanUseFire && !player.Contains(LoadoutItemId.Tail), "flame body and special attack become available, no normal tail");
                Check(!fire.TryAttack() || fire.IsFiring, "explicit Fire start has real lifecycle");
                fire.Cancel();
                Write(fire, "nextStart", 0d);
                // Real enemy with two temporary overlapping hurt colliders: each tick must still hit once.
                foreach (var c in enemy.GetComponentsInChildren<Collider2D>())
                {
                    c.enabled = false;
                }
                enemy.transform.position = (Vector3)body.position + Vector3.right * 3f;
                var wide = enemy.gameObject.AddComponent<BoxCollider2D>();
                wide.isTrigger = true;
                wide.size = new Vector2(10, 2);
                var duplicate = enemy.gameObject.AddComponent<BoxCollider2D>();
                duplicate.isTrigger = true;
                duplicate.size = new Vector2(10, 2);
                Physics2D.SyncTransforms();
                hp = enemy.CurrentHealth;
                yield return Sample(.06f, Key.Q);
                Check(fire.IsFiring && fire.TickCount == 1 && enemy.CurrentHealth == hp - player.FireDamage && art.CurrentAction == PlayerVisualAction.Fire, "Q emits slow flame; t0 damages entity once despite two colliders");
                Vector2 start = fire.Position;
                Check(!bite.TryAttack() && !sword.TryAttack() && !fire.TryAttack(), "fire blocks normal attacks and repeated fire");
                yield return Sample(.24f, Key.A);
                Check(facing.FacingSign == 1 && fire.Position.x > start.x && fire.Position.x - start.x < .6f, "flame locks initial direction and travels slowly while player moves");
                int pausedHp = enemy.CurrentHealth;
                Vector2 pausedPosition = fire.Position;
                float cd = fire.CooldownRemaining;
                Check(run.TryPause(), "pause accepted");
                yield return Sample(.25f);
                Check(enemy.CurrentHealth == pausedHp && fire.Position == pausedPosition && Mathf.Abs(fire.CooldownRemaining - cd) < .001f, "pause freezes flame movement, damage and cooldown");
                run.TryResume();
                yield return Sample(1.85f);
                Check(!fire.IsFiring && fire.TickCount == 10 && enemy.CurrentHealth == hp - player.FireDamage * 10, "two seconds gives exactly ten ticks, no endpoint extra tick");
                Check(!fire.TryAttack() && fire.CooldownRemaining > 5f, "8 second cooldown measured from start");
                player.TryRemoveLoadoutItem(LoadoutItemId.FlameTail);
                player.TryAddLoadoutItem(LoadoutItemId.FlameTail);
                Check(!fire.TryAttack(), "remove/reacquire cannot reset cooldown");
                yield return Sample(.08f, Key.D);
                yield return Sample(.05f);
                Write(fire, "nextStart", 0d);
                wall = new GameObject("C07 temporary wall");
                wall.transform.position = (Vector3)body.position + Vector3.right * 1.6f;
                wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4f);
                // Small target wholly beyond wall, outside the spawn damage radius.
                wide.size = duplicate.size = new Vector2(.3f, 1f);
                enemy.transform.position = (Vector3)body.position + Vector3.right * 2.5f;
                Physics2D.SyncTransforms();
                hp = enemy.CurrentHealth;
                Check(fire.TryAttack(), "fire starts before wall");
                yield return Sample(.8f);
                Check(!fire.IsFiring && enemy.CurrentHealth == hp, "wall stops travelling flame, no damage beyond wall");
                Destroy(wall);
                wall = null;
                yield return Sample(.08f);
                Write(fire,"nextStart",0d);
                Check(fire.TryAttack(), "second lifecycle starts after test cooldown reset");
                player.TryRemoveLoadoutItem(LoadoutItemId.FlameTail);
                Check(!fire.IsFiring && !Read<SpriteRenderer>(fire,"fireVisual").enabled, "losing FlameTail immediately cancels travelling damage and visuals");
                player.TryRemoveLoadoutItem(LoadoutItemId.Arms);
                player.TryAddLoadoutItem(LoadoutItemId.FlameTail);
                yield return Sample(.4f);
                Check(bite.TryAttack(), "no-arms flame combination still permits bite");
                Check(art.MissingAction && art.CurrentFamily == "Dog0_FlameTail" && art.CurrentAction == PlayerVisualAction.Bite, "missing Bite keeps correct body and labels missing pose, never substitutes Fire");
                yield return Sample(.4f);
                player.TryAddLoadoutItem(LoadoutItemId.Arms);
                player.TryAddLoadoutItem(LoadoutItemId.Legs);
                yield return Sample(.08f);
                Write(fire,"nextStart",0d);
                Check(fire.TryAttack(), "full flame body permits Fire");
                Check(art.MissingAction && art.CurrentFamily == "Dog2_FlameTail", "missing full-body Fire retains correct body");
                player.TryTakeDamage(new DamageRequest(100000,DamageKind.Enemy));
                yield return Sample(.06f);
                Check(run.Phase == RunPhase.Dead && !fire.IsFiring && !fire.TryAttack(), "death clears active flame and prevents new attacks");
                Check(player.transform.localScale == originalScale && collider.size == originalSize, "all body changes preserve root scale and collision shape");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (floor != null)
                {
                    Destroy(floor);
                }
                if (wall != null)
                {
                    Destroy(wall);
                }
                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                    keyboard = null;
                }
                Finished = true;
                Debug.Log("[C07 RESULT] " + Passed + " passed / " + Failed + " failed\n" + string.Join("\n",Results),this);
            }
        }
        private bool Claim(LoadoutItemId item, bool cancelFirst)
        {
            foreach (var chest in FindObjectsByType<Chest>(FindObjectsSortMode.None))
            {
                if (!chest.TryInteract(player.gameObject))
                {
                    continue;
                }
                var request = Read<ChoiceRequest>(panel,"currentRequest");
                var config = Read<ChestRewardConfig>(chest,"rewardConfig");
                int index = -1;
                for (int i=0;i<request.Options.Count;i++)
                {
                    foreach (var reward in config.Rewards)
                    {
                        if (reward.Id == request.Options[i].Id && reward.Kind == ChestRewardKind.Loadout && reward.Item == item)
                        {
                            index=i;
                        }
                    }
                }
                if (index<0)
                {
                    panel.CancelCurrent();
                    continue;
                }
                if (cancelFirst)
                {
                    panel.CancelCurrent();
                    if (player.Contains(item) || chest.IsClaimed || !chest.TryInteract(player.gameObject))
                    {
                        return false;
                    }
                }
                var cards=Read<List<ChoiceCardView>>(panel,"cards");
                Read<Button>(cards[index],"selectButton").onClick.Invoke();
                return player.Contains(item) && chest.IsClaimed && !panel.IsOpen;
            }
            return false;
        }
        private IEnumerator Sample(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }
        private void Check(bool ok,string message)
        {
            if(ok)
            {
                Passed++;
            }
            else
            {
                Failed++;
            }
            Results.Add((ok?"PASS ":"FAIL ")+message);
        }
        private static T Read<T>(object o,string f) => (T)o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
        private static void Write(object o,string f,object value)
        {
            o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
        }
        private void OnDestroy()
        {
            if(keyboard!=null)
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }
}
#endif
