// 职责：按玩家实际显示的2D移动接触帧发声；复用唯一运动组件的落地结果，不写运动/身体状态。
// 维护 audio-player-movement；依赖显式 PlayerState/Run/Whitebox/Presentation/Audio.Core。
// 交接 docs/handoffs/audio-player-movement.handoff；规范 AGENTS.md。参数实时生效，绑定在重新启用时读取。
using System;
using Regrowth.Core;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Presentation;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Audio
{
    /// <summary>Samples after the sprite presenter. Consumes suppressed frames too, never replays missed steps.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class PlayerMovementAudio : MonoBehaviour
    {
        [Serializable]
        public sealed class ContactTiming
        {
            [Tooltip("实际Move动画Clip；与表现配置中的引用一致。")]
            public AnimationClip clip;
            [Tooltip("接触帧，从0开始；仅当画面实际显示这些帧时发声，不补播跳过的帧。")]
            public int[] frames = { 0 };
        }

        [Header("显式玩家引用（重新启用生效）")]
        [SerializeField] private PlayerState state;
        [SerializeField] private RunController run;
        [SerializeField] private WhiteboxPlayer2D motor;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerCharacterPresentation presentation;
        [Header("接触帧与节奏（实时生效）")]
        [SerializeField, Tooltip("每个实际Move Clip的接触帧；缺配置时静默，不猜测步点。")]
        private ContactTiming[] contactTimings = Array.Empty<ContactTiming>();
        [SerializeField, Min(0.01f), Tooltip("实际水平位移速度阈值，单位/秒；顶墙虽有输入也不发声。")]
        private float minimumMoveSpeed = 0.1f;
        [SerializeField, Min(0.01f), Tooltip("无腿移动音最小间隔，游戏秒；暂定0.48，按素材试听微调。")]
        private float noFeetMinimumInterval = 0.48f;
        [SerializeField, Min(0.01f), Tooltip("有腿脚步最小间隔，游戏秒；暂定0.42，按素材试听微调。")]
        private float footstepMinimumInterval = 0.42f;

        private Vector2 previousPosition;
        private bool hasPosition;
        private float actualSpeed;
        private double lastPhysicsSample;
        private double nextSoundAt;
        private PlayerAnimationEntry observedAnimation;
        private int observedSequence = -1;

        public bool IsWired => state != null && run != null && motor != null && body != null
            && input != null && presentation != null;

        private void OnEnable()
        {
            if (!IsWired)
            {
                Debug.LogError("[MovementAudio] Bind state/run/motor/body/input/presentation on the player.", this);
                enabled = false;
                return;
            }
            motor.Relocated += ResetSampling;
            state.LoadoutChanged += ResetSampling;
            state.BodyChanged += ResetSampling;
            run.PhaseChanged += OnPhaseChanged;
            ResetSampling();
        }

        private void OnDisable()
        {
            if (motor != null) motor.Relocated -= ResetSampling;
            if (state != null)
            {
                state.LoadoutChanged -= ResetSampling;
                state.BodyChanged -= ResetSampling;
            }
            if (run != null) run.PhaseChanged -= OnPhaseChanged;
            ResetSampling();
        }

        private void OnPhaseChanged(RunPhase phase) => ResetSampling();

        private void ResetSampling()
        {
            hasPosition = false;
            actualSpeed = 0f;
            lastPhysicsSample = double.NegativeInfinity;
            observedAnimation = null;
            observedSequence = -1;
            // Keep the minimum interval across rapid stop/start, card swaps and teleports.
        }

        private void FixedUpdate()
        {
            if (!IsWired) return;
            Vector2 position = body.position;
            actualSpeed = hasPosition ? Mathf.Abs(position.x - previousPosition.x) / Time.fixedDeltaTime : 0f;
            previousPosition = position;
            hasPosition = true;
            lastPhysicsSample = Time.timeAsDouble;
        }

        private void LateUpdate()
        {
            if (!IsWired) return;
            PlayerAnimationEntry animation = presentation.CurrentAnimation;
            int sequence = presentation.CurrentFrameSequence;
            bool changed = observedAnimation != animation || observedSequence != sequence;
            observedAnimation = animation;
            observedSequence = sequence;
            if (!changed || !state.IsAlive || !run.IsGameplayActive || !motor.IsGameplayActive
                || !motor.IsGrounded || motor.IsDashing || motor.HasPendingRelocation || !body.simulated
                || !presentation.isActiveAndEnabled || presentation.MissingAction
                || presentation.CurrentAction != PlayerVisualAction.Move
                || actualSpeed < minimumMoveSpeed || Mathf.Abs(input.MoveX) < 0.01f
                || Time.timeAsDouble - lastPhysicsSample > Time.fixedDeltaTime * 2.5
                || Time.timeAsDouble < nextSoundAt || !IsContactFrame(animation, presentation.CurrentFrameIndex))
            {
                return;
            }
            bool hasLegs = state.Contains(LoadoutItemId.Legs);
            nextSoundAt = Time.timeAsDouble + Mathf.Max(0.01f, hasLegs ? footstepMinimumInterval : noFeetMinimumInterval);
            GameAudio.Play(hasLegs ? AudioCue.PlayerFootstep : AudioCue.PlayerMoveNoFeet, motor.gameObject);
        }

        private bool IsContactFrame(PlayerAnimationEntry animation, int frame)
        {
            if (animation == null || animation.clip == null || contactTimings == null) return false;
            foreach (ContactTiming timing in contactTimings)
            {
                if (timing == null || timing.clip != animation.clip || timing.frames == null) continue;
                return Array.IndexOf(timing.frames, frame) >= 0;
            }
            return false;
        }
    }
}
