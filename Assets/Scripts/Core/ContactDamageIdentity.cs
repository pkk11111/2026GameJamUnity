// 职责：为已有场景接触伤害源生成本局稳定标识，不使用GetInstanceID。
// 维护：structure-audit；依赖Unity；在来源Awake首次捕获，不每步分配字符串。
// 交接docs/handoffs/structure-audit.handoff；规范：根AGENTS.md。
using UnityEngine;

namespace Regrowth.Core
{
    public static class ContactDamageIdentity
    {
        /// <summary>按场景路径/层级名字和兄弟序号生成；调用者捕获一次，启停不重新生成。</summary>
        public static string Capture(Component source)
        {
            string path = source.GetType().FullName;
            for (Transform node = source.transform; node != null; node = node.parent)
            {
                path = node.name + "[" + node.GetSiblingIndex() + "]/" + path;
            }
            return source.gameObject.scene.path + "/" + path;
        }
    }
}
