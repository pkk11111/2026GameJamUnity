// 职责：唯一头顶提示视图，优先真实交互候选，其次附近教学；不消费输入或执行交互。
// 维护：controller/ui-readability；依赖Core/TMP/uGUI；状态仍由RunController/PlayerInteractor持有。
// 接线：显式绑定阶段、交互器、玩家碰撞体、相机及教学区域；LateUpdate跟随头顶，停用隐藏。
// 交接：docs/handoffs/ui-readability.handoff；规范：根目录AGENTS.md。
using System;
using Regrowth.Core;
using TMPro;
using UnityEngine;

namespace Regrowth.UI.Art
{
    public sealed class InteractionHintView : MonoBehaviour
    {
        [Serializable]
        private sealed class TutorialHint
        {
            public Transform anchor;
            [Min(0.1f)] public float radius = 3f;
            [TextArea(1, 2)] public string text;
        }

        [Serializable]
        private sealed class PromptCopy
        {
            public string source;
            public string text;
        }

        [SerializeField, Tooltip("唯一IInteractionState来源；只读当前可执行交互。")]
        private MonoBehaviour interactionSource;
        [SerializeField, Tooltip("唯一IRunContext来源；非Playing时不显示任何提示。")]
        private MonoBehaviour runSource;
        [SerializeField, Tooltip("玩家实体碰撞体，使用其顶边定位；不修改碰撞。")]
        private Collider2D playerBounds;
        [SerializeField, Tooltip("实际游戏相机；投影到HUD画布，不自动查找。")]
        private Camera worldCamera;
        [SerializeField] private GameObject hintRoot;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0f), Tooltip("碰撞体顶边以上的世界单位距离，实时生效。")]
        private float headOffset = 0.45f;
        [SerializeField, Min(100f), Tooltip("提示宽度，画布参考像素；实时生效。")]
        private float width = 620f;
        [SerializeField, Min(0f), Tooltip("距离画布边缘的参考像素，避免提示出屏。")]
        private float edgePadding = 24f;
        [SerializeField, Tooltip("附近教学区域；重叠取最近，同距沿Inspector顺序；只显示一条。")]
        private TutorialHint[] tutorials = Array.Empty<TutorialHint>();
        [SerializeField, Tooltip("精简文案映射，仅改显示，不改变交互目标或按键。")]
        private PromptCopy[] promptCopies = Array.Empty<PromptCopy>();

        private IInteractionState state;
        private IRunContext run;
        private RectTransform panel;
        private RectTransform parent;
        private Canvas canvas;

        private void OnEnable()
        {
            state = interactionSource as IInteractionState;
            run = runSource as IRunContext;
            panel = hintRoot ? hintRoot.GetComponent<RectTransform>() : null;
            parent = panel ? panel.parent as RectTransform : null;
            canvas = panel ? panel.GetComponentInParent<Canvas>() : null;
            Hide();
        }

        private void Start()
        {
            if (state == null || run == null || !playerBounds || !worldCamera || !panel || !parent || !canvas || !label)
            {
                Debug.LogWarning("InteractionHintView：检查交互器、阶段、玩家碰撞体、相机和HUD引用。", this);
            }
        }

        private void OnDisable()
        {
            state = null;
            run = null;
            Hide();
        }

        // 每帧读取快照同时覆盖阶段/距离变化；无额外订阅，也没有独立交互选择器。
        private void LateUpdate()
        {
            if (!interactionSource || !runSource || state == null || run == null || !run.IsGameplayActive
                || !playerBounds || !worldCamera || !panel || !parent || !canvas || !label)
            {
                Hide();
                return;
            }

            string text = state.HasTarget ? InteractionText() : TutorialText();
            if (string.IsNullOrWhiteSpace(text))
            {
                Hide();
                return;
            }

            Bounds bounds = playerBounds.bounds;
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(new Vector3(
                bounds.center.x, bounds.max.y + headOffset, bounds.center.z));
            if (screenPoint.z <= 0f)
            {
                Hide();
                return;
            }

            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, uiCamera, out Vector2 local))
            {
                Hide();
                return;
            }

            if (label.text != text)
            {
                label.text = text;
            }
            float panelWidth = Mathf.Min(width, Mathf.Max(32f, parent.rect.width - edgePadding * 2f));
            float panelHeight = label.GetPreferredValues(text, panelWidth - 16f, 0f).y + 16f;
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);
            // 固定中心锚点、底部pivot；靠近边缘时仅移动显示，绝不移动玩家。
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0f);
            Rect rect = parent.rect;
            local.x = Mathf.Clamp(local.x, rect.xMin + panelWidth * 0.5f + edgePadding,
                rect.xMax - panelWidth * 0.5f - edgePadding);
            local.y = Mathf.Clamp(local.y, rect.yMin + edgePadding,
                Mathf.Max(rect.yMin + edgePadding, rect.yMax - panelHeight - edgePadding));
            panel.localPosition = new Vector3(local.x, local.y, 0f);
            hintRoot.SetActive(true);
        }

        private string InteractionText()
        {
            string prompt = (state.Prompt ?? string.Empty).Trim();
            if (prompt.StartsWith("[E]", StringComparison.OrdinalIgnoreCase))
            {
                prompt = prompt.Substring(3).TrimStart(' ', '-');
            }
            if (prompt.StartsWith("E - ", StringComparison.OrdinalIgnoreCase))
            {
                prompt = prompt.Substring(4).Trim();
            }
            foreach (PromptCopy copy in promptCopies)
            {
                if (copy != null && string.Equals(copy.source, prompt, StringComparison.Ordinal))
                {
                    return copy.text;
                }
            }
            return "[E] " + (prompt.Length == 0 ? "Interact" : prompt);
        }

        private string TutorialText()
        {
            float best = float.PositiveInfinity;
            string text = string.Empty;
            foreach (TutorialHint hint in tutorials)
            {
                if (hint == null || !hint.anchor || !hint.anchor.gameObject.activeInHierarchy
                    || hint.radius <= 0f || string.IsNullOrWhiteSpace(hint.text))
                {
                    continue;
                }
                float distance = ((Vector2)hint.anchor.position - (Vector2)playerBounds.transform.position).sqrMagnitude;
                if (distance <= hint.radius * hint.radius && distance < best)
                {
                    best = distance;
                    text = hint.text;
                }
            }
            return text;
        }

        private void Hide()
        {
            if (hintRoot)
            {
                hintRoot.SetActive(false);
            }
            if (label)
            {
                label.text = string.Empty;
            }
        }
    }
}
