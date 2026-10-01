// 职责：真实 PlayerState/RunController 的独立验收和状态探针；不是 HUD、奖励或正式死亡界面。
// 模块/维护：controller，C02；依赖：Core/Runtime/Audio、Input System、uGUI/TMP。
// 接线：显式绑定唯一玩家状态、入口、阶段、输入、文本和测试按钮；停用时释放监听/虚拟设备/录音后端。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.C02
{
    /// <summary>只挂 C02_Smoke；调用已发布真实写口，不持有第二份玩家 HP/构筑/姿态。</summary>
    public sealed class C02SmokeDriver : MonoBehaviour
    {
        [Header("真实组件和测试展示")]
        [SerializeField, Tooltip("必填，唯一 C02 PlayerState。")] private PlayerState state;
        [SerializeField, Tooltip("必填，唯一 GameBootstrap。")] private GameBootstrap bootstrap;
        [SerializeField, Tooltip("必填，真实阶段。")] private RunController run;
        [SerializeField, Tooltip("必填，真实输入。")] private PlayerInputReader input;
        [SerializeField, Tooltip("必填，测试状态 TMP 文本，不是正式死亡 UI。")] private TMP_Text statusText;
        [SerializeField, Tooltip("必填，测试伤害按钮。")] private Button damageButton;
        [SerializeField, Tooltip("必填，测试治疗按钮。")] private Button healButton;
        [SerializeField, Tooltip("必填，测试获得/舍弃双手连剑；保留字段名以兼容现有场景引用。")] private Button uprightButton;
        [SerializeField, Tooltip("必填，测试获得/舍弃双腿；保留字段名以兼容现有场景引用。")] private Button swordButton;
        [SerializeField, Tooltip("必填，只启动独立验收，不实现重开。")] private Button verifyButton;

        [Header("独测参数")]
        [SerializeField, Tooltip("下次进入 Play 自动验收；检查结束玩家死亡，重新进入 Play 才是新测试生命周期。")]
        private bool autoVerify = true;
        [SerializeField, Min(1), Tooltip("测试伤害点数，暂定7；完整自动检查要求初始HP大于此值。实时读取。")]
        private int testDamage = 7;
        [SerializeField, Min(1), Tooltip("手动测试治疗点数，暂定5；实时读取。")]
        private int testHeal = 5;

        private readonly List<string> checks = new List<string>();
        private readonly List<string> eventOrder = new List<string>();
        private readonly object choiceOwner = new object();
        private Gamepad testPad;
        private RecordingBackend recorder;
        private bool verifying;
        private bool reentryWasRejected = true;
        private bool snapshotsWereConsistent = true;
        private int healthEvents;
        private int loadoutEvents;
        private int formEvents;
        private int diedEvents;
        private string report = "Not run";

        public int PassedChecks { get; private set; }
        public int FailedChecks { get; private set; }
        public bool IsVerifying => verifying;
        public string LastReport => report;

        private void OnEnable()
        {
            if (state == null || bootstrap == null || run == null || input == null || statusText == null
                || damageButton == null || healButton == null || uprightButton == null || swordButton == null || verifyButton == null)
            {
                Debug.LogError("C02SmokeDriver 缺少必要引用，检查 Inspector 的真实组件/文本/按钮。", this);
                enabled = false;
                return;
            }
            uprightButton.GetComponentInChildren<TMP_Text>().text = "TEST Arms + Sword";
            swordButton.GetComponentInChildren<TMP_Text>().text = "TEST Legs";
            state.HealthChanged += OnHealthChanged;
            state.LoadoutChanged += OnLoadoutChanged;
            state.FormChanged += OnFormChanged;
            state.Died += OnDied;
            input.PauseRequested += TogglePause;
            damageButton.onClick.AddListener(TestDamage);
            healButton.onClick.AddListener(TestHeal);
            uprightButton.onClick.AddListener(TestArms);
            swordButton.onClick.AddListener(TestLegs);
            verifyButton.onClick.AddListener(BeginVerification);
        }

        private void Start()
        {
            if (autoVerify)
            {
                BeginVerification();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Cleanup();
            if (state != null)
            {
                state.HealthChanged -= OnHealthChanged;
                state.LoadoutChanged -= OnLoadoutChanged;
                state.FormChanged -= OnFormChanged;
                state.Died -= OnDied;
            }
            if (input != null)
            {
                input.PauseRequested -= TogglePause;
            }
            if (damageButton != null)
            {
                damageButton.onClick.RemoveListener(TestDamage);
            }
            if (healButton != null)
            {
                healButton.onClick.RemoveListener(TestHeal);
            }
            if (uprightButton != null)
            {
                uprightButton.onClick.RemoveListener(TestArms);
            }
            if (swordButton != null)
            {
                swordButton.onClick.RemoveListener(TestLegs);
            }
            if (verifyButton != null)
            {
                verifyButton.onClick.RemoveListener(BeginVerification);
            }
        }

        private void Update()
        {
            statusText.text = "pawgatory / C02 V5 PLAYER STATE SMOKE\n"
                + "State probe only - result UI and restart belong to teammate tasks\n\n"
                + $"HP: {state.CurrentHealth} / {state.MaximumHealth}    Alive: {state.IsAlive}\n"
                + $"Form: {state.CurrentForm}    Bite: {state.CanBite} ({state.BiteDamage})    Sword: {state.CanUseSword} ({state.SwordDamage})\n"
                + $"Loadout: {state.Items.Count}/{state.Capacity}  [{string.Join(", ", state.Items)}]\n"
                + $"Phase: {run.Phase}    Time scale: {Time.timeScale:0.##}    Move X: {input.MoveX:0.00}\n"
                + $"Events: Health {healthEvents} / Loadout {loadoutEvents} / Form {formEvents} / Died {diedEvents}\n\n"
                + "TEST buttons invoke the real state API. No reward or payment service.\n"
                + "Esc/Start: TEST pause. A/D, Space: real input probe.\n"
                + "Death is terminal in this test life; exit/re-enter Play for a fresh life.\n\n"
                + report;
        }

        public void TestDamage()
        {
            if (!verifying && testDamage > 0)
            {
                state.TryTakeDamage(new DamageRequest(testDamage, DamageKind.Enemy));
            }
        }

        public void TestHeal()
        {
            if (!verifying)
            {
                state.TryHeal(testHeal);
            }
        }

        public void TestArms()
        {
            if (verifying)
            {
                return;
            }
            if (state.Contains(LoadoutItemId.Arms))
            {
                state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
            }
            else
            {
                state.TryAddLoadoutItem(LoadoutItemId.Arms);
            }
        }

        public void TestLegs()
        {
            if (verifying)
            {
                return;
            }
            if (state.Contains(LoadoutItemId.Legs))
            {
                state.TryRemoveLoadoutItem(LoadoutItemId.Legs);
            }
            else
            {
                state.TryAddLoadoutItem(LoadoutItemId.Legs);
            }
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

        public void BeginVerification()
        {
            if (verifying)
            {
                return;
            }
            if (!bootstrap.IsStarted || !state.IsAlive || state.Items.Count != 0
                || state.CurrentHealth != state.MaximumHealth || !run.IsGameplayActive || state.MaximumHealth <= testDamage)
            {
                report = "Verification needs a fresh full-health empty-loadout life and HP > testDamage.\nExit/re-enter Play; this button is not a restart.";
                return;
            }
            StartCoroutine(Verify());
        }

        private IEnumerator Verify()
        {
            verifying = true;
            PassedChecks = 0;
            FailedChecks = 0;
            checks.Clear();
            eventOrder.Clear();
            recorder = new RecordingBackend();
            GameAudio.InstallBackend(recorder);
            testPad = InputSystem.AddDevice<Gamepad>("C02SmokePad");
            try
            {
                yield return null;
                int maximum = state.MaximumHealth;
                Check(state.IsInitialized && state.IsBound && state.CurrentHealth == maximum, "real state initialized at full HP");
                Check(state.Capacity == LoadoutRules.Capacity && state.Capacity == 3 && state.Items.Count == 0
                    && state.HasBodyCore && state.CurrentForm == PlayerForm.Quadruped,
                    "three shared slots, body-enabled test start");
                Check(state.CanBite && !state.CanUseSword, "base bite permission without equipment");
                Check(state.BiteDamage >= 0 && state.SwordDamage >= 0, "configured combat snapshot exposed");

                int previousEvents = healthEvents;
                Check(!state.TryTakeDamage(default) && healthEvents == previousEvents, "default DamageRequest rejected");
                Check(!state.TryHeal(0) && !state.TryHeal(-1) && !state.TryHeal(1) && healthEvents == previousEvents,
                    "invalid/full-health healing does not mutate");
                Check(state.TryTakeDamage(new DamageRequest(testDamage, DamageKind.Enemy))
                    && state.CurrentHealth == maximum - testDamage && healthEvents == previousEvents + 1,
                    "enemy damage changes real HP once");
                Check(recorder.Count(AudioCue.PlayerHurt) == 1, "hurt cue only after actual damage");
                Check(state.TryHeal(int.MaxValue) && state.CurrentHealth == maximum, "large healing clamps without overflow");
                Check(state.TryTakeDamage(new DamageRequest(testDamage, DamageKind.Terrain))
                    && state.CurrentHealth == maximum - testDamage, "terrain uses same real health, no automatic shield");

                var view = state.Items;
                bool readOnly = false;
                try
                {
                    ((ICollection<LoadoutItemId>)view).Add(LoadoutItemId.Legs);
                }
                catch (NotSupportedException)
                {
                    readOnly = true;
                }
                Check(readOnly && state.Items.Count == 0, "external Items mutation rejected");

                int previousLoadout = loadoutEvents;
                Check(state.TryAddLoadoutItem(LoadoutItemId.Legs) && state.Items.Count == 1
                    && state.CanBite && !state.CanUseSword && loadoutEvents == previousLoadout + 1,
                    "legs occupy one shared slot without changing ordinary attack");
                Check(ReferenceEquals(view, state.Items) && view.Count == 1, "read-only live view remains stable");
                previousLoadout = loadoutEvents;
                Check(!state.TryAddLoadoutItem(LoadoutItemId.Legs) && loadoutEvents == previousLoadout, "duplicates do not notify");
                Check(state.TryAddLoadoutItem(LoadoutItemId.Arms) && state.CurrentForm == PlayerForm.Upright
                    && state.CanUseSword && !state.CanBite, "arms and sword are one item and switch ordinary attack");
                Check(state.TryRemoveLoadoutItem(LoadoutItemId.Arms) && state.Contains(LoadoutItemId.Legs)
                    && state.Items.Count == 1 && state.CanBite && !state.CanUseSword, "losing arms also removes sword permission");
                Check(state.TryAddLoadoutItem(LoadoutItemId.Arms) && state.CanUseSword, "arms regrowth restores sword permission");
                Check(state.TryRemoveLoadoutItem(LoadoutItemId.Legs) && state.CurrentForm == PlayerForm.Upright
                    && !state.CanBite && state.CanUseSword, "sword permission does not require legs");
                Check(state.TryAddLoadoutItem(LoadoutItemId.Tail) && state.TryAddLoadoutItem(LoadoutItemId.Legs)
                    && state.Items.Count == state.Capacity, "three body items fill the shared loadout");
                var fullSnapshot = state.Items.ToArray();
                previousLoadout = loadoutEvents;
                Check(!state.TryAddLoadoutItem(LoadoutItemId.Shield) && !state.TryAddLoadoutItem(LoadoutItemId.FlameBreath)
                    && !state.TryAddLoadoutItem(LoadoutItemId.Spear) && !state.TryAddLoadoutItem((LoadoutItemId)999)
                    && state.Items.SequenceEqual(fullSnapshot) && loadoutEvents == previousLoadout,
                    "full loadout, reserved and undefined items cannot mutate real player");
                Check(!state.TryAddLoadoutItem(LoadoutItemId.Dash) && !state.TryAddLoadoutItem(LoadoutItemId.DoubleJump)
                    && !state.TryAddLoadoutItem(LoadoutItemId.UprightForm) && !state.TryAddLoadoutItem(LoadoutItemId.Sword)
                    && state.Items.SequenceEqual(fullSnapshot), "legacy identities are rejected rather than silently mapped");
                Check(!state.TryReplaceLoadoutItem(LoadoutItemId.Arms, LoadoutItemId.Shield)
                    && state.Items.SequenceEqual(fullSnapshot), "failed full-loadout replacement preserves original state");

                // 三槽内部容器用隔离数据验收第四候选；不把未实现喷火投放地图奖励池。
                var isolated = new LoadoutCollection();
                foreach (var item in fullSnapshot)
                {
                    isolated.TryAdd(item);
                }
                Check(!isolated.TryAdd(LoadoutItemId.FlameBreath) && isolated.Items.Count == 3, "isolated capacity refuses fourth slot");
                Check(isolated.TryReplace(LoadoutItemId.Arms, LoadoutItemId.FlameBreath) && isolated.Items.Count == 3
                    && isolated.Contains(LoadoutItemId.Legs) && !state.Contains(LoadoutItemId.FlameBreath),
                    "isolated full-slot replacement is atomic without granting test skill to player");

                Check(state.TryRemoveLoadoutItem(LoadoutItemId.Tail), "removal releases one slot");
                previousLoadout = loadoutEvents;
                int previousForm = formEvents;
                Check(state.TryReplaceLoadoutItem(LoadoutItemId.Arms, LoadoutItemId.Tail)
                    && state.Items.Count == 2 && state.Contains(LoadoutItemId.Legs) && state.CurrentForm == PlayerForm.Quadruped
                    && state.CanBite && !state.CanUseSword
                    && loadoutEvents == previousLoadout + 1 && formEvents == previousForm + 1,
                    "real replacement commits loadout/form once and restores bite");
                var beforeFailure = state.Items.ToArray();
                previousLoadout = loadoutEvents;
                Check(!state.TryReplaceLoadoutItem(LoadoutItemId.Tail, LoadoutItemId.Tail)
                    && !state.TryReplaceLoadoutItem(LoadoutItemId.Tail, LoadoutItemId.Legs)
                    && !state.TryReplaceLoadoutItem(LoadoutItemId.Arms, LoadoutItemId.Arms)
                    && state.Items.SequenceEqual(beforeFailure) && loadoutEvents == previousLoadout,
                    "same/owned/missing replacement has no side effects");

                Check(run.TryBeginChoosing(choiceOwner), "real choice phase entered");
                Check(!state.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)) && !state.CanBite && !state.CanUseSword,
                    "choosing blocks damage and attack permission");
                Check(state.TryHeal(1) && state.TryAddLoadoutItem(LoadoutItemId.Arms), "state settlement commands allowed while choosing");
                Check(run.TryEndChoosing(choiceOwner) && state.CanUseSword, "settlement visible after choice ends");

                Check(run.TryPause(), "pause entered");
                beforeFailure = state.Items.ToArray();
                int beforePauseHealth = state.CurrentHealth;
                Check(!state.TryHeal(1) && !state.TryRemoveLoadoutItem(LoadoutItemId.Legs)
                    && !state.TryTakeDamage(new DamageRequest(1, DamageKind.Terrain)) && state.CurrentHealth == beforePauseHealth
                    && state.Items.SequenceEqual(beforeFailure), "paused state rejects gameplay and settlement writes");
                Check(run.TryResume(), "pause resumes");
                Check(reentryWasRejected && snapshotsWereConsistent, "event callbacks see committed state and cannot reenter writes");
                VerifyHeadState();
                VerifyRewards();

                for (int cycle = 0; cycle < 3; cycle++)
                {
                    int beforeHealth = state.CurrentHealth;
                    var beforeItems = state.Items.ToArray();
                    int beforeEvents = healthEvents;
                    bootstrap.enabled = false;
                    Check(!state.IsBound && state.IsInitialized && state.IsAlive && !state.TryHeal(1)
                        && !state.TryRemoveLoadoutItem(LoadoutItemId.Legs), "unbound state rejects commands cycle " + cycle);
                    bootstrap.enabled = true;
                    yield return null;
                    Check(state.IsBound && state.CurrentHealth == beforeHealth && state.Items.SequenceEqual(beforeItems)
                        && healthEvents == beforeEvents, "bootstrap cycling preserves state without duplicate initialization cycle " + cycle);
                }

                InputSystem.QueueStateEvent(testPad, new GamepadState { leftStick = Vector2.right }.WithButton(GamepadButton.South));
                yield return null;
                Check(input.MoveX > 0.5f, "real input active before death");
                eventOrder.Clear();
                int previousDied = diedEvents;
                previousEvents = healthEvents;
                Check(state.TryTakeDamage(new DamageRequest(int.MaxValue, DamageKind.Enemy)) && state.CurrentHealth == 0
                    && !state.IsAlive && healthEvents == previousEvents + 1 && diedEvents == previousDied + 1,
                    "lethal damage reaches zero and notifies once");
                Check(eventOrder.IndexOf("HealthChanged") >= 0 && eventOrder.IndexOf("HealthChanged") < eventOrder.IndexOf("Died"),
                    "death event occurs after health event");
                Check(run.Phase == RunPhase.Dead && !run.IsGameplayActive && Time.timeScale == 0f,
                    "zero HP enters Dead and stops the game");
                Check(input.MoveX == 0f && !input.JumpHeld && !input.TryConsumeJump() && !input.TryConsumeAttack()
                    && !input.TryConsumeInteract() && !input.TryConsumeDash(), "death immediately clears and locks real gameplay input");
                Check(!state.CanBite && !state.CanUseSword && !state.TryHeal(int.MaxValue)
                    && !state.TryTakeDamage(new DamageRequest(1, DamageKind.Terrain))
                    && !state.TryRemoveLoadoutItem(LoadoutItemId.Legs), "dead player cannot attack, heal, take damage or change loadout");
                Check(!run.TryResume() && !run.TryPause() && !run.TryBeginChoosing(choiceOwner), "ordinary phase commands cannot leave Dead");
                Check(recorder.Count(AudioCue.PlayerDied) == 1 && diedEvents == previousDied + 1, "death cue and notification only once despite bootstrap cycling");

                bootstrap.enabled = false;
                bootstrap.enabled = true;
                yield return null;
                Check(state.CurrentHealth == 0 && !state.IsAlive && run.Phase == RunPhase.Dead && Time.timeScale == 0f,
                    "cycling a dead life cannot resurrect or unlock input");
                Check(diedEvents == previousDied + 1 && recorder.Count(AudioCue.PlayerDied) == 1,
                    "rebinding dead life does not replay death");
                Check(reentryWasRejected && snapshotsWereConsistent, "all notifications retain consistent snapshots");

                report = $"AUTO CHECKS: {PassedChecks} passed / {FailedChecks} failed\n"
                    + "Actual state is Dead. No result UI or restart has been implemented.";
                Debug.Log("[C02] " + report + "\n" + string.Join("\n", checks), this);
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>隔离真实状态验证头部与躯干迁移；不组装教学地图，不产生第二个正式生命拥有者。</summary>
        private void VerifyHeadState()
        {
            var fixture = new GameObject("C02 Head State - test only");
            var head = fixture.AddComponent<PlayerState>();
            try
            {
                SetTestField(head, "prototypeStartWithBodyCore", false);
                SetTestField(head, "initialMaximumHealth", 77);
                Check(head.Initialize(run) && head.IsInitialized && head.IsAlive && !head.HasBodyCore
                    && head.CurrentHealth == 0 && head.MaximumHealth == 0 && head.Items.Count == 0,
                    "head is alive before HP is initialized");
                Check(!head.CanBite && !head.CanUseSword && !head.TryTakeDamage(new DamageRequest(1, DamageKind.Terrain))
                    && !head.TryApplyReward(new PlayerReward(LoadoutItemId.Legs)) && !head.TryHeal(1),
                    "head rejects attacks, damage and ordinary rewards");
                Check(head.WasEverOwned(LoadoutItemId.Legs) == false && head.EverOwnedItems.Count == 0,
                    "head preview cannot write successful ownership history");
                Check(run.TryPause() && !head.TryAcquireBodyCore() && !head.HasBodyCore,
                    "paused head cannot acquire body");
                Check(run.TryResume() && run.TryBeginChoosing(choiceOwner), "head acquisition uses existing choice lock");
                int bodyEvents = 0;
                int headHealthEvents = 0;
                bool ownerCommitted = false;
                bool bodySnapshotValid = true;
                bool bodyReentryRejected = true;
                head.BodyChanged += () =>
                {
                    bodyEvents++;
                    bodySnapshotValid &= ownerCommitted && head.HasBodyCore && head.CurrentHealth == 77 && head.MaximumHealth == 77;
                    bodyReentryRejected &= !head.TryApplyReward(new PlayerReward(LoadoutItemId.Legs));
                };
                head.HealthChanged += () => headHealthEvents++;
                Check(head.TryAcquireBodyCore(() => ownerCommitted = true) && head.HasBodyCore
                    && head.CurrentHealth == 77 && head.MaximumHealth == 77 && head.Items.Count == 0
                    && bodyEvents == 1 && headHealthEvents == 1 && bodySnapshotValid && bodyReentryRejected,
                    "body acquisition commits configured HP and owner flag before one notification");
                Check(!head.TryAcquireBodyCore() && bodyEvents == 1 && headHealthEvents == 1,
                    "body acquisition cannot repeat or refill life");
                Check(run.TryEndChoosing(choiceOwner) && head.CanBite && !head.CanUseSword,
                    "body enables bite after choice releases");
                Check(head.TryTakeDamage(new DamageRequest(7, DamageKind.Enemy))
                    && head.TryAddLoadoutItem(LoadoutItemId.Legs) && head.CurrentHealth == 70
                    && head.WasEverOwned(LoadoutItemId.Legs), "regrowing a part does not reinitialize body health");
                var history = head.EverOwnedItems;
                bool readOnly = false;
                try
                {
                    ((ICollection<LoadoutItemId>)history).Add(LoadoutItemId.Arms);
                }
                catch (NotSupportedException)
                {
                    readOnly = true;
                }
                Check(readOnly && head.TryRemoveLoadoutItem(LoadoutItemId.Legs)
                    && head.Items.Count == 0 && history.Count == 1 && head.WasEverOwned(LoadoutItemId.Legs),
                    "successful history is read-only and survives losing a part");
                head.Shutdown();
                Check(head.Initialize(run) && head.CurrentHealth == 70 && head.HasBodyCore
                    && head.WasEverOwned(LoadoutItemId.Legs), "head/body lifecycle rebind preserves successful state");
            }
            finally
            {
                head.Shutdown();
                Destroy(fixture);
            }
        }

        /// <summary>正面原子包验证，不包含代价、挑战或新局重置。</summary>
        private void VerifyRewards()
        {
            var items = state.Items.ToArray();
            var history = state.EverOwnedItems.ToArray();
            int hp = state.CurrentHealth;
            int max = state.MaximumHealth;
            int bite = state.BiteDamage;
            int sword = state.SwordDamage;
            bool consumed = false;
            bool snapshot = true;
            Action observer = () =>
            {
                snapshot &= consumed && state.CurrentHealth == Math.Min(max + 10, hp + 5)
                    && state.MaximumHealth == max + 10 && state.BiteDamage == bite + 3 && state.SwordDamage == sword + 3
                    && state.Items.SequenceEqual(items);
            };
            state.HealthChanged += observer;
            try
            {
                Check(state.TryApplyReward(new PlayerReward(heal: 5, maximumHealthIncrease: 10, attackIncrease: 3),
                    onCommitted: () => consumed = true) && consumed && snapshot,
                    "combined immediate reward commits owner and all stats before notification");
            }
            finally
            {
                state.HealthChanged -= observer;
            }
            Check(state.Items.SequenceEqual(items) && state.EverOwnedItems.SequenceEqual(history),
                "immediate effects consume no slot or successful item history");
            Check(state.TryHeal(int.MaxValue) && state.CurrentHealth == state.MaximumHealth,
                "new maximum health remains a valid healing bound");
            int beforeEvents = healthEvents;
            Check(state.TryApplyReward(new PlayerReward(heal: 5)) && healthEvents == beforeEvents,
                "full-health positive reward can be accepted without fake health notification");
            hp = state.CurrentHealth;
            max = state.MaximumHealth;
            bite = state.BiteDamage;
            Check(!state.TryApplyReward(default) && !state.TryApplyReward(new PlayerReward(heal: -1))
                && !state.TryApplyReward(new PlayerReward(maximumHealthIncrease: int.MaxValue))
                && !state.TryApplyReward(new PlayerReward(attackIncrease: int.MaxValue))
                && state.CurrentHealth == hp && state.MaximumHealth == max && state.BiteDamage == bite
                && state.Items.SequenceEqual(items), "invalid or overflowing packages have no partial writes");
            Check(!state.TryApplyReward(new PlayerReward(heal: 1), LoadoutItemId.Arms)
                && state.Items.SequenceEqual(items), "immediate package cannot remove unrelated held item");
        }

        private static void SetTestField(PlayerState target, string fieldName, object value)
        {
            typeof(PlayerState).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private void OnHealthChanged()
        {
            healthEvents++;
            eventOrder.Add("HealthChanged");
            snapshotsWereConsistent &= state.CurrentHealth >= 0 && state.CurrentHealth <= state.MaximumHealth
                && state.IsAlive == (state.IsInitialized && (!state.HasBodyCore || state.CurrentHealth > 0));
            reentryWasRejected &= !state.TryHeal(1) && !state.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy));
        }

        private void OnLoadoutChanged()
        {
            loadoutEvents++;
            snapshotsWereConsistent &= state.Items.Count <= state.Capacity && state.Items.Distinct().Count() == state.Items.Count
                && (state.CurrentForm == PlayerForm.Upright) == state.Contains(LoadoutItemId.Arms);
            reentryWasRejected &= !state.TryRemoveLoadoutItem(LoadoutItemId.Legs);
        }

        private void OnFormChanged(PlayerForm form)
        {
            formEvents++;
            snapshotsWereConsistent &= form == state.CurrentForm;
        }

        private void OnDied()
        {
            diedEvents++;
            eventOrder.Add("Died");
        }

        private void Check(bool passed, string label)
        {
            if (passed)
            {
                PassedChecks++;
            }
            else
            {
                FailedChecks++;
            }
            checks.Add((passed ? "PASS " : "FAIL ") + label);
        }

        private void Cleanup()
        {
            verifying = false;
            if (testPad != null && testPad.added)
            {
                InputSystem.RemoveDevice(testPad);
            }
            testPad = null;
            if (recorder != null)
            {
                GameAudio.UninstallBackend(recorder);
            }
            recorder = null;
        }

        // 独测后端仅记录实际成功触发，不代表 Wwise 可播放，也不影响 gameplay 判定。
        private sealed class RecordingBackend : IAudioBackend
        {
            private readonly Dictionary<AudioCue, int> counts = new Dictionary<AudioCue, int>();
            public int Count(AudioCue cue) => counts.TryGetValue(cue, out int value) ? value : 0;
            public void Play(AudioCue cue, GameObject emitter) => counts[cue] = Count(cue) + 1;
            public void StopAll(GameObject emitter) { }
        }
    }
}
