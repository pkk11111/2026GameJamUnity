// 职责：传送独立代价池与参数；共用宝箱抽卡算法/选择UI，不把运行候选存入资产。
// 维护controller/C08；依赖Core/Unity；交接portal-rules.handoff；规范AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    [Serializable]
    public sealed class PortalCostDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string title;
        [SerializeField] private TeleportCostKind kind;
        [SerializeField, Min(1), Tooltip("生命为百分比，攻击为百分点，敌人为固定增量，舍弃填1。")]
        private int amount = 1;
        public string Id => id;
        public string Title => title;
        public TeleportCostKind Kind => kind;
        public int Amount => amount;
        public PortalCostDefinition(string id, string title, TeleportCostKind kind, int amount)
        {
            this.id = id; this.title = title; this.kind = kind; this.amount = amount;
        }
        public PortalCostDefinition Copy() => (PortalCostDefinition)MemberwiseClone();
        public bool IsValid => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title)
            && Enum.IsDefined(typeof(TeleportCostKind), kind) && amount > 0
            && ((kind != TeleportCostKind.CurrentHealth && kind != TeleportCostKind.MaximumHealth) || amount <= 100);
    }

    [CreateAssetMenu(menuName = "pawgatory/Portal Cost Config")]
    public sealed class PortalCostConfig : ScriptableObject
    {
        [SerializeField] private string choiceTitle = "Choose a cost to teleport";
        [SerializeField, Range(1, 100)] private int minimumAttackPercent = 60;
        [SerializeField, Min(0f)] private float arrivalProtection = 0.5f;
        [SerializeField] private PortalCostDefinition[] costs =
        {
            new PortalCostDefinition("COST_SHED_LEGS", "Shed legs", TeleportCostKind.ShedLegs, 1),
            new PortalCostDefinition("COST_SHED_ARMS", "Shed arms and sword", TeleportCostKind.ShedArms, 1),
            new PortalCostDefinition("COST_SHED_TAIL", "Shed tail", TeleportCostKind.ShedTail, 1),
            new PortalCostDefinition("COST_SHED_FLAME_TAIL", "Shed flame tail", TeleportCostKind.ShedFlameTail, 1),
            new PortalCostDefinition("COST_CURRENT_HP", "Pay health", TeleportCostKind.CurrentHealth, 20),
            new PortalCostDefinition("COST_MAX_HP", "Reduce maximum health", TeleportCostKind.MaximumHealth, 10),
            new PortalCostDefinition("COST_ATTACK", "Weaken all attacks", TeleportCostKind.Attack, 10),
            new PortalCostDefinition("COST_ENEMY_HP", "Strengthen enemy health", TeleportCostKind.EnemyHealth, 50),
            new PortalCostDefinition("COST_ENEMY_ATTACK", "Strengthen enemy attack", TeleportCostKind.EnemyAttack, 5)
        };
        public string ChoiceTitle => choiceTitle;
        public int MinimumAttackPercent => minimumAttackPercent;
        public float ArrivalProtection => arrivalProtection;
        public IReadOnlyList<PortalCostDefinition> Costs => costs;
        public bool IsValid
        {
            get
            {
                if (costs == null || costs.Length < 3 || minimumAttackPercent <= 0 || minimumAttackPercent > 100
                    || arrivalProtection < 0f || float.IsNaN(arrivalProtection) || float.IsInfinity(arrivalProtection))
                {
                    return false;
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var kinds = new HashSet<TeleportCostKind>();
                foreach (var cost in costs)
                {
                    if (cost == null || !cost.IsValid || !ids.Add(cost.Id) || !kinds.Add(cost.Kind))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
