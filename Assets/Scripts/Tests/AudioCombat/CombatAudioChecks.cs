#if UNITY_EDITOR
// 职责：真实伤害、物理接触、攻击和Wwise计数验收；临时隔离夹具不保存。
// 依赖 Gameplay/Runtime/Audio/InputSystem/Editor；维护audio-full-events。
// 交接docs/handoffs/audio-full-events.handoff；规范AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Regrowth.Tests.AudioCombat
{
    public sealed class CombatAudioChecks : MonoBehaviour
    {
        public static CombatAudioChecks Current { get; private set; }
        public bool Finished { get; private set; }
        public int Failed { get; private set; }
        public readonly List<string> Results = new List<string>();
        private RewardAudioBackend backend;
        private Keyboard keyboard;
        private int Count(AudioCue cue) => backend.Count(cue);
        [MenuItem("Tools/Audio/Verify Combat Audio (Fresh Play)")]
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null) throw new InvalidOperationException("Fresh Play required.");
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Current = new GameObject("Audio combat checks - DO NOT SAVE").AddComponent<CombatAudioChecks>();
            Current.gameObject.hideFlags = HideFlags.DontSave;
            Current.StartCoroutine(Current.Verify());
        }
        private IEnumerator Verify()
        {
            backend = FindFirstObjectByType<RewardAudioBackend>();
            var state = FindFirstObjectByType<PlayerState>();
            var motor = state.GetComponent<WhiteboxPlayer2D>();
            var bite = state.GetComponent<PlayerBiteAttack>();
            var sword = state.GetComponent<PlayerSwordAttack>();
            var fire = state.GetComponent<PlayerFireAttack>();
            var music = FindFirstObjectByType<ExplorationMusicZones>();
            var enemies = FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None);
            var elite = enemies.Single(e => e.GetComponent<EnemyAudio>().IsElite);
            var normal = enemies.First(e => !e.GetComponent<EnemyAudio>().IsElite);
            keyboard = InputSystem.AddDevice<Keyboard>("Combat Audio Test Keyboard");
            GameObject floor = null;
            try
            {
                yield return Sample(.5f);
                Check(backend.IsReady && enemies.Count(e => e.GetComponent<EnemyAudio>().IsElite) == 1 && elite.name == "Elite_Exit", "one explicit top elite and ready bank");
                foreach (var contact in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None)) contact.enabled = false;
                uint musicId = music.MusicPlayingId;
                motor.TryTeleportTo((Vector2)elite.transform.position + Vector2.left * 2.5f);
                yield return Sample(.8f);
                Check(elite.GetComponent<EnemyBasicAI>().State == EnemyAIState.Chase && music.IsEliteMusic && music.MusicPlayingId == musicId, "real elite chase changes state without restarting music");
                floor = new GameObject("Audio test floor"); floor.transform.position = new Vector3(1000, 99, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(100, 1);
                Physics2D.SyncTransforms();
                int lands = Count(AudioCue.PlayerLand);
                motor.TryTeleportTo(new Vector2(1000, 100.3f)); yield return Sample(.6f);
                Check(!music.IsEliteMusic && music.MusicPlayingId == musicId, "leaving elite restores exploration on same instance");
                Check(Count(AudioCue.PlayerLand) == lands, "teleport arrival does not fake landing");
                yield return Sample(.08f, Key.Space); yield return Sample(1.3f);
                Check(Count(AudioCue.PlayerLand) == lands + 1, "real jump lands once");
                foreach (var enemy in new[] { normal, elite })
                {
                    enemy.GetComponent<EnemyBasicAI>().enabled = false;
                    var body = enemy.GetComponent<Rigidbody2D>(); body.linearVelocity = Vector2.zero; body.constraints = RigidbodyConstraints2D.FreezeAll;
                    enemy.TrySetRuntimeStats(1000, 1000, 1);
                    enemy.transform.position = new Vector3(1030, 100, 0);
                }
                int hurt = Count(AudioCue.NPCHurt);
                normal.TrySetRuntimeStats(1200, 1200, 1);
                Check(Count(AudioCue.NPCHurt) == hurt, "stat enhancement is not hurt");
                normal.transform.position = (Vector3)motor.Position + Vector3.right * 1.5f; Physics2D.SyncTransforms();
                int hit = Count(AudioCue.BiteHitNPC), action = Count(AudioCue.PlayerBite);
                Check(bite.TryAttack(), "bite accepted");
                Check(Count(AudioCue.BiteHitNPC) == hit + 1 && Count(AudioCue.PlayerBite) == action + 1 && Count(AudioCue.NPCHurt) == hurt + 1, "bite action and accepted normal hit/hurt");
                normal.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy));
                Check(Count(AudioCue.NPCHurt) == hurt + 1, "continuous hurt throttled");
                yield return Sample(.6f);
                normal.transform.position = new Vector3(1030, 100, 0);
                elite.transform.position = (Vector3)motor.Position + Vector3.right * 2; Physics2D.SyncTransforms();
                hit = Count(AudioCue.BiteHitElite); bite.TryAttack();
                Check(Count(AudioCue.BiteHitElite) == hit + 1 && Count(AudioCue.EliteHurt) == 1, "elite bite classification");
                yield return Sample(.6f); state.TryAddLoadoutItem(LoadoutItemId.Arms);
                hit = Count(AudioCue.SwordHitElite); action = Count(AudioCue.PlayerWeaponAttack);
                Check(sword.TryAttack(), "sword accepted");
                Check(Count(AudioCue.SwordHitElite) == hit + 1 && Count(AudioCue.PlayerWeaponAttack) == action + 1, "elite sword hit and swing");
                yield return Sample(.6f);
                elite.transform.position = new Vector3(1030, 100, 0);
                normal.transform.position = (Vector3)motor.Position + Vector3.right * 1.5f; Physics2D.SyncTransforms();
                hit = Count(AudioCue.SwordHitNPC); sword.TryAttack();
                Check(Count(AudioCue.SwordHitNPC) == hit + 1, "normal sword classification");
                yield return Sample(.6f);
                normal.transform.position = new Vector3(1030, 100, 0); Physics2D.SyncTransforms();
                hit = Count(AudioCue.SwordHitNPC); action = Count(AudioCue.PlayerWeaponAttack); sword.TryAttack();
                Check(Count(AudioCue.PlayerWeaponAttack) == action + 1 && Count(AudioCue.SwordHitNPC) == hit, "empty swing has action but no hit");
                yield return Sample(.6f); state.TryAddLoadoutItem(LoadoutItemId.FlameTail);
                foreach (var collider in normal.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                normal.transform.position = (Vector3)motor.Position + Vector3.right * 3;
                for (int i = 0; i < 2; i++) { var collider = normal.gameObject.AddComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(10, 2); }
                Physics2D.SyncTransforms();
                hit = Count(AudioCue.FireHit); action = Count(AudioCue.PlayerFire); int hp = normal.CurrentHealth;
                Check(fire.TryAttack(), "fire starts"); yield return Sample(2.2f);
                Check(Count(AudioCue.PlayerFire) == action + 1 && Count(AudioCue.FireHit) == hit + 1 && normal.CurrentHealth < hp - state.FireDamage, "multiple ticks and duplicate colliders: one fire hit per enemy per cast");
                yield return Sample(6f); hit = Count(AudioCue.FireHit);
                Check(fire.TryAttack(), "next cast accepted after real cooldown"); yield return Sample(2.2f);
                Check(Count(AudioCue.FireHit) == hit + 1, "next cast resets fire hit deduplication");
                int deaths = Count(AudioCue.NPCDeath); hurt = Count(AudioCue.NPCHurt);
                normal.TryTakeDamage(new DamageRequest(100000, DamageKind.Enemy)); normal.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy));
                Check(Count(AudioCue.NPCDeath) == deaths + 1 && Count(AudioCue.NPCHurt) == hurt, "fatal normal damage: death once, no hurt");
                var eliteContact = elite.GetComponentInChildren<EnemyContactAttack>(true);
                elite.transform.position = motor.transform.position; Physics2D.SyncTransforms();
                hurt = Count(AudioCue.PlayerHurt); action = Count(AudioCue.EliteAttack); eliteContact.enabled = true;
                yield return Sample(.12f); eliteContact.enabled = false;
                Check(Count(AudioCue.EliteAttack) == action + 1 && Count(AudioCue.PlayerHurt) == hurt + 1, "real elite contact damages player and sounds once");
                deaths = Count(AudioCue.EliteDeath); hurt = Count(AudioCue.EliteHurt);
                elite.TryTakeDamage(new DamageRequest(100000, DamageKind.Enemy)); elite.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy));
                Check(Count(AudioCue.EliteDeath) == deaths + 1 && Count(AudioCue.EliteHurt) == hurt, "fatal elite damage: death once, no hurt");
                var door = FindObjectsByType<WorldDoor>(FindObjectsSortMode.None).First();
                door.TrySetOpen(false); action = Count(AudioCue.DoorOpened); door.TrySetOpen(true); door.TrySetOpen(true); door.TrySetOpen(false);
                Check(Count(AudioCue.DoorOpened) == action + 1, "door opens once; repeat and close silent");
                yield return Sample(.6f);
                hurt = Count(AudioCue.PlayerHurt); deaths = Count(AudioCue.PlayerDied);
                state.TryTakeDamage(new DamageRequest(100000, DamageKind.Terrain)); state.TryTakeDamage(new DamageRequest(1, DamageKind.Terrain));
                yield return Sample(.1f);
                Check(!state.IsAlive && Count(AudioCue.PlayerDied) == deaths + 1 && Count(AudioCue.PlayerHurt) == hurt, "fatal player damage: death once without hurt");
                Check(music.MusicPlayingId == 0 && backend.IsReady && backend.LastPlayingId != 0, "death stops music while bank and death SFX stay valid");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                if (floor != null) Destroy(floor);
                Finished = true;
                Debug.Log("[CombatAudioChecks] " + (Results.Count - Failed) + " passed / " + Failed + " failed\n" + string.Join("\n", Results));
            }
        }
        private IEnumerator Sample(float seconds, params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return new WaitForSecondsRealtime(seconds); }
        private void Check(bool value, string label)
        { Results.Add((value ? "PASS " : "FAIL ") + label); if (!value) Failed++; }
    }
}
#endif
