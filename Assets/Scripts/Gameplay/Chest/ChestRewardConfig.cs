// 职责：只保存宝箱奖励与替换文案；不保存卡组、生命或已领取状态。
// Soap / T12；依赖Core/Unity；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    public enum ChestRewardKind { Loadout, Heal }

    [Serializable]
    public sealed class ChestRewardDefinition
    {
        [SerializeField, Tooltip("配置唯一稳定ID；首次成功打开时复制快照。")] private string id;
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private ChestRewardKind kind;
        [SerializeField, Tooltip("仅Dash/DoubleJump/UprightForm/Sword合法。")] private LoadoutItemId item;
        [SerializeField, Min(1), Tooltip("正数HP，首次打开读取；满血也允许领取。")] private int healAmount = 20;
        public string Id => id;
        public string Title => title;
        public string Description => description;
        public ChestRewardKind Kind => kind;
        public LoadoutItemId Item => item;
        public int HealAmount => healAmount;
        public bool IsValid => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(title)
            && ((kind == ChestRewardKind.Heal && healAmount > 0)
                || (kind == ChestRewardKind.Loadout && IsAllowed(item)));
        public static bool IsAllowed(LoadoutItemId value)
        {
            return value == LoadoutItemId.Dash || value == LoadoutItemId.DoubleJump
                || value == LoadoutItemId.UprightForm || value == LoadoutItemId.Sword;
        }
        internal ChestRewardDefinition Copy() => (ChestRewardDefinition)MemberwiseClone();
    }

    [CreateAssetMenu(menuName = "GROWL AGAIN/Chest Reward Config")]
    public sealed class ChestRewardConfig : ScriptableObject
    {
        [SerializeField, Tooltip("至少3个当前合法不同条目；保留项不能重复身份，不配置预留项。")]
        private ChestRewardDefinition[] rewards;
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
                if (rewards == null || rewards.Length < 3)
                {
                    return false;
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var items = new HashSet<LoadoutItemId>();
                foreach (var reward in rewards)
                {
                    if (reward == null || !reward.IsValid || !ids.Add(reward.Id)
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
