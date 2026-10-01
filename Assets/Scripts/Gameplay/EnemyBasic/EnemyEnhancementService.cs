// 职责：本局唯一敌人强化累计与注册；全体预验/批量提交/最后通知，未来生成从注册接点继承。
// 维护controller/C08；依赖Core/EnemyBasic；显式绑定，不搜索世界；交接portal-rules.handoff；规范AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    public sealed class EnemyEnhancementService : MonoBehaviour, IEnemyRegistrationAdapter
    {
        [SerializeField, Tooltip("全部场景敌人，含精英与暂未启用对象；未来生成者须绑定本服务注册。")]
        private EnemyBasic[] initialEnemies = new EnemyBasic[0];
        private readonly Dictionary<EnemyBasic, int[]> applied = new Dictionary<EnemyBasic, int[]>();
        private readonly List<EnemyBasic> changed = new List<EnemyBasic>();
        private int healthBonus;
        private int attackBonus;
        private bool committing;
        public int HealthBonus => healthBonus;
        public int AttackBonus => attackBonus;

        private void OnEnable()
        {
            foreach (var enemy in initialEnemies)
            {
                if (enemy != null && enemy.IsInitialized)
                {
                    Register(enemy);
                }
            }
        }

        public void Register(EnemyBasic enemy)
        {
            if (enemy == null || !enemy.IsAlive || applied.ContainsKey(enemy))
            {
                return;
            }
            if ((long)enemy.MaximumHealth + healthBonus > int.MaxValue || (long)enemy.AttackDamage + attackBonus > int.MaxValue)
            {
                Debug.LogError("Enemy enhancement overflow at registration; enemy disabled.", enemy);
                enemy.gameObject.SetActive(false);
                return;
            }
            int maximum = enemy.MaximumHealth + healthBonus;
            int current = (int)(((long)enemy.CurrentHealth * maximum + enemy.MaximumHealth - 1) / enemy.MaximumHealth);
            enemy.CommitEnhancement(maximum, current, enemy.AttackDamage + attackBonus);
            applied.Add(enemy, new[] { healthBonus, attackBonus });
            if (healthBonus != 0)
            {
                enemy.NotifyEnhancement();
            }
        }

        public void Unregister(EnemyBasic enemy)
        {
            // 暂时停用不注销活体：全图代价继续作用且重新启用不会重复加成。死体永久跳过。
        }

        public bool CanIncrease(TeleportCostKind kind, int amount)
        {
            if (!isActiveAndEnabled || committing || amount <= 0)
            {
                return false;
            }
            bool hp = kind == TeleportCostKind.EnemyHealth;
            if (!hp && kind != TeleportCostKind.EnemyAttack)
            {
                return false;
            }
            if ((long)(hp ? healthBonus : attackBonus) + amount > int.MaxValue)
            {
                return false;
            }
            foreach (var pair in applied)
            {
                var enemy = pair.Key;
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }
                if (!enemy.CanWriteEnhancement || (long)(hp ? enemy.MaximumHealth : enemy.AttackDamage) + amount > int.MaxValue)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>移动回调false无任何加成；成功批量写数值但不通知，玩家提交后调用PublishCommitted。</summary>
        public bool TryCommit(TeleportCostKind kind, int amount, Func<bool> tryMove)
        {
            if (!CanIncrease(kind, amount) || tryMove == null)
            {
                return false;
            }
            committing = true;
            try
            {
                if (!tryMove())
                {
                    return false;
                }
                bool hp = kind == TeleportCostKind.EnemyHealth;
                if (hp)
                {
                    healthBonus += amount;
                }
                else
                {
                    attackBonus += amount;
                }
                changed.Clear();
                foreach (var pair in applied)
                {
                    var enemy = pair.Key;
                    if (enemy == null || !enemy.IsAlive)
                    {
                        continue;
                    }
                    int maximum = enemy.MaximumHealth + (hp ? amount : 0);
                    int current = hp ? (int)(((long)enemy.CurrentHealth * maximum + enemy.MaximumHealth - 1) / enemy.MaximumHealth) : enemy.CurrentHealth;
                    enemy.CommitEnhancement(maximum, current, enemy.AttackDamage + (hp ? 0 : amount));
                    pair.Value[0] = healthBonus;
                    pair.Value[1] = attackBonus;
                    if (hp)
                    {
                        changed.Add(enemy);
                    }
                }
                return true;
            }
            finally { committing = false; }
        }

        public void PublishCommitted()
        {
            committing = true;
            try
            {
                foreach (var enemy in changed)
                {
                    if (enemy != null)
                    {
                        enemy.NotifyEnhancement();
                    }
                }
                changed.Clear();
            }
            finally { committing = false; }
        }
    }
}
