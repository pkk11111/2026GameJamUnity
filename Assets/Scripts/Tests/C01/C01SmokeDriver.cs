// 职责：C01 独立场景的可视输入探针与真实 Input System 自动验收；不是 T06 运动组件。
// 模块/维护：controller；依赖：Runtime/Core、Input System、uGUI/TMP；不写 Rigidbody2D。
// 接线：显式绑定入口/运行/输入/文本和按钮；虚拟设备仅测试期间存在，停用时移除。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System.Collections;
using System.Collections.Generic;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.C01
{
    /// <summary>只在 C01_Smoke 使用；所有按键来自正式适配器，测试只向 Input System 排队设备状态。</summary>
    public sealed class C01SmokeDriver : MonoBehaviour
    {
        [Header("场景接线")]
        [SerializeField, Tooltip("必填，测试场景唯一入口。")] private GameBootstrap bootstrap;
        [SerializeField, Tooltip("必填，真实阶段组件。")] private RunController run;
        [SerializeField, Tooltip("必填，真实输入组件。")] private PlayerInputReader input;
        [SerializeField, Tooltip("必填，TMP 状态文本。")] private TMP_Text statusText;
        [SerializeField, Tooltip("必填，测试暂停/恢复按钮。")] private Button pauseButton;
        [SerializeField, Tooltip("必填，测试选择锁按钮，不执行奖励/费用。")] private Button choiceButton;
        [SerializeField, Tooltip("必填，测试入口启停按钮，不代表重开。")] private Button lifecycleButton;
        [SerializeField, Tooltip("必填，重跑自动检查按钮。")] private Button verifyButton;

        [Header("测试参数")]
        [SerializeField, Tooltip("进入 Play 自动检查；下一次进入 Play 生效。")] private bool autoVerify = true;
        [SerializeField, Min(0.01f), Tooltip("过期检查等待真实秒；须大于输入缓冲，默认 0.4，测试运行时生效。")]
        private float expiryWaitSeconds = 0.4f;

        private readonly object choiceOwner = new object();
        private readonly List<string> checks = new List<string>();
        private Gamepad testPad;
        private Keyboard testKeyboard;
        private bool verifying;
        private int jumpCount;
        private int attackCount;
        private int interactCount;
        private int dashCount;
        private int pauseRequestCount;
        private float physicsMove;
        private string report = "Not run";

        public int PassedChecks { get; private set; }
        public int FailedChecks { get; private set; }
        public bool IsVerifying => verifying;
        public string LastReport => report;
        public int JumpCount => jumpCount;
        public int PauseRequestCount => pauseRequestCount;

        private void OnEnable()
        {
            if (bootstrap == null || run == null || input == null || statusText == null
                || pauseButton == null || choiceButton == null || lifecycleButton == null || verifyButton == null)
            {
                Debug.LogError("C01SmokeDriver 接线不完整；检查 Inspector 的全部测试引用。", this);
                enabled = false;
                return;
            }
            input.PauseRequested += OnPauseRequested;
            pauseButton.onClick.AddListener(TogglePause);
            choiceButton.onClick.AddListener(ToggleChoice);
            lifecycleButton.onClick.AddListener(CycleBootstrap);
            verifyButton.onClick.AddListener(BeginVerification);
        }

        private void Start()
        {
            if (autoVerify)
            {
                BeginVerification();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            verifying = false;
            CleanupDevices();
            if (input != null)
            {
                input.PauseRequested -= OnPauseRequested;
            }
            if (pauseButton != null) { pauseButton.onClick.RemoveListener(TogglePause); }
            if (choiceButton != null) { choiceButton.onClick.RemoveListener(ToggleChoice); }
            if (lifecycleButton != null) { lifecycleButton.onClick.RemoveListener(CycleBootstrap); }
            if (verifyButton != null) { verifyButton.onClick.RemoveListener(BeginVerification); }
        }

        private void FixedUpdate()
        {
            if (verifying || !run.IsGameplayActive)
            {
                return;
            }
            physicsMove = input.MoveX;
            if (input.TryConsumeJump()) { jumpCount++; }
            if (input.TryConsumeAttack()) { attackCount++; }
            if (input.TryConsumeInteract()) { interactCount++; }
            if (input.TryConsumeDash()) { dashCount++; }
        }

        private void Update()
        {
            statusText.text = "GROWL AGAIN / C01 INPUT + RUN SMOKE\n"
                + "Command probe only - no player locomotion or reward/payment\n\n"
                + $"Phase: {run.Phase}    Active: {run.IsGameplayActive}    Time scale: {Time.timeScale:0.##}\n"
                + $"Bootstrap: {bootstrap.IsStarted}    Input ready: {input.IsInitialized}\n"
                + $"Move X: {input.MoveX:0.00}    FixedUpdate sample: {physicsMove:0.00}    Jump held: {input.JumpHeld}\n"
                + $"Consumed: Jump {jumpCount} / Attack {attackCount} / Interact {interactCount} / Dash {dashCount}\n"
                + $"Pause requests: {pauseRequestCount}\n\n"
                + "A/D or arrows: move | Space: jump | Enter/click: attack\n"
                + "E: interact (press) | Shift: dash request | Esc: TEST pause\n"
                + "Gamepad: stick / South / West / North / East / Start\n\n"
                + report;
        }

        /// <summary>测试按钮入口；自动检查期间忽略手动请求，不实现正式暂停菜单。</summary>
        public void TogglePause()
        {
            if (verifying) { return; }
            if (run.IsGameplayActive) { run.TryPause(); }
            else { run.TryResume(); }
        }

        public void ToggleChoice()
        {
            if (verifying) { return; }
            if (run.Phase == Regrowth.Core.RunPhase.Choosing) { run.TryEndChoosing(choiceOwner); }
            else { run.TryBeginChoosing(choiceOwner); }
        }

        public void CycleBootstrap()
        {
            if (verifying) { return; }
            bootstrap.enabled = false;
            bootstrap.enabled = true;
        }

        public void BeginVerification()
        {
            if (!verifying && bootstrap.IsStarted && run.IsGameplayActive)
            {
                StartCoroutine(Verify());
            }
        }

        private void OnPauseRequested()
        {
            pauseRequestCount++;
            TogglePause();
        }

        private IEnumerator Verify()
        {
            verifying = true;
            PassedChecks = 0;
            FailedChecks = 0;
            checks.Clear();
            report = "Verifying actual Input System events...";
            testPad = InputSystem.AddDevice<Gamepad>("C01SmokePad");
            testKeyboard = InputSystem.AddDevice<Keyboard>("C01SmokeKeyboard");
            try
            {
                yield return Sample(new GamepadState());
                Check(bootstrap.IsStarted && input.IsInitialized && run.IsGameplayActive && Time.timeScale > 0f,
                    "startup Playing with real dependencies");

                yield return Sample(new GamepadState { leftStick = new Vector2(0.75f, 0f) });
                Check(input.MoveX > 0.5f, "real Move.x sampled in Update");
                var allButtons = new GamepadState().WithButton(GamepadButton.South).WithButton(GamepadButton.West)
                    .WithButton(GamepadButton.North).WithButton(GamepadButton.East);
                yield return Sample(allButtons);
                Check(input.TryConsumeJump() && !input.TryConsumeJump(), "Jump buffered and consumed once");
                Check(input.TryConsumeAttack() && !input.TryConsumeAttack(), "Attack consumed once");
                Check(input.TryConsumeInteract() && !input.TryConsumeInteract(), "Interact press consumed once");
                Check(input.TryConsumeDash() && !input.TryConsumeDash(), "Dash request consumed once");
                yield return Sample(new GamepadState());
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.E));
                yield return null;
                Check(input.TryConsumeInteract(), "keyboard E immediate, no Hold");
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;

                // 排队一次完整短点击；即便当前已释放，WasPressedThisFrame 仍能捕获边缘。
                InputSystem.QueueStateEvent(testPad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.QueueStateEvent(testPad, new GamepadState());
                yield return null;
                Check(input.TryConsumeJump(), "press/release in same dynamic update retained");

                yield return Sample(new GamepadState().WithButton(GamepadButton.South));
                Check(run.TryPause() && !input.TryConsumeJump() && input.MoveX == 0f && !input.JumpHeld,
                    "pause clears pending actions immediately");
                Check(!run.TryPause() && Time.timeScale == 0f, "repeat pause rejected and time frozen");
                yield return Sample(allButtons);
                Check(!input.TryConsumeJump() && !input.TryConsumeAttack() && !input.TryConsumeInteract()
                    && !input.TryConsumeDash(), "inactive gameplay rejects every action");
                Check(run.TryResume() && !run.TryResume(), "resume accepted once");
                yield return null;
                Check(!input.TryConsumeJump() && !input.TryConsumeAttack(), "held buttons do not leak on resume");
                yield return Sample(new GamepadState());
                yield return Sample(new GamepadState().WithButton(GamepadButton.South));
                Check(input.TryConsumeJump(), "released then pressed jump works after resume");

                yield return Sample(new GamepadState());
                Check(run.TryBeginChoosing(choiceOwner) && Time.timeScale == 0f, "choice lock enters Choosing");
                Check(!run.TryBeginChoosing(new object()) && !run.TryResume() && !run.TryEndChoosing(new object()),
                    "duplicate/wrong owner cannot bypass choice lock");
                yield return Sample(allButtons);
                Check(!input.TryConsumeJump() && input.MoveX == 0f, "choice blocks gameplay");
                Check(run.TryEndChoosing(choiceOwner) && !run.TryEndChoosing(choiceOwner), "owner releases once");
                yield return Sample(new GamepadState());
                yield return Sample(new GamepadState().WithButton(GamepadButton.South));
                input.DiscardGameplayInput();
                Check(!input.TryConsumeJump(), "explicit discard removes pre-teleport request");
                yield return null;
                Check(!input.TryConsumeJump(), "discard suppresses held button");

                yield return Sample(new GamepadState());
                yield return Sample(new GamepadState().WithButton(GamepadButton.South));
                yield return new WaitForSecondsRealtime(expiryWaitSeconds);
                Check(!input.TryConsumeJump(), "unconsumed buffer expires in real time");

                yield return Sample(new GamepadState());
                int pauseBefore = pauseRequestCount;
                yield return Sample(new GamepadState().WithButton(GamepadButton.Start));
                yield return null;
                Check(pauseRequestCount == pauseBefore + 1 && run.IsGameplayActive, "system request emitted once, no runtime policy");
                yield return Sample(new GamepadState());
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    bootstrap.enabled = false;
                    Check(!bootstrap.IsStarted && !input.IsInitialized && !run.IsGameplayActive && Time.timeScale == 1f,
                        "shutdown releases inputs/time cycle " + cycle);
                    bootstrap.enabled = true;
                    yield return null;
                    Check(bootstrap.IsStarted && input.IsInitialized && run.IsGameplayActive,
                        "restart wiring cycle " + cycle);
                    pauseBefore = pauseRequestCount;
                    yield return Sample(new GamepadState().WithButton(GamepadButton.Start));
                    Check(pauseRequestCount == pauseBefore + 1, "no duplicated pause subscriptions cycle " + cycle);
                    yield return Sample(new GamepadState());
                }

                // 退出独测后才允许探针 FixedUpdate 消费，真实验证帧缓冲到物理帧的接线。
                verifying = false;
                int jumpsBefore = jumpCount;
                yield return Sample(new GamepadState().WithButton(GamepadButton.South));
                yield return new WaitForFixedUpdate();
                yield return null;
                Check(jumpCount == jumpsBefore + 1, "real command consumed once by FixedUpdate probe");
                verifying = true;
                yield return Sample(new GamepadState());
                report = $"AUTO CHECKS: {PassedChecks} passed / {FailedChecks} failed\n"
                    + "Virtual devices removed. Manual probe and buttons are ready.";
                Debug.Log("[C01] " + report + "\n" + string.Join("\n", checks), this);
            }
            finally
            {
                verifying = false;
                CleanupDevices();
                input.DiscardGameplayInput();
            }
        }

        private IEnumerator Sample(GamepadState state)
        {
            InputSystem.QueueStateEvent(testPad, state);
            yield return null;
        }

        private void Check(bool passed, string label)
        {
            if (passed) { PassedChecks++; }
            else { FailedChecks++; }
            checks.Add((passed ? "PASS " : "FAIL ") + label);
        }

        private void CleanupDevices()
        {
            if (testPad != null && testPad.added) { InputSystem.RemoveDevice(testPad); }
            if (testKeyboard != null && testKeyboard.added) { InputSystem.RemoveDevice(testKeyboard); }
            testPad = null;
            testKeyboard = null;
        }
    }
}
