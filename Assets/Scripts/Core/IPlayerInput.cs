// 职责：集中输入的只读/单次消费端口，不处理运动、攻击或能力权限。
// 模块/维护：controller，C01；直接依赖：System.Action。
// 接交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>
    /// 唯一适配器在 Update 采样；运动在 FixedUpdate 消费 Jump，攻击/交互各由唯一接收端消费。
    /// 仅 Playing 提供 gameplay 输入；按钮缓冲有有效期，离开 Playing 即清空。
    /// </summary>
    public interface IPlayerInput
    {
        float MoveX { get; }
        bool JumpHeld { get; }

        /// <summary>系统按键请求；OnEnable 订阅/OnDisable 退订。请求不自行改变阶段。</summary>
        event Action PauseRequested;

        /// <summary>成功取走一个未过期跳跃请求；失败无副作用。不能由两个运动组件竞争消费。</summary>
        bool TryConsumeJump();
        bool TryConsumeAttack();
        bool TryConsumeInteract();
        bool TryConsumeDash();

        /// <summary>主线程调用；清空采样/缓冲并屏蔽仍按住的按钮。用于迁移/失焦/阶段切换。</summary>
        void DiscardGameplayInput();
    }
}
