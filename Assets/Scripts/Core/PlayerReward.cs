// 职责：不可变正面奖励包；Item为空表示即时效果，不执行校验以外的业务。
// 模块/维护：controller / T12-V5；交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
namespace Regrowth.Core
{
    public readonly struct PlayerReward
    {
        public LoadoutItemId? Item { get; }
        public int Heal { get; }
        public int MaximumHealthIncrease { get; }
        public int AttackIncrease { get; }
        public int AttackPercentIncrease { get; }
        public PlayerReward(LoadoutItemId? item = null, int heal = 0, int maximumHealthIncrease = 0, int attackIncrease = 0, int attackPercentIncrease = 0)
        {
            Item = item;
            Heal = heal;
            MaximumHealthIncrease = maximumHealthIncrease;
            AttackIncrease = attackIncrease;
            AttackPercentIncrease = attackPercentIncrease;
        }
        public bool IsValid => Heal >= 0 && MaximumHealthIncrease >= 0 && AttackIncrease >= 0 && AttackPercentIncrease >= 0
            && (!Item.HasValue || LoadoutRules.IsV5Item(Item.Value))
            && (Item.HasValue || Heal > 0 || MaximumHealthIncrease > 0 || AttackIncrease > 0 || AttackPercentIncrease > 0);
    }
}
