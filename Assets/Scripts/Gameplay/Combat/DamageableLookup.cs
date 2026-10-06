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
        /// <summary>
        /// 从攻击者实体中心向目标最近实体点检查实心墙/关闭门；角色和Trigger不挡攻击。
        /// 不能从可能已越过薄墙的攻击锚点起射线。调用者独占复用两个scratch列表。
        /// </summary>
        public static bool IsOccluded(Vector2 from, Collider2D targetCollider, IDamageable receiver,
            Transform attacker, LayerMask layers, List<RaycastHit2D> rayScratch, List<MonoBehaviour> componentScratch)
        {
            Collider2D endpoint = targetCollider;
            if (receiver is Component component)
            {
                Collider2D solid = component.GetComponent<Collider2D>();
                if (solid != null && solid.enabled && !solid.isTrigger)
                {
                    endpoint = solid;
                }
            }
            Vector2 to = endpoint.ClosestPoint(from);
            if ((to - from).sqrMagnitude <= 0.000001f)
            {
                return false;
            }
            rayScratch.Clear();
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = false };
            Physics2D.Linecast(from, to, filter, rayScratch);
            foreach (RaycastHit2D hit in rayScratch)
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(attacker)
                    && FindInParents(hit.collider.transform, componentScratch) == null)
                {
                    return true;
                }
            }
            return false;
        }

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
