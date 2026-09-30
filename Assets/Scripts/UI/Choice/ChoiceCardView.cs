// 职责：显示单个选项并回传稳定 ID；外观完全来自 Prefab，不创建美术。
// 模块/维护：Soap / T01；依赖：ChoiceOption、uGUI、TMP。
// 接线：Button、标题、说明必填；Icon 是可选的美术预留，不从玩法推导。
// 交接：docs/handoffs/Soap.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI
{
    /// <summary>ChoicePanel 创建的 Prefab 实例；每次绑定替换自己的监听，不改按钮美术。</summary>
    public sealed class ChoiceCardView : MonoBehaviour
    {
        [Header("卡片引用")]
        [SerializeField, Tooltip("选择按钮；必填。美术与过渡在 Button/Image 上编辑。")]
        private Button selectButton;
        [SerializeField, Tooltip("标题 TMP；必填，每次显示从 ChoiceOption 填充。")]
        private TMP_Text titleText;
        [SerializeField, Tooltip("说明 TMP；必填，每次显示从 ChoiceOption 填充。")]
        private TMP_Text descriptionText;
        [SerializeField, Tooltip("可选图标位置；当前契约没有图标字段，保留 Prefab 的 Sprite，不按 ID 猜资源。")]
        private Image icon;

        private string optionId;
        private Action<string> onSelected;

        internal bool HasRequiredReferences => selectButton != null && titleText != null && descriptionText != null;

        internal void Bind(ChoiceOption option, Action<string> selected)
        {
            selectButton.onClick.RemoveListener(Select);
            optionId = option.Id;
            onSelected = selected;
            titleText.text = option.Title;
            descriptionText.text = option.Description;
            selectButton.onClick.AddListener(Select);
        }

        internal void SetInteractable(bool value)
        {
            selectButton.interactable = value;
        }

        internal void Unbind()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(Select);
            }
            onSelected = null;
            optionId = null;
        }

        private void Select()
        {
            onSelected?.Invoke(optionId);
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
