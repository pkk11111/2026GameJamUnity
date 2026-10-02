// 职责：编辑器内真实 Wwise 状态验收；临时移动只发生在 Play，退出后不保存测试位置。
// 维护：audio-music-zones；交接：docs/handoffs/audio-music-zones.handoff；规范：根 AGENTS.md。
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Regrowth.Audio.Editor
{
    [InitializeOnLoad]
    public static class MusicZoneVerification
    {
        private const string Key = "MusicZones.Verification";
        private static double deadline;
        private static double nextCheck;
        private static int nextFrame;
        private static int step;
        private static uint musicId;
        private static ExplorationMusicZones controller;
        private static Transform player;
        private static readonly int[] Levels = { 1, 2, 3, 2, 1, 3, 1, 1, 2, 3 };
        private static readonly float[] Heights = { -4.4f, 30f, 85f, 30f, -4.4f, 85f, -4.4f, 13.8f, 14.2f, 58.2f };

        static MusicZoneVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            if (SessionState.GetBool(Key, false)) EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Audio/Verify Whitebox Music In Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/MainLevel.unity");
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 40;
                step = 0; musicId = 0; controller = null; player = null;
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.update -= Tick;
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
            }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || deadline == 0) return;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("初始化/测试超时");
                if (controller == null) controller = UnityEngine.Object.FindFirstObjectByType<ExplorationMusicZones>();
                if (controller == null || controller.MusicPlayingId == 0) return;
                if (player == null)
                {
                    var so = new SerializedObject(controller);
                    player = (Transform)so.FindProperty("player").objectReferenceValue;
                    foreach (var behaviour in player.GetComponents<MonoBehaviour>())
                        if (behaviour.GetType().Name == "WhiteboxPlayer2D") behaviour.enabled = false;
                    player.GetComponent<Rigidbody2D>().simulated = false;
                    musicId = controller.MusicPlayingId;
                    Move();
                    return;
                }
                // 编辑器初次显示/失焦会暂停游戏帧；等待游戏时间及帧数，而非仅等待墙钟。
                if (Time.unscaledTime < nextCheck || Time.frameCount < nextFrame) return;
                var result = AkUnitySoundEngine.GetState("Exploration_Level", out uint state);
                uint expected = AkUnitySoundEngine.GetIDFromString("Level" + Levels[step]);
                if (controller.CurrentLevel != Levels[step] || result != AKRESULT.AK_Success || state != expected)
                    throw new Exception($"step={step} Y={Heights[step]}, controller={controller.CurrentLevel}, Wwise={state}, expected={expected}, result={result}");
                if (controller.MusicPlayingId != musicId) throw new Exception("切层重启了音乐");
                Debug.Log($"[MusicZonesTest] PASS step={step} Y={Heights[step]} Level{Levels[step]} state={state} playingID={musicId}");
                step++;
                if (step == Levels.Length)
                {
                    Debug.Log("[MusicZonesTest] ALL PASS: actual Wwise states, upward/downward/teleport/boundary, unchanged music instance.");
                    Finish(0);
                }
                else Move();
            }
            catch (Exception error) { Debug.LogError("[MusicZonesTest] FAIL " + error); Finish(1); }
        }

        private static void Move()
        {
            player.position = new Vector3(player.position.x, Heights[step], player.position.z);
            Physics2D.SyncTransforms();
            nextCheck = Time.unscaledTime + 0.8;
            nextFrame = Time.frameCount + 10;
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetInt(Key + ".Exit", code);
            EditorApplication.ExitPlaymode();
        }
    }
}
