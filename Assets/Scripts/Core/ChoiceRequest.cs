// 职责：固定候选展示请求；不实现奖励或传送规则。
// 依赖：System/Collections.Generic、ChoiceOption。维护：总控；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;

namespace Regrowth.Core
{
    /// <summary>固定展示数据；奖励/代价恰好三项，满槽替换可展示四个旧项。</summary>
    public sealed class ChoiceRequest
    {
        public string Id { get; }
        public string Title { get; }
        public IReadOnlyList<ChoiceOption> Options { get; }

        public ChoiceRequest(string id, string title, IEnumerable<ChoiceOption> options)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Request id is required.", nameof(id));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var copy = new List<ChoiceOption>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (ChoiceOption option in options)
            {
                if (option == null || !ids.Add(option.Id))
                {
                    throw new ArgumentException("Options must have unique non-null ids.", nameof(options));
                }

                copy.Add(option);
                if (copy.Count > 4)
                {
                    throw new ArgumentException("At most four display options.", nameof(options));
                }
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("At least one option.", nameof(options));
            }

            Id = id;
            Title = title ?? string.Empty;
            Options = copy.AsReadOnly();
        }
    }
}

