// 移动音频维护enemy-ai：真实位移/地面/距离门控，经GameAudio发声；交接docs/handoffs/enemy-ai.handoff。
// 职责：已提交敌人伤害/死亡/接触攻击音频；不写战斗状态。
// 依赖 Core/EnemyBasic/Audio.Core/Wwise组件；维护 audio-full-events；规范 AGENTS.md。
// 交接 docs/handoffs/audio-full-events.handoff；精英身份由场景明确配置。
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Gameplay;
using UnityEngine;
namespace Regrowth.Audio
{
    [DefaultExecutionOrder(80), DisallowMultipleComponent, RequireComponent(typeof(EnemyBasic), typeof(AkGameObj))]
    public sealed class EnemyAudio : MonoBehaviour
    {
        [SerializeField] private bool elite;
        [SerializeField] private EnemyContactAttack contact;
        [SerializeField, Min(0f)] private float hurtInterval = 1f;
        [Header("移动声音（实时调参）")]
        [SerializeField, Tooltip("关闭可单独禁用脚步/翼声，不影响受伤和死亡。")]
        private bool movementAudio = true;
        [SerializeField, Tooltip("显式玩家根节点；仅附近播放，缺少引用时只关闭移动音频。")]
        private Transform movementListener;
        [SerializeField, Min(0.1f), Tooltip("移动声音启用距离，世界单位；避免远处全图敌人同时发声。")]
        private float movementAudibleRadius = 16f;
        [SerializeField, Min(0.01f), Tooltip("每次脚步所需实际水平行进距离，世界单位。")]
        private float stepDistance = 0.9f;
        [SerializeField, Min(0.05f), Tooltip("脚步最短间隔，游戏秒；高速追击也不密集连播。")]
        private float stepInterval = 0.4f;
        [SerializeField, Min(0.05f), Tooltip("蝙蝠正常飞行/悬停翼声间隔，游戏秒。")]
        private float flapInterval = 0.65f;
        [SerializeField, Min(0f), Tooltip("首声错开时间，秒；主图按实例设置避免同步发声。")]
        private float movementStartOffset;
        [SerializeField, Min(0.01f), Tooltip("脚底完整实体向下探测距离，世界单位；只影响脚步。")]
        private float groundProbeDistance = 0.08f;
        [SerializeField, Min(0f), Tooltip("实际位移对应速度低于此值时不积累脚步，单位/秒。")]
        private float minimumMoveSpeed = 0.1f;
        [SerializeField, Min(0.01f), Tooltip("单步大于此位移视为迁移，重置节奏而不补播，世界单位。")]
        private float maximumSampleDistance = 1f;
        private EnemyBasic owner;
        private EnemyBasicAI ai;
        private Rigidbody2D body;
        private Collider2D solid;
        private IRunContext run;
        private Vector2 lastPosition;
        private float distanceSinceStep;
        private double nextMovementTime;
        private readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>();
        public int MovementRequests { get; private set; }
        private double nextHurt;
        public bool IsElite => elite;
        private void OnEnable()
        {
            owner = GetComponent<EnemyBasic>();
            ai = GetComponent<EnemyBasicAI>();
            body = GetComponent<Rigidbody2D>();
            solid = GetComponent<Collider2D>();
            run = owner.RunContext;
            if (run != null)
            {
                run.PhaseChanged += ResetMotion;
            }
            ResetMotion(RunPhase.Playing);
            owner.DamageApplied += Hurt;
            owner.Died += Death;
            if (contact != null) contact.DamageApplied += Attack;
        }
        private void OnDisable()
        {
            if (run != null)
            {
                run.PhaseChanged -= ResetMotion;
            }
            if (owner != null) { owner.DamageApplied -= Hurt; owner.Died -= Death; }
            if (contact != null) contact.DamageApplied -= Attack;
        }
        private void ResetMotion(RunPhase phase)
        {
            if (body != null)
            {
                lastPosition = body.position;
            }
            distanceSinceStep = 0f;
            nextMovementTime = Time.timeAsDouble + stepInterval + movementStartOffset;
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }
            Vector2 delta = body.position - lastPosition;
            lastPosition = body.position;
            if (!movementAudio || movementListener == null || owner == null || !owner.CanAct
                || ai == null || !ai.isActiveAndEnabled || !ai.IsConfigured || ai.IsRecovering
                || ai.State == EnemyAIState.Alert || solid == null || !solid.enabled
                || !ValidPositive(movementAudibleRadius) || !ValidPositive(stepDistance)
                || !ValidPositive(stepInterval) || !ValidPositive(flapInterval)
                || !ValidPositive(groundProbeDistance) || !ValidPositive(maximumSampleDistance)
                || float.IsNaN(minimumMoveSpeed) || float.IsInfinity(minimumMoveSpeed) || minimumMoveSpeed < 0f
                || ((Vector2)movementListener.position - body.position).sqrMagnitude > movementAudibleRadius * movementAudibleRadius
                || delta.magnitude > maximumSampleDistance)
            {
                ResetMotion(RunPhase.Playing);
                return;
            }
            if (ai.IsFlying)
            {
                if (Time.timeAsDouble >= nextMovementTime)
                {
                    EmitMovement(AudioCue.NPCFly, flapInterval);
                }
                return;
            }
            if (Mathf.Abs(delta.x) <= minimumMoveSpeed * Time.fixedDeltaTime || !OnGround())
            {
                ResetMotion(RunPhase.Playing);
                return;
            }
            distanceSinceStep += Mathf.Abs(delta.x);
            if (distanceSinceStep >= stepDistance && Time.timeAsDouble >= nextMovementTime)
            {
                EmitMovement(AudioCue.NPCFootstep, stepInterval);
            }
        }

        private static bool ValidPositive(float value)
        {
            return value > 0f && !float.IsInfinity(value);
        }

        private bool OnGround()
        {
            groundHits.Clear();
            solid.Cast(Vector2.down, new ContactFilter2D { useTriggers = false }, groundHits, groundProbeDistance);
            foreach (var hit in groundHits)
            {
                if (hit.collider != null && hit.normal.y > 0.5f
                    && !hit.collider.transform.IsChildOf(transform)
                    && !hit.collider.transform.IsChildOf(movementListener)
                    && hit.collider.GetComponentInParent<EnemyBasic>() == null)
                {
                    return true;
                }
            }
            return false;
        }

        private void EmitMovement(AudioCue cue, float interval)
        {
            // 一次请求，不追补错过节拍；后台未就绪仍不缓存陈旧脚步。
            distanceSinceStep = 0f;
            nextMovementTime = Time.timeAsDouble + interval;
            MovementRequests++;
            GameAudio.Play(cue, gameObject);
        }

        private void Hurt(DamageRequest request)
        {
            if (!owner.IsAlive || Time.timeAsDouble < nextHurt) return;
            nextHurt = Time.timeAsDouble + hurtInterval;
            GameAudio.Play(elite ? AudioCue.EliteHurt : AudioCue.NPCHurt, gameObject);
        }
        private void Death() => GameAudio.Play(elite ? AudioCue.EliteDeath : AudioCue.NPCDeath, gameObject);
        private void Attack(DamageRequest request)
        {
            if (elite) GameAudio.Play(AudioCue.EliteAttack, gameObject);
        }
    }
}
