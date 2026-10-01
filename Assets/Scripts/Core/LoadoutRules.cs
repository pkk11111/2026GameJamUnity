// 职责：三槽结构和V5身份/来源互斥；纯规则不持有状态、不依赖Runtime。
// 模块/维护：controller / T12-V5；交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
namespace Regrowth.Core
{
    public static class LoadoutRules
    {
        public const int Capacity = 3;
        public static bool IsBodyItem(LoadoutItemId item) =>
            item == LoadoutItemId.Legs || item == LoadoutItemId.Arms || IsTail(item);
        public static bool IsTail(LoadoutItemId item) =>
            item == LoadoutItemId.Tail || item == LoadoutItemId.FlameTail;
        public static bool IsV5Item(LoadoutItemId item) =>
            IsBodyItem(item) || item == LoadoutItemId.FlameBreath;
        /// <summary>不同身份是否占用互斥身体位置或授予同一喷火来源。</summary>
        public static bool Conflicts(LoadoutItemId first, LoadoutItemId second) =>
            (IsTail(first) && IsTail(second))
            || (first == LoadoutItemId.FlameTail && second == LoadoutItemId.FlameBreath)
            || (second == LoadoutItemId.FlameTail && first == LoadoutItemId.FlameBreath);
    }
}
