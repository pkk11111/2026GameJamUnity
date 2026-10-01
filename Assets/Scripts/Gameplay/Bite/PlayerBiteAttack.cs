// Soap/T07：唯一Attack消费者与单次咬击伤害提交；不解释攻击许可规则、不写运动/共享状态。
// Inspector显式绑定输入/攻击读口/判定锚点/配置；权限与伤害由同一真实PlayerState提供。
// 表现可调整biteOrigin；动画事件不得再次扣血。交接见docs/handoffs/Soap.handoff。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Audio;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerBiteAttack : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private MonoBehaviour combatStateSource;
        [SerializeField] private Transform biteOrigin;
        [SerializeField] private BiteConfig config;
        private IPlayerInput input;
        private IPlayerCombatState combat;
        private double nextAttackTime;
        private bool wired;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        public float CooldownRemaining => Mathf.Max(0f, (float)(nextAttackTime - Time.timeAsDouble));
        public bool IsWired => wired;

        private void OnEnable()
        {
            input = inputSource as IPlayerInput;
            combat = combatStateSource as IPlayerCombatState;
            wired = inputSource != null && combatStateSource != null && input != null && combat != null
                && biteOrigin != null && config != null && config.IsValid;
            if (!wired) Debug.LogWarning("[T07 Bite] Missing input/combat/origin/valid config; inspect this component.", this);
        }

        private void FixedUpdate()
        {
            if (!wired || inputSource == null || combatStateSource == null || biteOrigin == null
                || config == null || !config.IsValid) return;
            // 请求先消费，冷却或外部权限拒绝时不排队；暂停输入清理由唯一C01适配器负责。
            if (!input.TryConsumeAttack() || Time.timeAsDouble < nextAttackTime || !combat.CanBite) return;
            nextAttackTime = Time.timeAsDouble + config.CooldownSeconds;
            try { GameAudio.Play(AudioCue.PlayerBite, gameObject); }
            catch (Exception ex)
            {
                Debug.LogWarning("[T07 Bite] Audio backend failed; attack continues: " + ex.Message, this);
            }
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = config.TargetLayers,
                useTriggers = config.IncludeTriggers };
            overlaps.Clear();
            damaged.Clear();
            Physics2D.OverlapCircle(biteOrigin.position, config.Radius, filter, overlaps);
            foreach (Collider2D collider in overlaps)
            {
                if (collider == null || collider.transform.IsChildOf(transform)) continue;
                IDamageable target = Receiver(collider.transform);
                if (target == null || ReferenceEquals(target, combatStateSource) || !damaged.Add(target)) continue;
                if (target is Component component && (component.transform.IsChildOf(transform)
                    || transform.IsChildOf(component.transform))) continue;
                if (!combat.CanBite) break;
                int amount = combat.BiteDamage;
                if (amount <= 0) continue; // DamageRequest拒绝0；不构造非法请求。
                // 当前契约只有Enemy/Terrain；使用既有合法combat分类，不新增PlayerAttack枚举。
                target.TryTakeDamage(new DamageRequest(amount, DamageKind.Enemy, gameObject));
            }
        }

        private static IDamageable Receiver(Transform node)
        {
            for (Transform current = node; current != null; current = current.parent)
                foreach (MonoBehaviour behaviour in current.GetComponents<MonoBehaviour>())
                    if (behaviour is IDamageable receiver) return receiver;
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (biteOrigin != null && config != null) Gizmos.DrawWireSphere(biteOrigin.position, config.Radius);
        }
    }
}
