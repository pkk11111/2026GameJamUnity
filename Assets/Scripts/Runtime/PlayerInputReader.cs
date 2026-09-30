// 职责：唯一 Input System 适配器；动态帧采样、限时缓冲、跨阶段清理。
// 模块/维护：controller，C01；直接依赖：Unity.InputSystem、IRunContext/IPlayerInput；GameBootstrap 显式注入运行上下文。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Regrowth.Runtime
{
    /// <summary>
    /// 私有运行副本不启停共享 UI 输入资产。禁止 gameplay 轮询 Keyboard 或第二个 PlayerInput。
    /// Move 连续值可在恢复后重新采样；按钮必须释放后再按，避免确认键泄漏至 gameplay。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerInput
    {
        [Header("输入资产及动作路径")]
        [SerializeField, Tooltip("必填；Input Actions 资产。按键在资产编辑器修改，初始化时复制为私有实例。")]
        private InputActionAsset inputActions;
        [SerializeField, Tooltip("Vector2 动作；只消费 x。重新初始化生效。")]
        private string moveActionPath = "Player/Move";
        [SerializeField, Tooltip("普通 Button，重新初始化生效。")]
        private string jumpActionPath = "Player/Jump";
        [SerializeField, Tooltip("普通 Button；权限由攻击组件验证。")]
        private string attackActionPath = "Player/Attack";
        [SerializeField, Tooltip("普通 Button，不能带 Hold；由唯一交互器消费。")]
        private string interactActionPath = "Player/Interact";
        [SerializeField, Tooltip("普通 Button；这里只发请求，不授予冲刺能力。")]
        private string dashActionPath = "Player/Dash";
        [SerializeField, Tooltip("系统按键，仅发布请求；正式暂停菜单策略另行接线。")]
        private string pauseActionPath = "System/Pause";

        [Header("缓冲")]
        [SerializeField, Min(0.01f), Tooltip("按钮请求有效期，真实秒；暂定 0.15，实时生效。每种动作最多一条。")]
        private float buttonBufferSeconds = 0.15f;

        private InputActionAsset runtimeActions;
        private InputAction move;
        private InputAction pause;
        private readonly InputAction[] buttons = new InputAction[4];
        private readonly float[] expiresAt = new float[4];
        private readonly bool[] buffered = new bool[4];
        private readonly bool[] blockedUntilRelease = new bool[4];
        private IRunContext run;
        private float moveX;
        private bool jumpHeld;
        private bool pauseBlocked;
        private int discardedFrame = -1;

        public bool IsInitialized => runtimeActions != null;
        public float MoveX => IsUsable ? moveX : 0f;
        public bool JumpHeld => IsUsable && jumpHeld;
        private bool IsUsable => IsInitialized && isActiveAndEnabled && run != null && run.IsGameplayActive;

        /// <summary>Update 中同步触发一次；由总控指定接收者决定如何处理。</summary>
        public event Action PauseRequested;

        // Bootstrap 可先于本组件 OnEnable 注入；运行采样仍要求 isActiveAndEnabled。
        internal bool Initialize(IRunContext context)
        {
            if (IsInitialized || (!enabled || !gameObject.activeInHierarchy) || context == null || inputActions == null
                || buttonBufferSeconds <= 0f || float.IsNaN(buttonBufferSeconds) || float.IsInfinity(buttonBufferSeconds)
                || InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                Debug.LogError("C01 PlayerInputReader 初始化失败：检查输入资产、组件启用、缓冲参数及 Dynamic Update 模式。", this);
                return false;
            }

            runtimeActions = Instantiate(inputActions);
            runtimeActions.name = inputActions.name + " (C01 runtime)";
            move = runtimeActions.FindAction(moveActionPath);
            buttons[0] = runtimeActions.FindAction(jumpActionPath);
            buttons[1] = runtimeActions.FindAction(attackActionPath);
            buttons[2] = runtimeActions.FindAction(interactActionPath);
            buttons[3] = runtimeActions.FindAction(dashActionPath);
            pause = runtimeActions.FindAction(pauseActionPath);

            if (move == null || move.type != InputActionType.Value || move.expectedControlType != "Vector2"
                || !ValidateButtons())
            {
                Debug.LogError("C01 PlayerInputReader 动作接线无效：Move 须 Vector2，所有按钮须无交互的不同 Button 动作。", this);
                Shutdown();
                return false;
            }

            run = context;
            run.PhaseChanged += OnPhaseChanged;
            move.Enable();
            foreach (InputAction button in buttons)
            {
                button.Enable();
            }
            pause.Enable();
            pauseBlocked = pause.IsPressed();
            DiscardGameplayInput();
            return true;
        }

        /// <summary>成功取走未过期请求；运动组件在 FixedUpdate 单次消费，失败不执行动作。</summary>
        public bool TryConsumeJump() => TryConsume(0);
        public bool TryConsumeAttack() => TryConsume(1);
        public bool TryConsumeInteract() => TryConsume(2);
        public bool TryConsumeDash() => TryConsume(3);

        public void DiscardGameplayInput()
        {
            moveX = 0f;
            jumpHeld = false;
            discardedFrame = Time.frameCount;
            for (int index = 0; index < buttons.Length; index++)
            {
                buffered[index] = false;
                blockedUntilRelease[index] = buttons[index] != null && buttons[index].enabled && buttons[index].IsPressed();
            }
        }

        internal void Shutdown()
        {
            if (run != null)
            {
                run.PhaseChanged -= OnPhaseChanged;
            }
            run = null;
            DiscardGameplayInput();
            if (runtimeActions != null)
            {
                runtimeActions.Disable();
                Destroy(runtimeActions);
                runtimeActions = null;
            }

            move = null;
            pause = null;
            Array.Clear(buttons, 0, buttons.Length);
        }

        private void Update()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (!pause.IsPressed())
            {
                pauseBlocked = false;
            }
            if (!pauseBlocked && pause.WasPressedThisFrame())
            {
                pauseBlocked = true;
                PauseRequested?.Invoke();
            }

            if (!IsUsable)
            {
                // 暂停时仍观察按住状态，恢复时不得消费暂停期间产生的按钮请求。
                DiscardGameplayInput();
                return;
            }

            if (Time.frameCount == discardedFrame)
            {
                return;
            }

            moveX = Mathf.Clamp(move.ReadValue<Vector2>().x, -1f, 1f);
            for (int index = 0; index < buttons.Length; index++)
            {
                InputAction button = buttons[index];
                if (blockedUntilRelease[index])
                {
                    if (!button.IsPressed())
                    {
                        blockedUntilRelease[index] = false;
                    }
                    continue;
                }

                if (button.WasPressedThisFrame())
                {
                    buffered[index] = true;
                    expiresAt[index] = Time.unscaledTime + buttonBufferSeconds;
                }
            }
            jumpHeld = !blockedUntilRelease[0] && buttons[0].IsPressed();
        }

        private bool TryConsume(int index)
        {
            if (!IsUsable || !buffered[index])
            {
                return false;
            }

            buffered[index] = false;
            return Time.unscaledTime <= expiresAt[index];
        }

        private bool ValidateButtons()
        {
            for (int index = 0; index < buttons.Length; index++)
            {
                if (!IsPlainButton(buttons[index]) || buttons[index] == move || buttons[index] == pause)
                {
                    return false;
                }
                for (int other = 0; other < index; other++)
                {
                    if (buttons[index] == buttons[other])
                    {
                        return false;
                    }
                }
            }
            return IsPlainButton(pause) && pause != move;
        }

        private static bool IsPlainButton(InputAction action)
        {
            if (action == null || action.type != InputActionType.Button || !string.IsNullOrEmpty(action.interactions))
            {
                return false;
            }
            foreach (InputBinding binding in action.bindings)
            {
                if (!string.IsNullOrEmpty(binding.interactions))
                {
                    return false;
                }
            }
            return true;
        }

        private void OnPhaseChanged(RunPhase nextPhase)
        {
            DiscardGameplayInput();
        }

        private void OnApplicationFocus(bool focused)
        {
            DiscardGameplayInput();
        }

        private void OnDisable()
        {
            Shutdown();
        }
    }
}
