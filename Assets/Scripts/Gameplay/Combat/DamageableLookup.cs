// 职责：战斗碰撞共用的伤害接收者查找；保持从碰撞节点向父级、按组件顺序取首个的语义。
// 维护：controller/structure-audit；依赖Core与Unity；不缓存实体、不持有生命或结算伤害。
// 交接：docs/handoffs/structure-audit.handoff；规范：根AGENTS.md。
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    public static class DamageableLookup
    {
        /// <summary>主线程同步查询；无接收者返回null。调用者独占并复用scratch，避免每层创建组件数组。</summary>
        public static IDamageable FindInParents(Transform node, List<MonoBehaviour> scratch)
        {
            for (Transform current = node; current != null; current = current.parent)
            {
                scratch.Clear();
                current.GetComponents(scratch);
                foreach (MonoBehaviour component in scratch)
                {
                    if (component is IDamageable receiver)
                    {
                        return receiver;
                    }
                }
            }
            return null;
        }
    }
}
