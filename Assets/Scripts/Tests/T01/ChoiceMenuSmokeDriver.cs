// 职责：T01 Play 独测与鼠标分阶段演示；回调仅记录 ID/次数，不模拟奖励或扣费。
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
            public DemoOption(string id, string title, string description)
            {
                this.id = id;
                this.title = title;
                this.description = description;
            }
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
        [SerializeField, Tooltip("四项阶段的额外测试卡；下次打开生效，不是正式构筑项。")]
        private DemoOption fourthOption = new DemoOption("smoke-option-d", "Placeholder D", "Fourth display option. No gameplay effect.");
        [SerializeField, Tooltip("进入 Play 时运行七类自动检查，再开始鼠标演示；重新进入 Play 生效。")]
        private bool runChecksOnStart = true;

        private int confirmedCount;
        private int cancelledCount;
        private string lastResult = "None";
        private int demoStage;
        private int stageConfirms;
        private int openAttempts;
        private string checkSummary = "Checks not run";

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

        private IEnumerator Start()
        {
            if (runChecksOnStart)
            {
                openButton.interactable = false;
                var checks = new ChoiceMenuPlayChecks(panel, CreateRequest("t01-checks", false), CreateRequest("t01-checks", true));
                yield return checks.Run();
                // 验证驱动自身启停后的 Open 监听；菜单的启停验证在 checks 中。
                for (int i = 0; i < 3; i++)
                {
                    enabled = false;
                    enabled = true;
                }
                int before = openAttempts;
                openButton.onClick.Invoke();
                checks.Check(openAttempts == before + 1, "7 driver re-enable leaves one Open listener");
                panel.CancelCurrent();
                checkSummary = checks.Summary;
                Debug.Log("[T01 CHECK SUMMARY] " + checkSummary, this);
                confirmedCount = 0;
                cancelledCount = 0;
                demoStage = 0;
            }
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
            openAttempts++;
            stageConfirms = 0;
            bool opened = panel.TryShow(CreateRequest("t01-smoke", false), Confirm, Cancel);
            if (opened)
            {
                lastResult = DemoInstruction();
            }
            RefreshStatus();
        }

        private ChoiceRequest CreateRequest(string id, bool four)
        {
            var displayOptions = new ChoiceOption[four ? 4 : 3];
            for (int i = 0; i < options.Length; i++)
            {
                displayOptions[i] = options[i].ToOption();
            }
            if (four)
            {
                displayOptions[3] = fourthOption.ToOption();
            }
            return new ChoiceRequest(id, requestTitle + (four ? " / FOUR OPTIONS" : ""), displayOptions);
        }

        private string DemoInstruction()
        {
            switch (demoStage % 4)
            {
                case 0: return "MOUSE 1: first click rejects; second click accepts";
                case 1: return "MOUSE 2: click any card -> four cards; then select D";
                case 2: return "MOUSE 3: click any card -> four cards; then Cancel";
                default: return "MOUSE 4: rapidly double-click a card; one confirm only";
            }
        }

        private bool Confirm(string id)
        {
            confirmedCount++;
            stageConfirms++;
            lastResult = "Selected ID: " + id;
            bool accepted = true;
            if (demoStage % 4 == 0 && stageConfirms == 1)
            {
                accepted = false;
                lastResult += " / FALSE: click again";
            }
            else if ((demoStage % 4 == 1 || demoStage % 4 == 2) && stageConfirms == 1)
            {
                accepted = false;
                bool replaced = panel.TryReplaceCurrent(CreateRequest("t01-smoke", true), Confirm, Cancel);
                lastResult += " / replaced=" + replaced + " / Cancel count=" + cancelledCount;
            }
            if (accepted)
            {
                demoStage++;
            }
            Debug.Log("[T01 MOUSE] ID=" + id + " accepted=" + accepted + " confirm=" + confirmedCount + " cancel=" + cancelledCount, this);
            StartCoroutine(RefreshAfterConfirm());
            return accepted;
        }

        private IEnumerator RefreshAfterConfirm()
        {
            yield return null;
            RefreshStatus();
        }

        private void Cancel()
        {
            cancelledCount++;
            demoStage++;
            // 关闭后再次取消不应再次进入本回调。
            panel.CancelCurrent();
            panel.CancelCurrent();
            lastResult = "Cancelled";
            Debug.Log("[T01] Cancel count=" + cancelledCount, this);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            statusText.text = checkSummary + "\n" + lastResult + "\nConfirm callbacks: " + confirmedCount + "   Cancel callbacks: " + cancelledCount +
                "\nMenu open: " + panel.IsOpen;
            openButton.interactable = !panel.IsOpen;
        }
    }
}
