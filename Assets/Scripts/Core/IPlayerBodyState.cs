// 职责：玩家躯干与本局成功持有历史的只读视图；状态归PlayerState，表现不得反写。
// 模块/维护：controller / T12-V5；交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
using System;
using System.Collections.Generic;

namespace Regrowth.Core
{
    public interface IPlayerBodyState
    {
        bool HasBodyCore { get; }
        IReadOnlyList<LoadoutItemId> EverOwnedItems { get; }
        bool WasEverOwned(LoadoutItemId item);
        /// <summary>躯干变化后通知；OnEnable订阅并读快照，OnDisable退订。</summary>
        event Action BodyChanged;
    }
}
