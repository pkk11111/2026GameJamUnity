// Soap/T07：分发器调用的咬击动作；不消费输入、不解释身体规则、不写运动/共享状态。
// Inspector显式绑定攻击读口/判定锚点/配置；与分发器使用同一真实PlayerState。
// biteOrigin为右向参考偏移，视觉须使用独立子对象；动画事件不得再次扣血。交接见docs/handoffs/Soap.handoff。
// 直接依赖Core/Audio.Core/Gameplay.Combat/Gameplay.Player/UnityEngine；规范：根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Audio;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerBiteAttack : MonoBehaviour, IPlayerAttackAction
    {
        [SerializeField, Tooltip("唯一玩家攻击读口，必须与PlayerAttackRouter绑定同一组件。")]
        private MonoBehaviour combatStateSource;
        [SerializeField, Tooltip("右向参考偏移；实际圆心按唯一FacingSign镜像，视觉不可移动此锚点。")]
        private Transform biteOrigin;
        [SerializeField, Tooltip("The unique PlayerFacing2D on the actual player, also bound to Sword/presentation.")]
        private PlayerFacing2D facingSource;
        [SerializeField, Tooltip("既有咬击范围/冷却/目标配置；数值每次动作即时读取。")]
        private BiteConfig config;
        private IPlayerCombatState combat;
        private double nextAttackTime;
        private bool wired;
        private bool attacking;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        public float CooldownRemaining => Mathf.Max(0f, (float)(nextAttackTime - Time.timeAsDouble));
        public bool IsWired => wired;
        public IPlayerCombatState CombatState => combat;
        public PlayerFacing2D Facing => facingSource;
        public Vector3 HitCenter
        {
            get
            {
                Vector3 offset = combatStateSource.transform.InverseTransformPoint(biteOrigin.position);
                offset.x *= facingSource.FacingSign;
                return combatStateSource.transform.TransformPoint(offset);
            }
        }
        /// <summary>Read-only accepted-attack notification; presentation must never settle damage.</summary>
        public event Action AttackStarted;

        private void OnEnable()
        {
            combat = combatStateSource as IPlayerCombatState;
            wired = combatStateSource != null && combat != null
                && biteOrigin != null && config != null && config.IsValid && facingSource != null && facingSource.IsWired;
            if (!wired)
            {
                Debug.LogWarning("[T07 Bite] Missing combat/origin/valid config; inspect this component.", this);
            }
        }

        /// <summary>只由唯一普通攻击分发器请求；拒绝不排队，动画不能再次调用结算。</summary>
        public bool TryAttack()
        {
            if (!isActiveAndEnabled || attacking || !wired || combatStateSource == null || biteOrigin == null
                || config == null || !config.IsValid || facingSource == null || !facingSource.isActiveAndEnabled
                || Time.timeAsDouble < nextAttackTime || !combat.CanBite)
            {
                return false;
            }
            attacking = true;
            try
            {
                nextAttackTime = Time.timeAsDouble + config.CooldownSeconds;
                try { AttackStarted?.Invoke(); }
                catch (Exception ex) { Debug.LogWarning("[T07 Bite] Presentation failed; attack continues: " + ex.Message, this); }
                try
                {
                    GameAudio.Play(AudioCue.PlayerBite, gameObject);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[T07 Bite] Audio backend failed; attack continues: " + ex.Message, this);
                }
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = config.TargetLayers,
                    useTriggers = config.IncludeTriggers };
                overlaps.Clear();
                damaged.Clear();
                Physics2D.OverlapCircle(HitCenter, config.Radius, filter, overlaps);
                foreach (Collider2D collider in overlaps)
                {
                    if (collider == null || collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }
                    IDamageable target = Receiver(collider.transform);
                    if (target == null || ReferenceEquals(target, combatStateSource) || !damaged.Add(target))
                    {
                        continue;
                    }
                    if (target is Component component && (component.transform.IsChildOf(transform)
                        || transform.IsChildOf(component.transform)
                        || (component.transform.position.x - combatStateSource.transform.position.x) * facingSource.FacingSign < 0f))
                    {
                        continue;
                    }
                    if (!isActiveAndEnabled || combatStateSource == null || !combat.CanBite)
                    {
                        break;
                    }
                    int amount = combat.BiteDamage;
                    if (amount <= 0) // DamageRequest拒绝0；不构造非法请求。
                    {
                        continue;
                    }
                    // 当前契约只有Enemy/Terrain；使用既有合法combat分类，不新增PlayerAttack枚举。
                    target.TryTakeDamage(new DamageRequest(amount, DamageKind.Enemy, gameObject));
                }
                return true;
            }
            finally
            {
                attacking = false;
            }
        }

        private static IDamageable Receiver(Transform node)
        {
            for (Transform current = node; current != null; current = current.parent)
            {
                foreach (MonoBehaviour behaviour in current.GetComponents<MonoBehaviour>())
                {
                    if (behaviour is IDamageable receiver)
                    {
                        return receiver;
                    }
                }
            }
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            if (biteOrigin != null && config != null && facingSource != null && combatStateSource != null)
            {
                Gizmos.DrawWireSphere(HitCenter, config.Radius);
            }
        }
    }
}
