// 职责：V5宝箱配置/原子领取；依赖Core，真实状态归PlayerState，UI只展示。
// 模块/维护：controller / T12（适配Soap模块）；交接：docs/handoffs/controller.handoff；规范：根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;

namespace Regrowth.Gameplay
{
    internal sealed class ChestClaimTransaction
    {
        private readonly IHealth health;
        private readonly IPlayerBodyState body;
        private readonly IPlayerRewardCommands rewards;
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
            body = health as IPlayerBodyState;
            rewards = commands as IPlayerRewardCommands;
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
            && health.IsAlive && run.IsGameplayActive && !flow.IsOpen && config.IsValid
            && (body == null || config.BodyTutorial != body.HasBodyCore);

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
                options.Add(new ChoiceOption(reward.Id, DisplayTitle(reward), Description(reward)));
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
            IReadOnlyList<ChestRewardDefinition> retained = cached;
            // Legacy test-only regrowth option; production configs disable it.
            if (cached == null && config.FavorRegrowth && body != null)
            {
                var preferred = new List<ChestRewardDefinition>();
                foreach (var reward in config.Rewards)
                {
                    if (IsAvailable(reward) && reward.Kind == ChestRewardKind.Loadout && body.WasEverOwned(reward.Item))
                    {
                        preferred.Add(reward);
                    }
                }
                if (preferred.Count > 0)
                {
                    retained = new[] { preferred[UnityEngine.Random.Range(0, preferred.Count)] };
                }
            }
            return FixedChoiceDeck.Select(retained, config.Rewards, config.BodyTutorial ? 1 : 3,
                value => value.Id, IsAvailable, count => UnityEngine.Random.Range(0, count), value => value.Copy());
        }
        private bool IsAvailable(ChestRewardDefinition reward)
        {
            if (reward.Kind == ChestRewardKind.BodyCore)
            {
                return body != null && !body.HasBodyCore;
            }
            if (reward.Kind != ChestRewardKind.Loadout)
            {
                return true;
            }
            if (loadout.Contains(reward.Item))
            {
                return false;
            }
            foreach (var held in loadout.Items)
            {
                // 互斥尾巴可作为明确换尾候选；同喷火来源不能重复。
                if (LoadoutRules.Conflicts(held, reward.Item)
                    && !(LoadoutRules.IsTail(held) && LoadoutRules.IsTail(reward.Item)))
                {
                    return false;
                }
            }
            return true;
        }
        private string DisplayTitle(ChestRewardDefinition reward)
        {
            return reward.Kind == ChestRewardKind.Loadout && body != null && body.WasEverOwned(reward.Item)
                && !string.IsNullOrWhiteSpace(reward.RegrowthTitle) ? reward.RegrowthTitle : reward.Title;
        }

        private string Description(ChestRewardDefinition reward)
        {
            if (!config.UseCombatPercentages)
            {
                return reward.Description;
            }
            switch (reward.Kind)
            {
                case ChestRewardKind.Heal:
                    return "Restore " + ((long)health.MaximumHealth * config.HealPercent + 99) / 100 + " HP."
                        + (health.CurrentHealth == health.MaximumHealth ? " Already at full health." : "");
                case ChestRewardKind.MaximumHealth:
                    return "Increase maximum and current HP by " + ((long)health.MaximumHealth * config.MaximumHealthPercent + 99) / 100 + ".";
                case ChestRewardKind.Attack:
                    return "All attacks: +" + config.AttackPointIncrease + " damage per hit (including each fire tick).";
                default: return reward.Description;
            }
        }
        private bool Apply(Session expected, ChestRewardDefinition reward, LoadoutItemId? removed = null)
        {
            if (rewards != null)
            {
                if (reward.Kind == ChestRewardKind.BodyCore)
                {
                    return rewards.TryAcquireBodyCore(() => Complete(expected));
                }
                int heal = reward.Kind == ChestRewardKind.Heal ? reward.HealAmount : reward.BonusHeal;
                if (config.UseCombatPercentages && reward.Kind != ChestRewardKind.Loadout)
                {
                    long amount = reward.Kind == ChestRewardKind.Heal
                        ? ((long)health.MaximumHealth * config.HealPercent + 99) / 100
                        : ((long)health.MaximumHealth * config.MaximumHealthPercent + 99) / 100;
                    if (amount > int.MaxValue)
                    {
                        return false;
                    }
                    PlayerReward percentageReward = reward.Kind == ChestRewardKind.Heal ? new PlayerReward(heal: (int)amount)
                        : reward.Kind == ChestRewardKind.MaximumHealth ? new PlayerReward(heal: (int)amount, maximumHealthIncrease: (int)amount)
                        : new PlayerReward(attackIncrease: config.AttackPointIncrease);
                    return rewards.TryApplyReward(percentageReward, null, () => Complete(expected));
                }
                var grant = new PlayerReward(reward.Kind == ChestRewardKind.Loadout ? (LoadoutItemId?)reward.Item : null,
                    heal, reward.Kind == ChestRewardKind.MaximumHealth ? reward.EffectAmount : 0,
                    reward.Kind == ChestRewardKind.Attack ? reward.EffectAmount : 0);
                return rewards.TryApplyReward(grant, removed, () => Complete(expected));
            }
            // 旧独测替身兼容；正式Chest要求IPlayerRewardCommands，不会走这里。
            if (reward.BonusHeal != 0)
            {
                return false;
            }
            bool applied = reward.Kind == ChestRewardKind.Heal
                ? health.CurrentHealth == health.MaximumHealth || commands.TryHeal(reward.HealAmount)
                : reward.Kind == ChestRewardKind.Loadout && (removed.HasValue
                    ? commands.TryReplaceLoadoutItem(removed.Value, reward.Item) : commands.TryAddLoadoutItem(reward.Item));
            return applied && Complete(expected);
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
                || !IsAvailable(reward))
            {
                return false;
            }
            submitting = true;
            try
            {
                if (reward.Kind != ChestRewardKind.Loadout)
                {
                    return Apply(expected, reward);
                }
                LoadoutItemId? oldTail = null;
                foreach (var held in loadout.Items)
                {
                    if (LoadoutRules.IsTail(held) && LoadoutRules.IsTail(reward.Item))
                    {
                        oldTail = held;
                    }
                }
                if (oldTail.HasValue)
                {
                    var replacement = oldTail.Value;
                    var swap = new ChoiceOption("swap-tail", "Replace " + config.ItemTitle(replacement),
                        "Lose the old tail and its ability; gain the selected tail.");
                    if (flow.TryReplace(new ChoiceRequest(requestId, config.ReplacementTitle, new[] { swap }),
                        oldId => ConfirmReplacement(expected, reward,
                            new Dictionary<string, LoadoutItemId> { { "swap-tail", replacement } }, oldId), () => Cancel(expected)))
                    {
                        expected.Stage = 1;
                        expected.PendingReward = reward;
                    }
                    return false;
                }
                if (loadout.Items.Count < loadout.Capacity)
                {
                    return Apply(expected, reward);
                }
                if (loadout.Capacity != LoadoutRules.Capacity || loadout.Items.Count != LoadoutRules.Capacity)
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
                || !reward.IsValid || !IsAvailable(reward)
                || !oldItems.TryGetValue(id, out var oldItem) || !loadout.Contains(oldItem))
            {
                return false;
            }
            submitting = true;
            try
            {
                return Apply(expected, reward, oldItem);
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
