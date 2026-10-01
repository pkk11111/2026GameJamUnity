// 职责：PlayerState 内部的唯一三项存储，提供不可写的活视图和原子替换。
// 模块/维护：controller，C02；直接依赖：Collections.Generic、Regrowth.Core；仅 PlayerState 正式持有。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;

namespace Regrowth.Runtime
{
    /// <summary>
    /// 只处理容量/身份/重复，不发事件。PlayerState 在外围限制首版三项；
    /// 内部测试可用已定义的预留身份验收满槽替换，但不会授予正式玩家。
    /// </summary>
    internal sealed class LoadoutCollection
    {
        internal const int SlotCapacity = LoadoutRules.Capacity;
        private readonly List<LoadoutItemId> items = new List<LoadoutItemId>(SlotCapacity);
        internal IReadOnlyList<LoadoutItemId> Items { get; }

        internal LoadoutCollection()
        {
            Items = items.AsReadOnly();
        }

        internal bool Contains(LoadoutItemId item) => items.Contains(item);

        internal bool TryAdd(LoadoutItemId item)
        {
            if (!Enum.IsDefined(typeof(LoadoutItemId), item) || items.Count >= SlotCapacity || Contains(item))
            {
                return false;
            }
            items.Add(item);
            return true;
        }

        internal bool TryRemove(LoadoutItemId item) => items.Remove(item);

        internal bool TryReplace(LoadoutItemId removedItem, LoadoutItemId addedItem)
        {
            int index = items.IndexOf(removedItem);
            if (index < 0 || !Enum.IsDefined(typeof(LoadoutItemId), addedItem) || Contains(addedItem))
            {
                return false;
            }
            // 校验全部完成后只写一次；失败保留原列表，满槽替换不出现临时空槽。
            items[index] = addedItem;
            return true;
        }
    }
}
