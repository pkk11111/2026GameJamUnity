// 职责：T03真实InputSystem→C03→开关→门/物理独测及灰盒控制；不进入正式场景。
// 模块/维护：Soap / T03；依赖唯一Runtime/PlayerState及既有T06，不修改共享代码。
// 测试设备事件经过正式Reader/Interactor，不直接调用TryConsumeInteract；夹具定位仅测试，不是传送系统。
// 交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System.Collections;
using System.Reflection;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.T03
{
    public sealed class T03SmokeDriver : MonoBehaviour
    {
        [Header("真实接线与测试夹具")]
        [SerializeField] private RunController run;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerState state;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private WorldSwitch switchA;
        [SerializeField] private WorldSwitch switchB;
        [SerializeField] private WorldDoor doorA;
        [SerializeField] private WorldDoor doorB;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button choosingButton;
        [SerializeField] private Button checksButton;
        [SerializeField, Tooltip("true下次Play自动检查并最终Dead；false可手动AD/Space/E和Run Checks。")]
        private bool autoVerify;
        private readonly object choiceOwner = new object();
        private bool testing;
        private int passed;
        private int failed;
        private string summary = "A/D move, Space jump, E interact. Two independent switch/door pairs.";
        private int activations;
        private int openings;
        private Recorder recorder;

        private sealed class Recorder : IAudioBackend
        {
            public int SwitchCues { get; private set; }
            public int DoorCues { get; private set; }
            public void Play(AudioCue cue, GameObject emitter)
            {
                if (cue == AudioCue.SwitchActivated)
                {
                    SwitchCues++;
                }
                if (cue == AudioCue.DoorOpened)
                {
                    DoorCues++;
                }
            }
            public void StopAll(GameObject emitter) { }
        }

        private void OnEnable()
        {
            pauseButton.onClick.AddListener(TogglePause);
            choosingButton.onClick.AddListener(ToggleChoosing);
            checksButton.onClick.AddListener(StartChecks);
            switchA.Activated += OnActivated;
            doorA.Opened += OnOpened;
        }
        private void OnDisable()
        {
            pauseButton.onClick.RemoveListener(TogglePause);
            choosingButton.onClick.RemoveListener(ToggleChoosing);
            checksButton.onClick.RemoveListener(StartChecks);
            switchA.Activated -= OnActivated;
            doorA.Opened -= OnOpened;
            if (recorder != null)
            {
                GameAudio.UninstallBackend(recorder);
            }
            if (Keyboard.current != null && testing)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }
        }
        private void OnActivated() => activations++;
        private void OnOpened() => openings++;
        private IEnumerator Start()
        {
            yield return null;
            if (autoVerify)
            {
                StartChecks();
            }
        }
        private void Update()
        {
            status.text = summary + "\nPhase=" + run.Phase + " / Target=" + interactor.InteractionId + " / " + interactor.Prompt
                + "\nDoor A=" + doorA.IsOpen + " / Door B=" + doorB.IsOpen + " / A activations=" + activations + " / openings=" + openings;
            pauseButton.interactable = !testing && (run.Phase == RunPhase.Playing || run.Phase == RunPhase.Paused);
            choosingButton.interactable = !testing && (run.Phase == RunPhase.Playing || run.Phase == RunPhase.Choosing);
            checksButton.interactable = !testing && run.IsGameplayActive && !doorA.IsOpen && !doorB.IsOpen;
        }
        private void TogglePause()
        {
            if (run.Phase == RunPhase.Playing)
            {
                run.TryPause();
            }
            else
            {
                run.TryResume();
            }
        }
        private void ToggleChoosing()
        {
            if (run.Phase == RunPhase.Playing)
            {
                run.TryBeginChoosing(choiceOwner);
            }
            else
            {
                run.TryEndChoosing(choiceOwner);
            }
        }
        private void StartChecks()
        {
            if (!testing && run.IsGameplayActive && !doorA.IsOpen && !doorB.IsOpen && Keyboard.current != null)
            {
                StartCoroutine(Verify());
            }
        }
        private void Check(bool condition, string label)
        {
            if (condition)
            {
                passed++;
            }
            else
            {
                failed++;
            }
            Debug.Log($"[T03 CHECK {(condition ? "PASS" : "FAIL")}] {label}", this);
        }
        private IEnumerator Keys(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }
        private void Place(float x)
        {
            body.position = new Vector2(x, 0.02f);
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            Physics2D.SyncTransforms();
            input.DiscardGameplayInput();
        }
        private IEnumerator Verify()
        {
            testing = true;
            recorder = new Recorder();
            GameAudio.InstallBackend(recorder); // 只记录入口请求，不是Wwise声音验证。
            yield return Keys(0.1f);
            Check(run.IsGameplayActive && state.IsInitialized && interactor.IsInitialized, "unique real C01/C02/C03 initialized");
            Check(!doorA.IsOpen && !doorB.IsOpen && !switchA.IsActivated, "initial closed snapshot");
            string id = switchA.InteractionId;
            Check(id == "t03-switch-a" && switchB.InteractionId == "t03-switch-b" && id != switchB.InteractionId, "stable unique serialized IDs");
            Place(-9f);
            yield return Keys(0.12f);
            Check(!interactor.HasTarget, "outside interaction range");
            Place(-3f);
            yield return Keys(0.12f);
            Check(interactor.InteractionId == id, "real C03 selects switch in range");
            for (int i = 0; i < 20; i++)
            {
                switchA.CanInteract(state.gameObject);
            }
            Check(!doorA.IsOpen && activations == 0 && recorder.SwitchCues == 0 && recorder.DoorCues == 0, "CanInteract is side effect free");
            Check(switchA.GetComponentsInChildren<Collider2D>().Length == 2, "two colliders bridge one InteractionTarget");
            Place(-1.5f);
            yield return Keys(0.7f, Key.D);
            yield return Keys(0.12f);
            Check(body.position.x < -0.8f, "closed door physically blocks real locomotion");
            Place(-3f);
            yield return Keys(0.12f);
            yield return Keys(0.4f, Key.E);
            Check(doorA.IsOpen && switchA.IsActivated && activations == 1 && openings == 1, "real E -> Reader -> Interactor -> Switch -> Door once");
            Check(recorder.SwitchCues == 1 && recorder.DoorCues == 1, "one actual switch/door cue request (no sound claim)");
            Check(!switchA.CanInteract(state.gameObject) && !interactor.HasTarget, "used switch no longer selectable");
            yield return Keys(0.15f);
            yield return Keys(0.2f, Key.E);
            for (int i = 0; i < 10; i++)
            {
                switchA.TryInteract(state.gameObject);
                doorA.TryOpen();
            }
            Check(activations == 1 && openings == 1 && recorder.SwitchCues == 1 && recorder.DoorCues == 1, "repeat/hold/multiple colliders no repeat transition");
            yield return Keys(0.12f);
            Place(-1.5f);
            yield return Keys(0.8f, Key.D);
            yield return Keys(0.12f);
            Check(body.position.x > 1f, "opened door physically passable");
            Place(-9f);
            yield return Keys(0.12f);
            Place(-3f);
            yield return Keys(0.12f);
            doorA.enabled = false;
            doorA.enabled = true;
            switchA.enabled = false;
            switchA.enabled = true;
            Check(doorA.IsOpen && switchA.IsActivated && !switchA.CanInteract(state.gameObject), "leave/return and enable cycle preserve opened state");
            var doorField = typeof(WorldSwitch).GetField("door", BindingFlags.NonPublic | BindingFlags.Instance);
            switchB.enabled = false;
            doorField.SetValue(switchB, null);
            switchB.enabled = true; // 预期配置Warning带组件上下文，不产生Error。
            Check(!switchB.CanInteract(state.gameObject) && !switchB.TryInteract(state.gameObject) && !doorB.IsOpen, "missing Door safely rejects with contextual diagnostic");
            switchB.enabled = false;
            doorField.SetValue(switchB, doorB);
            switchB.enabled = true;
            Place(6f);
            yield return Keys(0.12f);
            Check(interactor.InteractionId == switchB.InteractionId, "restored explicit Door binding / second switch selected");
            Check(run.TryPause(), "real pause accepted");
            yield return Keys(0.2f, Key.E);
            Check(!doorB.IsOpen && !interactor.HasTarget && !switchB.TryInteract(state.gameObject), "Paused blocks real Interact and direct receiver");
            Check(run.TryResume(), "real resume accepted");
            yield return Keys(0.15f, Key.E);
            Check(!doorB.IsOpen, "held pause input does not replay on resume");
            yield return Keys(0.12f);
            Check(run.TryBeginChoosing(choiceOwner), "real Choosing accepted");
            yield return Keys(0.2f, Key.E);
            Check(!doorB.IsOpen && !interactor.HasTarget && !switchB.TryInteract(state.gameObject), "Choosing blocks real Interact");
            Check(run.TryEndChoosing(choiceOwner), "real Choosing ends");
            yield return Keys(0.15f, Key.E);
            Check(!doorB.IsOpen, "held choosing input does not replay");
            yield return Keys(0.12f);
            Check(state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Terrain)), "real PlayerState death accepted");
            yield return Keys(0.2f, Key.E);
            Check(run.Phase == RunPhase.Dead && !doorB.IsOpen && !interactor.HasTarget && !switchB.TryInteract(state.gameObject), "Dead blocks real Interact");
            yield return Keys(0.1f);
            Check(switchA.InteractionId == id && activations == 1 && openings == 1, "stable ID and one transition remain at end");
            GameAudio.UninstallBackend(recorder);
            recorder = null;
            testing = false;
            summary = $"T03 CHECKS: {passed} passed / {failed} failed. Exit Play to repeat; no restart system.";
            Debug.Log($"[T03 CHECK SUMMARY] {passed} passed / {failed} failed", this);
        }
    }
}
