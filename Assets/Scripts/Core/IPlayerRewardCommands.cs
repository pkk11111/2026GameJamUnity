// 职责：总控统一正面奖励/躯干命令；仅服务调用，UI不可直接写。
// 模块/维护：controller / T12-V5；交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
using System;

namespace Regrowth.Core
{
    public interface IPlayerRewardCommands
    {
        /// <summary>
        /// 主线程Playing/Choosing且存活有躯干；全部验证后一次写入槽/历史/数值。
        /// false无副作用；满血正数回血包仍可成功。removedItem非空要求真实持有。
        /// onCommitted仅标记服务本身已消费，在状态通知之前调用，不得再写玩家或抛异常。
        /// 不包含负面/收费/重开，不授予未实现技能进入正式奖励池的许可。
        /// </summary>
        bool TryApplyReward(PlayerReward reward, LoadoutItemId? removedItem = null, Action onCommitted = null);
        /// <summary>头部安全阶段一次授予躯干并初始化配置HP；取消不调用。重复/死后/暂停拒绝。</summary>
        bool TryAcquireBodyCore(Action onCommitted = null);
    }
}
