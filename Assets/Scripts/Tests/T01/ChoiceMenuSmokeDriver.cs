// 职责：T01 独立手动演示；回调仅记录 ID/次数，不模拟奖励或扣费。
// 模块/维护：Soap / T01；依赖：ChoicePanel、ChoiceRequest/Option、uGUI、TMP。
// 接线：菜单、重开按钮、状态 TMP 必填；测试数据在场景 Inspector 中编辑。
// 交接：docs/handoffs/Soap.handoff；规范：根目录 AGENTS.md。测试场景不进正式构建。
using System;
using System.Collections;
using Regrowth.Core;
using Regrowth.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.Tests.T01
{
    public sealed class ChoiceMenuSmokeDriver : MonoBehaviour
    {
        [Serializable]
        private sealed class DemoOption
        {
            [SerializeField, Tooltip("测试稳定 ID；与标题独立，不授予真实项。")]
            private string id;
            [SerializeField, Tooltip("测试标题；下次打开生效。")]
            private string title;
            [SerializeField, TextArea, Tooltip("测试说明；下次打开生效。")]
            private string description;
            internal ChoiceOption ToOption() => new ChoiceOption(id, title, description);
        }

        [Header("独立测试引用")]
        [SerializeField, Tooltip("本场景的 ChoicePanel；必填。")]
        private ChoicePanel panel;
        [SerializeField, Tooltip("菜单关闭后再次打开的按钮；必填。")]
        private Button openButton;
        [SerializeField, Tooltip("显示测试 ID、回调次数和菜单状态；必填。")]
        private TMP_Text statusText;
        [Header("仅展示的测试数据")]
        [SerializeField, Tooltip("请求标题；下次打开生效。")]
        private string requestTitle = "T01 - Choose one placeholder";
        [SerializeField, Tooltip("三个测试选项；不属于正式奖励池，下次打开生效。")]
        private DemoOption[] options;

        private int confirmedCount;
        private int cancelledCount;
        private string lastResult = "None";

        private void OnEnable()
        {
            if (panel == null || openButton == null || statusText == null || options == null || options.Length != 3)
            {
                Debug.LogError("[T01] Smoke driver requires panel, openButton, statusText and three demo options.", this);
                enabled = false;
                return;
            }
            openButton.onClick.AddListener(Open);
        }

        private void Start()
        {
            Open();
        }

        private void OnDisable()
        {
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(Open);
            }
            if (panel != null)
            {
                panel.CancelCurrent();
            }
        }

        private void Open()
        {
            var displayOptions = new ChoiceOption[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                displayOptions[i] = options[i].ToOption();
            }
            bool opened = panel.TryShow(new ChoiceRequest("t01-smoke", requestTitle, displayOptions), Confirm, Cancel);
            if (opened)
            {
                lastResult = "Waiting for selection";
            }
            RefreshStatus();
        }

        private bool Confirm(string id)
        {
            confirmedCount++;
            lastResult = "Selected ID: " + id;
            Debug.Log("[T01] Confirm accepted ID=" + id + " count=" + confirmedCount, this);
            StartCoroutine(RefreshAfterConfirm());
            return true;
        }

        private IEnumerator RefreshAfterConfirm()
        {
            yield return null;
            RefreshStatus();
        }

        private void Cancel()
        {
            cancelledCount++;
            lastResult = "Cancelled";
            Debug.Log("[T01] Cancel count=" + cancelledCount, this);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            statusText.text = lastResult + "\nConfirm callbacks: " + confirmedCount + "   Cancel callbacks: " + cancelledCount +
                "\nMenu open: " + panel.IsOpen;
            openButton.interactable = !panel.IsOpen;
        }
    }
}
