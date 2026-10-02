// Soap/T09：只扫描显式攻击Trigger，只向显式玩家IDamageable提交；无AI/移动/第二份攻击值。
// 固定步扫描，单个接收者一次周期一次尝试；阶段切换清计时，无暂停补发。详见Soap.handoff。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyContactAttack : MonoBehaviour
    {
        [SerializeField, Tooltip("同一敌人根的唯一生命/运行数值持有者；禁止绑定另一个敌人。")]
        private EnemyBasic owner;
        [SerializeField, Tooltip("本敌人子层级的攻击Trigger，独立于根实体Collider；大小在Collider Inspector调。")]
        private Collider2D attackTrigger;
        [SerializeField, Tooltip("实际唯一玩家组件，必须同时实现IDamageable和IHealth；不绑定代理。")]
        private MonoBehaviour playerDamageableSource;
        private IDamageable player;
        private IHealth playerHealth;
        private IRunContext subscribedRun;
        private double nextDamageTime;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<MonoBehaviour> receiverComponents = new List<MonoBehaviour>();
        // 只读成功事件便于诊断真实Kind/Source；订阅者不能再次结算。
        public event Action<DamageRequest> DamageApplied;

        private void OnEnable()
        {
            player = playerDamageableSource as IDamageable;
            playerHealth = playerDamageableSource as IHealth;
            if (owner == null || attackTrigger == null || !attackTrigger.isTrigger
                || !attackTrigger.transform.IsChildOf(owner.transform) || player == null || playerHealth == null)
            {
                Debug.LogWarning("[T09 Contact] Bind local owner/attack Trigger and actual player IHealth/IDamageable.", this);
            }
            nextDamageTime = Time.timeAsDouble;
            BindRun();
        }
        private void BindRun()
        {
            IRunContext current = owner != null ? owner.RunContext : null;
            if (ReferenceEquals(current, subscribedRun))
            {
                return;
            }
            if (subscribedRun != null)
            {
                subscribedRun.PhaseChanged -= OnPhaseChanged;
            }
            subscribedRun = current;
            if (subscribedRun != null)
            {
                subscribedRun.PhaseChanged += OnPhaseChanged;
            }
        }
        private void OnPhaseChanged(RunPhase phase)
        {
            // 每次恢复至少等一个新间隔；没有catch-up循环，也没有积压接触队列。
            nextDamageTime = Time.timeAsDouble + (owner != null && owner.Config != null ? owner.Config.ContactInterval : 0f);
        }
        private void FixedUpdate()
        {
            BindRun();
            if (owner == null || !owner.CanAct || owner.Config == null || !owner.Config.IsValid
                || attackTrigger == null || !attackTrigger.enabled || !attackTrigger.isTrigger
                || !attackTrigger.transform.IsChildOf(owner.transform)
                || !attackTrigger.gameObject.activeInHierarchy || playerDamageableSource == null
                || !playerDamageableSource.isActiveAndEnabled || player == null || playerHealth == null
                || !playerHealth.IsAlive || owner.AttackDamage <= 0 || Time.timeAsDouble < nextDamageTime)
            {
                return;
            }
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = owner.Config.PlayerLayers,
                useTriggers = owner.Config.IncludePlayerTriggers };
            overlaps.Clear();
            attackTrigger.Overlap(filter, overlaps);
            foreach (Collider2D collider in overlaps)
            {
                if (collider == null || !ReferenceEquals(DamageableLookup.FindInParents(collider.transform, receiverComponents), player))
                {
                    continue;
                }
                nextDamageTime = Time.timeAsDouble + owner.Config.ContactInterval;
                var request = new DamageRequest(owner.AttackDamage, DamageKind.Enemy, owner.gameObject);
                if (player.TryTakeDamage(request))
                {
                    DamageApplied?.Invoke(request);
                }
                break; // 无论成功/保护拒绝，所有玩家Collider共享本次周期。
            }
        }
        private void OnDisable()
        {
            if (subscribedRun != null)
            {
                subscribedRun.PhaseChanged -= OnPhaseChanged;
            }
            subscribedRun = null; overlaps.Clear();
        }
    }
}
