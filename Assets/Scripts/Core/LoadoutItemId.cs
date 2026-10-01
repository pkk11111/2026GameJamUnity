// 职责：统一技能与装备的保留项身份；不包含即时正面/负面效果。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>稳定构筑身份。旧 Dash/DoubleJump/UprightForm/Sword 数值只留迁移追溯；V5 使用 Legs/Arms/Tail。喷火两种载体预留。</summary>
    public enum LoadoutItemId
    {
        Dash = 1,
        DoubleJump = 2,
        Shield = 3,
        FlameBreath = 4,
        UprightForm = 5,
        Sword = 101,
        Spear = 102,
        Legs = 201,
        Arms = 202,
        Tail = 203,
        FlameTail = 204,
    }
}
