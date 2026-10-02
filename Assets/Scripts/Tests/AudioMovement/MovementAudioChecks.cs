#if UNITY_EDITOR
// 职责：真实键盘输入/运动/身体状态/Wwise投递验证；隔离地板与墙只在Play临时创建。
// 维护 audio-player-movement；交接 docs/handoffs/audio-player-movement.handoff；规范 AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Presentation;
using Regrowth.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Regrowth.Tests.AudioMovement
{
    [DefaultExecutionOrder(200)]
    public sealed class MovementAudioChecks : MonoBehaviour
    {
        public static MovementAudioChecks Current { get; private set; }
        public bool Finished { get; private set; }
        public int Failed { get; private set; }
        public readonly List<string> Results = new List<string>();
        private Keyboard keyboard;
        private PlayerState state;
        private RunController run;
        private WhiteboxPlayer2D motor;
        private PlayerCharacterPresentation art;
        private PlayerMovementAudio movement;
        private RewardAudioBackend backend;
        private int auditedTotal;
        private int auditErrors;
        private int auditedSounds;
        private double previousSoundTime = double.NegativeInfinity;
        private float previousInterval;

        [MenuItem("Tools/Audio/Verify Player Movement Audio (Fresh Play)")]
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null) throw new InvalidOperationException("Fresh Play required.");
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Current = new GameObject("Movement Audio Checks - DO NOT SAVE").AddComponent<MovementAudioChecks>();
            Current.gameObject.hideFlags = HideFlags.DontSave;
            Current.StartCoroutine(Current.Verify());
        }

        private int Total => backend.Count(AudioCue.PlayerMoveNoFeet) + backend.Count(AudioCue.PlayerFootstep);

        private IEnumerator Verify()
        {
            state = FindFirstObjectByType<PlayerState>();
            run = FindFirstObjectByType<RunController>();
            motor = state.GetComponent<WhiteboxPlayer2D>();
            art = state.GetComponent<PlayerCharacterPresentation>();
            movement = state.GetComponent<PlayerMovementAudio>();
            backend = FindFirstObjectByType<RewardAudioBackend>();
            keyboard = InputSystem.AddDevice<Keyboard>("Movement Audio Test Keyboard");
            GameObject floor = null, wall = null;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (!backend.IsReady && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Check(backend.IsReady && movement.IsWired, "real backend ready and player explicitly wired");
                Check(!state.Contains(LoadoutItemId.Legs), "fresh player has no legs");
                floor = new GameObject("Movement audio temporary floor");
                floor.transform.position = new Vector3(1000, 99, 0);
                floor.AddComponent<BoxCollider2D>().size = new Vector2(200, 1);
                Physics2D.SyncTransforms();
                motor.TryTeleportTo(new Vector2(1000, 100.3f));
                yield return Sample(.6f);
                Check(motor.IsGrounded, "real motor raycasts detect the isolated floor");
                int before = Total;
                yield return Sample(.7f);
                Check(Total == before, "idle is silent");
                int scoot = backend.Count(AudioCue.PlayerMoveNoFeet), foot = backend.Count(AudioCue.PlayerFootstep);
                yield return Sample(2.2f, Key.D);
                Check(backend.Count(AudioCue.PlayerMoveNoFeet) >= scoot + 2 && backend.Count(AudioCue.PlayerFootstep) == foot, "no legs: actual movement posts Play_Move_NoFoots only");
                yield return Sample(.08f, Key.D, Key.Space);
                before = Total;
                yield return Sample(.3f, Key.D);
                Check(!motor.IsGrounded && Total == before, "airborne movement is silent");
                motor.TryTeleportTo(new Vector2(1000, 100.3f));
                yield return Sample(.6f);
                Check(state.TryAddLoadoutItem(LoadoutItemId.Legs), "grant actual Legs state");
                scoot = backend.Count(AudioCue.PlayerMoveNoFeet); foot = backend.Count(AudioCue.PlayerFootstep);
                yield return Sample(2.2f, Key.D);
                Check(backend.Count(AudioCue.PlayerFootstep) >= foot + 2 && backend.Count(AudioCue.PlayerMoveNoFeet) == scoot, "with legs: actual movement posts Play_Footstep only");
                Check(state.TryAddLoadoutItem(LoadoutItemId.Arms), "grant Arms independently");
                scoot = backend.Count(AudioCue.PlayerMoveNoFeet); foot = backend.Count(AudioCue.PlayerFootstep);
                yield return Sample(1.2f, Key.D);
                Check(backend.Count(AudioCue.PlayerFootstep) > foot && backend.Count(AudioCue.PlayerMoveNoFeet) == scoot, "sword body still uses footsteps when Legs are present");
                Check(state.TryRemoveLoadoutItem(LoadoutItemId.Legs), "remove actual Legs state while moving");
                scoot = backend.Count(AudioCue.PlayerMoveNoFeet); foot = backend.Count(AudioCue.PlayerFootstep);
                yield return Sample(1.8f, Key.D);
                Check(backend.Count(AudioCue.PlayerMoveNoFeet) > scoot && backend.Count(AudioCue.PlayerFootstep) == foot, "losing Legs immediately switches back without stale foot cues");
                wall = new GameObject("Movement audio temporary wall");
                wall.transform.position = (Vector3)motor.Position + Vector3.right * 2;
                wall.AddComponent<BoxCollider2D>().size = new Vector2(1, 8);
                Physics2D.SyncTransforms();
                yield return Sample(.6f, Key.D);
                before = Total;
                float x = motor.Position.x;
                yield return Sample(1.2f, Key.D);
                Check(Mathf.Abs(motor.Position.x - x) < .02f && Total == before, "holding movement against a solid wall is silent");
                motor.TryTeleportTo(new Vector2(1000, 100.3f));
                yield return Sample(.6f);
                before = Total;
                yield return Sample(.6f);
                Check(Total == before, "teleport and stationary landing do not produce a step");
                yield return Sample(.5f, Key.D);
                Check(run.TryPause(), "pause accepted");
                before = Total;
                yield return Sample(.7f, Key.D);
                Check(Total == before, "paused movement is silent");
                run.TryResume();
                Check(run.TryBeginChoosing(this), "choice phase accepted");
                before = Total;
                yield return Sample(.7f, Key.D);
                Check(Total == before, "choice UI suppresses movement audio");
                run.TryEndChoosing(this);
                yield return Sample(.6f);
                Check(state.TryAddLoadoutItem(LoadoutItemId.Tail), "grant dash ability for suppression check");
                before = Total;
                yield return Sample(.08f, Key.D, Key.LeftShift);
                Check(motor.IsDashing && Total == before, "dash uses its own cue without movement steps");
                yield return Sample(.5f);
                movement.enabled = false;
                before = Total;
                yield return Sample(.7f, Key.D);
                Check(Total == before, "disabled component does not post sounds");
                movement.enabled = true;
                yield return Sample(1.2f, Key.D);
                Check(Total > before, "reenabling resumes without duplicate subscriptions");
                state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Enemy));
                before = Total;
                yield return Sample(.7f, Key.D);
                Check(!state.IsAlive && Total == before, "dead player never posts movement audio");
                Check(auditedSounds >= 8 && auditErrors == 0, "every posted sound matches displayed contact frame, grounded state, leg ownership and minimum interval (sounds=" + auditedSounds + ", errors=" + auditErrors + ")");
                Check(backend.LastPlayingId != 0, "Wwise returned a valid playing ID");
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                keyboard = null;
                if (floor != null) Destroy(floor);
                if (wall != null) Destroy(wall);
                Finished = true;
                Debug.Log("[MovementAudioChecks] " + (Results.Count - Failed) + " passed / " + Failed + " failed\n" + string.Join("\n", Results));
            }
        }

        private void LateUpdate()
        {
            if (backend == null || movement == null || Finished) return;
            int total = Total;
            if (total == auditedTotal) return;
            bool legs = state.Contains(LoadoutItemId.Legs);
            var data = new SerializedObject(movement);
            var timings = data.FindProperty("contactTimings");
            bool contact = false;
            for (int i = 0; i < timings.arraySize; i++)
            {
                var entry = timings.GetArrayElementAtIndex(i);
                if (art.CurrentAnimation == null || entry.FindPropertyRelative("clip").objectReferenceValue != art.CurrentAnimation.clip) continue;
                var frames = entry.FindPropertyRelative("frames");
                for (int f = 0; f < frames.arraySize; f++) contact |= frames.GetArrayElementAtIndex(f).intValue == art.CurrentFrameIndex;
            }
            if (total != auditedTotal + 1 || !contact || !motor.IsGrounded || motor.IsDashing || !run.IsGameplayActive
                || art.CurrentAction != PlayerVisualAction.Move || Time.timeAsDouble - previousSoundTime + .001 < previousInterval) auditErrors++;
            previousInterval = data.FindProperty(legs ? "footstepMinimumInterval" : "noFeetMinimumInterval").floatValue;
            previousSoundTime = Time.timeAsDouble;
            auditedSounds++;
            auditedTotal = total;
        }

        private IEnumerator Sample(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }

        private void Check(bool ok, string message)
        {
            Results.Add((ok ? "PASS " : "FAIL ") + message);
            if (!ok) Failed++;
        }
    }
}
#endif
