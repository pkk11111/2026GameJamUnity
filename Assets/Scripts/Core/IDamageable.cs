// 职责：伤害接收端口；不实现奖励或传送规则。
// 依赖：DamageRequest。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>返回 true 仅表示实际扣血；default 请求、死亡或无敌须拒绝。</summary>
    public interface IDamageable
    {
        bool TryTakeDamage(DamageRequest request);
    }
}

