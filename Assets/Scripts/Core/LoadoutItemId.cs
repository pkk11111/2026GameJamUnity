// 职责：统一技能与装备的保留项身份；不包含即时正面/负面效果。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>统一构筑槽身份；盾/喷火/长枪预留，不表示首版已实现。</summary>
    public enum LoadoutItemId
    {
        Dash = 1,
        DoubleJump = 2,
        Shield = 3,
        FlameBreath = 4,
        UprightForm = 5,
        Sword = 101,
        Spear = 102,
    }
}
