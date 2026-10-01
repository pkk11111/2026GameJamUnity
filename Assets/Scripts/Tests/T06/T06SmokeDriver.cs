// 职责：T06 独立灰盒状态/阶段按钮与真实输入+物理 Play 检查，不是正式暂停/重开/伤害玩法。
// 模块/维护：Soap / T06；依赖：正式 C01/C02、PlayerLocomotion、Input System、uGUI/TMP。
// 接线：Editor 工具保存引用；测试注入现有 Keyboard 的设备事件，经正常 Update 到唯一 Reader。
// 交接：docs/handoffs/Soap.handoff；规范：根 AGENTS.md。不进正式构建。
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.T06
{
    public sealed class T06SmokeDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("必填：正式运动组件。")]
        private PlayerLocomotion motor;
        [SerializeField, Tooltip("必填：该玩家刚体。")]
        private Rigidbody2D body;
        [SerializeField, Tooltip("必填：真实C01/C02接线。")]
        private RunController run;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerState state;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button choosingButton;
        [SerializeField] private Button deathButton;
        [SerializeField] private Button checksButton;
        [SerializeField, Tooltip("地面自动检查起点；世界单位。")]
        private Vector2 testStart = new Vector2(-10f, 0.1f);
        [SerializeField, Tooltip("平台边缘接触检查点；世界单位。")]
        private Vector2 edgePoint = new Vector2(-1.72f, 2.77f);
        [SerializeField, Tooltip("靠墙空中检查点；世界单位。")]
        private Vector2 wallPoint = new Vector2(9f, 2.4f);
        private int jumps;
        private int passed;
        private int failed;
        private bool testing;
        private string summary = "Manual: A/D move, Space jump. Tests end in Dead; exit/re-enter Play.";
        private readonly object choiceOwner = new object();

        private void OnEnable()
        {
            if (motor == null || body == null || run == null || input == null || state == null || status == null
                || pauseButton == null || choosingButton == null || deathButton == null || checksButton == null)
            {
                Debug.LogError("[T06] Smoke driver missing Inspector reference.", this);
                enabled = false;
                return;
            }
            motor.Jumped += OnJumped;
            input.PauseRequested += TogglePause;
            pauseButton.onClick.AddListener(TogglePause);
            choosingButton.onClick.AddListener(ToggleChoosing);
            deathButton.onClick.AddListener(Kill);
            checksButton.onClick.AddListener(StartChecks);
        }

        private void OnDisable()
        {
            if (motor != null)
            {
                motor.Jumped -= OnJumped;
            }
            if (input != null)
            {
                input.PauseRequested -= TogglePause;
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(TogglePause);
            }
            if (choosingButton != null)
            {
                choosingButton.onClick.RemoveListener(ToggleChoosing);
            }
            if (deathButton != null)
            {
                deathButton.onClick.RemoveListener(Kill);
            }
            if (checksButton != null)
            {
                checksButton.onClick.RemoveListener(StartChecks);
            }
            if (testing && Keyboard.current != null)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }
        }

        private void OnJumped()
        {
            jumps++;
            Debug.Log("[T06 JUMP] actual jump=" + jumps, motor);
        }

        private void Update()
        {
            status.text = summary + "\nPhase=" + run.Phase + "  Grounded=" + motor.IsGrounded + "  Jump events=" + jumps
                + "\nPosition=" + body.position.ToString("F2") + "  actual horizontal speed=" + motor.ActualHorizontalSpeed.ToString("F2")
                + "  footstep condition=" + motor.CanRequestFootsteps + " (NO FOOTSTEP AUDIO)";
            pauseButton.interactable = !testing && (run.Phase == RunPhase.Playing || run.Phase == RunPhase.Paused);
            choosingButton.interactable = !testing && (run.Phase == RunPhase.Playing || run.Phase == RunPhase.Choosing);
            deathButton.interactable = !testing && run.IsGameplayActive && state.IsAlive;
            checksButton.interactable = !testing && run.IsGameplayActive && state.IsAlive;
        }

        private void TogglePause()
        {
            if (testing)
            {
                return;
            }
            if (run.Phase == RunPhase.Playing)
            {
                run.TryPause();
            }
            else if (run.Phase == RunPhase.Paused)
            {
                run.TryResume();
            }
        }

        private void ToggleChoosing()
        {
            if (testing)
            {
                return;
            }
            if (run.Phase == RunPhase.Playing)
            {
                run.TryBeginChoosing(choiceOwner);
            }
            else if (run.Phase == RunPhase.Choosing)
            {
                run.TryEndChoosing(choiceOwner);
            }
        }

        private void Kill()
        {
            state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Terrain));
        }

        private void StartChecks()
        {
            if (!testing && run.IsGameplayActive && Keyboard.current != null)
            {
                StartCoroutine(Verify());
            }
        }

        private void Check(bool condition, string label)
        {
            if (condition)
            {
                passed++;
                Debug.Log("[T06 PASS] " + label, this);
            }
            else
            {
                failed++;
                Debug.LogError("[T06 FAIL] " + label, this);
            }
        }

        private IEnumerator Keys(float seconds, params Key[] keys)
        {
            // 注入标准设备事件，不轮询设备、不建新设备/适配器，不直接伪造 MoveX 或消费结果。
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }

        private void Place(Vector2 point)
        {
            // 独测夹具定位，唯一正式运动写者仍是 motor；不提供正式传送/复活入口。
            body.position = point;
            body.linearVelocity = Vector2.zero;
            input.DiscardGameplayInput();
            Physics2D.SyncTransforms();
        }

        private IEnumerator Verify()
        {
            testing = true;
            passed = failed = 0;
            summary = "Running real Input -> FixedUpdate -> Physics checks...";
            float originalSpeed = Read("moveSpeed");
            float originalJump = Read("jumpVelocity");
            float originalLength = Read("rayLength");
            Place(testStart);
            yield return Keys(0.4f);
            Check(motor.IsGrounded, "2 flat ground via rays");
            Check(Mathf.Abs(Time.fixedDeltaTime - 0.02f) < 0.00001f, "G33 global fixed step unchanged at 0.02");
            float startX = body.position.x;
            yield return Keys(0.3f, Key.D);
            Check(body.position.x > startX + 0.5f && motor.CanRequestFootsteps, "1 right moves; actual grounded movement available for future footsteps");
            startX = body.position.x;
            yield return Keys(0.3f, Key.A);
            Check(body.position.x < startX - 0.5f, "1 left moves");
            yield return Keys(0.1f);
            Check(!motor.CanRequestFootsteps, "G34 stationary has no footstep condition");
            int before = jumps;
            yield return Keys(0.08f, Key.Space);
            Check(jumps == before + 1 && !motor.IsGrounded && body.linearVelocity.y > 0f, "2 grounded press causes one real jump");
            Check(!motor.CanRequestFootsteps, "G34 airborne no footsteps");
            yield return Keys(0.05f);
            yield return Keys(0.1f, Key.Space);
            Check(jumps == before + 1, "3 airborne second press cannot jump");
            yield return Keys(1.1f);
            Check(motor.IsGrounded, "4 lands reliably");
            yield return Keys(0.08f, Key.Space);
            Check(jumps == before + 2, "4 landed player jumps again once");
            yield return Keys(1.2f);

            Place(edgePoint);
            yield return Keys(0.15f);
            Check(motor.IsGrounded, "5 outer foot ray supports platform edge");
            yield return Keys(0.08f, Key.Space);
            Check(!motor.IsGrounded, "5 jump off edge clears Grounded");
            yield return Keys(0.05f);
            Place(wallPoint);
            yield return Keys(0.12f, Key.D);
            Check(!motor.IsGrounded && !motor.CanRequestFootsteps, "6 airborne wall not ground or footsteps");
            yield return Keys(0.9f, Key.D);
            Check(body.position.x < wallPoint.x + 0.1f && motor.IsGrounded && !motor.CanRequestFootsteps, "6 grounded wall blocks movement; holding direction no footsteps");
            yield return Keys(0.1f);
            Place(testStart);
            yield return Keys(0.3f);

            Check(run.TryPause(), "7 real C01 pause");
            Vector2 frozen = body.position;
            before = jumps;
            yield return Keys(0.2f, Key.D, Key.Space);
            Check(body.position == frozen && jumps == before && !motor.CanRequestFootsteps, "7 pause freezes physics/jump/footstep condition");
            Check(run.TryResume(), "8 real resume");
            yield return Keys(0.12f, Key.Space);
            Check(jumps == before, "8 held paused jump does not replay");
            yield return Keys(0.08f);
            Check(run.TryBeginChoosing(choiceOwner), "9 real choice lock");
            frozen = body.position;
            yield return Keys(0.2f, Key.A, Key.Space);
            Check(body.position == frozen && jumps == before && !motor.CanRequestFootsteps, "9 Choosing blocks movement/jump/footsteps");
            Check(run.TryEndChoosing(choiceOwner), "9 release same choice owner");
            yield return Keys(0.12f, Key.Space);
            Check(jumps == before, "9 choice input does not replay");
            yield return Keys(0.08f);

            // Inspector 对应序列化字段的暂定调参试验，测试后还原；不写共享配置。
            Write("moveSpeed", originalSpeed * 0.5f);
            startX = body.position.x;
            yield return Keys(0.4f, Key.D);
            Check(Mathf.Abs((body.position.x - startX) / 0.4f - originalSpeed * 0.5f) < 0.7f, "11 configured movement speed takes effect");
            yield return Keys(0.1f);
            Write("jumpVelocity", originalJump * 0.75f);
            before = jumps;
            yield return Keys(0.06f, Key.Space);
            Check(jumps == before + 1 && body.linearVelocity.y > 0f && body.linearVelocity.y < originalJump * 0.75f, "11 configured jump velocity takes effect");
            yield return Keys(1.1f);
            Place(testStart + Vector2.up * 0.3f);
            Write("rayLength", originalLength * 4f);
            yield return Keys(0.04f);
            Check(motor.IsGrounded, "11 configurable ray length observes ground from raised origin");
            Write("moveSpeed", originalSpeed);
            Write("jumpVelocity", originalJump);
            Write("rayLength", originalLength);
            Place(testStart);
            yield return Keys(0.3f);
            Kill();
            Check(run.Phase == RunPhase.Dead && !state.IsAlive, "10 real PlayerState death -> Bootstrap -> Dead");
            frozen = body.position;
            before = jumps;
            yield return Keys(0.2f, Key.D, Key.Space);
            Check(body.position == frozen && jumps == before && !motor.CanRequestFootsteps && input.MoveX == 0f, "10 Dead freezes physics and rejects movement/jump/footsteps");
            yield return Keys(0.05f);
            summary = "T06 CHECKS: " + passed + " passed / " + failed + " failed. Exit/re-enter Play for new test lifecycle.";
            Debug.Log("[T06 SUMMARY] " + summary, this);
            testing = false;
        }

        private float Read(string field) => (float)typeof(PlayerLocomotion).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor);
        private void Write(string field, float value) => typeof(PlayerLocomotion).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(motor, value);
    }
}
