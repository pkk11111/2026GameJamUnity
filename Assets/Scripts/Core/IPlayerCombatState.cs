// 职责：玩家攻击数值与当前攻击权限的只读视图，不执行输入/攻击/伤害。
// 模块/维护：controller，C02；直接依赖：无。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>由唯一 PlayerState 提供；动作开始与命中时都重新验证，不能从 Sprite 推导。</summary>
    public interface IPlayerCombatState
    {
        int BiteDamage { get; }
        int SwordDamage { get; }
        bool CanBite { get; }
        bool CanUseSword { get; }
    }
}
