// 职责：显示一个只读构筑槽；外观由Prefab/TMP/Image提供，不拥有构筑状态。
// 模块/维护：Soap / T02；依赖：uGUI、TMP；交接：docs/handoffs/Soap.handoff；规范：根AGENTS.md。
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI
{
    public sealed class HudSlotView : MonoBehaviour
    {
        [SerializeField, Tooltip("必填：槽位标题，字体/颜色在TMP编辑。")]
        private TMP_Text title;
        [SerializeField, Tooltip("必填：可用性文字。")]
        private TMP_Text availability;
        [SerializeField, Tooltip("可选：条目图标，Sprite来自HUD序列化映射。")]
        private Image icon;
        [SerializeField, Tooltip("可选：空槽占位物体。")]
        private GameObject emptyView;
        [SerializeField, Tooltip("可选：不可用标记，可替换图框/颜色/动画。")]
        private GameObject unavailableView;

        public bool IsConfigured => title != null && availability != null;

        /// <summary>主线程展示当前快照，不修改状态或设置素材/颜色默认值。</summary>
        public void Display(string label, Sprite sprite, bool occupied, bool unavailable, string status)
        {
            title.text = label;
            availability.text = status;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = occupied && sprite != null;
            }
            if (emptyView != null)
            {
                emptyView.SetActive(!occupied);
            }
            if (unavailableView != null)
            {
                unavailableView.SetActive(unavailable);
            }
        }
    }
}
