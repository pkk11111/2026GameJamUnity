// 职责：卡片展示端口；不实现奖励或传送规则。
// 依赖：System.Func/Action、ChoiceRequest。维护：总控；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>只呈现选择；确认 false 不关闭；取消通知一次；实现尚未提供。</summary>
    public interface IChoicePresenter
    {
        bool IsOpen { get; }
        bool TryShow(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel);
        /// <summary>同请求 Id 内切换到替换阶段；不关闭、不取消、不释放输入锁。</summary>
        bool TryReplaceCurrent(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel);
        void CancelCurrent();
    }
}

