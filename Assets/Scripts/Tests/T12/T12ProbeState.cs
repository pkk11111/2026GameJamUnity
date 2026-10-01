// 职责：仅替换事务的隔离四槽夹具，900为测试旧槽身份；绝不绑定真实玩家或正式Config。
// Soap / T12；依赖Core；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;

namespace Regrowth.Tests.T12
{
    internal sealed class T12ProbeState : IHealth, ILoadoutState, IPlayerStateCommands
    {
        private readonly List<LoadoutItemId> items = new List<LoadoutItemId>
            { LoadoutItemId.Dash, LoadoutItemId.DoubleJump, LoadoutItemId.UprightForm, (LoadoutItemId)900 };
        public int CurrentHealth => 100;
        public int MaximumHealth => 100;
        public bool IsAlive => true;
        public int Capacity => 4;
        public IReadOnlyList<LoadoutItemId> Items => items.AsReadOnly();
        public int ReplaceCalls { get; private set; }
        public bool RejectReplace { get; set; }
        public event Action HealthChanged { add { } remove { } }
        public event Action Died { add { } remove { } }
        public event Action LoadoutChanged { add { } remove { } }
        public bool Contains(LoadoutItemId item) => items.Contains(item);
        public bool TryHeal(int amount) => false;
        public bool TryAddLoadoutItem(LoadoutItemId item) => false;
        public bool TryRemoveLoadoutItem(LoadoutItemId item) => false;
        public bool TryReplaceLoadoutItem(LoadoutItemId oldItem, LoadoutItemId newItem)
        {
            if (RejectReplace || !items.Contains(oldItem) || items.Contains(newItem))
            {
                return false;
            }
            items[items.IndexOf(oldItem)] = newItem;
            ReplaceCalls++;
            return true;
        }
    }
}
