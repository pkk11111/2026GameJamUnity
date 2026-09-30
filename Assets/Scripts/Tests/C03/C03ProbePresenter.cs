// 职责：C03测试用IChoicePresenter替身及可点击按钮，验证总控事务；正式UI由T01实现。
// 模块/维护：controller，C03测试；依赖：Core/uGUI/TMP；不得放正式入口。
// 接线：显式面板、文字、四选项按钮和取消按钮，组件自身保持启用。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.Tests.C03
{
    public sealed class C03ProbePresenter : MonoBehaviour, IChoicePresenter
    {
        [SerializeField, Tooltip("必填测试面板，不能是本组件物体。")] private GameObject panelRoot;
        [SerializeField, Tooltip("必填测试文字。")] private TMP_Text choiceText;
        [SerializeField, Tooltip("必填四个测试选项按钮。")] private Button[] optionButtons = new Button[4];
        [SerializeField, Tooltip("必填取消测试按钮。")] private Button cancelButton;
        private ChoiceRequest request;
        private Func<string, bool> confirm;
        private Action cancel;
        private bool submitting;
        private int version;
        private readonly UnityEngine.Events.UnityAction[] clickHandlers = new UnityEngine.Events.UnityAction[4];

        public bool IsOpen => request != null;
        public int OptionCount => request != null ? request.Options.Count : 0;
        public bool RejectNextShow { get; set; }
        public bool RejectNextReplace { get; set; }
        public Func<string, bool> RejectedConfirm { get; private set; }

        public Func<string, bool> CaptureConfirm() => confirm;
        public Action CaptureCancel() => cancel;

        private void OnEnable()
        {
            if (panelRoot == null || panelRoot == gameObject || choiceText == null || cancelButton == null
                || optionButtons == null || optionButtons.Length != 4 || Array.Exists(optionButtons, b => b == null))
            {
                Debug.LogError("C03ProbePresenter测试接线不完整，检查面板/文字/四按钮/取消。", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int index = i;
                clickHandlers[i] = () => SubmitIndex(index);
                optionButtons[i].onClick.AddListener(clickHandlers[i]);
            }
            cancelButton.onClick.AddListener(CancelCurrent);
            panelRoot.SetActive(false);
        }

        public bool TryShow(ChoiceRequest next, Func<string, bool> tryConfirm, Action onCancel)
        {
            if (IsOpen || next == null || tryConfirm == null)
            {
                return false;
            }
            if (RejectNextShow)
            {
                RejectNextShow = false;
                RejectedConfirm = tryConfirm;
                return false;
            }
            SetRequest(next, tryConfirm, onCancel);
            return true;
        }

        public bool TryReplaceCurrent(ChoiceRequest next, Func<string, bool> tryConfirm, Action onCancel)
        {
            if (!IsOpen || next == null || next.Id != request.Id || tryConfirm == null)
            {
                return false;
            }
            if (RejectNextReplace)
            {
                RejectNextReplace = false;
                return false;
            }
            SetRequest(next, tryConfirm, onCancel);
            return true;
        }

        public bool TrySubmit(string id)
        {
            if (!IsOpen || submitting)
            {
                return false;
            }
            var callback = confirm;
            int beforeVersion = version;
            submitting = true;
            try
            {
                bool accepted = callback(id);
                if (accepted && beforeVersion == version)
                {
                    Close();
                }
                return accepted;
            }
            finally
            {
                submitting = false;
            }
        }

        public void CancelCurrent()
        {
            if (!IsOpen)
            {
                return;
            }
            Action callback = cancel;
            Close();
            callback?.Invoke();
        }

        private void SetRequest(ChoiceRequest next, Func<string, bool> tryConfirm, Action onCancel)
        {
            request = next;
            confirm = tryConfirm;
            cancel = onCancel;
            version++;
            panelRoot.SetActive(true);
            choiceText.text = "TEST TRANSACTION / " + request.Title + "\n";
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool visible = i < request.Options.Count;
                optionButtons[i].gameObject.SetActive(visible);
                if (visible)
                {
                    optionButtons[i].GetComponentInChildren<TMP_Text>().text = request.Options[i].Title;
                }
            }
        }

        private void SubmitIndex(int index)
        {
            if (request != null && index < request.Options.Count)
            {
                TrySubmit(request.Options[index].Id);
            }
        }

        private void Close()
        {
            request = null;
            confirm = null;
            cancel = null;
            version++;
            panelRoot.SetActive(false);
        }

        private void OnDisable()
        {
            CancelCurrent();
            // 只移除自己的监听，不清除其他测试者挂入的回调。
            if (optionButtons != null)
            {
                for (int i = 0; i < optionButtons.Length && i < clickHandlers.Length; i++)
                {
                    if (optionButtons[i] != null && clickHandlers[i] != null)
                    {
                        optionButtons[i].onClick.RemoveListener(clickHandlers[i]);
                        clickHandlers[i] = null;
                    }
                }
            }
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelCurrent);
            }
        }
    }
}
