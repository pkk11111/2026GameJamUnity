// 职责：统一构筑槽只读视图；不执行领取、替换、舍弃或姿态切换。
// 依赖：System.Action、Collections.Generic、LoadoutItemId。维护：总控；规范：AGENTS.md。
using System;
using System.Collections.Generic;

namespace Regrowth.Core
{
    /// <summary>Capacity 固定为 4；Items 不可被调用方修改；完成变更后通知。</summary>
    public interface ILoadoutState
    {
        int Capacity { get; }
        IReadOnlyList<LoadoutItemId> Items { get; }
        bool Contains(LoadoutItemId item);
        event Action LoadoutChanged;
    }
}
