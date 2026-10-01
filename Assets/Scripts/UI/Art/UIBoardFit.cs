// 职责：1920x1080设计画板等比适配Canvas；不驱动卡内文字。
// 依赖：UnityEngine；维护Dada，交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using UnityEngine;
namespace Regrowth.UI.Art
{
    public sealed class UIBoardFit : MonoBehaviour
    {
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Vector2 referenceSize = new Vector2(1920,1080);
        [SerializeField, Tooltip("角落HUD只缩小，标准16:9下保持参考尺寸。")]
        private bool shrinkOnly;
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (!canvasRect) { return; }
            var size=canvasRect.rect.size;
            float scale=Mathf.Min(size.x/referenceSize.x,size.y/referenceSize.y);
            transform.localScale=Vector3.one*(shrinkOnly ? Mathf.Min(1,scale) : scale);
        }
    }
}
