// 职责：传送代价稳定身份；数值由配置给出，不在数据层执行业务。
// 维护controller/C08；依赖无；交接docs/handoffs/portal-rules.handoff；规范AGENTS.md。
namespace Regrowth.Core
{
    public enum TeleportCostKind
    {
        ShedLegs = 1,
        ShedArms = 2,
        ShedTail = 3,
        ShedFlameTail = 4,
        CurrentHealth = 5,
        MaximumHealth = 6,
        Attack = 7,
        EnemyHealth = 8,
        EnemyAttack = 9
    }
}
