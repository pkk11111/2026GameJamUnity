// 职责：唯一交互器当前候选的只读提示端口；不发起交互或执行奖励。
// 模块/维护：controller，C03；依赖：System.Action；HUD 只读此视图。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>OnEnable 读快照并订阅，OnDisable 退订；暂停/无候选时为空。</summary>
    public interface IInteractionState
    {
        bool HasTarget { get; }
        string InteractionId { get; }
        string Prompt { get; }
        event Action TargetChanged;
    }
}
