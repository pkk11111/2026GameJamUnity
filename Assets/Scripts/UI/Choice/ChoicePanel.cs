// 职责：IChoicePresenter 的展示与回调生命周期；不拥有任何 gameplay 状态或时间倍率。
// 模块/维护：controller适配Soap / T01（T12地图导航）；依赖：Regrowth.Core、ChoiceCardView、uGUI、TMP。
// 接线：组件放在持续启用的根节点，viewRoot 为可隐藏的子节点；卡片外观来自序列化 Prefab。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI
{
    /// <summary>Unity 主线程调用；只回传 Id。调用者负责阶段锁、重新验证和实际结算。</summary>
    public sealed class ChoicePanel : MonoBehaviour, IChoicePresenter
    {
        [Header("展示引用")]
        [SerializeField, Tooltip("需要隐藏的子节点；必填，不能是组件所在根节点。")]
        private GameObject viewRoot;
        [SerializeField, Tooltip("请求标题 TMP；必填。字体与颜色在 TMP 组件上修改。")]
        private TMP_Text requestTitle;
        [SerializeField, Tooltip("卡片容器；必填。排列和尺寸由容器/Prefab 的布局组件配置。")]
        private Transform cardContainer;
        [SerializeField, Tooltip("独立 ChoiceCardView Prefab；必填，修改该资产可替换所有卡片美术。")]
        private ChoiceCardView cardPrefab;
        [SerializeField, Tooltip("取消按钮；必填，组件启用时订阅，停用时退订。")]
        private Button cancelButton;
        [SerializeField, Tooltip("拒绝确认提示 TMP；必填，false 时显示，不关闭事务。")]
        private TMP_Text feedbackText;
        [SerializeField, TextArea, Tooltip("确认未被接受时的提示；实时读取，不改变业务。")]
        private string rejectedMessage = "Selection was not accepted. Please try again or cancel.";

        private readonly List<ChoiceCardView> cards = new List<ChoiceCardView>();
        private ChoiceRequest currentRequest;
        private Func<string, bool> confirm;
        private Action cancel;
        private bool submitting;
        private bool cancelPending;
        private int revision;

        public bool IsOpen => currentRequest != null;

        private void Awake()
        {
            if (viewRoot != null && viewRoot != gameObject)
            {
                viewRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(CancelFromButton);
            }
        }

        private void OnDisable()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelFromButton);
            }
            CancelCurrent();
        }

        /// <summary>已有事务、停用、空参数或缺引用时返回 false，无回调。</summary>
        public bool TryShow(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel)
        {
            if (IsOpen || submitting || !CanPresent(request, tryConfirm))
            {
                return false;
            }
            Present(request, tryConfirm, onCancel);
            return true;
        }

        /// <summary>仅允许相同请求 Id；替换展示和回调，不取消旧阶段，不关闭事务。</summary>
        public bool TryReplaceCurrent(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel)
        {
            if (!IsOpen || request == null || request.Id != currentRequest.Id || !CanPresent(request, tryConfirm))
            {
                return false;
            }
            Present(request, tryConfirm, onCancel);
            return true;
        }

        /// <summary>先清理再通知一次；确认栈中的取消延至返回，避免回调重入。</summary>
        public void CancelCurrent()
        {
            if (!IsOpen)
            {
                return;
            }
            if (submitting)
            {
                cancelPending = true;
                return;
            }
            Action callback = cancel;
            Close();
            callback?.Invoke();
        }

        private void CancelFromButton()
        {
            if (!IsOpen || submitting) return;
            GameAudio.Play(AudioCue.UIConfirm);
            CancelCurrent();
        }

        private bool CanPresent(ChoiceRequest request, Func<string, bool> tryConfirm)
        {
            if (!isActiveAndEnabled || request == null || tryConfirm == null)
            {
                return false;
            }
            if (viewRoot == null || viewRoot == gameObject || !viewRoot.transform.IsChildOf(transform) ||
                requestTitle == null || cardContainer == null || cardPrefab == null ||
                !cardPrefab.HasRequiredReferences || cancelButton == null || feedbackText == null)
            {
                Debug.LogError("[T01] ChoicePanel: missing/invalid Inspector references (including Card Prefab).", this);
                return false;
            }
            return true;
        }

        private void Present(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel)
        {
            ClearCards();
            currentRequest = request;
            confirm = tryConfirm;
            cancel = onCancel;
            revision++;
            requestTitle.text = request.Title;
            feedbackText.text = string.Empty;
            foreach (ChoiceOption option in request.Options)
            {
                ChoiceCardView card = Instantiate(cardPrefab, cardContainer, false);
                card.Bind(option, Submit, CancelCurrent);
                card.SetInteractable(!submitting);
                cards.Add(card);
            }
            viewRoot.SetActive(true);
            GameAudio.Play(AudioCue.CardsPresented);
            if (cards.Count > 0)
            {
                cards[0].Focus();
            }
        }

        private void Submit(string optionId)
        {
            if (!IsOpen || submitting)
            {
                return;
            }
            bool valid = false;
            foreach (ChoiceOption option in currentRequest.Options)
            {
                valid |= option.Id == optionId;
            }
            if (!valid)
            {
                return;
            }
            submitting = true;
            SetInteractable(false);
            int submittedRevision = revision;
            try
            {
                bool accepted = confirm(optionId);
                if (accepted || (IsOpen && submittedRevision != revision))
                {
                    GameAudio.Play(AudioCue.CardSelected);
                }
                // 回调可把同事务换为新阶段并返回 false；不要覆盖新阶段的提示。
                if (accepted && IsOpen)
                {
                    Close();
                }
                else if (IsOpen && submittedRevision == revision)
                {
                    feedbackText.text = rejectedMessage;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (IsOpen)
                {
                    feedbackText.text = rejectedMessage;
                }
            }
            finally
            {
                submitting = false;
                SetInteractable(true);
                if (IsOpen && cards.Count > 0)
                {
                    cards[0].FocusIfNone();
                }
                if (cancelPending)
                {
                    cancelPending = false;
                    CancelCurrent();
                }
            }
        }

        private void SetInteractable(bool value)
        {
            foreach (ChoiceCardView card in cards)
            {
                card.SetInteractable(value);
            }
            if (cancelButton != null)
            {
                cancelButton.interactable = value;
            }
        }

        private void Close()
        {
            currentRequest = null;
            confirm = null;
            cancel = null;
            revision++;
            viewRoot.SetActive(false);
            ClearCards();
        }

        private void ClearCards()
        {
            foreach (ChoiceCardView card in cards)
            {
                card.Unbind();
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            cards.Clear();
        }
    }
}
