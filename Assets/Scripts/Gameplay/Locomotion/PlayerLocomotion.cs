// 职责：基础水平移动、一次地面跳跃和唯一射线 Grounded；不管理阶段、生命或技能。
// 模块/维护：Soap / T06；依赖：IPlayerInput、IRunContext、IHealth、Rigidbody2D、GameAudio。
// 接线：Inspector 显式绑定唯一输入/阶段/生命及脚底锚点；只有本组件写本玩家运动速度。
// 交接：docs/handoffs/Soap.handoff；规范：根 AGENTS.md。
using System;
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerLocomotion : MonoBehaviour
    {
        [Header("必填接线（启用时读取）")]
        [SerializeField, Tooltip("实现 IPlayerInput 的唯一 PlayerInputReader；不添加第二个输入源。")]
        private MonoBehaviour inputSource;
        [SerializeField, Tooltip("实现 IRunContext 的唯一 RunController。")]
        private MonoBehaviour runSource;
        [SerializeField, Tooltip("实现 IHealth 的唯一 PlayerState，只读存活状态。")]
        private MonoBehaviour healthSource;
        [SerializeField, Tooltip("本玩家 Rigidbody2D；本组件是唯一速度写入者。")]
        private Rigidbody2D body;
        [SerializeField, Tooltip("脚底射线锚点；必填，玩家子节点，可与未来 Sprite/Animator 独立。")]
        private Transform groundOrigin;

        [Header("运动暂定参数（实时生效）")]
        [SerializeField, Min(0f), Tooltip("水平速度，单位/游戏秒；灰盒默认5。")]
        private float moveSpeed = 5f;
        [SerializeField, Min(0.01f), Tooltip("起跳竖直速度，单位/游戏秒；灰盒默认8，不叠加旧竖直速度。")]
        private float jumpVelocity = 8f;
        [SerializeField, Min(0.001f), Tooltip("实际水平位移速度阈值，单位/游戏秒；未来脚步条件默认0.05。")]
        private float movementThreshold = 0.05f;

        [Header("唯一地面射线（实时生效，下一次扫描读取）")]
        [SerializeField, Tooltip("可落地碰撞层；Trigger 与玩家自身仍会排除，不改全局Layer。")]
        private LayerMask groundLayers;
        [SerializeField, Tooltip("相对 groundOrigin 的本地偏移；默认三条脚底射线。")]
        private Vector2[] rayOffsets = { new Vector2(-0.38f, 0f), Vector2.zero, new Vector2(0.38f, 0f) };
        [SerializeField, Min(0.001f), Tooltip("向下射线长度，世界单位；灰盒默认0.12。")]
        private float rayLength = 0.12f;
        [SerializeField, Min(0.001f), Tooltip("扫描间隔，游戏秒；默认0.02，实际精度受FixedUpdate步长限制。")]
        private float groundScanInterval = 0.02f;
        [SerializeField, Range(0f, 1f), Tooltip("地面法线最小Y值；默认0.65，排除竖直墙面。")]
        private float minimumGroundNormalY = 0.65f;
        [SerializeField, Min(0f), Tooltip("允许判为落地的最大向上速度；默认0.1，防止起跳后脚底射线仍命中导致再跳。")]
        private float maximumGroundedUpwardSpeed = 0.1f;

        private IPlayerInput input;
        private IRunContext run;
        private IHealth health;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private Vector2 previousPosition;
        private float nextScanTime;
        private float horizontalSpeed;
        private bool ready;

        public bool IsGrounded { get; private set; }
        public float ActualHorizontalSpeed => ready && run.IsGameplayActive && health.IsAlive ? horizontalSpeed : 0f;
        /// <summary>未来脚步门控；不是播放频率或音频请求，不发未发布 Cue。</summary>
        public bool CanRequestFootsteps => ready && isActiveAndEnabled && run.IsGameplayActive && health.IsAlive
            && IsGrounded && Mathf.Abs(horizontalSpeed) >= movementThreshold;
        /// <summary>仅实际起跳后同步通知；OnEnable订阅/OnDisable退订；不得在事件中再次写运动速度。</summary>
        public event Action Jumped;

        private void OnEnable()
        {
            input = inputSource as IPlayerInput;
            run = runSource as IRunContext;
            health = healthSource as IHealth;
            if (input == null || run == null || health == null || body == null || body.gameObject != gameObject
                || groundOrigin == null || !ValidParameters())
            {
                Debug.LogError("[T06] PlayerLocomotion: check inputSource(IPlayerInput), runSource(IRunContext), healthSource(IHealth), local body, groundOrigin and ray/movement parameters.", this);
                enabled = false;
                return;
            }
            ready = true;
            run.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(run.Phase);
        }

        private void OnDisable()
        {
            if (run != null)
            {
                run.PhaseChanged -= OnPhaseChanged;
            }
            ready = false;
            IsGrounded = false;
            horizontalSpeed = 0f;
        }

        private bool ValidParameters()
        {
            return rayOffsets != null && rayOffsets.Length > 0 && groundLayers.value != 0
                && Positive(rayLength) && Positive(groundScanInterval) && Positive(jumpVelocity)
                && Positive(movementThreshold) && moveSpeed >= 0f && !float.IsNaN(moveSpeed) && !float.IsInfinity(moveSpeed);
        }

        private static bool Positive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnPhaseChanged(RunPhase phase)
        {
            // 暂停不写/清零物理速度；恢复保留运动，但重新采样位移，绝不累积暂停期间动作。
            previousPosition = body != null ? body.position : Vector2.zero;
            horizontalSpeed = 0f;
            nextScanTime = 0f;
        }

        private void FixedUpdate()
        {
            if (!ready || !run.IsGameplayActive || !health.IsAlive)
            {
                return;
            }
            horizontalSpeed = (body.position.x - previousPosition.x) / Time.fixedDeltaTime;
            previousPosition = body.position;
            if (Time.fixedTime >= nextScanTime)
            {
                ScanGround();
                nextScanTime = Time.fixedTime + groundScanInterval;
            }
            Vector2 velocity = body.linearVelocity;
            velocity.x = input.MoveX * moveSpeed;
            // 每个请求消费一次。空中请求丢弃，不在本模块排队到落地后补跳。
            bool requestedJump = input.TryConsumeJump();
            bool jumped = requestedJump && IsGrounded;
            if (jumped)
            {
                velocity.y = jumpVelocity;
                IsGrounded = false;
            }
            body.linearVelocity = velocity;
            if (jumped)
            {
                GameAudio.Play(AudioCue.PlayerJump, gameObject);
                Jumped?.Invoke();
            }
        }

        private void ScanGround()
        {
            IsGrounded = false;
            if (body.linearVelocity.y > maximumGroundedUpwardSpeed)
            {
                return;
            }
            var filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayers);
            filter.useTriggers = false;
            foreach (Vector2 offset in rayOffsets)
            {
                Vector2 origin = groundOrigin.TransformPoint(offset);
                int count = Physics2D.Raycast(origin, Vector2.down, filter, hits, rayLength);
                for (int i = 0; i < count; i++)
                {
                    Collider2D candidate = hits[i].collider;
                    if (candidate == null || candidate.isTrigger || candidate.attachedRigidbody == body
                        || candidate.transform.IsChildOf(transform) || hits[i].normal.y < minimumGroundNormalY)
                    {
                        continue;
                    }
                    IsGrounded = true;
                    return;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundOrigin == null || rayOffsets == null)
            {
                return;
            }
            foreach (Vector2 offset in rayOffsets)
            {
                Vector3 origin = groundOrigin.TransformPoint(offset);
                Gizmos.DrawLine(origin, origin + Vector3.down * rayLength);
            }
        }
    }
}
