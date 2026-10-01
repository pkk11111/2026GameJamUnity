// 职责：只读生命与死亡通知；不实现奖励或传送规则。
// 依赖：System.Action。维护：总控；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>生命只读视图；更新后通知，死亡生命周期只通知一次。</summary>
    public interface IHealth
    {
        int CurrentHealth { get; }
        int MaximumHealth { get; }
        bool IsAlive { get; }
        event Action HealthChanged;
        event Action Died;
    }
}

