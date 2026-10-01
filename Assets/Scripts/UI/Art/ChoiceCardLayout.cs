// 职责：只排列动态创建的卡，不触碰ChoicePanel的事务/按钮监听。
// 依赖：CardVisual、uGUI；维护Dada，交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using System.Linq;
using UnityEngine;
namespace Regrowth.UI.Art
{
    public sealed class ChoiceCardLayout : MonoBehaviour
    {
        [SerializeField, Tooltip("三卡中心水平间距，参考画布像素；实时生效。")]
        private float spacing = 500;
        private float lastSpacing;
        private int signature = -1;
        private void OnEnable() { signature = -1; }
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            var cards = GetComponentsInChildren<CardVisual>().Where(c => c.transform.parent == transform).ToArray();
            int next = cards.Length;
            foreach (var c in cards) { next = unchecked(next*31+c.GetInstanceID()); }
            if (signature == next && lastSpacing == spacing) { return; }
            signature = next; lastSpacing = spacing;
            for (int i = 0; i < cards.Length; i++)
            {
                var rect = (RectTransform)cards[i].transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
                float factor = cards.Length > 3 ? .78f : 1;
                int variant=cards.Length == 1 ? 1 : i == 0 ? 0 : i == cards.Length-1 ? 2 : 1;
                rect.anchoredPosition = new Vector2((i-(cards.Length-1)*.5f)*spacing*factor,0);
                rect.localScale = Vector3.one*factor;
                cards[i].ApplyVariant(variant);
            }
        }
    }
}
