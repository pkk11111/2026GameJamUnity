// 职责：标识当前姿态；不提供按键变身入口。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>姿态仅随奖励/代价结算改变，具体联动由 PlayerState 实现。</summary>
    public enum PlayerForm
    {
        Quadruped = 0,
        Upright = 1,
    }
}
