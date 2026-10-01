// 职责：宝箱与传送共用的固定候选抽取；不持有状态、不执行奖励/代价。
// 维护：controller/C08；依赖System；交接docs/handoffs/portal-rules.handoff；规范AGENTS.md。
using System;
using System.Collections.Generic;

namespace Regrowth.Core
{
    public static class FixedChoiceDeck
    {
        /// <summary>保留合法卡位，仅无放回补缺；不足则null。调用者仅在菜单接受后保存结果。</summary>
        public static T[] Select<T>(IReadOnlyList<T> cached, IEnumerable<T> pool, int count,
            Func<T, string> id, Func<T, bool> available, Func<int, int> randomIndex, Func<T, T> copy) where T : class
        {
            var result = new T[count];
            var used = new HashSet<string>(StringComparer.Ordinal);
            int missing = count;
            for (int i = 0; cached != null && i < count && i < cached.Count; i++)
            {
                T value = cached[i];
                if (value != null && available(value) && used.Add(id(value)))
                {
                    result[i] = copy(value);
                    missing--;
                }
            }
            var legal = new List<T>();
            var poolIds = new HashSet<string>(used, StringComparer.Ordinal);
            foreach (T value in pool)
            {
                if (value != null && available(value) && poolIds.Add(id(value)))
                {
                    legal.Add(value);
                }
            }
            if (legal.Count < missing)
            {
                return null;
            }
            for (int i = 0; i < count; i++)
            {
                if (result[i] != null)
                {
                    continue;
                }
                int selected = randomIndex(legal.Count);
                result[i] = copy(legal[selected]);
                legal.RemoveAt(selected);
            }
            return result;
        }
    }
}
