// 职责：单箱固定三卡与唯一领取事务；共享状态只经IPlayerStateCommands写。
// Soap / T12；依赖Core/ChestRewardConfig；无UI/Runtime依赖，隔离替换测试复用同一实现。
// 交接docs/handoffs/Soap.handoff；规范根AGENTS.md。总控适配：重开排除已持有项，只补失效卡；完整奖励池等待新规则。
using System;
using System.Collections.Generic;
using Regrowth.Core;

namespace Regrowth.Gameplay
{
    internal sealed class ChestClaimTransaction
    {
        private readonly IHealth health;
        private readonly ILoadoutState loadout;
        private readonly IPlayerStateCommands commands;
        private readonly IRunContext run;
        private readonly IChoiceFlow flow;
        private readonly ChestRewardConfig config;
        private readonly string requestId;
        private readonly Action completed;
        private readonly Func<bool> ownerValid;
        private ChestRewardDefinition[] cached;
        private Session session;
        private bool submitting;
        private bool cancelRequested;
        internal bool Claimed { get; private set; }
        internal bool IsPending => session != null;
        internal IReadOnlyList<ChestRewardDefinition> Cached => cached;
        private sealed class Session
        {
            internal int Stage;
            internal ChestRewardDefinition PendingReward;
        }

        internal ChestClaimTransaction(IHealth health, ILoadoutState loadout, IPlayerStateCommands commands,
            IRunContext run, IChoiceFlow flow, ChestRewardConfig config, string requestId, Action completed, Func<bool> ownerValid)
        {
            this.health = health;
            this.loadout = loadout;
            this.commands = commands;
            this.run = run;
            this.flow = flow;
            this.config = config;
            this.requestId = requestId;
            this.completed = completed;
            this.ownerValid = ownerValid;
        }

        internal bool CanBegin => !Claimed && session == null && !submitting && ownerValid()
            && health.IsAlive && run.IsGameplayActive && !flow.IsOpen && config.IsValid;

        /// <summary>主线程；只有TryBegin成功才固定缓存。失败不消耗、不假造候选。</summary>
        internal bool TryBegin()
        {
            if (!CanBegin)
            {
                return false;
            }
            var selected = SelectCurrentOptions();
            if (selected == null)
            {
                return false;
            }
            var options = new List<ChoiceOption>();
            foreach (var reward in selected)
            {
                options.Add(new ChoiceOption(reward.Id, reward.Title, reward.Description));
            }
            var started = new Session();
            session = started;
            bool accepted = false;
            try
            {
                accepted = flow.TryBegin(new ChoiceRequest(requestId, config.ChoiceTitle, options),
                    id => ConfirmReward(started, selected, id), () => Cancel(started));
                if (accepted)
                {
                    cached = selected;
                }
                return accepted;
            }
            finally
            {
                if (!accepted && ReferenceEquals(session, started))
                {
                    session = null;
                }
            }
        }

