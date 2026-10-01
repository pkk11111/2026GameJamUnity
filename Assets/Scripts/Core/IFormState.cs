// 职责：当前姿态只读视图；表现层不能据此自行切换玩法状态。
// 依赖：System.Action、PlayerForm。维护：总控；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>姿态实际改变后通知；没有手动变身命令。</summary>
    public interface IFormState
    {
        PlayerForm CurrentForm { get; }
        event Action<PlayerForm> FormChanged;
    }
}
