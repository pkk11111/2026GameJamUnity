#if UNITY_EDITOR
// 职责：主图蝙蝠、全层墙体索敌/飞行碰撞、移动声音的显式Play检查。
// 维护enemy-ai；依赖真实Enemy/Audio/Player/Run；仅临时DontSave夹具，退出Play丢弃。
// 交接docs/handoffs/enemy-ai.handoff；规范AGENTS.md；不保存正式场景/配置。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Tests.AudioCombat
{
    public sealed class EnemyFlightChecks : MonoBehaviour
    {
        public static EnemyFlightChecks Current { get; private set; }
        public readonly List<string> Results = new List<string>();
        public int Failed { get; private set; }
        public bool Finished { get; private set; }
        public string Stage { get; private set; }
        private PlayerState player;
        private RunController run;
        private WhiteboxPlayer2D motor;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Probe : IAudioBackend
        {
            public readonly Dictionary<AudioCue, int> Counts = new Dictionary<AudioCue, int>();
            public void Play(AudioCue cue, GameObject emitter)
            {
                Counts[cue] = Count(cue) + 1;
            }
            public void StopAll(GameObject emitter) { }
            public int Count(AudioCue cue) => Counts.TryGetValue(cue, out int n) ? n : 0;
        }
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null || FindFirstObjectByType<PlayerState>() == null)
            {
                throw new InvalidOperationException("Fresh MainLevel Play required.");
            }
            var go = new GameObject("Enemy flight checks - temporary") { hideFlags = HideFlags.DontSave };
            Current = go.AddComponent<EnemyFlightChecks>();
            Current.StartCoroutine(Current.Verify());
        }
        private void Check(bool value, string description)
        {
            Results.Add((value ? "PASS " : "FAIL ") + description);
            if (!value)
            {
                Failed++;
                Debug.LogError("[EnemyFlight] " + description, this);
            }
        }
        private static void Set(object o, string field, object value)
        {
            o.GetType().GetField(field, Private).SetValue(o, value);
        }
        private static T Read<T>(object o, string field)
        {
            return (T)o.GetType().GetField(field, Private).GetValue(o);
        }
        private static void Place(Rigidbody2D b, Vector2 p)
        {
            b.position = p;
            b.transform.position = p;
            b.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }
        private IEnumerator Target(Vector2 p)
        {
            Check(motor.TryTeleportTo(p), "fixture target relocation");
            yield return new WaitForFixedUpdate();
            yield return null;
        }
        private static GameObject Block(Vector2 point, Vector2 size)
        {
            var go = new GameObject("temporary flight blocker") { hideFlags = HideFlags.DontSave };
            go.transform.position = point;
            go.layer = 2; // 非Default层也必须遮挡；不修改全局Layer设置。
            go.AddComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms();
            return go;
        }
        private static void State(EnemyBasicAI ai, EnemyAIState state)
        {
            Set(ai, "<State>k__BackingField", state);
        }
        private IEnumerator VerifySight(EnemyBasicAI ai, float y)
        {
            Stage = "Sight and territory " + ai.name;
            var rb = ai.GetComponent<Rigidbody2D>();
            ai.enabled = true;
            Set(ai, "leftBoundary", null);
            Set(ai, "rightBoundary", null);
            Set(ai, "leftOffset", 12f);
            Set(ai, "rightOffset", 12f);
            Set(ai, "spawn", new Vector2(0f, y));
            Place(rb, new Vector2(0f, y));
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            State(ai, EnemyAIState.Patrol);
            var wall = Block(new Vector2(3f, y), new Vector2(.05f, 10f));
            yield return Target(new Vector2(7.5f, y));
            var acquire = typeof(EnemyBasicAI).GetMethod("CanAcquire", Private);
            Check(!(bool)acquire.Invoke(ai, null), ai.name + " non-default-layer wall blocks detection");
            yield return new WaitForSeconds(.4f);
            Check(ai.State == EnemyAIState.Patrol, "blocked target never enters alert/chase");
            wall.GetComponent<Collider2D>().isTrigger = true;
            Physics2D.SyncTransforms();
            Check((bool)acquire.Invoke(ai, null), "7.5-unit visible target acquired; decorative trigger excluded");
            yield return new WaitForSeconds(.04f);
            Check(ai.State == EnemyAIState.Alert, "visible target first enters alert");
            yield return new WaitForSeconds(.35f);
            Check(ai.State == EnemyAIState.Chase, "alert completes without bypassing delay");
            yield return Target(new Vector2(8.2f, y));
            Check(!(bool)acquire.Invoke(ai, null), "8.2-unit target outside radius8");
            Destroy(wall);
            if (ai.IsFlying)
            {
                yield return Target(new Vector2(1f, ai.TopBound + 1f));
                yield return new WaitForSeconds(.04f);
                Check(ai.State == EnemyAIState.Return, "bat leaves chase when target exits vertical territory");
            }
            ai.enabled = false;
        }
        private IEnumerator VerifyFlightCollision(EnemyBasicAI ai)
        {
            Stage = "Flight collision and lifecycle";
            ai.enabled = true;
            var rb = ai.GetComponent<Rigidbody2D>();
            var solid = ai.GetComponent<Collider2D>();
            Set(ai, "spawn", new Vector2(0f, 200f));
            Set(ai, "flightBelow", 4f);
            Set(ai, "flightAbove", 5f);
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            foreach (Vector2 direction in new[] { Vector2.right, Vector2.up, Vector2.down })
            {
                Place(rb, new Vector2(0f, 200f));
                State(ai, EnemyAIState.Chase);
                yield return Target(rb.position + direction * 3f);
                Place(rb, new Vector2(0f, 200f));
                State(ai, EnemyAIState.Chase);
                Vector2 half = solid.bounds.extents;
                var wall = Block(rb.position + direction * ((direction.x != 0f ? half.x : half.y) + .12f),
                    direction.x != 0f ? new Vector2(.05f, 8f) : new Vector2(8f, .05f));
                // 高速模式只存在于此夹具，证明扫掠不依赖小位移碰巧不穿墙。
                Set(ai, "chaseFactor", 100f);
                yield return new WaitForSeconds(.16f);
                var distance = solid.Distance(wall.GetComponent<Collider2D>());
                Check(!distance.isOverlapped && Vector2.Dot(rb.position - new Vector2(0f, 200f), direction) < .15f,
                    "high-speed full-body sweep blocks " + direction + " position=" + rb.position + " overlap=" + distance.isOverlapped + " gap=" + distance.distance + " state=" + ai.State);
                Set(ai, "chaseFactor", ai.GetComponent<EnemyBasic>().Config.ChaseSpeedFactor);
                wall.SetActive(false);
                Destroy(wall);
            }
            Place(rb, new Vector2(0f, 200f));
            yield return Target(new Vector2(3f, 202f));
            State(ai, EnemyAIState.Chase);
            yield return new WaitForSeconds(.12f);
            Check(rb.position.x > .1f && rb.position.y > 200.05f, "bat pursues in two dimensions");
            run.TryPause();
            Vector2 paused = rb.position;
            yield return new WaitForSecondsRealtime(.15f);
            Check((rb.position - paused).sqrMagnitude < .0001f && rb.linearVelocity == Vector2.zero,
                "pause freezes complete flight velocity");
            run.TryResume();
            Check(ai.TryApplyWeaponHit(-1), "flying enemy accepts sword recoil");
            float x = rb.position.x;
            yield return new WaitForSeconds(.1f);
            Check(rb.position.x < x - .1f, "flight recoil overrides pursuit");
            ai.enabled = false;
            Check(!ai.IsRecovering && rb.linearVelocity == Vector2.zero, "flight disable clears both axes and recoil");
            ai.enabled = true;
            Check(rb.gravityScale == 0f, "flight enable restores no-gravity ownership");
            ai.enabled = false;
        }
        private IEnumerator VerifySound(EnemyBasicAI walker, EnemyBasicAI bat)
        {
            Stage = "Actual movement audio";
            var backend = FindFirstObjectByType<RewardAudioBackend>();
            foreach (var audio in FindObjectsByType<EnemyAudio>(FindObjectsSortMode.None))
            {
                Set(audio, "movementAudio", false);
            }
            var sound = walker.GetComponent<EnemyAudio>();
            Set(sound, "movementAudio", true);
            Set(sound, "movementStartOffset", 0f);
            var rb = walker.GetComponent<Rigidbody2D>();
            walker.enabled = true;
            Set(walker, "spawn", new Vector2(0f, 300f));
            State(walker, EnemyAIState.Patrol);
            Set(walker, "patrolPauseRemaining", 0f);
            Set(walker, "patrolDirection", 1);
            Place(rb, new Vector2(0f, 300f));
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            var floor = Block(new Vector2(0f, 300f - walker.GetComponent<Collider2D>().bounds.extents.y - .505f), new Vector2(30f, 1f));
            yield return Target(new Vector2(-12f, 300f));
            int requests = sound.MovementRequests;
            int posted = backend.Count(AudioCue.NPCFootstep);
            yield return new WaitForSeconds(1.8f);
            Check(sound.MovementRequests > requests, "actual grounded patrol emits footstep requests");
            Check(backend.IsReady && backend.Count(AudioCue.NPCFootstep) > posted, "Wwise receives successful NPC footstep events");

            var probe = new Probe();
            GameAudio.InstallBackend(probe);
            int before = sound.MovementRequests;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
            yield return new WaitForSeconds(.9f);
            Check(sound.MovementRequests == before, "blocked walking command without displacement is silent");
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            floor.SetActive(false);
            Place(rb, new Vector2(0f, 305f));
            yield return new WaitForSeconds(.5f);
            Check(probe.Count(AudioCue.NPCFootstep) == 0, "airborne walking emits no footsteps");
            walker.enabled = false;
            Destroy(floor);
            Set(sound, "movementAudio", false);

            bat.enabled = true;
            Set(bat, "spawn", new Vector2(0f, 200f));
            Place(bat.GetComponent<Rigidbody2D>(), new Vector2(0f, 200.75f));
            State(bat, EnemyAIState.Patrol);
            var wings = bat.GetComponent<EnemyAudio>();
            Set(wings, "movementAudio", true);
            Set(wings, "movementStartOffset", 0f);
            yield return Target(new Vector2(-12f, 200f));
            yield return new WaitForSeconds(1.6f);
            Check(probe.Count(AudioCue.NPCFly) >= 1 && probe.Count(AudioCue.NPCFly) <= 3
                && probe.Count(AudioCue.NPCFootstep) == 0, "bat emits rate-limited wings, never ground footsteps");
            int flaps = probe.Count(AudioCue.NPCFly);
            run.TryPause();
            yield return new WaitForSecondsRealtime(.8f);
            Check(probe.Count(AudioCue.NPCFly) == flaps, "pause emits no wings");
            run.TryResume();
            yield return Target(new Vector2(1000f, 200f));
            yield return new WaitForSeconds(.9f);
            Check(probe.Count(AudioCue.NPCFly) == flaps, "distant bat emits no wings");
            yield return Target(new Vector2(-12f, 200f));
            GameAudio.InstallBackend(backend);
            posted = backend.Count(AudioCue.NPCFly);
            yield return new WaitForSeconds(1.3f);
            Check(backend.Count(AudioCue.NPCFly) > posted, "Wwise receives successful wing events");
            var owner = bat.GetComponent<EnemyBasic>();
            owner.TryTakeDamage(new DamageRequest(owner.CurrentHealth, DamageKind.Enemy, player.gameObject));
            int deadRequests = wings.MovementRequests;
            yield return new WaitForSeconds(.9f);
            Check(wings.MovementRequests == deadRequests && !bat.GetComponent<Rigidbody2D>().simulated,
                "dead bat has no movement or wing requests");
        }
        private IEnumerator Verify()
        {
            player = FindFirstObjectByType<PlayerState>();
            motor = player.GetComponent<WhiteboxPlayer2D>();
            run = FindFirstObjectByType<RunController>();
            var intro = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(x => x.GetType().Name == "OpeningStoryIntro");
            if (intro != null) { intro.enabled = false; }
            run.TryResume();
            foreach (var contact in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None)) { contact.enabled = false; }
            foreach (var spike in FindObjectsByType<PrototypeSpike2D>(FindObjectsSortMode.None)) { spike.enabled = false; }
            var all = FindObjectsByType<EnemyBasicAI>(FindObjectsSortMode.None);
            var bats = all.Where(x => x.IsFlying).OrderBy(x => x.name).ToArray();
            Check(bats.Select(x => x.name).SequenceEqual(new[] { "Enemy_05", "Enemy_06", "Enemy_08", "Enemy_09", "Enemy_11" }), "exactly five left-branch enemies are bats");
            Check(all.Length == 14 && all.All(x => x.IsConfigured && x.AggroRadius == 8f), "all14 valid; normal/bat/elite radius8");
            player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            yield return Target(new Vector2(1000f, 200f));
            Stage = "Real branch hover";
            var low = bats.Select(b => b.transform.position.y).ToArray();
            var high = low.ToArray();
            bool bounded = true;
            float end = Time.time + 8f;
            while (Time.time < end)
            {
                for (int i = 0; i < bats.Length; i++)
                {
                    var b = bats[i];
                    var bounds = b.GetComponent<Collider2D>().bounds;
                    low[i] = Mathf.Min(low[i], b.transform.position.y);
                    high[i] = Mathf.Max(high[i], b.transform.position.y);
                    bounded &= bounds.min.x >= b.LeftBound - .03f && bounds.max.x <= b.RightBound + .03f
                        && bounds.min.y >= b.BottomBound - .03f && bounds.max.y <= b.TopBound + .03f;
                }
                yield return new WaitForFixedUpdate();
            }
            Check(bounded, "all five bats remain in authored flight territories for8 seconds");
            for (int i = 0; i < bats.Length; i++)
            {
                Check(high[i] - low[i] > .2f && bats[i].GetComponent<Rigidbody2D>().gravityScale == 0f,
                    bats[i].name + " actually hovers in branch, vertical span " + (high[i] - low[i]));
            }
            foreach (var ai in all) { ai.enabled = false; }
            var walker = all.Single(x => x.name == "Enemy_04");
            yield return VerifySight(walker, 300f);
            yield return VerifySight(bats[0], 200f);
            yield return VerifyFlightCollision(bats[0]);
            yield return VerifySound(walker, bats[0]);
            Finished = true;
            Stage = "Finished";
            Debug.Log("[EnemyFlight] " + (Results.Count - Failed) + " passed / " + Failed + " failed\n" + string.Join("\n", Results));
        }
    }
}
#endif
