// 职责：定义已确认的跨类别交互顺序，不负责距离检测。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>数值升序为类别优先级；同类别的选择由交互器处理。</summary>
    public enum InteractionKind
    {
        Portal = 0,
        Switch = 1,
        Chest = 2,
    }
}
