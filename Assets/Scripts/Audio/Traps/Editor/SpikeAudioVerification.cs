// 职责：真实场景物理接触与Wwise事件投递验收；退出Play恢复测试移动。
// 维护：audio-traps；交接：docs/handoffs/audio-traps.handoff；规范：根AGENTS.md。
using System;
using System.Linq;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEditor;
using UnityEngine;

namespace Regrowth.Audio.Editor
{
    [InitializeOnLoad]
    public static class SpikeAudioVerification
    {
        private const string Key = "Audio.SpikeVerification";
        private static double deadline;
        private static SpikeAudioEmitter[] emitters;
        private static WhiteboxPlayer2D player;
        private static Rigidbody2D body;
        private static int index;
        private static bool touching;
        private static float waitUntil;
        private static double contactDeadline;

        static SpikeAudioVerification()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Audio/Verify Whitebox Spike Audio In Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出Play。");
            SpikeAudioSetup.Install();
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                emitters = null; player = null; index = 0; touching = false;
                deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Key, false);
            }
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("测试超时");
                if (emitters == null)
                {
                    var music = UnityEngine.Object.FindFirstObjectByType<ExplorationMusicZones>();
                    player = UnityEngine.Object.FindFirstObjectByType<WhiteboxPlayer2D>();
                    if (music == null || music.MusicPlayingId == 0 || player == null || !player.IsGameplayActive) return;
                    emitters = UnityEngine.Object.FindObjectsByType<SpikeAudioEmitter>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
                    if (emitters.Length != 7) throw new Exception("预期7个地刺，实际" + emitters.Length);
                    body = player.GetComponent<Rigidbody2D>();
                    body.constraints = RigidbodyConstraints2D.FreezeAll;
                    var run = UnityEngine.Object.FindFirstObjectByType<RunController>();
                    if (!run.TryPause()) throw new Exception("无法测试暂停");
                    emitters[0].GetComponent<PrototypeSpike2D>().SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                    if (emitters[0].PostedCount != 0) throw new Exception("暂停期间错误发声");
                    run.TryResume();
                    MoveAway();
                    return;
                }
                var audio = emitters[index];
                if (!touching)
                {
                    if (Time.time < waitUntil) return;
                    body.position = audio.GetComponent<Collider2D>().bounds.center;
                    Physics2D.SyncTransforms();
                    touching = true;
                    contactDeadline = EditorApplication.timeSinceStartup + 5;
                    return;
                }
                if (audio.PostedCount == 0)
                {
                    if (EditorApplication.timeSinceStartup > contactDeadline) throw new Exception(audio.name + " 物理接触后未发声");
                    return;
                }
                if (audio.PostedCount != 1 || audio.LastPlayingId == 0) throw new Exception(audio.name + " 重复或无效投递");
                var spike = audio.GetComponent<PrototypeSpike2D>();
                spike.SendMessage("OnTriggerStay2D", player.GetComponent<Collider2D>());
                spike.SendMessage("OnTriggerEnter2D", player.GetComponent<Collider2D>());
                if (audio.PostedCount != 1) throw new Exception(audio.name + " 保护期重复发声");
                Debug.Log($"[SpikeAudioTest] PASS {audio.name}: physical contact, playingID={audio.LastPlayingId}, repeated callbacks suppressed.");
                index++;
                if (index == emitters.Length)
                {
                    Debug.Log("[SpikeAudioTest] ALL PASS: 7 physical spike contacts + protection deduplication + pause suppression.");
                    Finish();
                }
                else MoveAway();
            }
            catch (Exception error) { Debug.LogError("[SpikeAudioTest] FAIL " + error); Finish(); }
        }

        private static void MoveAway()
        {
            body.position = new Vector2(-63, 130);
            Physics2D.SyncTransforms();
            waitUntil = Time.time + 0.8f;
            touching = false;
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
    }
}
