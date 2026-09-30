// 职责：不可变伤害请求；不实现奖励或传送规则。
// 依赖：System.Enum/异常、UnityEngine.GameObject、DamageKind。维护：总控；规范：根目录 AGENTS.md。
using System;
using UnityEngine;

namespace Regrowth.Core
{
    /// <summary>一次不可变伤害请求；正数伤害，来源允许 null。</summary>
    public readonly struct DamageRequest
    {
        public int Amount { get; }
        public DamageKind Kind { get; }
        public GameObject Source { get; }

        public DamageRequest(int amount, DamageKind kind, GameObject source = null)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (!Enum.IsDefined(typeof(DamageKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            Amount = amount;
            Kind = kind;
            Source = source;
        }
    }
}

