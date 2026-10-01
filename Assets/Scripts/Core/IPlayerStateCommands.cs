// 职责：唯一玩家状态的最小写端口；调用方负责奖励/舍弃事务，不在 UI 直接调用。
// 模块/维护：controller，C02；直接依赖：LoadoutItemId。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>
    /// 主线程，由总控服务或独测驱动使用。Playing/Choosing 且存活时可提交；
    /// false 表示没有修改状态，不代表满血回血奖励不能领取。
    /// 不含百分比代价、负面叠加、生命上限修改、姿态直设或新局重置。
    /// </summary>
    public interface IPlayerStateCommands
    {
        /// <summary>正数治疗，实际回血才 true；满血/死亡/非法请求 false，不复活。</summary>
        bool TryHeal(int amount);

        /// <summary>V5保留项、未持有、槽未满才 true；满槽必须另提交替换。</summary>
        bool TryAddLoadoutItem(LoadoutItemId item);

        /// <summary>确实持有的首版项才移除；删除双手连剑一起撤下，恢复基础咬击。</summary>
        bool TryRemoveLoadoutItem(LoadoutItemId item);

        /// <summary>旧项存在、新项未持有且不同才原子替换；失败不先移除旧项。</summary>
        bool TryReplaceLoadoutItem(LoadoutItemId removedItem, LoadoutItemId addedItem);
    }
}