        // 取消不刷新有效卡。跨宝箱获得的已有项必须从下次展示中移除；不足三项时拒绝打开。
        // 新数组仅在选择入口接受后缓存，失败不得部分改写旧卡组。
        private ChestRewardDefinition[] SelectCurrentOptions()
        {
            var selected = new ChestRewardDefinition[3];
            var retainedIds = new HashSet<string>(StringComparer.Ordinal);
            var retainedItems = new HashSet<LoadoutItemId>();
            int missing = 3;
            if (cached != null)
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    var reward = cached[i];
                    if (reward.Kind == ChestRewardKind.Loadout && loadout.Contains(reward.Item))
                    {
                        continue;
                    }
                    selected[i] = reward;
                    retainedIds.Add(reward.Id);
                    if (reward.Kind == ChestRewardKind.Loadout)
                    {
                        retainedItems.Add(reward.Item);
                    }
                    missing--;
                }
            }
            var legal = new List<ChestRewardDefinition>();
            foreach (var reward in config.Rewards)
            {
                if (!retainedIds.Contains(reward.Id) && (reward.Kind == ChestRewardKind.Heal
                    || (!loadout.Contains(reward.Item) && !retainedItems.Contains(reward.Item))))
                {
                    legal.Add(reward);
                }
            }
            if (legal.Count < missing)
            {
                return null;
            }
            // 无放回等概率补空位；不改变保留卡的位置、内容或顺序。
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] != null)
                {
                    continue;
                }
                int index = UnityEngine.Random.Range(0, legal.Count);
                selected[i] = legal[index].Copy();
                legal.RemoveAt(index);
            }
            return selected;
        }
        private bool CanConfirm(Session expected, int stage)
        {
            return ReferenceEquals(session, expected) && expected.Stage == stage && !Claimed && !submitting
                && ownerValid() && health.IsAlive && run.Phase == RunPhase.Choosing && config.IsValid;
        }

        private bool ConfirmReward(Session expected, ChestRewardDefinition[] selected, string id)
        {
            if (!CanConfirm(expected, 0))
            {
                return false;
            }
            ChestRewardDefinition reward = Array.Find(selected, value => value.Id == id);
            if (reward == null || !reward.IsValid
                || (reward.Kind == ChestRewardKind.Loadout && loadout.Contains(reward.Item)))
            {
                return false;
            }
            submitting = true;
            try
            {
                if (reward.Kind == ChestRewardKind.Heal)
                {
                    // 满血无变化也可领取；不造伤害，不把TryHeal的false一概视作成功。
                    bool healed = health.CurrentHealth == health.MaximumHealth || commands.TryHeal(reward.HealAmount);
                    return healed && Complete(expected);
                }
                if (loadout.Items.Count < loadout.Capacity)
                {
                    return commands.TryAddLoadoutItem(reward.Item) && Complete(expected);
                }
                if (loadout.Capacity != 4 || loadout.Items.Count != 4)
                {
                    return false;
                }
                var oldItems = new Dictionary<string, LoadoutItemId>(StringComparer.Ordinal);
                var options = new List<ChoiceOption>();
                foreach (var item in loadout.Items)
                {
                    string optionId = ((int)item).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (oldItems.ContainsKey(optionId))
                    {
                        return false;
                    }
                    oldItems.Add(optionId, item);
                    options.Add(new ChoiceOption(optionId, config.ItemTitle(item), config.ReplacementDescription));
                }
                if (flow.TryReplace(new ChoiceRequest(requestId, config.ReplacementTitle, options),
                    oldId => ConfirmReplacement(expected, reward, oldItems, oldId), () => Cancel(expected)))
                {
                    expected.Stage = 1;
                    expected.PendingReward = reward;
                }
                return false; // 切阶段绝不先写构筑/消耗箱子；维持同一个Choosing事务。
            }
            finally
            {
                submitting = false;
                FinishDeferredCancel();
            }
        }

        private bool ConfirmReplacement(Session expected, ChestRewardDefinition reward,
            Dictionary<string, LoadoutItemId> oldItems, string id)
        {
            if (!CanConfirm(expected, 1) || !ReferenceEquals(expected.PendingReward, reward)
                || !reward.IsValid || loadout.Contains(reward.Item)
                || !oldItems.TryGetValue(id, out var oldItem) || !loadout.Contains(oldItem))
            {
                return false;
            }
            submitting = true;
            try
            {
                return commands.TryReplaceLoadoutItem(oldItem, reward.Item) && Complete(expected);
            }
            finally
            {
                submitting = false;
                FinishDeferredCancel();
            }
        }

        private bool Complete(Session expected)
        {
            if (!ReferenceEquals(session, expected) || Claimed)
            {
                return false;
            }
            Claimed = true;
            session = null;
            completed();
            return true;
        }
        private void Cancel(Session expected)
        {
            if (ReferenceEquals(session, expected))
            {
                session = null;
            }
        }
        internal void CancelCurrent()
        {
            if (submitting)
            {
                cancelRequested = true;
                return;
            }
            if (session != null)
            {
                // 先使迟到业务回调失效；只有本箱持有的事务才通知协调器。
                session = null;
                if (flow.IsOpen && flow.RequestId == requestId)
                {
                    flow.Cancel();
                }
            }
        }
        private void FinishDeferredCancel()
        {
            if (cancelRequested)
            {
                cancelRequested = false;
                CancelCurrent();
            }
        }
    }
}
