// 职责：敌人/地形伤害分类；不实现奖励或传送规则。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>骨盾只阻挡 Enemy；献祭不使用伤害入口。</summary>
    public enum DamageKind
    {
        Enemy = 0,
        Terrain = 1,
    }
}

