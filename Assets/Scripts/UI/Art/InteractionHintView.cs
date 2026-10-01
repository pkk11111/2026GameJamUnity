// 职责：只读唯一IInteractionState，显示当前交互键与目标提示，不消费输入或执行交互。
// 维护Dada；依赖Core/TMP；显式绑定PlayerInteractor；交接docs/handoffs/Dada.handoff。
using Regrowth.Core;
using TMPro;
using UnityEngine;

namespace Regrowth.UI.Art
{
    public sealed class InteractionHintView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour interactionSource;
        [SerializeField] private GameObject hintRoot;
        [SerializeField] private TMP_Text label;
        private IInteractionState state;

        private void OnEnable()
        {
            state = interactionSource as IInteractionState;
            if (state != null) { state.TargetChanged += Refresh; }
            Refresh();
        }

        private void OnDisable()
        {
            if (state != null) { state.TargetChanged -= Refresh; }
            state = null;
            if (hintRoot) { hintRoot.SetActive(false); }
        }

        private void Refresh()
        {
            bool visible = interactionSource && state != null && state.HasTarget;
            if (hintRoot) { hintRoot.SetActive(visible); }
            if (!label) { return; }
            if (!visible) { label.text = string.Empty; return; }
            string prompt = (state.Prompt ?? string.Empty).Trim();
            // Existing targets include bare text, "[E] Open chest", or "E - ...".
            if (prompt.StartsWith("[E]", System.StringComparison.OrdinalIgnoreCase))
            { prompt = prompt.Substring(3).TrimStart(' ', '-'); }
            if (prompt.StartsWith("E - ", System.StringComparison.OrdinalIgnoreCase))
            { prompt = prompt.Substring(4).Trim(); }
            label.text = "[E]  " + (prompt.Length == 0 ? "Interact" : prompt);
        }
    }
}
