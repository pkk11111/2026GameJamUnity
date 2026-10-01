// 职责：总控选择事务端口，连接业务发起者、展示者和运行阶段；UI 不调用状态写口。
// 模块/维护：controller，C03；依赖：ChoiceRequest、System；同场景只有唯一正式实现。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>Unity主线程调用；业务重新校验并结算，true才完成；不生成候选/支付/移动。</summary>
    public interface IChoiceFlow
    {
        bool IsOpen { get; }
        string RequestId { get; }

        /// <summary>普通奖励/代价恰好三项，教学/指定舍弃单卡；已有事务/非法状态拒绝，不调用回调。</summary>
        bool TryBegin(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel);

        /// <summary>同事务切换单卡确认或三个旧项；四项仅保留旧展示兼容；不关闭/不取消旧阶段，失败没有副作用。
        /// 在确认回调内切入替换阶段后须返回false；最后一阶段才实际提交并返回true。</summary>
        bool TryReplace(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel);

        /// <summary>取消当前阶段一次并释放本事务；确认中取消延后到提交回调结束。</summary>
        void Cancel();
    }
}
