#if UNITY_EDITOR
// 职责：两扇条件门真实接近/取消/确认与音频边沿验证；只在全新Play临时使用。
// 依赖Challenge/Runtime/UI/Audio/WhiteBox；维护audio-full-events；规范AGENTS.md。
// 交接docs/handoffs/audio-full-events.handoff；测试位置与刚体冻结不保存。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.Challenge;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using Regrowth.UI;
using UnityEngine;
using UnityEngine.UI;
namespace Regrowth.Tests.AudioCombat
{
    public sealed class InspectionAudioChecks : MonoBehaviour
    {
        public static InspectionAudioChecks Current { get; private set; }
        public bool Finished { get; private set; }
        public int Failed { get; private set; }
        public readonly List<string> Results = new List<string>();
        public static void StartChecks()
        {
            if (!Application.isPlaying || Current != null) throw new InvalidOperationException("Fresh Play required.");
            Current = new GameObject("Inspection audio checks - DO NOT SAVE").AddComponent<InspectionAudioChecks>();
            Current.gameObject.hideFlags = HideFlags.DontSave;
            Current.StartCoroutine(Current.Verify());
        }
        private IEnumerator Verify()
        {
            var state = FindFirstObjectByType<PlayerState>();
            var body = state.GetComponent<Rigidbody2D>();
            var panel = FindFirstObjectByType<ChoicePanel>();
            var backend = FindFirstObjectByType<RewardAudioBackend>();
            state.GetComponent<WhiteboxPlayer2D>().enabled = false;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            foreach (var contact in FindObjectsByType<EnemyContactAttack>(FindObjectsSortMode.None)) contact.enabled = false;
            try
            {
                yield return new WaitForSecondsRealtime(.3f);
                foreach (var gate in FindObjectsByType<InspectionDoor2D>(FindObjectsSortMode.None))
                {
                    var direction = Read<Vector2>(gate, "outsideDirection").normalized;
                    var blocker = Read<BoxCollider2D>(gate, "blocker");
                    float gap = blocker.size.x * Mathf.Abs(blocker.transform.lossyScale.x) * .5f
                        + state.GetComponent<Collider2D>().bounds.extents.x + .02f;
                    Vector2 near = (Vector2)blocker.transform.TransformPoint(blocker.offset) + direction * gap + Vector2.down * 1.75f;
                    Vector2 far = near + direction * 6;
                    Move(body, far); yield return new WaitForSecondsRealtime(.1f);
                    int count = backend.Count(AudioCue.DoorOpened);
                    Move(body, near); yield return new WaitForSecondsRealtime(.1f);
                    Check(panel.IsOpen && !gate.IsOpen, gate.name + " real approach opens confirmation only");
                    panel.CancelCurrent(); yield return new WaitForSecondsRealtime(.1f);
                    Check(backend.Count(AudioCue.DoorOpened) == count && !gate.IsOpen, gate.name + " cancel silent");
                    Move(body, far); yield return new WaitForSecondsRealtime(.1f);
                    Move(body, near); yield return new WaitForSecondsRealtime(.1f);
                    var cards = Read<List<ChoiceCardView>>(panel, "cards");
                    var button = Read<Button>(cards[0], "selectButton");
                    button.onClick.Invoke(); button.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
                    Check(gate.IsOpen && backend.Count(AudioCue.DoorOpened) == count + 1, gate.name + " actual open sounds once across repeated UI and updates");
                    Move(body, far); yield return new WaitForSecondsRealtime(.1f);
                    Check(gate.IsUnlocked && gate.IsOpen && backend.Count(AudioCue.DoorOpened) == count + 1, gate.name + " stays unlocked without repeated sound");
                }
            }
            finally
            {
                panel.CancelCurrent(); Finished = true;
                Debug.Log("[InspectionAudioChecks] " + (Results.Count - Failed) + "/" + Failed + "\n" + string.Join("\n", Results));
            }
        }
        private static void Move(Rigidbody2D body, Vector2 position) { body.position = position; Physics2D.SyncTransforms(); }
        private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private void Check(bool value, string label) { Results.Add((value ? "PASS " : "FAIL ") + label); if (!value) Failed++; }
    }
}
#endif
