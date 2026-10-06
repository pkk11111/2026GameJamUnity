// 职责：Level 白板跑跳、二段跳、冲刺、击退与试走回位；唯一 Rigidbody2D 写入者。
// 模块/维护：controller / Level 适配；依赖：Core、Runtime 集中输入/阶段、Audio Core。
// 接线：绑定 inputReader/runController；能力开关仅为用户保留的白板测试配置，只在useLoadoutAbilities关闭时覆盖权限。
// 交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。所有调参实时读取，出生点首次 Awake 记录。
using System;
using System.Collections.Generic;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    /// <summary>保留原运动参数与测试技能；业务不得直接写本刚体。暂停清请求，保留已发生的物理状态。</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-180)]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class WhiteboxPlayer2D : MonoBehaviour, Regrowth.Gameplay.IPlayerBaseMoveSpeedProvider
    {
        [Header("总控接线")]
        [SerializeField, Tooltip("必填；唯一输入适配器，含白板可选 Reset 动作。")]
        private PlayerInputReader inputReader;
        [SerializeField, Tooltip("必填；唯一运行阶段，不在本组件修改时间倍率。")]
        private RunController runController;
        [SerializeField, Tooltip("读取唯一构筑决定腿/尾权限；关闭后才使用下方旧白板技能开关。")]
        private bool useLoadoutAbilities = true;
        [SerializeField, Tooltip("读取模式必填，唯一PlayerState；不维护第二份能力状态。")]
        private PlayerState playerState;
        [SerializeField, Tooltip("主图显式绑定唯一朝向；旧独测未绑定时才保留原输入朝向。")]
        private Regrowth.Gameplay.PlayerFacing2D facingSource;
        [SerializeField] private Regrowth.Gameplay.PlayerActionGate actionGate;
        private bool DoubleJumpEnabled => useLoadoutAbilities
            ? playerState != null && playerState.Contains(LoadoutItemId.Legs) : enableDoubleJump;
        private bool DashEnabled => useLoadoutAbilities
            ? playerState != null && playerState.Contains(LoadoutItemId.Tail) : enableDash;

        [Header("移动（单位/秒、单位、重力倍率；实时生效）")]
        [SerializeField, Min(0f), Tooltip("水平速度；保留地图作者保存值。")]
        private float moveSpeed = 5f;
        [SerializeField, Min(0.1f), Tooltip("首次跳跃脚底上升高度，单位。")]
        private float jumpHeight = 3f;
        [SerializeField, Min(0.1f), Tooltip("物理重力倍率。")]
        private float gravityScale = 2f;

        [Header("仅白板：二段跳")]
        [SerializeField, Tooltip("旧试走开关；读取构筑时无效，正式二段跳来自腿。")]
        private bool enableDoubleJump;
        [SerializeField, Min(0.1f), Tooltip("第二跳相对起跳点上升高度，单位。")]
        private float doubleJumpHeight = 3f;

        [Header("仅白板：冲刺")]
        [SerializeField, Tooltip("测试开关；不表示正式玩家开局有 Dash。")]
        private bool enableDash = true;
        [SerializeField, Tooltip("每次离地允许一次空中冲刺，保留原白板行为。")]
        private bool allowAirDash = true;
        [SerializeField, Min(0.1f), Tooltip("无阻挡水平冲刺距离，单位。")]
        private float dashDistance = 3f;
        [SerializeField, Min(0.02f), Tooltip("冲刺时长，游戏秒。")]
        private float dashDuration = 0.15f;
        [SerializeField, Min(0f), Tooltip("冲刺结束后的冷却，游戏秒。")]
        private float dashCooldown = 0.35f;

        [Header("地面射线与跳跃辅助（实时生效）")]
        [SerializeField, Tooltip("地面层；自动排除自身刚体和 Trigger。")]
        private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.001f), Tooltip("脚底以下检测距离，单位。")]
        private float groundCheckDistance = 0.04f;
        [SerializeField, Min(0.001f), Tooltip("G33 地面扫描周期，游戏秒；默认 0.02。")]
        private float groundScanInterval = 0.02f;
        [SerializeField, Min(0f), Tooltip("射线起点在脚底上方的距离，单位。")]
        private float rayStartOffset = 0.02f;
        [SerializeField, Range(0f, 1f), Tooltip("左右射线相对半宽的比例，避免脚底边缘漏检。")]
        private float raySpread = 0.9f;
        [SerializeField, Range(0f, 1f), Tooltip("可站立表面的最小法线 Y 分量。")]
        private float minimumGroundNormal = 0.65f;
        [SerializeField, Min(0f), Tooltip("离开平台后仍允许首跳的游戏秒。")]
        private float coyoteTime = 0.08f;
        [SerializeField, Min(0.02f), Tooltip("已消费跳跃请求等待落地的游戏秒。")]
        private float jumpBufferTime = 0.12f;
        [SerializeField, Min(0f), Tooltip("起跳后短暂忽略地面，游戏秒。")]
        private float takeoffGroundLock = 0.1f;

        private Rigidbody2D body;
        private BoxCollider2D playerCollider;
        private PhysicsMaterial2D originalMaterial;
        private PhysicsMaterial2D testMaterial;
        private float originalGravityScale;
        private Vector2 startPosition;
        private float startRotation;
        private readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>();
        private float facing = 1f;
        private float jumpBuffer;
        private float coyoteRemaining;
        private float groundLock;
        private float scanRemaining;
        private bool groundSample;
        private float dashRemaining;
        private float dashSpeed;
        private float dashDirection;
        private float cooldownRemaining;
        private bool dashing;
        private bool airDashUsed;
        private bool doubleJumpUsed;
        private float knockbackRemaining;
        private float spikeProtectionRemaining;
        private bool relocationPending;
        private Vector2 relocationPosition;
        private bool relocationIsReset;
        private Func<Func<bool>, bool> paidRelocation;
        private Action<bool> paidFinished;
        [SerializeField, Range(0.001f, 0.1f), Tooltip("落点实体检测收缩量，避免贴地接触被误判嵌入；单位。")]
        private float landingSkin = 0.02f;
        private readonly List<Collider2D> landingHits = new List<Collider2D>();
        private bool knockbackPending;
        private Vector2 knockbackVelocity;

        public bool IsGameplayActive => isActiveAndEnabled && body != null && inputReader != null
            && inputReader.IsInitialized && runController != null && runController.IsGameplayActive;
        public bool IsGrounded { get; private set; }
        public bool IsDashing => dashing;
        /// <summary>AI读取唯一基础速度；不包含冲刺/击退，不复制配置。</summary>
        public float BaseMoveSpeed => moveSpeed;
        /// <summary>实际R回位/传送完成后主线程通知；入口许可订阅者OnEnable注册、OnDisable退订，不重复执行迁移。</summary>
        public event Action Relocated;

        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public bool HasPendingRelocation => relocationPending;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<BoxCollider2D>();
            startPosition = body.position;
            startRotation = body.rotation;
            originalGravityScale = body.gravityScale;
            originalMaterial = playerCollider.sharedMaterial;
            testMaterial = new PhysicsMaterial2D("Whitebox Player - Runtime")
            {
                friction = 0f,
                bounciness = 0f,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void OnEnable()
        {
            if (inputReader == null || runController == null || (useLoadoutAbilities && playerState == null))
            {
                Debug.LogError("Level WhiteboxPlayer2D 缺少 inputReader/runController/playerState 引用。", this);
                enabled = false;
                return;
            }
            playerCollider.sharedMaterial = testMaterial;
            runController.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(runController.Phase);
        }

        private void FixedUpdate()
        {
            if (!IsGameplayActive)
            {
                return;
            }
            if (paidRelocation != null)
            {
                var commit = paidRelocation;
                var completed = paidFinished;
                paidRelocation = null;
                paidFinished = null;
                Vector2 target = relocationPosition;
                bool moved = false;
                bool success = CanLandAt(target) && commit(() =>
                {
                    ClearMotionRequests();
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.position = target;
                    body.gravityScale = gravityScale;
                    body.WakeUp();
                    moved = true;
                    return true;
                });
                relocationPending = false;
                if (success && moved)
                {
                    try { Relocated?.Invoke(); }
                    catch (Exception exception) { Debug.LogException(exception, this); }
                    GameAudio.Play(AudioCue.Teleported, gameObject);
                }
                completed?.Invoke(success && moved);
                return;
            }
            if (inputReader.TryConsumePrototypeReset())
            {
                QueueRelocation(startPosition, true);
            }
            if (relocationPending)
            {
                ApplyRelocation();
                return;
            }

            float dt = Time.fixedDeltaTime;
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
            groundLock = Mathf.Max(0f, groundLock - dt);
            spikeProtectionRemaining = Mathf.Max(0f, spikeProtectionRemaining - dt);
            if (knockbackPending)
            {
                body.gravityScale = gravityScale;
                body.linearVelocity = knockbackVelocity;
                body.WakeUp();
                knockbackPending = false;
            }
            if (knockbackRemaining > 0f)
            {
                knockbackRemaining = Mathf.Max(0f, knockbackRemaining - dt);
                body.gravityScale = gravityScale;
                jumpBuffer = 0f;
                inputReader.TryConsumeJump();
                inputReader.TryConsumeDash();
                IsGrounded = false;
                return;
            }

            float moveInput = inputReader.MoveX;
            if (facingSource != null)
            {
                facing = facingSource.FacingSign;
            }
            else if (moveInput != 0f)
            {
                facing = Mathf.Sign(moveInput);
            }
            if (inputReader.TryConsumeJump())
            {
                jumpBuffer = jumpBufferTime;
            }
            bool requestDash = inputReader.TryConsumeDash();
            if (dashing && (dashRemaining <= 0f || !DashEnabled))
            {
                dashing = false;
                body.linearVelocity = Vector2.zero;
            }

            scanRemaining -= dt;
            if (scanRemaining <= 0.000001f)
            {
                groundSample = CheckGround();
                scanRemaining = Mathf.Max(0.001f, groundScanInterval);
            }
            IsGrounded = groundLock <= 0f && body.linearVelocity.y <= 0.05f && groundSample;
            if (!dashing && IsGrounded)
            {
                coyoteRemaining = coyoteTime;
                airDashUsed = false;
                doubleJumpUsed = false;
            }
            else
            {
                coyoteRemaining = Mathf.Max(0f, coyoteRemaining - dt);
            }

            if (!dashing)
            {
                body.gravityScale = gravityScale;
                Vector2 velocity = body.linearVelocity;
                velocity.x = moveInput * moveSpeed;
                bool canGroundJump = IsGrounded || coyoteRemaining > 0f;
                bool canDoubleJump = DoubleJumpEnabled && !doubleJumpUsed;
                if (jumpBuffer > 0f && (canGroundJump || canDoubleJump))
                {
                    bool isDoubleJump = !canGroundJump;
                    float height = isDoubleJump ? doubleJumpHeight : jumpHeight;
                    float gravity = Mathf.Abs(Physics2D.gravity.y * gravityScale);
                    velocity.y = Mathf.Sqrt(2f * gravity * Mathf.Max(0.1f, height)) + gravity * dt * 0.5f;
                    if (isDoubleJump)
                    {
                        doubleJumpUsed = true;
                    }
                    jumpBuffer = coyoteRemaining = 0f;
                    groundLock = takeoffGroundLock;
                    IsGrounded = false;
                    GameAudio.Play(isDoubleJump ? AudioCue.PlayerDoubleJump : AudioCue.PlayerJump, gameObject);
                }
                body.linearVelocity = velocity;
                if (requestDash && DashEnabled && cooldownRemaining <= 0f
                    && (IsGrounded || (allowAirDash && !airDashUsed))
                    && (actionGate == null || actionGate.TryBegin(this, Mathf.Max(0.02f, dashDuration))))
                {
                    dashing = true;
                    airDashUsed = true;
                    dashRemaining = Mathf.Max(0.02f, dashDuration);
                    dashSpeed = dashDistance / dashRemaining;
                    dashDirection = facing;
                    cooldownRemaining = dashRemaining + dashCooldown;
                    coyoteRemaining = 0f;
                    GameAudio.Play(AudioCue.PlayerDash, gameObject);
                }
            }
            if (dashing)
            {
                float step = Mathf.Min(dt, dashRemaining);
                body.gravityScale = 0f;
                body.linearVelocity = new Vector2(dashDirection * dashSpeed * step / dt, 0f);
                dashRemaining = Mathf.Max(0f, dashRemaining - step);
            }
            jumpBuffer = Mathf.Max(0f, jumpBuffer - dt);
        }

        /// <summary>
        /// 地刺伤害与击退入口。先验证原有保护/迁移/参数，再通过唯一PlayerState扣Terrain伤害。
        /// true表示实际扣血；存活者排队原击退，致命命中交给既有死亡流程。拒绝无扣血/击退。
        /// </summary>
        public bool TrySpikeHit(Vector2 velocity, float controlLock, float protectionTime, int damage, GameObject source)
        {
            if (playerState == null || damage <= 0 || !CanAcceptSpikeKnockback(velocity, controlLock, protectionTime))
            {
                return false;
            }
            if (!playerState.TryTakeDamage(new DamageRequest(damage, DamageKind.Terrain, source)))
            {
                return false;
            }
            if (playerState.IsAlive)
            {
                QueueSpikeKnockback(velocity, controlLock, protectionTime);
            }
            return true;
        }

        /// <summary>正式地刺排队，true仅为候选接收；仲裁实际扣血且存活才排击退，败选无音频/击退。</summary>
        public bool TryQueueSpikeHit(Vector2 velocity, float controlLock, int damage,
            MonoBehaviour source, string sourceId, Action<DamageRequest> onApplied)
        {
            if (playerState == null || damage <= 0 || source == null
                || !CanAcceptSpikeKnockback(velocity, controlLock, 0f))
            {
                return false;
            }
            return playerState.TryQueueContactDamage(new DamageRequest(damage, DamageKind.Terrain, source.gameObject),
                source, sourceId, request =>
                {
                    if (playerState.IsAlive)
                    {
                        QueueSpikeMotion(velocity, controlLock);
                    }
                    onApplied?.Invoke(request);
                });
        }

        /// <summary>兼容旧白板纯击退；正式地刺使用TryQueueSpikeHit。物理写入仍在下一FixedUpdate。</summary>
        public bool TrySpikeKnockback(Vector2 velocity, float controlLock, float protectionTime)
        {
            if (!CanAcceptSpikeKnockback(velocity, controlLock, protectionTime))
            {
                return false;
            }
            QueueSpikeKnockback(velocity, controlLock, protectionTime);
            return true;
        }

        private bool CanAcceptSpikeKnockback(Vector2 velocity, float controlLock, float protectionTime)
        {
            return IsGameplayActive && !relocationPending && spikeProtectionRemaining <= 0f
                && (playerState == null || !playerState.HasDamageProtection)
                && Finite(velocity.x) && Finite(velocity.y) && Finite(controlLock) && Finite(protectionTime)
                && controlLock >= 0f && protectionTime >= 0f;
        }

        private void QueueSpikeKnockback(Vector2 velocity, float controlLock, float protectionTime)
        {
            QueueSpikeMotion(velocity, controlLock);
            spikeProtectionRemaining = Mathf.Max(knockbackRemaining, protectionTime);
        }

        private void QueueSpikeMotion(Vector2 velocity, float controlLock)
        {
            dashing = false;
            dashRemaining = jumpBuffer = coyoteRemaining = 0f;
            groundLock = Mathf.Max(takeoffGroundLock, controlLock);
            knockbackRemaining = Mathf.Max(Time.fixedDeltaTime, controlLock);
            knockbackVelocity = velocity;
            knockbackPending = true;
        }


        /// <summary>仅白板免费迁移；true 表示排队接受，下一物理帧移动。无扣费/安全落点/抵达免疫承诺。</summary>
        public bool TryTeleportTo(Vector2 destination)
        {
            return QueueRelocation(destination, false);
        }

        /// <summary>无写入安全落点检查；排除自身与Trigger，使用当前玩家稳定实体形状及地形层。</summary>
        public bool CanLandAt(Vector2 destination)
        {
            if (!isActiveAndEnabled || body == null || playerCollider == null || !playerCollider.enabled
                || !body.simulated || !Finite(destination.x) || !Finite(destination.y))
            {
                return false;
            }
            Physics2D.SyncTransforms();
            Bounds bounds = playerCollider.bounds;
            Vector2 center = destination + (Vector2)bounds.center - body.position;
            Vector2 size = new Vector2(Mathf.Max(0.001f, bounds.size.x - landingSkin * 2f), Mathf.Max(0.001f, bounds.size.y - landingSkin * 2f));
            var filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayers);
            filter.useTriggers = false;
            landingHits.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, landingHits);
            foreach (var hit in landingHits)
            {
                if (hit != null && hit.attachedRigidbody != body && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Choosing确认仅排队。菜单关闭后下一物理步复验并原子支付/移动；失败无费用。</summary>
        public bool TryQueuePaidTeleport(Vector2 destination, Func<Func<bool>, bool> commit, Action<bool> completed)
        {
            if (runController == null || runController.Phase != RunPhase.Choosing || relocationPending
                || playerState == null || !playerState.IsAlive || !playerState.HasBodyCore
                || commit == null || !CanLandAt(destination))
            {
                return false;
            }
            relocationPosition = destination;
            relocationPending = true;
            paidRelocation = commit;
            paidFinished = completed;
            inputReader.DiscardGameplayInput();
            return true;
        }

        public void CancelPaidTeleport()
        {
            if (paidRelocation == null)
            {
                return;
            }
            var callback = paidFinished;
            paidRelocation = null;
            paidFinished = null;
            relocationPending = false;
            callback?.Invoke(false);
        }

        /// <summary>仅白板回到首次出生点；不复活、不重置门、HP 或构筑，不是正式 Restart。</summary>
        public bool TryResetToStart()
        {
            return QueueRelocation(startPosition, true);
        }

        private bool QueueRelocation(Vector2 destination, bool reset)
        {
            if (!IsGameplayActive || relocationPending || !Finite(destination.x) || !Finite(destination.y))
            {
                return false;
            }
            relocationPosition = destination;
            relocationIsReset = reset;
            relocationPending = true;
            inputReader.DiscardGameplayInput();
            return true;
        }

        private void ApplyRelocation()
        {
            bool reset = relocationIsReset;
            Vector2 destination = relocationPosition;
            ClearMotionRequests();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.position = destination;
            if (reset)
            {
                body.rotation = startRotation;
            }
            body.gravityScale = gravityScale;
            body.WakeUp();
            Relocated?.Invoke();
            if (!reset)
            {
                GameAudio.Play(AudioCue.Teleported, gameObject);
            }
        }

        private bool CheckGround()
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(groundLayers);
            filter.useTriggers = false;
            Bounds bounds = playerCollider.bounds;
            for (int ray = -1; ray <= 1; ray++)
            {
                Vector2 origin = new Vector2(bounds.center.x + ray * bounds.extents.x * raySpread,
                    bounds.min.y + rayStartOffset);
                groundHits.Clear();
                Physics2D.Raycast(origin, Vector2.down, filter, groundHits, rayStartOffset + groundCheckDistance);
                foreach (RaycastHit2D hit in groundHits)
                {
                    if (hit.collider != null && hit.rigidbody != body && hit.normal.y >= minimumGroundNormal)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            if (phase != RunPhase.Playing)
            {
                CancelPaidTeleport();
                jumpBuffer = 0f;
                relocationPending = false;
                // 已结算的地刺击退须跨暂停/选择保留；死亡/胜利才丢弃。
                if (phase == RunPhase.Dead || phase == RunPhase.Won)
                {
                    knockbackPending = false;
                }
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            // 输入适配器清设备缓冲；本地落地等待/迁移也不能跨失焦残留。
            if (!focused)
            {
                CancelPaidTeleport();
                jumpBuffer = 0f;
                relocationPending = false;
            }
        }

        private void ClearMotionRequests()
        {
            jumpBuffer = coyoteRemaining = groundLock = dashRemaining = cooldownRemaining = scanRemaining = 0f;
            knockbackRemaining = spikeProtectionRemaining = 0f;
            dashing = airDashUsed = doubleJumpUsed = relocationPending = knockbackPending = false;
            IsGrounded = groundSample = false;
        }

        private void OnDisable()
        {
            CancelPaidTeleport();
            if (runController != null)
            {
                runController.PhaseChanged -= OnPhaseChanged;
            }
            ClearMotionRequests();
            if (body != null)
            {
                body.gravityScale = originalGravityScale;
            }
            if (playerCollider != null && playerCollider.sharedMaterial == testMaterial)
            {
                playerCollider.sharedMaterial = originalMaterial;
            }
        }

        private void OnDestroy()
        {
            if (testMaterial != null)
            {
                Destroy(testMaterial);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
