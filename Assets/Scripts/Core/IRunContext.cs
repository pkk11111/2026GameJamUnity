// 职责：只读运行状态；不实现奖励或传送规则。
// 依赖：System.Action、RunPhase。维护：总控；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>只读阶段；仅 Playing 时 IsGameplayActive 为 true。</summary>
    public interface IRunContext
    {
        RunPhase Phase { get; }
        bool IsGameplayActive { get; }
        event Action<RunPhase> PhaseChanged;
    }
}

