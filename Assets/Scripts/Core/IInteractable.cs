// 职责：交互接收端口；不实现奖励或传送规则。
// 依赖：UnityEngine.GameObject、InteractionKind。维护：总控；规范：根目录 AGENTS.md。
using UnityEngine;

namespace Regrowth.Core
{
    /// <summary>接受交互不等于支付完成；Id 必须在场景中唯一稳定。</summary>
    public interface IInteractable
    {
        string InteractionId { get; }
        InteractionKind Kind { get; }
        string Prompt { get; }
        bool CanInteract(GameObject actor);
        bool TryInteract(GameObject actor);
    }
}

