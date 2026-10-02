// 职责：真实白板事务/UI/运动消费路径及 Wwise 投递验收；测试输入缓冲仅限 Editor。
// 维护：audio-rewards；退出 Play 自动恢复场景与奖励状态。
using System;
using System.Linq;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using Regrowth.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Regrowth.Audio.Editor
{
    [InitializeOnLoad]
    public static class RewardAudioVerification
    {
        private const string Key = "Audio.RewardVerification";
        private static double deadline;
        private static RewardAudioBackend backend;
        private static int checks;
        static RewardAudioVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Audio/Verify Whitebox Reward Audio In Play Mode")]
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
            {
                deadline = EditorApplication.timeSinceStartup + 60;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                SessionState.SetBool(Key, false);
            }
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("等待音频/玩家超时");
                backend = UnityEngine.Object.FindFirstObjectByType<RewardAudioBackend>();
                var player = UnityEngine.Object.FindFirstObjectByType<WhiteboxPlayer2D>();
                if (backend == null || !backend.IsReady || player == null || !player.IsGameplayActive) return;
                EditorApplication.update -= Tick;
                checks = 0;
                var state = player.GetComponent<PlayerState>();
                var panel = UnityEngine.Object.FindFirstObjectByType<ChoicePanel>();
                var chests = UnityEngine.Object.FindObjectsByType<Chest>(FindObjectsSortMode.None);
                foreach (var chest in chests)
                {
                    int opened = backend.Count(AudioCue.ChestOpened), popped = backend.Count(AudioCue.CardsPresented);
                    Check(chest.TryInteract(player.gameObject), chest.name + " opens");
                    Check(backend.Count(AudioCue.ChestOpened) == opened + 1 && backend.Count(AudioCue.CardsPresented) == popped + 1,
                        chest.name + " open/pop exactly once");
                    Check(!chest.TryInteract(player.gameObject) && backend.Count(AudioCue.ChestOpened) == opened + 1, "repeated open rejected");
                    var card = panel.GetComponentsInChildren<ChoiceCardView>().First();
                    var pointer = new PointerEventData(EventSystem.current);
                    int hover = backend.Count(AudioCue.CardHovered), selected = backend.Count(AudioCue.CardSelected);
                    card.OnPointerEnter(pointer); card.OnPointerEnter(pointer);
                    Check(backend.Count(AudioCue.CardHovered) == hover + 1, "hover deduplicated while choosing");
                    card.OnPointerExit(pointer); card.OnPointerEnter(pointer);
                    Check(backend.Count(AudioCue.CardHovered) == hover + 2, "reenter plays again");
                    panel.CancelCurrent();
                    Check(!chest.IsClaimed && backend.Count(AudioCue.CardSelected) == selected, "cancel silent and unclaimed");
                }
                Check(chests.Length == 9, "all nine chests");
                Check(chests[0].TryInteract(player.gameObject), "open for real reward");
                int before = backend.Count(AudioCue.CardSelected);
                var chosen = panel.GetComponentsInChildren<ChoiceCardView>().First();
                var button = (Button)Field(chosen, "selectButton");
                button.onClick.Invoke(); button.onClick.Invoke();
                Check(chests[0].IsClaimed && !panel.IsOpen && backend.Count(AudioCue.CardSelected) == before + 1, "real claim, duplicate confirmation suppressed");
                foreach (var item in state.Items.ToArray()) state.TryRemoveLoadoutItem(item);
                var body = player.GetComponent<Rigidbody2D>();
                body.position = new Vector2(-63, 125);
                Physics2D.SyncTransforms();
                Invoke(player, "ClearMotionRequests");
                var input = (PlayerInputReader)Field(player, "inputReader");
                int dash = backend.Count(AudioCue.PlayerDash), jump = backend.Count(AudioCue.PlayerDoubleJump);
                Inject(input, 3); Invoke(player, "FixedUpdate");
                Inject(input, 0); Invoke(player, "FixedUpdate");
                Check(backend.Count(AudioCue.PlayerDash) == dash && backend.Count(AudioCue.PlayerDoubleJump) == jump, "missing ability produces no action audio");
                int gain = backend.Count(AudioCue.AbilityGained);
                Check(state.TryAddLoadoutItem(LoadoutItemId.Tail) && state.TryAddLoadoutItem(LoadoutItemId.Legs), "grant actual abilities");
                Check(backend.Count(AudioCue.AbilityGained) == gain + 2, "gain sound per successful grant");
                Check(!state.TryAddLoadoutItem(LoadoutItemId.Legs) && backend.Count(AudioCue.AbilityGained) == gain + 2, "duplicate grant silent");
                Invoke(player, "ClearMotionRequests");
                Inject(input, 0); Invoke(player, "FixedUpdate");
                Check(backend.Count(AudioCue.PlayerDoubleJump) == jump + 1, "actual airborne double jump");
                Inject(input, 0); Invoke(player, "FixedUpdate");
                Check(backend.Count(AudioCue.PlayerDoubleJump) == jump + 1, "third jump silent");
                Inject(input, 3); Invoke(player, "FixedUpdate");
                Check(backend.Count(AudioCue.PlayerDash) == dash + 1, "actual dash start");
                Inject(input, 3); Invoke(player, "FixedUpdate");
                Check(backend.Count(AudioCue.PlayerDash) == dash + 1, "in-progress dash silent");
                int loss = backend.Count(AudioCue.AbilityLost);
                gain = backend.Count(AudioCue.AbilityGained);
                Check(state.TryRemoveLoadoutItem(LoadoutItemId.Tail), "remove owned ability");
                Check(backend.Count(AudioCue.AbilityLost) == loss + 1 && backend.Count(AudioCue.AbilityGained) == gain, "loss exactly once");
                Check(!state.TryRemoveLoadoutItem(LoadoutItemId.Tail) && backend.Count(AudioCue.AbilityLost) == loss + 1, "missing ability removal silent");
                Check(state.TryReplaceLoadoutItem(LoadoutItemId.Legs, LoadoutItemId.Arms), "atomic ability replacement");
                Check(backend.Count(AudioCue.AbilityLost) == loss + 2 && backend.Count(AudioCue.AbilityGained) == gain + 1, "replacement loss and gain once each");
                Check(!state.TryReplaceLoadoutItem(LoadoutItemId.Legs, LoadoutItemId.Tail)
                    && backend.Count(AudioCue.AbilityLost) == loss + 2 && backend.Count(AudioCue.AbilityGained) == gain + 1, "invalid replacement silent");
                Check(backend.LastPlayingId != 0, "Wwise valid playing ID");
                Debug.Log($"[RewardAudioTest] ALL PASS: {checks} checks; nine real chest flows, pointer UI, claim, gain, movement input-buffer consumption, Wwise ID={backend.LastPlayingId}.");
                EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                Debug.LogError("[RewardAudioTest] FAIL " + error);
                EditorApplication.update -= Tick;
                EditorApplication.ExitPlaymode();
            }
        }
        private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Inject(PlayerInputReader reader, int index)
        {
            ((bool[])Field(reader, "buffered"))[index] = true;
            ((float[])Field(reader, "expiresAt"))[index] = Time.unscaledTime + 1;
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }
    }
}
