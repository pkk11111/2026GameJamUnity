// Editor诊断：录制开箱前/弹窗中/取消后输出，采样真实音乐进度；不改变音频混音。
using System;
using System.IO;
using Regrowth.Gameplay;
using Regrowth.UI;
using Regrowth.Gameplay.WhiteBox;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Regrowth.Audio.Editor
{
    [InitializeOnLoad]
    public static class ChoiceMusicAudit
    {
        private const string Key = "Audio.ChoiceMusicAudit";
        private static double start, next;
        private static int step;
        private static ExplorationMusicZones music;
        private static ChoicePanel panel;
        private static uint id;
        private static int previous;
        static ChoiceMusicAudit() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Audio/Audit Choice Music In Play Mode")]
        public static void Run()
        {
            RewardAudioSetup.Install();
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }
        private static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            { step = 0; start = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
            if (change == PlayModeStateChange.EnteredEditMode)
            { EditorApplication.update -= Tick; SessionState.SetBool(Key, false); }
        }
        private static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - start > 45) throw new Exception("Audit timeout");
                if (step == 0)
                {
                    music = UnityEngine.Object.FindFirstObjectByType<ExplorationMusicZones>();
                    if (music == null || music.MusicPlayingId == 0) return;
                    id = music.MusicPlayingId;
                    panel = UnityEngine.Object.FindFirstObjectByType<ChoicePanel>();
                    var player = UnityEngine.Object.FindFirstObjectByType<WhiteboxPlayer2D>();
                    player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
                    var capture = AkUnitySoundEngine.StartOutputCapture(Path.GetFullPath("Logs/ChoiceMusic.wav"));
                    Debug.Log("[ChoiceMusicAudit] Capture=" + capture);
                    next = now + 5; step = 1; return;
                }
                if (now < next) return;
                using (var info = new AkSegmentInfo())
                {
                    var result = AkUnitySoundEngine.GetPlayingSegmentInfo(id, info);
                    Debug.Log($"[ChoiceMusicAudit] step={step} result={result} position={info.iCurrentPosition} timeScale={Time.timeScale} level={music.CurrentLevel} sameID={id == music.MusicPlayingId}");
                    if (result != AKRESULT.AK_Success || id != music.MusicPlayingId || (step > 1 && info.iCurrentPosition - previous < 3500))
                        throw new Exception("Music stopped, restarted or slowed across popup");
                    previous = info.iCurrentPosition;
                }
                if (step == 1)
                {
                    var player = UnityEngine.Object.FindFirstObjectByType<WhiteboxPlayer2D>();
                    if (!UnityEngine.Object.FindFirstObjectByType<Chest>().TryInteract(player.gameObject)) throw new Exception("Chest refused");
                }
                else if (step == 2)
                {
                    var button = new SerializedObject(panel).FindProperty("cancelButton").objectReferenceValue as Button;
                    var hover = button.GetComponent<ChoiceCancelHover>();
                    var backend = UnityEngine.Object.FindFirstObjectByType<RewardAudioBackend>();
                    int h = backend.Count(AudioCue.UIHovered), c = backend.Count(AudioCue.UIConfirm);
                    hover.OnPointerEnter(new PointerEventData(EventSystem.current));
                    hover.OnPointerEnter(new PointerEventData(EventSystem.current));
                    button.onClick.Invoke(); button.onClick.Invoke();
                    if (backend.Count(AudioCue.UIHovered) != h + 1 || backend.Count(AudioCue.UIConfirm) != c + 1 || panel.IsOpen)
                        throw new Exception("Cancel hover/click deduplication failed");
                    Debug.Log("[ChoiceMusicAudit] Cancel hover/click PASS");
                }
                else
                {
                    Debug.Log("[ChoiceMusicAudit] PASS music timeline continues before/during/after popup; capture requires waveform review.");
                    Finish(); return;
                }
                step++; next = now + 5;
            }
            catch (Exception error) { Debug.LogError("[ChoiceMusicAudit] FAIL " + error); Finish(); }
        }
        private static void Finish()
        {
            AkUnitySoundEngine.StopOutputCapture();
            EditorApplication.update -= Tick;
            EditorApplication.ExitPlaymode();
        }
    }
}
