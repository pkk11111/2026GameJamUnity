// 职责：每固定步扫描原椭圆地刺，提交Terrain候选；实际胜出扣血后才击退/发音频。
// 模块/维护：controller / Level 适配；依赖：PolygonCollider2D、WhiteboxPlayer2D 运动请求。
// 接线：根缩放为 1；shape 参数首次 Awake 读取，击退参数实时读取；物理速度由玩家 FixedUpdate 写。
// 修复维护structure-audit；交接docs/handoffs/structure-audit.handoff；规范根AGENTS.md。
// 音频增量：audio-traps；接受击退后通知音频，见 docs/handoffs/audio-traps.handoff。
using UnityEngine;
using System.Collections.Generic;
using Regrowth.Core;
using System;

namespace Regrowth.Gameplay.WhiteBox
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class PrototypeSpike2D : MonoBehaviour
    {
        /// <summary>兼容事件名：地刺实际扣血成功后同步通知；存活者同时排队击退，致命命中进入死亡；禁用时退订。</summary>
        public event Action KnockbackAccepted;
        [Header("椭圆检测形状（下次 Play 或重建生效）")]
        [SerializeField, Tooltip("椭圆宽高，局部单位，均为正数。")]
        private Vector2 ellipseSize = new Vector2(3f, 1f);
        [SerializeField, Tooltip("椭圆局部偏移，单位。")]
        private Vector2 ellipseOffset = Vector2.zero;
        [SerializeField, Min(1), Tooltip("每次有效地刺命中扣除的HP；Terrain伤害，保护期间不重复扣血。实时读取。")]
        private int damage = 2;
        [Header("击退（实时读取）")]
        [SerializeField, Min(0.1f), Tooltip("击退速度，单位/秒。")]
        private float knockbackSpeed = 10f;
        [SerializeField, Min(0.02f), Tooltip("受击后移动锁，游戏秒。")]
        private float controlLockTime = 0.25f;
        // 保留旧序列化数据；正式保护已统一到PlayerState.damageProtectionSeconds，不再使用此值。
        [SerializeField, HideInInspector] private float protectionTime = 0.6f;
        [SerializeField, Range(0f, 1f), Tooltip("侧向接触最小向上分量；0 为纯椭圆法线。")]
        private float minimumUpwardDirection = 0.35f;
        private const int Segments = 64;
        private PolygonCollider2D detection;
        private string sourceId;
        private Action<DamageRequest> onApplied;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();

        private void Awake()
        {
            detection = GetComponent<PolygonCollider2D>();
            sourceId = ContactDamageIdentity.Capture(this);
            onApplied = NotifyApplied;
            RebuildShape();
        }

        /// <summary>编辑器菜单或首次 Awake 重建本地 Collider；不在 OnValidate 修改组件/资产。</summary>
        [ContextMenu("Rebuild Whitebox Spike Shape")]
        public void RebuildShape()
        {
            PolygonCollider2D detection = GetComponent<PolygonCollider2D>();
            if (detection == null || ellipseSize.x <= 0f || ellipseSize.y <= 0f
                || !Finite(ellipseSize.x) || !Finite(ellipseSize.y)
                || !Finite(ellipseOffset.x) || !Finite(ellipseOffset.y))
            {
                Debug.LogError("Level PrototypeSpike2D 检测器或 ellipseSize/ellipseOffset 无效。", this);
                enabled = false;
                return;
            }
            detection.isTrigger = true;
            detection.offset = ellipseOffset;
            detection.pathCount = 1;
            Vector2[] points = new Vector2[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                points[i] = new Vector2(Mathf.Cos(angle) * ellipseSize.x * 0.5f,
                    Mathf.Sin(angle) * ellipseSize.y * 0.5f);
            }
            detection.SetPath(0, points);
        }

        private void FixedUpdate()
        {
            if (detection == null || !detection.enabled)
            {
                return;
            }
            overlaps.Clear();
            detection.Overlap(new ContactFilter2D { useTriggers = false }, overlaps);
            foreach (Collider2D other in overlaps)
            {
                Hit(other);
            }
        }

        private void NotifyApplied(DamageRequest request)
        {
            KnockbackAccepted?.Invoke();
        }

        private void Hit(Collider2D other)
        {
            WhiteboxPlayer2D player = other.GetComponentInParent<WhiteboxPlayer2D>();
            if (!isActiveAndEnabled || player == null || !player.IsGameplayActive || other.isTrigger)
            {
                return;
            }
            Vector2 delta = (Vector2)transform.InverseTransformPoint(other.bounds.center) - ellipseOffset;
            float a = ellipseSize.x * 0.5f;
            float b = ellipseSize.y * 0.5f;
            Vector2 normal = new Vector2(delta.x / (a * a), delta.y / (b * b));
            if (normal.sqrMagnitude < 0.000001f)
            {
                normal = Vector2.up;
            }
            Vector2 direction = ((Vector2)transform.TransformDirection(normal.normalized)).normalized;
            if (minimumUpwardDirection > 0f && direction.y < minimumUpwardDirection)
            {
                float horizontal = Mathf.Sqrt(1f - minimumUpwardDirection * minimumUpwardDirection);
                direction = new Vector2(Mathf.Sign(direction.x) * horizontal, minimumUpwardDirection);
            }
            player.TryQueueSpikeHit(direction * knockbackSpeed, controlLockTime, damage, this, sourceId, onApplied);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 previous = transform.TransformPoint(ellipseOffset + new Vector2(ellipseSize.x * 0.5f, 0f));
            for (int i = 1; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                Vector2 point = ellipseOffset + new Vector2(Mathf.Cos(angle) * ellipseSize.x * 0.5f,
                    Mathf.Sin(angle) * ellipseSize.y * 0.5f);
                Vector3 next = transform.TransformPoint(point);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
