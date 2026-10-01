// 职责：实际白板机关成功/重复/远距/暂停的音频验证；退出Play恢复位置与门状态。
// 维护：audio-rewards；规范：根AGENTS.md。
using System;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEditor;
using UnityEngine;

namespace Regrowth.Audio.Editor
{
    [InitializeOnLoad]
    public static class SwitchAudioVerification
    {
        private const string Key = "Audio.SwitchVerification";
        private static PrototypeToggleButton2D[] switches;
        private static WhiteboxPlayer2D player;
        private static RewardAudioBackend backend;
        private static int index, repeats;
        private static double deadline;
        private static float next;
        static SwitchAudioVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Audio/Verify Whitebox Switch Audio In Play Mode")]
        public static void Run()
        {
            RewardAudioSetup.Install();
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                switches = null; index = repeats = 0; next = 0;
                deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Key, false); }
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("测试超时");
                if (switches == null)
                {
                    backend = UnityEngine.Object.FindFirstObjectByType<RewardAudioBackend>();
                    player = UnityEngine.Object.FindFirstObjectByType<WhiteboxPlayer2D>();
                    if (backend == null || !backend.IsReady || player == null || !player.IsGameplayActive) return;
                    switches = UnityEngine.Object.FindObjectsByType<PrototypeToggleButton2D>(FindObjectsSortMode.None);
                    if (switches.Length != 8) throw new Exception("预期8个机关，实际" + switches.Length);
                    player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
                }
                if (Time.time < next) return;
                var target = switches[index];
                var body = player.GetComponent<Rigidbody2D>();
                int before = backend.Count(AudioCue.SwitchActivated);
                body.position = (Vector2)target.transform.position + Vector2.up * 20;
                Physics2D.SyncTransforms();
                if (target.TryInteract(player.gameObject) || backend.Count(AudioCue.SwitchActivated) != before) throw new Exception("远距误播");
                body.position = target.transform.position;
                Physics2D.SyncTransforms();
                var run = UnityEngine.Object.FindFirstObjectByType<RunController>();
                if (!run.TryPause()) throw new Exception("暂停失败");
                if (target.TryInteract(player.gameObject) || backend.Count(AudioCue.SwitchActivated) != before) throw new Exception("暂停误播");
                run.TryResume();
                if (!target.TryInteract(player.gameObject) || backend.Count(AudioCue.SwitchActivated) != before + 1 || backend.LastPlayingId == 0)
                    throw new Exception(target.name + "成功交互未投递");
                if (target.TryInteract(player.gameObject) || backend.Count(AudioCue.SwitchActivated) != before + 1) throw new Exception("重复交互误播");
                Debug.Log($"[SwitchAudioTest] PASS {target.name} toggle={repeats + 1} playingID={backend.LastPlayingId}");
                if (++repeats == 2) { repeats = 0; index++; }
                if (index == switches.Length)
                {
                    Debug.Log("[SwitchAudioTest] ALL PASS: 8 switches, 16 successful toggles; distance/pause/duplicate rejection silent.");
                    Finish(); return;
                }
                next = Time.time + 0.4f;
            }
            catch (Exception error) { Debug.LogError("[SwitchAudioTest] FAIL " + error); Finish(); }
        }
        private static void Finish() { EditorApplication.update -= Tick; EditorApplication.ExitPlaymode(); }
    }
}
