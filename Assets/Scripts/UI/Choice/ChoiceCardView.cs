// 职责：显示单个选项并回传稳定 ID；外观完全来自 Prefab，不创建美术。
// 模块/维护：controller适配Soap / T01（T12地图导航）；依赖：ChoiceOption、uGUI、TMP。
// 接线：Button、标题、说明必填；Dada表现补充：可选Catalog按显式ID配图与精简文案。
// 表现交接：docs/handoffs/Dada.handoff；原选择回调/事务归属不变。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using Regrowth.Audio;
using Regrowth.UI.Art;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Regrowth.UI
{
    /// <summary>ChoicePanel 创建的 Prefab 实例；每次绑定替换自己的监听，不改按钮美术。</summary>
    public sealed class ChoiceCardView : MonoBehaviour, ICancelHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("卡片引用")]
        [SerializeField, Tooltip("选择按钮；必填。美术与过渡在 Button/Image 上编辑。")]
        private Button selectButton;
        [SerializeField, Tooltip("标题 TMP；必填，每次显示从 ChoiceOption 填充。")]
        private TMP_Text titleText;
        [SerializeField, Tooltip("说明 TMP；必填，每次显示从 ChoiceOption 填充。")]
        private TMP_Text descriptionText;
        [SerializeField, Tooltip("图标由可选的显式表现映射填写；不匹配时清空，避免复用上张卡的图。")]
        private Image icon;
        [SerializeField, Tooltip("仅显示层：稳定ID到图标和已审核精简文案；不改回传ID或奖励。")]
        private CardPresentationCatalog presentation;

        private string optionId;
        private bool optionEnabled;
        private Action<string> onSelected;
        private Action onCancelled;
        private bool pointerInside;

        internal bool HasRequiredReferences => selectButton != null && titleText != null && descriptionText != null;

        internal void Bind(ChoiceOption option, Action<string> selected, Action cancelled = null)
        {
            selectButton.onClick.RemoveListener(Select);
            optionId = option.Id;
            optionEnabled = option.IsEnabled;
            onSelected = selected;
            onCancelled = cancelled;
            titleText.text = option.Title;
            descriptionText.text = option.Description;
            if (presentation)
            {
                presentation.Resolve(option, out var sprite, out var heading, out var body);
                titleText.text = heading;
                descriptionText.text = body;
                if (icon)
                {
                    icon.sprite = sprite;
                    icon.color = Color.white;
                    icon.enabled = sprite;
                }
            }
            selectButton.onClick.AddListener(Select);
        }

        internal void SetInteractable(bool value)
        {
            selectButton.interactable = value && optionEnabled;
        }

        internal void Unbind()
        {
            pointerInside = false;
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(Select);
            }
            onSelected = null;
            onCancelled = null;
            optionId = null;
        }

        /// <summary>默认选中第一张，键盘/手柄无须先点鼠标；取消仍经菜单唯一生命周期。</summary>
        internal void Focus()
        {
            if (EventSystem.current != null && selectButton != null && selectButton.interactable)
            {
                selectButton.Select();
            }
        }
        internal void FocusIfNone()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
            {
                Focus();
            }
        }
        public void OnCancel(BaseEventData eventData)
        {
            onCancelled?.Invoke();
            eventData.Use();
        }

        private void Select()
        {
            if (optionEnabled && selectButton.interactable)
            {
                onSelected?.Invoke(optionId);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (pointerInside) return;
            pointerInside = true;
            if (isActiveAndEnabled && onSelected != null && selectButton != null && selectButton.IsInteractable())
                GameAudio.Play(AudioCue.CardHovered);
        }

        public void OnPointerExit(PointerEventData eventData) => pointerInside = false;
        private void OnDisable() => pointerInside = false;

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
