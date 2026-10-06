// 警觉/剑击恢复时抑制接触候选；维护enemy-ai，交接docs/handoffs/enemy-ai.handoff。
// 职责：显式敌人Trigger每固定步扫描玩家，向唯一接触仲裁器提交候选，无独立接触冷却。
// 原作者Soap；修复structure-audit；依赖Core/Combat/Unity；交接docs/handoffs/structure-audit.handoff。
// 顺序100提交，玩家顺序200结算；DamageApplied仅实际扣血后触发；规范根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class EnemyContactAttack : MonoBehaviour
    {
        [SerializeField, Tooltip("本敌人唯一生命/伤害持有者。")]
        private EnemyBasic owner;
        [SerializeField, Tooltip("本敌人子节点攻击Trigger，大小仍由Collider配置。")]
        private Collider2D attackTrigger;
        [SerializeField, Tooltip("唯一玩家，须实现IDamageable/IHealth/IContactDamageReceiver。")]
        private MonoBehaviour playerDamageableSource;
        private EnemyBasicAI ai;
        private IDamageable player;
        private IHealth playerHealth;
        private Collider2D playerBody;
        private IContactDamageReceiver receiver;
        private string sourceId;
        private Action<DamageRequest> onApplied;
        private readonly List<RaycastHit2D> obstructionHits = new List<RaycastHit2D>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<MonoBehaviour> receiverComponents = new List<MonoBehaviour>();
        public event Action<DamageRequest> DamageApplied;

        private void Awake()
        {
            sourceId = ContactDamageIdentity.Capture(this);
            onApplied = NotifyApplied;
        }

        private void OnEnable()
        {
            ai = owner != null ? owner.GetComponent<EnemyBasicAI>() : null;
            player = playerDamageableSource as IDamageable;
            playerHealth = playerDamageableSource as IHealth;
            playerBody = playerDamageableSource != null ? playerDamageableSource.GetComponent<Collider2D>() : null;
            receiver = playerDamageableSource as IContactDamageReceiver;
            if (owner == null || attackTrigger == null || !attackTrigger.isTrigger
                || !attackTrigger.transform.IsChildOf(owner.transform)
                || player == null || playerHealth == null || receiver == null || playerBody == null || playerBody.isTrigger)
            {
                Debug.LogWarning("[Contact] Bind local owner/attack Trigger and actual player contact receiver.", this);
            }
        }

        private void FixedUpdate()
        {
            if (owner == null || !owner.CanAct || (ai != null && !ai.CanDealContactDamage) || owner.Config == null || !owner.Config.IsValid
                || attackTrigger == null || !attackTrigger.enabled || !attackTrigger.isTrigger
                || !attackTrigger.transform.IsChildOf(owner.transform) || !attackTrigger.gameObject.activeInHierarchy
                || playerDamageableSource == null || !playerDamageableSource.isActiveAndEnabled
                || player == null || playerHealth == null || receiver == null
                || !playerHealth.IsAlive || owner.AttackDamage <= 0 || playerBody == null
                || !playerBody.enabled || playerBody.isTrigger)
            {
                return;
            }
            var filter = new ContactFilter2D
            {
                useLayerMask = true, layerMask = owner.Config.PlayerLayers,
                useTriggers = false
            };
            overlaps.Clear();
            attackTrigger.Overlap(filter, overlaps);
            foreach (Collider2D collider in overlaps)
            {
                if (collider != playerBody)
                {
                    continue;
                }
                // 只认唯一玩家根实体；附属交互Trigger不能扩大受伤区域。
                if (DamageableLookup.IsOccluded(owner.transform.position, playerBody, player, owner.transform,
                    owner.Config.ContactObstructionLayers, obstructionHits, receiverComponents))
                {
                    continue;
                }
                receiver.TryQueueContactDamage(new DamageRequest(owner.AttackDamage, DamageKind.Enemy, owner.gameObject),
                    this, sourceId, onApplied);
                break; // 玩家多Collider在此来源同一步只有一份候选。
            }
        }

        private void NotifyApplied(DamageRequest request)
        {
            DamageApplied?.Invoke(request);
        }

        private void OnDisable()
        {
            overlaps.Clear();
        }
    }
}
