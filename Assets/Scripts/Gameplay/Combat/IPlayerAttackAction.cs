// Soap/T07 V5：Gameplay内部动作接点；T08绑定同一个Router，不增加Core契约。
// 直接依赖Core；交接：docs/handoffs/Soap.handoff；规则：根AGENTS.md。
using Regrowth.Core;

namespace Regrowth.Gameplay
{
    public interface IPlayerAttackAction
    {
        IPlayerCombatState CombatState { get; }
        /// <summary>Unity主线程由唯一Router调用；动作自行复验权限/冷却，拒绝返回false且不排队。</summary>
        bool TryAttack();
    }
}
