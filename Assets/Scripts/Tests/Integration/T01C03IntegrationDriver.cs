// 职责：Soap T01正式展示与C03真实交互/选择事务接线验收，仅用于独立集成场景。
// 模块/维护：controller / C05-T01；依赖：Core、Runtime、UI.Choice、C03测试目标、InputSystem、uGUI/TMP。
// 接线：Inspector显式绑定全部组件；不生成正式奖励池/支付/传送，不作为正式HUD或重开。
// 交接：docs/handoffs/controller.handoff；规范：根目录AGENTS.md。
using System;
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.Tests.C03;
using Regrowth.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.Integration
{
    public sealed class T01C03IntegrationDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("唯一启动入口。")] private GameBootstrap bootstrap;
        [SerializeField, Tooltip("唯一阶段。")] private RunController run;
        [SerializeField, Tooltip("集中输入。")] private PlayerInputReader input;
        [SerializeField, Tooltip("真实玩家状态。")] private PlayerState state;
        [SerializeField, Tooltip("唯一交互端。")] private PlayerInteractor interactor;
        [SerializeField, Tooltip("唯一选择事务。")] private ChoiceCoordinator flow;
        [SerializeField, Tooltip("Soap的正式IChoicePresenter。")] private ChoicePanel panel;
        [SerializeField, Tooltip("局部IInteractable测试触发物，不是正式箱子。")] private C03ProbeTarget target;
        [SerializeField, Tooltip("测试信息，非正式HUD。")] private TMP_Text statusText;
        [SerializeField, Tooltip("手动三项事务按钮。")] private Button openButton;
        [SerializeField, Tooltip("下次Play执行检查，结束进入Dead；关闭后可手动E/按钮测试。")]
        private bool autoVerify = true;

        private Keyboard keyboard;
        private bool verifying;
        private int manualConfirms;
        private int manualCancels;
        public int PassedChecks { get; private set; }
        public int FailedChecks { get; private set; }
        public string LastReport { get; private set; } = "E or TEST Choice opens the real Soap menu.";

        private static readonly ChoiceOption[] Three =
        {
            new ChoiceOption("test-sword", "TEST Sword", "Grant Sword through the real player command."),
            new ChoiceOption("test-upright", "TEST Upright", "Grant Upright through the real player command."),
            new ChoiceOption("test-heal", "TEST Heal", "Heal 5. Full health is an accepted test choice.")
        };
        private static readonly ChoiceOption[] Four =
        {
            new ChoiceOption("a", "TEST A", "Transaction display only."),
            new ChoiceOption("b", "TEST B", "Transaction display only."),
            new ChoiceOption("c", "TEST C", "Transaction display only."),
            new ChoiceOption("d", "TEST D", "Final test commit; no new gameplay item.")
        };

        private void OnEnable()
        {
            if (bootstrap == null || run == null || input == null || state == null || interactor == null
                || flow == null || panel == null || target == null || statusText == null || openButton == null)
            {
                Debug.LogError("[T01+C03] Missing Inspector references.", this);
                enabled = false;
                return;
            }
            target.OnTry = _ => OpenManualChoice();
            openButton.onClick.AddListener(OpenFromButton);
            input.PauseRequested += ToggleTestPause;
        }

        private void Start()
        {
            if (autoVerify)
            {
                StartCoroutine(Verify());
            }
        }

        private void Update()
        {
            statusText.text = "GROWL AGAIN / SOAP T01 + C03 INTEGRATION\n"
                + "Real menu, input, run controller and player; local interaction probe.\n\n"
                + $"Phase: {run.Phase}    Time: {Time.timeScale:0.##}    HP: {state.CurrentHealth}/{state.MaximumHealth}\n"
                + $"Target: {interactor.InteractionId}    Flow open: {flow.IsOpen}    UI open: {panel.IsOpen}\n"
                + $"Loadout: [{string.Join(", ", state.Items)}]    Manual confirms/cancels: {manualConfirms}/{manualCancels}\n\n"
                + "E: open TEST three choices. Escape: TEST pause.\n"
                + "Auto checks end in Dead; disable autoVerify before Play for manual checks.\n\n"
                + LastReport;
            openButton.interactable = !verifying && run.IsGameplayActive && !flow.IsOpen;
        }

        private void ToggleTestPause()
        {
            if (verifying)
            {
                return;
            }
            if (run.Phase == RunPhase.Playing)
            {
                run.TryPause();
            }
            else if (run.Phase == RunPhase.Paused)
            {
                run.TryResume();
            }
        }

        private void OpenFromButton()
        {
            if (!verifying)
            {
                OpenManualChoice();
            }
        }

        private bool OpenManualChoice()
        {
            return flow.TryBegin(new ChoiceRequest("integration-manual", "TEST / SOAP MENU + REAL STATE", Three), id =>
            {
                bool committed;
                switch (id)
                {
                    case "test-sword":
                        committed = state.TryAddLoadoutItem(LoadoutItemId.Sword);
                        break;
                    case "test-upright":
                        committed = state.TryAddLoadoutItem(LoadoutItemId.UprightForm);
                        break;
                    case "test-heal":
                        state.TryHeal(5);
                        committed = state.IsAlive;
                        break;
                    default:
                        committed = false;
                        break;
                }
                if (committed)
                {
                    manualConfirms++;
                }
                return committed;
            }, () => manualCancels++);
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
        }

        private ChoiceCardView[] Cards => Field<Transform>(panel, "cardContainer").GetComponentsInChildren<ChoiceCardView>();
        private static Button CardButton(ChoiceCardView card) => Field<Button>(card, "selectButton");
        private Button CancelButton => Field<Button>(panel, "cancelButton");

        private void Check(bool condition, string label)
        {
            if (condition)
            {
                PassedChecks++;
            }
            else
            {
                FailedChecks++;
                Debug.LogError("[T01+C03 FAIL] " + label, this);
            }
        }

        private IEnumerator Verify()
        {
            verifying = true;
            PassedChecks = 0;
            FailedChecks = 0;
            try
            {
                yield return null;
                Check(bootstrap.IsStarted && run.IsGameplayActive && state.IsAlive && state.Items.Count == 0,
                    "real bootstrap fresh state");
                Check(!flow.IsOpen && !panel.IsOpen, "closed real presenter");

                keyboard = InputSystem.AddDevice<Keyboard>("T01C03TestKeyboard");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                Check(target.Attempts == 1 && flow.IsOpen && panel.IsOpen, "real E consumed by interactor opens Soap menu once");
                Check(run.Phase == RunPhase.Choosing && Time.timeScale == 0f, "real choosing pause");
                Check(!interactor.HasTarget && !input.TryConsumeInteract(), "choosing clears prompt/input");
                Check(Cards.Length == 3, "real three cards");
                CardButton(Cards[0]).onClick.Invoke();
                Check(state.Contains(LoadoutItemId.Sword) && manualConfirms == 1, "real button commits Sword once");
                Check(!flow.IsOpen && !panel.IsOpen && run.Phase == RunPhase.Playing && Time.timeScale > 0f,
                    "commit closes UI and releases pause");
                yield return null;
                yield return null;
                Check(target.Attempts == 1 && !flow.IsOpen, "held E does not reopen");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;

                int confirmations = 0;
                int cancellations = 0;
                var request = new ChoiceRequest("integration-retry", "TEST / REJECT THEN RETRY", Three);
                Check(flow.TryBegin(request, _ => ++confirmations > 1, () => cancellations++), "retry begins via flow");
                CardButton(Cards[0]).onClick.Invoke();
                Check(confirmations == 1 && flow.IsOpen && panel.IsOpen && run.Phase == RunPhase.Choosing,
                    "false stays paused with menu");
                Check(!string.IsNullOrEmpty(Field<TMP_Text>(panel, "feedbackText").text), "real rejection feedback");
                var retryButton = CardButton(Cards[1]);
                retryButton.onClick.Invoke();
                retryButton.onClick.Invoke();
                Check(confirmations == 2 && cancellations == 0 && !flow.IsOpen && !panel.IsOpen && run.IsGameplayActive,
                    "retry completes once and releases");
                yield return null;

                int oldCancels = 0;
                int newCancels = 0;
                bool replaced = false;
                Check(flow.TryBegin(new ChoiceRequest("integration-replace", "TEST / THREE TO FOUR", Three), _ =>
                {
                    replaced = flow.TryReplace(new ChoiceRequest("integration-replace", "TEST / FOUR STAGE", Four),
                        finalId => finalId == "d", () => newCancels++);
                    return false;
                }, () => oldCancels++), "replacement begins");
                var oldButton = CardButton(Cards[0]);
                oldButton.onClick.Invoke();
                Check(replaced && Cards.Length == 4 && flow.IsOpen && panel.IsOpen && run.Phase == RunPhase.Choosing
                    && Time.timeScale == 0f && oldCancels == 0, "same transaction four cards keeps pause");
                oldButton.onClick.Invoke();
                Check(flow.IsOpen && Cards.Length == 4, "old-stage button inert");
                CancelButton.onClick.Invoke();
                CancelButton.onClick.Invoke();
                flow.Cancel();
                Check(oldCancels == 0 && newCancels == 1 && !flow.IsOpen && !panel.IsOpen && run.IsGameplayActive,
                    "current replacement cancel exactly once");
                Check(state.Contains(LoadoutItemId.Sword), "cancel preserves actual loadout");
                yield return null;

                int finalCommits = 0;
                Check(flow.TryBegin(new ChoiceRequest("integration-final", "TEST / FINAL COMMIT", Three), _ =>
                {
                    flow.TryReplace(new ChoiceRequest("integration-final", "TEST / FOUR DISPLAY OPTIONS", Four), finalId =>
                    {
                        if (finalId != "d")
                        {
                            return false;
                        }
                        if (state.TryRemoveLoadoutItem(LoadoutItemId.Sword))
                        {
                            finalCommits++;
                            return true;
                        }
                        return false;
                    }, () => newCancels++);
                    return false;
                }, () => oldCancels++), "new replacement begins");
                CardButton(Cards[0]).onClick.Invoke();
                Check(state.Contains(LoadoutItemId.Sword) && flow.IsOpen && Cards.Length == 4, "no write before final stage");
                var finalButton = CardButton(Cards[3]);
                finalButton.onClick.Invoke();
                finalButton.onClick.Invoke();
                Check(finalCommits == 1 && !state.Contains(LoadoutItemId.Sword) && !flow.IsOpen && !panel.IsOpen
                    && run.IsGameplayActive, "final real state write once and resume");
                yield return null;

                int disableCancels = 0;
                Check(flow.TryBegin(new ChoiceRequest("integration-disable", "TEST / PRESENTER DISABLE", Three),
                    _ => true, () => disableCancels++), "disable transaction begins");
                panel.enabled = false;
                yield return null;
                Check(disableCancels == 1 && !flow.IsOpen && !panel.IsOpen && run.IsGameplayActive,
                    "real presenter disable cancels and resumes");
                panel.enabled = true;

                for (int cycle = 0; cycle < 2; cycle++)
                {
                    int exitCancels = 0;
                    Check(flow.TryBegin(new ChoiceRequest("integration-exit-" + cycle, "TEST / BOOTSTRAP EXIT", Three),
                        _ => true, () => exitCancels++), "exit transaction " + cycle);
                    bootstrap.enabled = false;
                    Check(exitCancels == 1 && !flow.IsOpen && !panel.IsOpen && !input.IsInitialized,
                        "exit cleanup " + cycle);
                    bootstrap.enabled = true;
                    yield return null;
                    Check(bootstrap.IsStarted && run.IsGameplayActive && state.IsAlive && state.Items.Count == 0,
                        "rebind without reset/old lock " + cycle);
                }

                Check(state.TryTakeDamage(new DamageRequest(int.MaxValue, DamageKind.Terrain)), "real lethal damage accepted");
                Check(run.Phase == RunPhase.Dead && Time.timeScale == 0f && !interactor.HasTarget
                    && !flow.TryBegin(new ChoiceRequest("integration-dead", "MUST NOT OPEN", Three), _ => true, null),
                    "dead stops input/interaction/new menu");
            }
            finally
            {
                if (keyboard != null)
                {
                    InputSystem.RemoveDevice(keyboard);
                    keyboard = null;
                }
                verifying = false;
                LastReport = $"INTEGRATION CHECKS: {PassedChecks} passed / {FailedChecks} failed";
                Debug.Log("[T01+C03] " + LastReport, this);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OpenFromButton);
            }
            if (input != null)
            {
                input.PauseRequested -= ToggleTestPause;
            }
            if (target != null)
            {
                target.OnTry = null;
            }
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;
            }
        }
    }
}
