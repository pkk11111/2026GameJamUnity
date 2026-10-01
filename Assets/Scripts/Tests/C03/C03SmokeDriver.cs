// 职责：真实C03交互器/选择协调器独立Play验收和接线探针；不属于正式地图/菜单/奖励。
// 模块/维护：controller，C03测试；依赖：Core/Runtime/InputSystem/uGUI/TMP。
// 接线：唯一运行/输入/状态/交互/选择、四个测试目标、测试菜单、文本与四个控制按钮。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.C03
{
    public sealed class C03SmokeDriver : MonoBehaviour
    {
        [SerializeField, Tooltip("必填。")] private GameBootstrap bootstrap;
        [SerializeField, Tooltip("必填。")] private RunController run;
        [SerializeField, Tooltip("必填。")] private PlayerInputReader input;
        [SerializeField, Tooltip("必填。")] private PlayerState state;
        [SerializeField, Tooltip("必填。")] private PlayerInteractor interactor;
        [SerializeField, Tooltip("必填。")] private ChoiceCoordinator flow;
        [SerializeField, Tooltip("必填测试展示替身。")] private C03ProbePresenter presenter;
        [SerializeField, Tooltip("必填测试传送A。")] private C03ProbeTarget portalA;
        [SerializeField, Tooltip("必填测试传送B，带多个Collider。")] private C03ProbeTarget portalB;
        [SerializeField, Tooltip("必填测试开关。")] private C03ProbeTarget switchTarget;
        [SerializeField, Tooltip("必填测试宝箱。")] private C03ProbeTarget chestTarget;
        [SerializeField, Tooltip("必填测试文本。")] private TMP_Text statusText;
        [SerializeField, Tooltip("必填测试可用切换。")] private Button availabilityButton;
        [SerializeField, Tooltip("必填测试选择请求。")] private Button choiceButton;
        [SerializeField, Tooltip("必填测试暂停。")] private Button pauseButton;
        [SerializeField, Tooltip("必填自动验收。")] private Button verifyButton;
        [SerializeField, Tooltip("下次Play自动检查，结束为Dead；编辑器重进Play是新测试生命周期。")]
        private bool autoVerify = true;

        private readonly List<string> checks = new List<string>();
        private Gamepad pad;
        private bool verifying;
        private int targetEvents;
        private string report = "Not run";
        public int PassedChecks { get; private set; }
        public int FailedChecks { get; private set; }
        public bool IsVerifying => verifying;
        public string LastReport => report;

        private void OnEnable()
        {
            if (bootstrap == null || run == null || input == null || state == null || interactor == null || flow == null
                || presenter == null || portalA == null || portalB == null || switchTarget == null || chestTarget == null
                || statusText == null || availabilityButton == null || choiceButton == null || pauseButton == null || verifyButton == null)
            {
                Debug.LogError("C03SmokeDriver 测试接线不完整，检查Inspector。", this);
                enabled = false;
                return;
            }
            interactor.TargetChanged += OnTargetChanged;
            input.PauseRequested += TogglePause;
            availabilityButton.onClick.AddListener(ToggleAvailability);
            choiceButton.onClick.AddListener(OpenExampleChoice);
            pauseButton.onClick.AddListener(TogglePause);
            verifyButton.onClick.AddListener(BeginVerification);
            portalA.OnTry = _ => OpenExample();
        }

        private void Start()
        {
            if (autoVerify)
            {
                BeginVerification();
            }
        }

        private void Update()
        {
            statusText.text = "GROWL AGAIN / C03 INTERACTION + TRANSACTION SMOKE\n"
                + "Real controller, local world/menu probes. No formal reward/payment/map.\n\n"
                + $"Phase: {run.Phase}    Time: {Time.timeScale:0.##}    HP: {state.CurrentHealth}/{state.MaximumHealth}\n"
                + $"Target: {interactor.InteractionId}    Prompt: {interactor.Prompt}\n"
                + $"Available portals: {portalA.Available}/{portalB.Available}    Target events: {targetEvents}\n"
                + $"Requests: Portal A {portalA.Attempts} / B {portalB.Attempts} / Switch {switchTarget.Attempts} / Chest {chestTarget.Attempts}\n"
                + $"Choice: {flow.RequestId}    Open: {flow.IsOpen}    Cards: {presenter.OptionCount}\n"
                + $"Loadout {state.Items.Count}/{state.Capacity}: [{string.Join(", ", state.Items)}]\n\n"
                + "E/Gamepad North: REAL Interact. Esc/Start: TEST pause.\n"
                + "Category > nearest anchor > stable ID. Unavailable objects skipped.\n"
                + "TEST choice grants through real state API; no random pool/chest payment.\n\n"
                + report;
        }

        private void OnTargetChanged() => targetEvents++;

        private void ToggleAvailability()
        {
            if (verifying)
            {
                return;
            }
            portalA.Available = !portalA.Available;
            portalB.Available = portalA.Available;
        }

        private void TogglePause()
        {
            if (verifying)
            {
                return;
            }
            if (run.IsGameplayActive)
            {
                run.TryPause();
            }
            else
            {
                run.TryResume();
            }
        }

        private void OpenExampleChoice()
        {
            if (!verifying)
            {
                OpenExample();
            }
        }

        private bool OpenExample()
        {
            return flow.TryBegin(new ChoiceRequest("c03-example", "TEST state commands", new[]
            {
                new ChoiceOption("upright", "TEST Upright", "No random reward service"),
                new ChoiceOption("sword", "TEST Sword", "No chest consumption"),
                new ChoiceOption("heal", "TEST Heal", "Full HP accepted as test business")
            }), id =>
            {
                if (id == "upright")
                {
                    return state.TryAddLoadoutItem(LoadoutItemId.UprightForm);
                }
                if (id == "sword")
                {
                    return state.TryAddLoadoutItem(LoadoutItemId.Sword);
                }
                state.TryHeal(5);
                return true;
            }, () => { });
        }

        public void BeginVerification()
        {
            if (verifying)
            {
                return;
            }
            if (!bootstrap.IsStarted || !state.IsAlive || state.Items.Count != 0 || !run.IsGameplayActive)
            {
                report = "Needs fresh empty-loadout life; exit/re-enter Play. This is not restart.";
                return;
            }
            StartCoroutine(Verify());
        }

        private static ChoiceRequest Request(string id, int count)
        {
            var options = new List<ChoiceOption>();
            for (int i = 0; i < count; i++)
            {
                string key = i.ToString();
                options.Add(new ChoiceOption(key, "TEST " + key, "Controller lifecycle check"));
            }
            return new ChoiceRequest(id, "TEST transaction", options);
        }

        private IEnumerator Verify()
        {
            verifying = true;
            PassedChecks = 0;
            FailedChecks = 0;
            checks.Clear();
            portalA.OnTry = null;
            pad = InputSystem.AddDevice<Gamepad>("C03SmokePad");
            try
            {
                yield return null;
                Check(bootstrap.IsStarted && interactor.IsInitialized && flow.IsInitialized && run.IsGameplayActive,
                    "explicit bootstrap initialized real C03 dependencies");
                interactor.RefreshTarget();
                Check(interactor.InteractionId == portalA.InteractionId, "portal priority overrides closer switch/chest and ID breaks distance tie");
                int beforeEvents = targetEvents;
                interactor.RefreshTarget();
                Check(targetEvents == beforeEvents, "unchanged candidate does not repeat event");
                portalB.transform.position = new Vector3(-0.8f, 0f, 0f);
                Physics2D.SyncTransforms();
                interactor.RefreshTarget();
                Check(interactor.InteractionId == portalB.InteractionId, "same category uses nearest anchor before ID");
                portalB.transform.position = new Vector3(-1.25f, 0f, 0f);
                Physics2D.SyncTransforms();
                portalA.Available = false;
                portalB.Available = false;
                interactor.RefreshTarget();
                Check(interactor.InteractionId == switchTarget.InteractionId, "unavailable portals skipped to available switch");
                switchTarget.Available = false;
                interactor.RefreshTarget();
                Check(interactor.InteractionId == chestTarget.InteractionId, "unavailable switch skipped to chest");
                chestTarget.Available = false;
                interactor.RefreshTarget();
                Check(!interactor.HasTarget && interactor.Prompt == string.Empty, "no eligible candidate clears prompt");
                portalA.Available = true;
                portalB.Available = true;
                switchTarget.Available = true;
                chestTarget.Available = true;

                portalA.Accepts = false;
                beforeEvents = switchTarget.Attempts;
                int beforeAttempts = portalA.Attempts;
                Check(!interactor.TryInteractCurrent() && portalA.Attempts == beforeAttempts + 1
                    && switchTarget.Attempts == beforeEvents, "receiver rejection never falls back to a second object");
                Check(!interactor.TryInteractCurrent() && portalA.Attempts == beforeAttempts + 1, "same frame cannot send duplicate request");
                yield return null;
                bool reentryRejected = false;
                portalA.OnTry = _ => { reentryRejected = !interactor.TryInteractCurrent(); return true; };
                Check(interactor.TryInteractCurrent() && reentryRejected, "request callback cannot reenter interactor");
                portalA.OnTry = null;
                portalA.Accepts = true;

                portalA.Available = false;
                beforeAttempts = portalB.Attempts;
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
                yield return null;
                Check(portalB.Attempts == beforeAttempts + 1, "real InputSystem Interact reaches a multi-Collider target exactly once");
                yield return null;
                yield return null;
                Check(portalB.Attempts == beforeAttempts + 1, "held Interact does not repeat requests");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                portalA.Available = true;
                var oldPosition = state.transform.position;
                state.transform.position = new Vector3(10, 0, 0);
                Physics2D.SyncTransforms();
                interactor.RefreshTarget();
                Check(!interactor.HasTarget && !interactor.TryInteractCurrent(), "outside radius cannot interact");
                state.transform.position = oldPosition;
                Physics2D.SyncTransforms();
                Check(run.TryPause(), "test pause accepted");
                interactor.RefreshTarget();
                Check(!interactor.HasTarget && !interactor.TryInteractCurrent(), "paused interactor clears prompt and rejects requests");
                Check(run.TryResume(), "test pause resumes");

                int confirmed = 0;
                int cancelled = 0;
                var three = Request("session", 3);
                Check(!flow.TryBegin(Request("one", 1), _ => true, () => cancelled++)
                    && cancelled == 0 && run.IsGameplayActive, "formal opening requires three cards without callbacks on rejection");
                Check(flow.TryBegin(three, _ => { confirmed++; return false; }, () => cancelled++)
                    && flow.IsOpen && presenter.IsOpen && run.Phase == RunPhase.Choosing && Time.timeScale == 0f,
                    "three-card transaction opens and pauses real run");
                Check(!interactor.HasTarget && !interactor.TryInteractCurrent() && !input.TryConsumeInteract(),
                    "choice phase blocks interactor and clears real input");
                Check(!flow.TryBegin(three, _ => true, () => cancelled++) && cancelled == 0, "overlapping transaction rejected without cancel callback");
                Check(!presenter.TrySubmit("unknown") && confirmed == 0, "unknown option cannot call business");
                Check(!presenter.TrySubmit("0") && confirmed == 1 && flow.IsOpen && presenter.IsOpen, "business false keeps paused menu");
                var staleConfirm = presenter.CaptureConfirm();
                var staleCancel = presenter.CaptureCancel();
                Check(!flow.TryReplace(Request("other", 4), _ => true, () => cancelled++) && presenter.OptionCount == 3,
                    "different transaction ID cannot replace");
                presenter.RejectNextReplace = true;
                Check(!flow.TryReplace(Request("session", 4), _ => true, () => cancelled++) && presenter.OptionCount == 3,
                    "presenter replacement refusal preserves current phase callbacks");
                int replacementCancelled = 0;
                Check(flow.TryReplace(Request("session", 4), _ => false, () => replacementCancelled++)
                    && presenter.OptionCount == 4 && run.Phase == RunPhase.Choosing && Time.timeScale == 0f && cancelled == 0,
                    "same-ID four-card replacement keeps pause and does not cancel old phase");
                Check(!staleConfirm("0") && confirmed == 1, "old phase confirm becomes stale");
                staleCancel();
                Check(flow.IsOpen && replacementCancelled == 0, "old phase cancellation cannot cancel replacement");
                presenter.CancelCurrent();
                flow.Cancel();
                Check(!flow.IsOpen && !presenter.IsOpen && replacementCancelled == 1 && cancelled == 0 && run.IsGameplayActive,
                    "cancel replacement once, close menu and release stage");

                bool replacedInCallback = false;
                int finalConfirmed = 0;
                Check(flow.TryBegin(Request("nested", 3), _ =>
                {
                    replacedInCallback = flow.TryReplace(Request("nested", 4), __ =>
                    {
                        finalConfirmed++;
                        return state.TryAddLoadoutItem(LoadoutItemId.Sword);
                    }, () => cancelled++);
                    return false;
                }, () => cancelled++), "reward-style replacement transaction begins");
                Check(!presenter.TrySubmit("0") && replacedInCallback && presenter.OptionCount == 4 && flow.IsOpen && state.Items.Count == 0,
                    "confirm can enter replacement without premature state mutation");
                staleConfirm = presenter.CaptureConfirm();
                Check(presenter.TrySubmit("2") && finalConfirmed == 1 && state.Contains(LoadoutItemId.Sword)
                    && !flow.IsOpen && !presenter.IsOpen && run.IsGameplayActive,
                    "final confirm changes real player once and completes transaction");
                Check(!staleConfirm("2") && !presenter.TrySubmit("2") && finalConfirmed == 1,
                    "completed callbacks and duplicate clicks cannot reapply");
                Check(state.TryRemoveLoadoutItem(LoadoutItemId.Sword), "test grant removed through real write port");

                presenter.RejectNextShow = true;
                int beforeCancel = cancelled;
                Check(!flow.TryBegin(Request("rejected", 3), _ => true, () => cancelled++) && !flow.IsOpen && run.IsGameplayActive
                    && cancelled == beforeCancel, "presenter open rejection rolls back lock without business cancel");
                Check(!presenter.RejectedConfirm("0"), "rejected opening callback is stale");

                bool callbackReentryRejected = false;
                Func<string, bool> callback = null;
                Check(flow.TryBegin(Request("guard", 3), _ =>
                {
                    callbackReentryRejected = !callback("0") && !flow.TryBegin(Request("reentry", 3), __ => true, null);
                    return false;
                }, null), "guard test transaction begins");
                callback = presenter.CaptureConfirm();
                Check(!presenter.TrySubmit("0") && callbackReentryRejected && flow.IsOpen, "confirmation rejects reentry and nested begin");
                flow.Cancel();

                int deferredCancelled = 0;
                Check(flow.TryBegin(Request("deferred", 3), _ => { flow.Cancel(); return false; }, () => deferredCancelled++),
                    "deferred cancel transaction begins");
                Check(!presenter.TrySubmit("0") && deferredCancelled == 1 && !flow.IsOpen && !presenter.IsOpen && run.IsGameplayActive,
                    "cancel inside failed confirmation is deferred and delivered once");
                int committedCancel = 0;
                Check(flow.TryBegin(Request("commit", 3), _ => { flow.Cancel(); return true; }, () => committedCancel++),
                    "accepted commit transaction begins");
                Check(presenter.TrySubmit("0") && committedCancel == 0 && !flow.IsOpen && run.IsGameplayActive,
                    "accepted commit completes rather than cancelling committed business");

                presenter.enabled = false;
                Check(!flow.TryBegin(Request("disabled-ui", 3), _ => true, null) && run.IsGameplayActive,
                    "disabled presenter cannot acquire choice lock");
                presenter.enabled = true;
                int uiDisabledCancelled = 0;
                Check(flow.TryBegin(Request("ui-disable", 3), _ => false, () => uiDisabledCancelled++),
                    "presenter lifetime transaction begins");
                presenter.enabled = false;
                Check(!flow.IsOpen && !presenter.IsOpen && uiDisabledCancelled == 1 && run.IsGameplayActive,
                    "presenter disable cancels once and releases real phase");
                presenter.enabled = true;

                for (int cycle = 0; cycle < 3; cycle++)
                {
                    int cycleCancelled = 0;
                    Check(flow.TryBegin(Request("cycle-" + cycle, 3), _ => false, () => cycleCancelled++),
                        "lifecycle transaction begins " + cycle);
                    staleConfirm = presenter.CaptureConfirm();
                    bootstrap.enabled = false;
                    Check(cycleCancelled == 1 && !flow.IsOpen && !presenter.IsOpen && !flow.IsInitialized
                        && !interactor.IsInitialized && !staleConfirm("0"), "shutdown cancels once, rejects stale callback and detaches " + cycle);
                    bootstrap.enabled = true;
                    yield return null;
                    Check(bootstrap.IsStarted && interactor.IsInitialized && flow.IsInitialized && state.Items.Count == 0 && run.IsGameplayActive,
                        "explicit rebind restores clean C03 dependencies " + cycle);
                }

                // 接收端取得真实菜单锁：验证输入→交互→事务的整条接线，不做正式奖励池。
                portalA.OnTry = _ => flow.TryBegin(Request("input-chain", 3), __ => true, null);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
                yield return null;
                Check(flow.IsOpen && run.Phase == RunPhase.Choosing && !interactor.HasTarget,
                    "real Interact receiver opens paused transaction");
                flow.Cancel();
                yield return null;
                Check(!flow.IsOpen && run.IsGameplayActive && !input.TryConsumeInteract(), "cancellation cannot leak held confirm/Interact input");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;

                Check(state.TryTakeDamage(new DamageRequest(int.MaxValue, DamageKind.Enemy)) && run.Phase == RunPhase.Dead,
                    "real player death stops C03 scene");
                interactor.RefreshTarget();
                Check(!interactor.HasTarget && !interactor.TryInteractCurrent() && !flow.TryBegin(three, _ => true, null),
                    "Dead refuses interaction and new transaction");
                report = $"AUTO CHECKS: {PassedChecks} passed / {FailedChecks} failed\n"
                    + "Actual run is Dead; world/menu probes are test-only.";
                Debug.Log("[C03] " + report + "\n" + string.Join("\n", checks), this);
            }
            finally
            {
                portalA.OnTry = _ => OpenExample();
                Cleanup();
            }
        }

        private void Check(bool pass, string label)
        {
            if (pass)
            {
                PassedChecks++;
            }
            else
            {
                FailedChecks++;
            }
            checks.Add((pass ? "PASS " : "FAIL ") + label);
        }

        private void Cleanup()
        {
            verifying = false;
            if (pad != null && pad.added)
            {
                InputSystem.RemoveDevice(pad);
            }
            pad = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Cleanup();
            if (interactor != null)
            {
                interactor.TargetChanged -= OnTargetChanged;
            }
            if (input != null)
            {
                input.PauseRequested -= TogglePause;
            }
            if (availabilityButton != null)
            {
                availabilityButton.onClick.RemoveListener(ToggleAvailability);
            }
            if (choiceButton != null)
            {
                choiceButton.onClick.RemoveListener(OpenExampleChoice);
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(TogglePause);
            }
            if (verifyButton != null)
            {
                verifyButton.onClick.RemoveListener(BeginVerification);
            }
            if (portalA != null)
            {
                portalA.OnTry = null;
            }
        }
    }
}
