// 职责：整局阶段身份；不实现奖励或传送规则。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>整局阶段身份；状态控制器尚未实现。</summary>
    public enum RunPhase
    {
        Playing = 0,
        Choosing = 1,
        Paused = 2,
        Dead = 3,
        Won = 4,
    }
}

