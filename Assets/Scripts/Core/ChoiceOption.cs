// 职责：卡片选项数据；不实现奖励或传送规则。
// 依赖：System.ArgumentException。维护：总控；规范：根目录 AGENTS.md。
using System;

namespace Regrowth.Core
{
    /// <summary>展示数据不执行效果；回传 Id 而不是标题。</summary>
    public sealed class ChoiceOption
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }

        public ChoiceOption(string id, string title, string description)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Option id is required.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Option title is required.", nameof(title));
            }

            Id = id;
            Title = title;
            Description = description ?? string.Empty;
        }
    }
}

