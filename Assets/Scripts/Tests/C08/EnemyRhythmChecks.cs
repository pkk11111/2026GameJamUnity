#if UNITY_EDITOR
// 职责：显式MainLevel Play验证警觉/巡逻/剑击反馈；仅临时DontSave夹具，不进入正式构建。
// 维护enemy-ai；依赖真实敌人、剑、玩家和运行组件；交接docs/handoffs/enemy-ai.handoff；规范AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Tests.C08
{
    public sealed class EnemyRhythmChecks : MonoBehaviour
    {
        public static EnemyRhythmChecks Current { get; private set; }
        public readonly List<string> Results = new List<string>();
        public bool Finished { get; private set; }
        public int Failed { get; private set; }
        private PlayerState player;
        private WhiteboxPlayer2D motor;
        private RunController run;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // 仅在全新主图Play中显式调用；测试改动退出Play后丢弃。
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null)
            {
                throw new InvalidOperationException("Fresh MainLevel Play required.");
            }
            var go = new GameObject("Enemy rhythm checks - temporary") { hideFlags = HideFlags.DontSave };
            Current = go.AddComponent<EnemyRhythmChecks>();
            Current.StartCoroutine(Current.Verify());
        }

        private void Check(bool value, string description)
        {
            Results.Add((value ? "PASS " : "FAIL ") + description);
            if (!value)
            {
                Failed++;
                Debug.LogError("[EnemyRhythm] " + description, this);
            }
        }

        private static void Set(object obj, string field, object value)
        {
            obj.GetType().GetField(field, Private).SetValue(obj, value);
        }

        private static T Read<T>(object obj, string field)
        {
            return (T)obj.GetType().GetField(field, Private).GetValue(obj);
        }

        private static void Place(Rigidbody2D body, Vector2 point)
        {
            body.position = point;
            body.transform.position = point;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private IEnumerator Target(Vector2 point)
        {
            Check(motor.TryTeleportTo(point), "fixture target relocation accepted");
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private IEnumerator VerifyEnemy(EnemyBasicAI ai)
        {
            var enemy = ai.GetComponent<EnemyBasic>();
            var body = ai.GetComponent<Rigidbody2D>();
            var solid = ai.GetComponent<Collider2D>();
            var config = enemy.Config;
            float mid = (ai.LeftBound + ai.RightBound) * .5f;
            float separation = solid.bounds.extents.x + player.GetComponent<Collider2D>().bounds.extents.x + .3f;
            yield return Target(new Vector2(1000f, 200f));
            ai.enabled = true;
            body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            Place(body, new Vector2(mid, 200f));
            Set(ai, "<State>k__BackingField", EnemyAIState.Patrol);
            Set(ai, "patrolPauseRemaining", 0f);
            Check(ai.IsConfigured && config.IsAIValid, enemy.name + " real saved config valid");
            Check(!ai.TryApplyWeaponHit(float.NaN) && !ai.TryApplyWeaponHit(0f), "invalid weapon directions rejected");
            yield return Target(new Vector2(mid - separation - 1f, 200f));
            yield return new WaitForSeconds(.04f);
            Check(ai.State == EnemyAIState.Alert && Mathf.Abs(body.linearVelocity.x) < .001f,
                enemy.name + " acquisition stops in Alert");
            Check(ai.FacingDirection == -1 && !ai.CanDealContactDamage, "alert faces target and suppresses contact");
            float alert = Read<float>(ai, "alertRemaining");
            Check(run.TryPause(), "pause accepted");
            yield return new WaitForSecondsRealtime(.15f);
            Check(Mathf.Approximately(alert, Read<float>(ai, "alertRemaining")), "pause freezes alert timer");
            run.TryResume();
            yield return new WaitForSeconds(config.AlertSeconds + .08f);
            Check(ai.State == EnemyAIState.Chase && body.linearVelocity.x < 0f, "alert completes into chase");
            yield return new WaitForSeconds(.12f);
            Check(ai.State == EnemyAIState.Chase, "chase does not repeat alert");

            yield return Target(new Vector2(1000f, 200f));
            Set(ai, "<State>k__BackingField", EnemyAIState.Patrol);
            Set(ai, "patrolDirection", 1);
            Set(ai, "patrolPauseRemaining", 0f);
            Place(body, new Vector2(ai.RightBound - solid.bounds.extents.x - .02f, 200f));
            yield return new WaitForSeconds(.06f);
            Check(ai.PatrolDirection == -1 && Mathf.Abs(body.linearVelocity.x) < .001f,
                "patrol edge turns and pauses");
            yield return new WaitForSeconds(config.PatrolPauseSeconds + .06f);
            Check(body.linearVelocity.x < 0f, "patrol continues after finite pause");

            // 真实接触源验证：警觉和恢复时不伤，结束后仍按既有接触规则伤害。
            Place(body, new Vector2(mid, 200f));
            yield return Target(new Vector2(mid - separation + .275f, 200f));
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            yield return new WaitForSeconds(.6f);
            var contact = enemy.GetComponentInChildren<EnemyContactAttack>(true);
            Set(ai, "<State>k__BackingField", EnemyAIState.Alert);
            Set(ai, "alertRemaining", .3f);
            int contactHP = player.CurrentHealth;
            contact.enabled = true;
            yield return new WaitForSeconds(.06f);
            Check(player.CurrentHealth == contactHP, "real contact source is suppressed during alert");
            Set(ai, "<State>k__BackingField", EnemyAIState.Chase);
            ai.TryApplyWeaponHit(1f);
            yield return new WaitForSeconds(.04f);
            Check(player.CurrentHealth == contactHP, "real contact source is suppressed during recovery");
            yield return new WaitForSeconds(.25f);
            Check(player.CurrentHealth < contactHP, "contact resumes after recovery without adding invulnerability");
            contact.enabled = false;
            player.TryHeal(player.MaximumHealth);
            body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

            foreach (int direction in new[] { 1, -1 })
            {
                Place(body, new Vector2(mid, 200f));
                yield return Target(new Vector2(mid - direction * separation, 200f));
                Set(ai, "<State>k__BackingField", EnemyAIState.Chase);
                Set(player.GetComponent<PlayerFacing2D>(), "<FacingSign>k__BackingField", direction);
                player.TryAddLoadoutItem(LoadoutItemId.Arms);
                enemy.TrySetRuntimeStats(500, 500, 5);
                // 冻结夹具等待冷却，再恢复真实AI验证武器接线。
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                yield return new WaitForSeconds(.65f);
                body.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
                var sword = player.GetComponent<PlayerSwordAttack>();
                int hp = enemy.CurrentHealth;
                float x = body.position.x;
                Check(sword.TryAttack() && enemy.CurrentHealth == hp - player.SwordDamage,
                    "actual sword damages once direction " + direction);
                Check(ai.IsRecovering && !ai.CanDealContactDamage, "accepted sword queues recovery before contact");
                float recovery = Read<float>(ai, "recoveryRemaining");
                run.TryPause();
                yield return new WaitForSecondsRealtime(.12f);
                Check(Mathf.Approximately(recovery, Read<float>(ai, "recoveryRemaining")), "pause freezes recoil recovery");
                Check(!ai.TryApplyWeaponHit(direction), "paused hit feedback rejected");
                run.TryResume();
                yield return new WaitForSeconds(config.WeaponKnockbackSeconds + .02f);
                float distance = (body.position.x - x) * direction;
                Check(distance > .04f && distance <= config.WeaponKnockbackSpeed * config.WeaponKnockbackSeconds + .08f,
                    enemy.name + " bounded recoil against chase direction " + direction + " distance " + distance);
                yield return new WaitForSeconds(config.WeaponRecoverySeconds + .04f);
                Check(!ai.IsRecovering && ai.State == EnemyAIState.Chase, "recovery ends and chase resumes");
            }

            yield return Target(new Vector2(1000f, 200f));
            Place(body, new Vector2(mid, 200f));
            var go = new GameObject("temporary recoil wall") { hideFlags = HideFlags.DontSave };
            var wall = go.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(.1f, 8f);
            go.transform.position = solid.bounds.center + Vector3.right * (solid.bounds.extents.x + .075f);
            Physics2D.SyncTransforms();
            float before = body.position.x;
            Check(ai.TryApplyWeaponHit(1f), "recoil accepted near wall");
            yield return new WaitForSeconds(config.WeaponKnockbackSeconds);
            Check(body.position.x - before < .04f && solid.bounds.max.x <= wall.bounds.min.x + .005f,
                "recoil does not cross thin wall");
            wall.enabled = false;
            Destroy(go);
            Place(body, new Vector2(ai.RightBound - solid.bounds.extents.x - .005f, 200f));
            ai.TryApplyWeaponHit(1f);
            yield return new WaitForSeconds(config.WeaponKnockbackSeconds);
            Check(solid.bounds.max.x <= ai.RightBound + .005f, "recoil stays inside territory");
            ai.TryApplyWeaponHit(-1f);
            ai.enabled = false;
            Check(!ai.IsRecovering && Mathf.Abs(body.linearVelocity.x) < .001f, "disable clears queued recoil");
            ai.enabled = true;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            ai.TryApplyWeaponHit(1f);
            enemy.TryTakeDamage(new DamageRequest(enemy.CurrentHealth, DamageKind.Enemy, player.gameObject));
            Check(!enemy.IsAlive && !ai.IsRecovering && !body.simulated && !ai.TryApplyWeaponHit(1f),
                "death clears feedback and never pushes corpse");
            ai.enabled = false;
        }

        private IEnumerator Verify()
        {
            player = FindFirstObjectByType<PlayerState>();
            motor = player.GetComponent<WhiteboxPlayer2D>();
            run = FindFirstObjectByType<RunController>();
            var intro = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "OpeningStoryIntro");
            if (intro != null)
            {
                intro.enabled = false;
            }
            run.TryResume();
            foreach (var c in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None))
            {
                c.enabled = false;
            }
            foreach (var s in FindObjectsByType<PrototypeSpike2D>(FindObjectsSortMode.None))
            {
                s.enabled = false;
            }
            var all = FindObjectsByType<EnemyBasicAI>(FindObjectsSortMode.None);
            Check(all.Length == 14 && all.All(x => x.IsConfigured), "14 main-level enemy AI configured");
            foreach (var ai in all)
            {
                ai.enabled = false;
            }
            player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            yield return VerifyEnemy(all.Single(x => x.name == "Enemy_04"));
            yield return VerifyEnemy(all.Single(x => x.name == "Elite_Exit"));
            Finished = true;
            Debug.Log("[EnemyRhythm] " + (Results.Count - Failed) + " passed / " + Failed + " failed\n" + string.Join("\n", Results));
        }
    }
}
#endif
