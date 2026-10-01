// 职责：真实E→C03→Chest→T01→真实PlayerState→HUD及隔离替换的Play验收。
// Soap / T12；依赖既有正式组件，反射只在Tests获取真实卡片按钮/断引用/迟到回调。
// Inspector显式接线；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。不进正式Build。
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using Regrowth.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.T12
{
    public sealed class T12SmokeDriver : MonoBehaviour
    {
        [SerializeField] private RunController run;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerState state;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private ChoiceCoordinator flow;
        [SerializeField] private ChoicePanel panel;
        [SerializeField] private PlayerHud hud;
        [SerializeField, Tooltip("主箱/伤后Heal/满血Heal/失效选项/缺引用/候选不足，各自独立生命周期。")] private Chest[] chests;
        [SerializeField] private ChestRewardConfig replacementConfig;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button checksButton;
        [SerializeField] private Button damageButton;
        [SerializeField] private bool autoVerify;
        private bool testing;
        private int passed;
        private int failed;
        private string summary = "T12: E opens nearby chest. Run Checks on fresh Play; TEST Damage is isolated.";
        private Recorder recorder;
        private sealed class Recorder : IAudioBackend
        {
            internal int ChestCues;
            public void Play(AudioCue cue, GameObject emitter)
            {
                if (cue == AudioCue.ChestOpened)
                {
                    ChestCues++;
                }
            }
            public void StopAll(GameObject emitter) { }
        }
        private static T Field<T>(object value, string name)
        {
            return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        }
        private ChoiceCardView[] Cards => Field<Transform>(panel, "cardContainer").GetComponentsInChildren<ChoiceCardView>();
        private Button CancelButton => Field<Button>(panel, "cancelButton");
        private Button OptionButton(string id)
        {
            return Cards.Where(card => Field<string>(card, "optionId") == id)
                .Select(card => Field<Button>(card, "selectButton")).First();
        }
        private string[] OptionIds() => Cards.Select(card => Field<string>(card, "optionId")).ToArray();
        private void OnEnable()
        {
            checksButton.onClick.AddListener(StartChecks);
            damageButton.onClick.AddListener(Damage);
        }
        private void OnDisable()
        {
            checksButton.onClick.RemoveListener(StartChecks);
            damageButton.onClick.RemoveListener(Damage);
            if (recorder != null)
            {
                GameAudio.UninstallBackend(recorder);
            }
            if (testing && Keyboard.current != null)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            }
        }
        private IEnumerator Start()
        {
            yield return null;
            if (autoVerify)
            {
                StartChecks();
            }
        }
        private void Damage() => state.TryTakeDamage(new DamageRequest(30, DamageKind.Terrain));
        private void StartChecks()
        {
            if (!testing && run.IsGameplayActive && !chests[0].IsClaimed && Keyboard.current != null)
            {
                StartCoroutine(Verify());
            }
        }
        private void Update()
        {
            status.text = summary + "\nPhase=" + run.Phase + " / timeScale=" + Time.timeScale
                + " / target=" + interactor.InteractionId + "\nHP=" + state.CurrentHealth + " / Loadout=" + state.Items.Count;
            checksButton.interactable = !testing && run.IsGameplayActive && !chests[0].IsClaimed;
            damageButton.interactable = !testing && run.IsGameplayActive;
        }
        private void Check(bool valid, string label)
        {
            if (valid)
            {
                passed++;
            }
            else
            {
                failed++;
            }
            Debug.Log("[T12 CHECK " + (valid ? "PASS" : "FAIL") + "] " + label, this);
        }
        private IEnumerator Keys(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
        }
        private void SelectFixture(int index)
        {
            foreach (var chest in chests)
            {
                chest.gameObject.SetActive(false);
            }
            chests[index].gameObject.SetActive(true);
            input.DiscardGameplayInput();
        }
        private IEnumerator Verify()
        {
            testing = true;
            recorder = new Recorder();
            GameAudio.InstallBackend(recorder);
            yield return Keys(0.15f);
            Check(state.IsBound && flow.IsInitialized && interactor.IsInitialized, "one real C01/C02/C03 chain initialized");
            foreach (var chest in chests)
            {
                Check(Field<ChestRewardConfig>(chest, "rewardConfig").IsValid, "saved config valid, no reserved item: " + chest.InteractionId);
            }
            state.TryAddLoadoutItem(LoadoutItemId.Dash);
            SelectFixture(0);
            yield return Keys(0.15f);
            Check(interactor.InteractionId == chests[0].InteractionId, "real interactor targets Chest");
            for (int i = 0; i < 20; i++)
            {
                chests[0].CanInteract(state.gameObject);
            }
            Check(chests[0].Transaction == null && !flow.IsOpen && recorder.ChestCues == 0, "CanInteract never draws or opens");
            yield return Keys(0.25f, Key.E);
            Check(flow.IsOpen && panel.IsOpen && chests[0].Transaction.IsPending, "real E -> Reader -> Interactor -> Chest -> ChoicePanel");
            Check(run.Phase == RunPhase.Choosing && Time.timeScale == 0f, "Choosing freezes gameplay");
            string[] fixedIds = OptionIds();
            Check(fixedIds.Length == 3 && fixedIds.Distinct().Count() == 3 && !fixedIds.Contains("dash"), "three unique options / owned Dash excluded");
            Check(fixedIds.Contains("heal") && state.CurrentHealth == state.MaximumHealth, "full health Heal is eligible");
            Check(!chests[0].TryInteract(state.gameObject) && recorder.ChestCues == 0, "Choosing repeat interaction rejected / no success cue");
            CancelButton.onClick.Invoke();
            Check(!chests[0].IsClaimed && state.Items.Count == 1 && run.IsGameplayActive, "cancel preserves chest and real state");
            yield return Keys(0.15f);
            yield return Keys(0.2f, Key.E);
            Check(OptionIds().SequenceEqual(fixedIds), "cancel/reopen identical three cards, no reroll");
            Func<string, bool> late = Field<Func<string, bool>>(panel, "confirm");
            Button sword = OptionButton("sword");
            sword.onClick.Invoke();
            sword.onClick.Invoke();
            Check(state.Contains(LoadoutItemId.Sword) && chests[0].IsClaimed && !flow.IsOpen && run.IsGameplayActive, "real state Loadout claim closes once");
            Check(!late("sword") && recorder.ChestCues == 1, "double/late confirm cannot claim or play twice");
            Check(hud.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == "SWORD")
                && hud.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == "HELD / UNAVAILABLE"), "T02 real event refresh, quadruped Sword unavailable");
            Check(!chests[0].CanInteract(state.gameObject) && !chests[0].TryInteract(state.gameObject), "claimed Chest cannot reopen");
            SelectFixture(1);
            Check(state.TryTakeDamage(new DamageRequest(30, DamageKind.Terrain)), "real damage for Heal test");
            int before = state.CurrentHealth;
            chests[1].TryInteract(state.gameObject);
            int heal = chests[1].Transaction.Cached.First(value => value.Id == "heal").HealAmount;
            OptionButton("heal").onClick.Invoke();
            Check(state.CurrentHealth == Math.Min(before + heal, state.MaximumHealth) && chests[1].IsClaimed, "non-full Heal uses configured actual amount");
            Check(Field<TMP_Text>(hud, "healthText").text.Contains(state.CurrentHealth.ToString()), "HUD health event refresh");
            state.TryHeal(state.MaximumHealth);
            SelectFixture(2);
            chests[2].TryInteract(state.gameObject);
            OptionButton("heal").onClick.Invoke();
            Check(state.CurrentHealth == state.MaximumHealth && chests[2].IsClaimed && recorder.ChestCues == 3, "full Heal accepted with no HP change, consumed once");
            SelectFixture(3);
            chests[3].TryInteract(state.gameObject);
            state.TryAddLoadoutItem(LoadoutItemId.UprightForm); // 隔离模拟缓存项被外部命令取得。
            OptionButton("upright").onClick.Invoke();
            Check(flow.IsOpen && !chests[3].IsClaimed && recorder.ChestCues == 3, "external stale option false / menu stays / C04 no refill");
            Check(!Field<Func<string, bool>>(panel, "confirm")("unknown"), "invalid option false");
            CancelButton.onClick.Invoke();
            SelectFixture(4);
            chests[4].enabled = false;
            var field = typeof(Chest).GetField("choiceFlowSource", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(chests[4], null);
            chests[4].enabled = true;
            Check(!chests[4].CanInteract(state.gameObject) && !chests[4].TryInteract(state.gameObject), "missing flow safe refusal, contextual Warning");
            chests[4].enabled = false;
            field.SetValue(chests[4], flow);
            chests[4].enabled = true;
            SelectFixture(5);
            Check(!chests[5].TryInteract(state.gameObject) && !flow.IsOpen && !chests[5].IsClaimed, "fewer than three legal options refuses without filling");
            SelectFixture(4);
            run.TryPause();
            yield return Keys(0.2f, Key.E);
            Check(!flow.IsOpen && !interactor.HasTarget && !chests[4].IsClaimed, "Paused formal input cannot open");
            run.TryResume();
            yield return Keys(0.12f);
            // 相同正式flow/panel，只换纯测试的四槽读写夹具；真实PlayerState绝不接收900。
            var probe = new T12ProbeState();
            int completions = 0;
            var transaction = new ChestClaimTransaction(probe, probe, probe, run, flow, replacementConfig,
                "t12-isolated-replacement", () => completions++, () => true);
            Check(transaction.TryBegin(), "isolated full-slot begins through real ChoiceFlow");
            Func<string, bool> oldStage = Field<Func<string, bool>>(panel, "confirm");
            OptionButton("sword").onClick.Invoke();
            Check(Cards.Length == 4 && flow.IsOpen && run.Phase == RunPhase.Choosing && Time.timeScale == 0f, "same request three -> four retains pause");
            Check(probe.Items.Count == 4 && !probe.Contains(LoadoutItemId.Sword) && probe.ReplaceCalls == 0 && !transaction.Claimed, "enter replacement never deletes old item");
            Check(!oldStage("sword") && probe.ReplaceCalls == 0, "old-stage callback rejected");
            CancelButton.onClick.Invoke();
            Check(probe.Items.Count == 4 && probe.Contains((LoadoutItemId)900) && !transaction.Claimed && completions == 0, "replacement cancel preserves all old items and chest");
            Check(transaction.TryBegin(), "isolated canceled chest reopens");
            OptionButton("sword").onClick.Invoke();
            probe.RejectReplace = true;
            OptionButton("900").onClick.Invoke();
            Check(flow.IsOpen && probe.ReplaceCalls == 0 && !transaction.Claimed, "failed atomic replace does not consume");
            probe.RejectReplace = false;
            Button final = OptionButton("900");
            Func<string, bool> lateReplace = Field<Func<string, bool>>(panel, "confirm");
            final.onClick.Invoke();
            final.onClick.Invoke();
            Check(transaction.Claimed && probe.ReplaceCalls == 1 && completions == 1 && probe.Items.Count == 4
                && probe.Contains(LoadoutItemId.Sword) && !probe.Contains((LoadoutItemId)900) && !flow.IsOpen, "atomic final replacement exactly once");
            Check(!lateReplace("900") && !transaction.TryBegin() && completions == 1, "late final callback inert");
            Check(state.Items.All(ChestRewardDefinition.IsAllowed), "test identity never enters real PlayerState");
            Check(state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Terrain)), "real death for lock check");
            yield return Keys(0.2f, Key.E);
            Check(run.Phase == RunPhase.Dead && !flow.IsOpen && !interactor.HasTarget && !chests[4].IsClaimed, "Dead formal input cannot open");
            yield return Keys(0.1f);
            GameAudio.UninstallBackend(recorder);
            recorder = null;
            testing = false;
            summary = "T12: " + passed + " passed / " + failed + " failed. Exit Play to repeat.";
            Debug.Log("[T12 CHECK SUMMARY] " + passed + " passed / " + failed + " failed", this);
        }
    }
}
