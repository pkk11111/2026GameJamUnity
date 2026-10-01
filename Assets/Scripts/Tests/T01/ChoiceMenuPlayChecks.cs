// 职责：在真实 Play 中对已接线 ChoicePanel / Card Button 执行七类生命周期检查。
// 模块/维护：Soap / T01；依赖：Core、UI.Choice、uGUI、TMP；只保存测试计数。
// 接线：由 SmokeDriver 传入场景实例；反射仅用于观察私有展示引用，不进入正式 UI。
// 交接：docs/handoffs/Soap.handoff；规范：根 AGENTS.md。不执行任何 gameplay。
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.Tests.T01
{
    /// <summary>复用真实卡片 Button.onClick；不直接调用 Submit 或伪造 UI 实现。</summary>
    internal sealed class ChoiceMenuPlayChecks
    {
        private readonly ChoicePanel panel;
        private readonly ChoiceRequest three;
        private readonly ChoiceRequest four;
        private int passed;
        private int failed;

        internal string Summary => "PLAY CHECKS: " + passed + " passed / " + failed + " failed (7 groups)";

        internal ChoiceMenuPlayChecks(ChoicePanel panel, ChoiceRequest three, ChoiceRequest four)
        {
            this.panel = panel;
            this.three = three;
            this.four = four;
        }

        internal void Check(bool condition, string label)
        {
            if (condition)
            {
                passed++;
                Debug.Log("[T01 CHECK PASS] " + label, panel);
            }
            else
            {
                failed++;
                Debug.LogError("[T01 CHECK FAIL] " + label, panel);
            }
        }

        // 只访问已注入 panel 内的引用；不按对象名或资源路径查找正式素材。
        private static T Field<T>(object source, string name)
        {
            return (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        }

        private ChoiceCardView[] Cards => Field<Transform>(panel, "cardContainer").GetComponentsInChildren<ChoiceCardView>();
        private Button CardButton(ChoiceCardView card) => Field<Button>(card, "selectButton");
        private Button CancelButton => Field<Button>(panel, "cancelButton");

        private bool Displays(ChoiceRequest request)
        {
            ChoiceCardView[] cards = Cards;
            if (!panel.IsOpen || cards.Length != request.Options.Count || Field<TMP_Text>(panel, "requestTitle").text != request.Title)
            {
                return false;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                if (Field<TMP_Text>(cards[i], "titleText").text != request.Options[i].Title ||
                    Field<TMP_Text>(cards[i], "descriptionText").text != request.Options[i].Description)
                {
                    return false;
                }
            }
            return true;
        }

        internal IEnumerator Run()
        {
            int confirms = 0;
            int cancels = 0;
            string selected = null;
            Check(panel.TryShow(three, id => { confirms++; selected = id; return confirms > 1; }, () => cancels++), "1 opens three");
            Check(Displays(three), "1 title/description and three cards match request");
            CardButton(Cards[0]).onClick.Invoke();
            Check(panel.IsOpen && confirms == 1 && cancels == 0, "1 false keeps open; exactly one callback");
            Check(!string.IsNullOrEmpty(Field<TMP_Text>(panel, "feedbackText").text), "1 rejection feedback displayed");
            yield return null;
            Check(confirms == 1, "1 no delayed duplicate callback");
            CardButton(Cards[1]).onClick.Invoke();
            Check(!panel.IsOpen && confirms == 2 && selected == three.Options[1].Id, "1 retry returns correct B ID and closes");
            yield return null;

            confirms = 0;
            cancels = 0;
            int stageTwoConfirms = 0;
            int stageTwoCancels = 0;
            bool replaced = false;
            Check(panel.TryShow(three, id =>
            {
                confirms++;
                replaced = panel.TryReplaceCurrent(four, finalId => { selected = finalId; stageTwoConfirms++; return true; }, () => stageTwoCancels++);
                return false;
            }, () => cancels++), "2 opens first stage");
            Button oldCard = CardButton(Cards[0]);
            oldCard.onClick.Invoke();
            Check(replaced && Displays(four) && confirms == 1 && cancels == 0 && stageTwoCancels == 0, "2 same transaction three -> four without closing/cancel");
            oldCard.onClick.Invoke();
            Check(confirms == 1 && stageTwoConfirms == 0, "2 removed stage listener inert");
            yield return null;
            CardButton(Cards[3]).onClick.Invoke();
            Check(!panel.IsOpen && stageTwoConfirms == 1 && selected == four.Options[3].Id && stageTwoCancels == 0, "2 final D true closes once");
            yield return null;

            confirms = 0;
            cancels = 0;
            int foreignCallbacks = 0;
            Check(panel.TryShow(three, id => { selected = id; confirms++; return true; }, () => cancels++), "3 opens original");
            Button originalCard = CardButton(Cards[0]);
            var wrong = new ChoiceRequest("different-transaction", "Must never appear", four.Options);
            Check(!panel.TryReplaceCurrent(wrong, id => { foreignCallbacks++; return true; }, () => foreignCallbacks++), "3 wrong request ID rejected");
            Check(Displays(three) && CardButton(Cards[0]) == originalCard && confirms == 0 && cancels == 0 && foreignCallbacks == 0, "3 original view and callbacks unchanged");
            Check(!panel.TryShow(wrong, id => { foreignCallbacks++; return true; }, () => foreignCallbacks++), "4 duplicate TryShow rejected");
            Check(Displays(three) && CardButton(Cards[0]) == originalCard && foreignCallbacks == 0, "4 duplicate does not replace or invoke callbacks");
            originalCard.onClick.Invoke();
            Check(!panel.IsOpen && confirms == 1 && selected == three.Options[0].Id && foreignCallbacks == 0, "3/4 original transaction still confirms A");
            yield return null;

            confirms = 0;
            cancels = 0;
            Check(panel.TryShow(three, id => { confirms++; return true; }, () => cancels++), "5 opens cancel transaction");
            CancelButton.onClick.Invoke();
            CancelButton.onClick.Invoke();
            panel.CancelCurrent();
            Check(!panel.IsOpen && cancels == 1 && confirms == 0, "5 cancel button + repeated closed cancel notify once");
            yield return null;
            cancels = 0;
            stageTwoCancels = 0;
            Check(panel.TryShow(three, id => false, () => cancels++), "5 opens replacement cancel transaction");
            Check(panel.TryReplaceCurrent(four, id => true, () => stageTwoCancels++), "5 enters four stage");
            CancelButton.onClick.Invoke();
            panel.CancelCurrent();
            CancelButton.onClick.Invoke();
            Check(!panel.IsOpen && cancels == 0 && stageTwoCancels == 1, "5 four-stage cancel invokes current callback once only");
            yield return null;

            confirms = 0;
            Button rapid = null;
            Check(panel.TryShow(three, id =>
            {
                confirms++;
                rapid.onClick.Invoke(); // 提交回调中重入同一真实按钮。
                return true;
            }, () => cancels++), "6 opens rapid transaction");
            rapid = CardButton(Cards[0]);
            rapid.onClick.Invoke();
            for (int i = 0; i < 20; i++)
            {
                rapid.onClick.Invoke();
            }
            Check(!panel.IsOpen && confirms == 1, "6 reentrant + 20 rapid button invocations confirm once");
            yield return null;

            int previousCallbacks = 0;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                int currentCallbacks = 0;
                int currentCancels = 0;
                panel.enabled = false;
                panel.enabled = true;
                Check(panel.TryShow(three, id => { currentCallbacks++; return true; }, () => currentCancels++), "7 reopen after panel toggle " + cycle);
                Button stale = CardButton(Cards[0]);
                stale.onClick.Invoke();
                stale.onClick.Invoke();
                Check(currentCallbacks == 1 && !panel.IsOpen, "7 one current callback after reopen " + cycle);
                Check(panel.TryShow(three, id => { previousCallbacks++; return true; }, () => currentCancels++), "7 reopen after completion " + cycle);
                stale.onClick.Invoke();
                Check(previousCallbacks == 0 && panel.IsOpen, "7 stale card cannot confirm new transaction " + cycle);
                CancelButton.onClick.Invoke();
                CancelButton.onClick.Invoke();
                Check(currentCancels == 1 && !panel.IsOpen, "7 one cancel after repeated enable " + cycle);
                yield return null;
            }
            cancels = 0;
            Check(panel.TryShow(three, id => true, () => cancels++), "7 opens before disabling");
            panel.enabled = false;
            panel.enabled = true;
            panel.CancelCurrent();
            Check(!panel.IsOpen && cancels == 1, "7 disabling open panel cancels exactly once");
            yield return null;
        }
    }
}
