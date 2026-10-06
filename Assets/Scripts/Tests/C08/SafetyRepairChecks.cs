#if UNITY_EDITOR
// 职责：显式主图回归：三怪巡逻/追击/返程、统一接触结算/保护、近战遮挡。
// 维护：structure-audit；依赖真实Runtime/Gameplay组件；仅临时Play对象，不保存、不进正式包。
// 会改变本次Play生命/位置；结束后退出Play恢复。交接docs/handoffs/structure-audit.handoff；规范AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using UnityEngine;

namespace Regrowth.Tests.C08
{
    [DefaultExecutionOrder(150)]
    public sealed class SafetyRepairChecks : MonoBehaviour
    {
        public static SafetyRepairChecks Current { get; private set; }
        public readonly List<string> Results = new List<string>();
        public bool Finished { get; private set; }
        public string Stage { get; private set; }
        public int Failed { get; private set; }
        private PlayerState player;
        private RunController run;
        private WhiteboxPlayer2D motor;
        private Rigidbody2D body;
        private Action nextFixed;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null)
            {
                throw new InvalidOperationException("Fresh MainLevel Play required.");
            }
            var go = new GameObject("Safety repair checks - temporary");
            go.hideFlags = HideFlags.DontSave;
            Current = go.AddComponent<SafetyRepairChecks>();
            Current.StartCoroutine(Current.Verify());
        }

        private void FixedUpdate()
        {
            var action = nextFixed;
            nextFixed = null;
            action?.Invoke();
        }

        private IEnumerator Step(Action action = null)
        {
            nextFixed = action;
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        private void Check(bool success, string description)
        {
            Results.Add((success ? "PASS " : "FAIL ") + description);
            if (!success)
            {
                Failed++;
                Debug.LogError("[SafetyRepair] " + description, this);
            }
        }

        private static T Read<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field, Private).GetValue(target);
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, Private).SetValue(target, value);
        }

        private IEnumerator ReadyHealth()
        {
            yield return new WaitForSeconds(0.56f);
            player.TryHeal(player.MaximumHealth);
            yield return Step();
        }


        private void PlaceBody(Rigidbody2D target, Vector2 position)
        {
            target.position = position;
            target.transform.position = position;
            target.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private IEnumerator VerifyContactEnvelope(EnemyBasic enemy)
        {
            Stage = "Contact envelope / walls " + enemy.name;
            var contact = enemy.GetComponentInChildren<EnemyContactAttack>(true);
            var enemyBody = enemy.GetComponent<Rigidbody2D>();
            var enemySolid = enemy.GetComponent<Collider2D>();
            var playerSolid = player.GetComponent<Collider2D>();
            var block = new GameObject("temporary contact obstruction");
            block.hideFlags = HideFlags.DontSave;
            var wall = block.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(.005f, 8f);
            wall.enabled = false;
            var child = new GameObject("temporary player interaction trigger");
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(player.transform, false);
            var extra = child.AddComponent<BoxCollider2D>();
            extra.isTrigger = true;
            extra.size = new Vector2(8f, 8f);
            extra.enabled = false;
            var trigger = Read<Collider2D>(contact, "attackTrigger");
            Check(Mathf.Abs(trigger.bounds.size.x - enemySolid.bounds.size.x - .1f) < .002f
                && Mathf.Abs(trigger.bounds.size.y - enemySolid.bounds.size.y - .1f) < .002f,
                enemy.name + " saved damage envelope extends only .05 per side");
            foreach (int sign in new[] { 1, -1 })
            {
                contact.enabled = false;
                yield return ReadyHealth();
                float offset = playerSolid.bounds.extents.x + enemySolid.bounds.extents.x;
                PlaceBody(enemyBody, body.position + Vector2.right * sign * (offset + .4f));
                int hp = player.CurrentHealth;
                contact.enabled = true;
                yield return new WaitForSeconds(.12f);
                contact.enabled = false;
                Check(player.CurrentHealth == hp, enemy.name + " gap .4 does not damage direction " + sign);

                // 大附属Trigger碰到伤害区也不能代替根实体受伤。
                extra.enabled = true;
                Physics2D.SyncTransforms();
                contact.enabled = true;
                yield return new WaitForSeconds(.1f);
                contact.enabled = false;
                extra.enabled = false;
                Check(player.CurrentHealth == hp, enemy.name + " child interaction trigger cannot receive damage " + sign);

                PlaceBody(enemyBody, body.position + Vector2.right * sign * (offset + .025f));
                block.transform.position = playerSolid.bounds.center
                    + Vector3.right * sign * (playerSolid.bounds.extents.x + .0125f);
                wall.enabled = true;
                wall.isTrigger = false;
                Physics2D.SyncTransforms();
                contact.enabled = true;
                yield return new WaitForSeconds(.12f);
                contact.enabled = false;
                Check(player.CurrentHealth == hp, enemy.name + " thin solid blocks contact damage " + sign);

                wall.enabled = false;
                Physics2D.SyncTransforms();
                contact.enabled = true;
                yield return new WaitForSeconds(.12f);
                contact.enabled = false;
                Check(player.CurrentHealth == hp - enemy.AttackDamage,
                    enemy.name + " open blocker allows exactly one touch hit " + sign);

                yield return ReadyHealth();
                hp = player.CurrentHealth;
                wall.enabled = true;
                wall.isTrigger = true;
                Physics2D.SyncTransforms();
                contact.enabled = true;
                yield return new WaitForSeconds(.12f);
                contact.enabled = false;
                wall.enabled = false;
                Check(player.CurrentHealth == hp - enemy.AttackDamage,
                    enemy.name + " trigger decoration does not block damage " + sign);
            }
            Destroy(block);
            Destroy(child);
            contact.enabled = false;
        }

        private IEnumerator VerifyBlockedMotion(EnemyBasicAI enemy)
        {
            Stage = "Blocked chase / return";
            var rb = enemy.GetComponent<Rigidbody2D>();
            var solid = enemy.GetComponent<Collider2D>();
            var saved = rb.position;
            var constraints = rb.constraints;
            rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            float mid = (enemy.LeftBound + enemy.RightBound) * .5f;
            PlaceBody(rb, new Vector2(mid, 200f));
            motor.TryTeleportTo(new Vector2(Mathf.Min(mid + 3f, enemy.RightBound - .1f), 200f));
            yield return Step();
            yield return new WaitForSeconds(enemy.GetComponent<EnemyBasic>().Config.AlertSeconds + .08f);
            Check(enemy.State == EnemyAIState.Chase, "visible target acquired before closing obstacle");
            var block = new GameObject("temporary AI closed door");
            block.hideFlags = HideFlags.DontSave;
            var wall = block.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(.2f, 8f);
            block.transform.position = solid.bounds.center + Vector3.right * (solid.bounds.extents.x + .125f);
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            float stuck = rb.position.x;
            Check(enemy.State == EnemyAIState.Chase && Mathf.Abs(rb.linearVelocity.x) < .001f,
                "closed door stops chase commands without clearing aggro");
            yield return new WaitForSeconds(.3f);
            Check(Mathf.Abs(rb.position.x - stuck) < .015f, "blocked chase has no creep or jitter");
            wall.enabled = false;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.12f);
            Check(rb.position.x > stuck + .1f, "opening door resumes chase");

            motor.TryTeleportTo(new Vector2(1000f, 0f));
            yield return Step();
            float displaced = Mathf.Min(enemy.SpawnPosition.x + 2f, enemy.RightBound - solid.bounds.extents.x - .1f);
            if (Mathf.Abs(displaced - enemy.SpawnPosition.x) < .5f)
            {
                displaced = Mathf.Max(enemy.SpawnPosition.x - 2f, enemy.LeftBound + solid.bounds.extents.x + .1f);
            }
            PlaceBody(rb, new Vector2(displaced, 200f));
            int direction = enemy.SpawnPosition.x < displaced ? -1 : 1;
            block.transform.position = solid.bounds.center + Vector3.right * direction * (solid.bounds.extents.x + .125f);
            wall.enabled = true;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            stuck = rb.position.x;
            Check(enemy.State == EnemyAIState.Return && Mathf.Abs(rb.linearVelocity.x) < .001f,
                "closed door stops return without tiny velocity commands");
            wall.enabled = false;
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            Check((rb.position.x - stuck) * direction > .2f, "opening door resumes return toward spawn");
            Destroy(block);
            rb.constraints = constraints;
            PlaceBody(rb, saved);
        }

        private IEnumerator Verify()
        {
            player = FindFirstObjectByType<PlayerState>();
            run = FindFirstObjectByType<RunController>();
            motor = player.GetComponent<WhiteboxPlayer2D>();
            body = player.GetComponent<Rigidbody2D>();
            var intro = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "OpeningStoryIntro");
            if (intro != null) { intro.enabled = false; }
            run.TryResume();
            Set(FindFirstObjectByType<GameBootstrap>(), "restartOnDeath", false);
            foreach (var contact in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None))
            {
                contact.enabled = false;
            }
            var spikes = FindObjectsByType<PrototypeSpike2D>(FindObjectsSortMode.None);
            foreach (var spike in spikes) { spike.enabled = false; }

            Stage = "Crowd patrol";
            var all = FindObjectsByType<EnemyBasicAI>(FindObjectsSortMode.None);
            var crowd = all.Where(x => x.name == "Enemy_01" || x.name == "Enemy_02" || x.name == "Enemy_03")
                .OrderBy(x => x.name).ToArray();
            Check(crowd.Length == 3 && all.Length == 14 && all.All(x => x.IsConfigured), "real 14 AI wired; exact bottom three present");
            var min = crowd.Select(x => x.transform.position.x).ToArray();
            var max = min.ToArray();
            bool stayedInside = true;
            float until = Time.time + 18f;
            while (Time.time < until)
            {
                for (int i = 0; i < crowd.Length; i++)
                {
                    min[i] = Mathf.Min(min[i], crowd[i].transform.position.x);
                    max[i] = Mathf.Max(max[i], crowd[i].transform.position.x);
                }
                stayedInside &= all.All(x => x.GetComponent<Collider2D>().bounds.min.x >= x.LeftBound - .06f
                    && x.GetComponent<Collider2D>().bounds.max.x <= x.RightBound + .06f);
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < crowd.Length; i++)
            {
                Check(max[i] - min[i] > 3f, crowd[i].name + " patrol span > 3, actual " + (max[i] - min[i]).ToString("F2"));
            }
            Check(stayedInside, "all 14 remain inside authored bounds");

            Stage = "Crowd chase / return";
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            foreach (var enemy in crowd)
            {
                enemy.GetComponent<Rigidbody2D>().position = enemy.SpawnPosition;
                enemy.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            }
            motor.TryTeleportTo(new Vector2(40.5f, -8.6f));
            yield return Step();
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(crowd.Max(x => x.GetComponent<EnemyBasic>().Config.AlertSeconds) + .1f);
            foreach (var enemy in crowd) { Check(enemy.State == EnemyAIState.Chase, enemy.name + " acquires visible player above crowd"); }
            motor.TryTeleportTo(new Vector2(37.4f, -8.6f));
            yield return new WaitForSeconds(1.3f);
            Check(crowd.All(x => x.State == EnemyAIState.Chase), "left lure preserves crowd chase");
            motor.TryTeleportTo(new Vector2(44f, -8.6f));
            yield return new WaitForSeconds(1.3f);
            Check(crowd.All(x => x.State == EnemyAIState.Chase), "right lure preserves crowd chase");
            motor.TryTeleportTo(new Vector2(60f, -8.6f));
            var returned = new bool[crowd.Length];
            yield return Step();
            until = Time.time + 12f;
            while (Time.time < until && returned.Any(x => !x))
            {
                for (int i = 0; i < crowd.Length; i++)
                {
                    returned[i] |= crowd[i].State == EnemyAIState.Patrol
                        && Mathf.Abs(crowd[i].transform.position.x - crowd[i].SpawnPosition.x) < .12f;
                }
                yield return new WaitForFixedUpdate();
            }
            for (int i = 0; i < crowd.Length; i++) { Check(returned[i], crowd[i].name + " returns to original spawn and patrol"); }

            // 无障碍且速度低于接触裕量时不能每步反向；只改本次Play快照。
            var slow = all.Single(x => x.name == "Enemy_04");
            motor.TryTeleportTo(new Vector2(1000f, 0f));
            yield return Step();
            var slowBody = slow.GetComponent<Rigidbody2D>();
            var savedPosition = slowBody.position;
            float savedPatrolSpeed = Read<float>(slow, "patrolSpeed");
            slowBody.position = new Vector2((slow.LeftBound + slow.RightBound) * .5f, 200f);
            Set(slow, "patrolSpeed", .1f);
            Set(slow, "patrolPauseRemaining", 0f); // 本用例隔离速度，不测试已新增巡逻停顿。
            Physics2D.SyncTransforms();
            int slowDirection = slow.PatrolDirection;
            float slowX = slowBody.position.x;
            yield return new WaitForSeconds(.3f);
            Check(slow.PatrolDirection == slowDirection && (slowBody.position.x - slowX) * slowDirection > .01f,
                "slow unblocked patrol advances without flip-flop");
            Set(slow, "patrolSpeed", savedPatrolSpeed);
            slowBody.position = savedPosition;
            slowBody.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();

            yield return VerifyBlockedMotion(slow);

            Stage = "Damage arbitration";
            motor.TryTeleportTo(new Vector2(1000f, 0f));
            yield return Step();
            yield return ReadyHealth();
            int before = player.CurrentHealth, callbacks = 0;
            string chosen = "";
            yield return Step(() =>
            {
                player.TryQueueContactDamage(new DamageRequest(5, DamageKind.Terrain, gameObject), this, "spike", _ => callbacks++);
                player.TryQueueContactDamage(new DamageRequest(9, DamageKind.Enemy, gameObject), this, "enemy-z", _ => { callbacks++; chosen = "z"; });
                player.TryQueueContactDamage(new DamageRequest(9, DamageKind.Enemy, gameObject), this, "enemy-a", _ => { callbacks++; chosen = "a"; });
            });
            Check(player.CurrentHealth == before - 9 && callbacks == 1 && chosen == "a", "max damage once, ordinal tie winner, one callback");
            Check(!player.TryTakeDamage(new DamageRequest(2, DamageKind.Terrain)), "same protection rejects direct terrain damage");
            Check(!player.TryTakeDamage(default), "default damage rejected");
            int protectedHealth = player.CurrentHealth;
            run.TryPause();
            yield return new WaitForSecondsRealtime(.65f);
            Check(player.CurrentHealth == protectedHealth && player.HasDamageProtection, "pause freezes game-time protection");
            run.TryResume();
            yield return Step();
            Check(player.HasDamageProtection, "resume preserves remaining protection");
            object choiceOwner = new object();
            Check(run.TryBeginChoosing(choiceOwner), "choice phase opens");
            yield return new WaitForSecondsRealtime(.6f);
            Check(player.HasDamageProtection && player.CurrentHealth == protectedHealth, "choice freezes shared protection");
            Check(run.TryEndChoosing(choiceOwner), "choice releases its own lock");
            yield return ReadyHealth();
            before = player.CurrentHealth;
            yield return Step(() =>
            {
                player.TryQueueContactDamage(new DamageRequest(9, DamageKind.Enemy, gameObject), this, "enemy-a", _ => chosen = "a");
                player.TryQueueContactDamage(new DamageRequest(9, DamageKind.Terrain, gameObject), this, "enemy-z", _ => chosen = "z");
            });
            Check(player.CurrentHealth == before - 9 && chosen == "a", "tie result independent of submission order");
            yield return ReadyHealth();
            before = player.CurrentHealth;
            yield return Step(() =>
            {
                player.TryQueueContactDamage(new DamageRequest(9, DamageKind.Enemy, gameObject), this, "stale", null);
                run.TryPause();
            });
            run.TryResume();
            yield return Step();
            Check(player.CurrentHealth == before, "phase change discards queued contact");

            // Actual enemy contact and actual spike both submit in order100, state resolves in order200.
            Stage = "Real enemy / spike integration";
            var target = all.Single(x => x.name == "Enemy_04").GetComponent<EnemyBasic>();
            target.GetComponent<EnemyBasicAI>().enabled = false;
            target.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            target.GetComponent<Rigidbody2D>().position = body.position;
            var contactSource = target.GetComponentInChildren<EnemyContactAttack>(true);
            var activeSpike = spikes[0];
            activeSpike.transform.position = body.position;
            int spikeEvents = 0, enemyEvents = 0;
            activeSpike.KnockbackAccepted += () => spikeEvents++;
            contactSource.DamageApplied += _ => enemyEvents++;
            yield return ReadyHealth();
            before = player.CurrentHealth;
            yield return Step(() => { contactSource.enabled = true; activeSpike.enabled = true; Physics2D.SyncTransforms(); });
            yield return Step();
            contactSource.enabled = false;
            activeSpike.enabled = false;
            Check(player.CurrentHealth == before - target.AttackDamage && enemyEvents == 1 && spikeEvents == 0,
                "real mixed contact chooses enemy 5 over spike 2; loser has no feedback");
            Check(Read<float>(motor, "knockbackRemaining") <= 0f, "losing spike does not knock back");
            yield return ReadyHealth();
            before = player.CurrentHealth;
            activeSpike.enabled = true;
            yield return Step();
            activeSpike.enabled = false;
            Check(player.CurrentHealth == before - 2 && spikeEvents == 1, "actual spike alone damages once");
            Check(Read<float>(motor, "knockbackRemaining") > 0f, "winning spike queues original knockback");
            run.TryPause();
            bool pending = Read<bool>(motor, "knockbackPending");
            yield return new WaitForSecondsRealtime(.1f);
            Check(Read<bool>(motor, "knockbackPending") == pending, "pause does not discard already committed knockback");
            run.TryResume();
            motor.TryTeleportTo(body.position);
            yield return Step();

            Stage = "Pause exploit / arrival";
            yield return ReadyHealth();
            target.GetComponent<Rigidbody2D>().position = body.position;
            Physics2D.SyncTransforms();
            before = player.CurrentHealth;
            contactSource.enabled = true;
            yield return Step();
            int firstHealth = player.CurrentHealth;
            Check(firstHealth == before - target.AttackDamage, "contact has no extra initial cooldown");
            until = Time.time + .65f;
            while (Time.time < until)
            {
                run.TryPause();
                yield return null;
                run.TryResume();
                yield return Step();
            }
            contactSource.enabled = false;
            Check(player.CurrentHealth < firstHealth, "repeated pause/resume cannot postpone contact indefinitely");
            yield return ReadyHealth();
            Check(player.TryCommitTeleportCost(TeleportCostKind.CurrentHealth, 20, 6, .5f, () => true, null),
                "existing cost transaction remains valid");
            before = player.CurrentHealth;
            contactSource.enabled = true;
            yield return new WaitForSeconds(.25f);
            Check(player.CurrentHealth == before, "arrival protection suppresses contact");
            yield return new WaitForSeconds(.35f);
            contactSource.enabled = false;
            Check(player.CurrentHealth == before - target.AttackDamage, "contact resumes after arrival protection");
            before = player.CurrentHealth;
            Check(player.TryCommitTeleportCost(TeleportCostKind.CurrentHealth, 20, 6, .5f, () => true, null)
                && player.CurrentHealth < before, "damage protection does not cancel health payment");

            yield return VerifyContactEnvelope(target);
            var elite = all.Single(x => x.name == "Elite_Exit").GetComponent<EnemyBasic>();
            elite.GetComponent<EnemyBasicAI>().enabled = false;
            elite.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            yield return VerifyContactEnvelope(elite);
            // 后续近战夹具仍使用原普通怪；精英远离它。
            elite.GetComponent<Rigidbody2D>().position = new Vector2(1200f, 100f);
            elite.transform.position = new Vector2(1200f, 100f);

            Stage = "Melee occlusion";
            yield return ReadyHealth();
            var bite = player.GetComponent<PlayerBiteAttack>();
            var sword = player.GetComponent<PlayerSwordAttack>();
            var facing = player.GetComponent<PlayerFacing2D>();
            var blocker = new GameObject("temporary thin wall");
            blocker.hideFlags = HideFlags.DontSave;
            var wall = blocker.AddComponent<BoxCollider2D>();
            wall.size = new Vector2(.1f, 5f);
            var extra = new GameObject("temporary extra target collider");
            extra.hideFlags = HideFlags.DontSave;
            extra.transform.SetParent(target.transform, false);
            extra.AddComponent<BoxCollider2D>().isTrigger = true;
            foreach (int direction in new[] { 1, -1 })
            {
                Set(facing, "<FacingSign>k__BackingField", direction);
                target.GetComponent<Rigidbody2D>().position = body.position + Vector2.right * direction * 2.2f;
                blocker.transform.position = body.position + Vector2.right * direction * .65f;
                target.TrySetRuntimeStats(500, 500, 5);
                player.TryRemoveLoadoutItem(LoadoutItemId.Arms);
                wall.enabled = true;
                Physics2D.SyncTransforms();
                yield return new WaitForSeconds(.65f);
                int hp = target.CurrentHealth;
                Check(bite.TryAttack() && target.CurrentHealth == hp, "bite blocked by thin wall direction " + direction);
                yield return new WaitForSeconds(.65f);
                wall.enabled = false;
                Physics2D.SyncTransforms();
                Check(bite.TryAttack() && target.CurrentHealth == hp - player.BiteDamage,
                    "unblocked bite hits once across multiple colliders direction " + direction);
                yield return new WaitForSeconds(.65f);
                player.TryAddLoadoutItem(LoadoutItemId.Arms);
                wall.enabled = true;
                Physics2D.SyncTransforms();
                hp = target.CurrentHealth;
                Check(sword.TryAttack() && target.CurrentHealth == hp, "sword blocked by wall direction " + direction);
                yield return new WaitForSeconds(.65f);
                wall.enabled = false;
                Physics2D.SyncTransforms();
                Check(sword.TryAttack() && target.CurrentHealth == hp - player.SwordDamage,
                    "open blocker allows sword once direction " + direction);
                yield return new WaitForSeconds(.65f);
                wall.enabled = true;
                wall.isTrigger = true;
                Physics2D.SyncTransforms();
                hp = target.CurrentHealth;
                Check(sword.TryAttack() && target.CurrentHealth == hp - player.SwordDamage, "trigger does not occlude direction " + direction);
                wall.isTrigger = false;
            }
            Destroy(blocker);
            Stage = "Death / victory ordering";
            yield return ReadyHealth();
            int deaths = 0;
            player.Died += () => deaths++;
            yield return Step(() => player.TryQueueContactDamage(new DamageRequest(player.CurrentHealth, DamageKind.Enemy, gameObject),
                this, "fatal", null));
            Check(!player.IsAlive && run.Phase == RunPhase.Dead && deaths == 1, "fatal contact enters Dead exactly once");
            Check(!run.TryWin(player) && !player.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)), "dead player cannot win or take further damage");
            Check(!player.TryCommitTeleportCost(TeleportCostKind.CurrentHealth, 20, 6, .5f, () => true, null),
                "dead player cannot pay or teleport");
            Stage = "Finished";
            Finished = true;
            Debug.Log("[SafetyRepair] " + (Results.Count - Failed) + " passed / " + Failed + " failed\n" + string.Join("\n", Results));
        }
    }
}
#endif
