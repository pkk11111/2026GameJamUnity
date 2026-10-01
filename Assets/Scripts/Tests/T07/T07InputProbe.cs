// T07隔离输入观察器：转发真实Reader，不自己轮询设备/缓存或授予请求；只在Play测试临时创建。
// 维护Soap；依赖Core/UnityEngine；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Tests.T07
{
    public sealed class T07InputProbe : MonoBehaviour, IPlayerInput
    {
        public IPlayerInput Source;
        public int SuccessfulConsumes { get; private set; }
        public float MoveX => Source.MoveX;
        public bool JumpHeld => Source.JumpHeld;
        public event Action PauseRequested { add { Source.PauseRequested += value; } remove { Source.PauseRequested -= value; } }
        public bool TryConsumeAttack()
        {
            bool consumed = Source.TryConsumeAttack();
            if (consumed)
            {
                SuccessfulConsumes++;
            }
            return consumed;
        }
        public bool TryConsumeJump() => Source.TryConsumeJump();
        public bool TryConsumeInteract() => Source.TryConsumeInteract();
        public bool TryConsumeDash() => Source.TryConsumeDash();
        public void DiscardGameplayInput() => Source.DiscardGameplayInput();
    }
}
