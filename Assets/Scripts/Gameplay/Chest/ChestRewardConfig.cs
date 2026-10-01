// 职责：V5宝箱配置/原子领取；依赖Core，真实状态归PlayerState，UI只展示。
// 模块/维护：controller / T12（适配Soap模块）；交接：docs/handoffs/controller.handoff；规范：根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    public enum ChestRewardKind { Loadout = 0, Heal = 1, MaximumHealth = 2, Attack = 3, BodyCore = 4 }

    [Serializable]
    public sealed class ChestRewardDefinition
    {
        [SerializeField, Tooltip("配置唯一稳定ID；首次成功打开时复制快照。")] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ChestRewardKind kind;
        [SerializeField, Tooltip("V5身体/技能身份；旧Dash/DoubleJump/UprightForm/Sword不可配置。")] private LoadoutItemId item;
        [SerializeField, Min(1), Tooltip("正数HP，首次打开读取；满血也允许领取。")] private int healAmount = 20;
        [SerializeField, Min(1), Tooltip("加上限/加攻击数值；上限增加不自动回血，可另配bonusHeal。")] private int effectAmount = 10;
        [SerializeField, Min(0), Tooltip("部件再生或上限奖励附带回血；所有效果同时提交，取消不回血。")] private int bonusHeal;
        [SerializeField, Tooltip("曾拥有且当前缺失时的卡名；为空则用原卡名。")] private string regrowthTitle;
        public int EffectAmount => effectAmount;
        public int BonusHeal => bonusHeal;
        public string RegrowthTitle => regrowthTitle;
        public string Id => id;
        public string Title => title;
        public string Description => description;
        public ChestRewardKind Kind => kind;
        public LoadoutItemId Item => item;
        public int HealAmount => healAmount;
        public bool IsValid => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title)
            && bonusHeal >= 0 && ((kind == ChestRewardKind.Heal && healAmount > 0)
                || ((kind == ChestRewardKind.MaximumHealth || kind == ChestRewardKind.Attack) && effectAmount > 0)
                || kind == ChestRewardKind.BodyCore || (kind == ChestRewardKind.Loadout && IsAllowed(item)));
        public static bool IsAllowed(LoadoutItemId value)
        {
            return LoadoutRules.IsV5Item(value);
        }
        internal ChestRewardDefinition Copy() => (ChestRewardDefinition)MemberwiseClone();
    }

    [CreateAssetMenu(menuName = "pawgatory/Chest Reward Config")]
    public sealed class ChestRewardConfig : ScriptableObject
    {
        [SerializeField, Tooltip("至少3个当前合法不同条目；保留项不能重复身份，不配置预留项。")]
        private ChestRewardDefinition[] rewards;
        [SerializeField, Tooltip("只用于固定躯干箱；恰好一个BodyCore奖励，不进入普通池。")] private bool bodyTutorial;
        [SerializeField, Tooltip("首次生成时优先一项合法的曾持有缺失身体；其余等概率。")] private bool favorRegrowth;
        public bool BodyTutorial => bodyTutorial;
        public bool FavorRegrowth => favorRegrowth;
        [SerializeField, Tooltip("按combat规则使用百分比回血/上限与共享攻击百分点；正式主图启用，旧独测可保留固定值。")]
        private bool useCombatPercentages;
        [SerializeField, Range(1, 100)] private int healPercent = 20;
        [SerializeField, Range(1, 100)] private int maximumHealthPercent = 10;
        [SerializeField, Min(1)] private int attackPercentIncrease = 10;
        public bool UseCombatPercentages => useCombatPercentages;
        public int HealPercent => healPercent;
        public int MaximumHealthPercent => maximumHealthPercent;
        public int AttackPercentIncrease => attackPercentIncrease;
        [SerializeField] private string choiceTitle = "Choose one reward";
        [SerializeField] private string replacementTitle = "Choose an old item to replace";
        [SerializeField] private string replacementDescription = "Replace this held item.";
        public string ChoiceTitle => choiceTitle;
        public string ReplacementTitle => replacementTitle;
        public string ReplacementDescription => replacementDescription;
        public IReadOnlyList<ChestRewardDefinition> Rewards => rewards;

        public bool IsValid
        {
            get
            {
                if (rewards == null || (bodyTutorial ? rewards.Length != 1 : rewards.Length < 3)
                    || (useCombatPercentages && (healPercent < 1 || healPercent > 100
                    || maximumHealthPercent < 1 || maximumHealthPercent > 100 || attackPercentIncrease < 1)))
                {
                    return false;
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var items = new HashSet<LoadoutItemId>();
                foreach (var reward in rewards)
                {
                    if (reward == null || !reward.IsValid || !ids.Add(reward.Id)
                        || (bodyTutorial != (reward.Kind == ChestRewardKind.BodyCore))
                        || (reward.Kind == ChestRewardKind.Loadout && !items.Add(reward.Item)))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        internal string ItemTitle(LoadoutItemId item)
        {
            foreach (var reward in rewards)
            {
                if (reward.Kind == ChestRewardKind.Loadout && reward.Item == item)
                {
                    return reward.Title;
                }
            }
            return item.ToString(); // 未配置的旧项仍可标识；测试旧身份不进入正式池。
        }
    }
}
