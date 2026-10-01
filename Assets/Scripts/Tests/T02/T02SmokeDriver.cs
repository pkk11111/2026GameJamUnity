// 职责：T02真实C02状态/只读HUD独测，替身只覆盖未发布的上限命令及监听计数。
// 模块/维护：Soap / T02；依赖Runtime/Core/UI.Hud/TMP/uGUI；测试不进入正式场景。
// 接线：全部Inspector必填，自动检查后可鼠标Damage/Heal/Scenario复现；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.Tests.T02
{
    public sealed class T02SmokeDriver : MonoBehaviour
    {
        [SerializeField] private PlayerState state;
        [SerializeField] private PlayerHud hud;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text formText;
        [SerializeField] private HudSlotView[] slots;
        [SerializeField] private HudProbeState probeA;
        [SerializeField] private HudProbeState probeB;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button damageButton;
        [SerializeField] private Button healButton;
        [SerializeField] private Button scenarioButton;
        [SerializeField] private bool autoVerify = true;
        private int passed;
        private int failed;
        private int scenario;

        private void OnEnable()
        {
            damageButton.onClick.AddListener(Damage);
            healButton.onClick.AddListener(Heal);
            scenarioButton.onClick.AddListener(NextScenario);
        }
        private void OnDisable()
        {
            damageButton.onClick.RemoveListener(Damage);
            healButton.onClick.RemoveListener(Heal);
            scenarioButton.onClick.RemoveListener(NextScenario);
        }
        private IEnumerator Start()
        {
            yield return null;
            if (autoVerify)
            {
                Verify();
            }
        }
        private void Damage() => state.TryTakeDamage(new DamageRequest(10, DamageKind.Enemy, gameObject));
        private void Heal() => state.TryHeal(5);
        private void NextScenario()
        {
            ClearLoadout();
            switch (scenario++ % 5)
            {
                case 1: state.TryAddLoadoutItem(LoadoutItemId.Sword); break;
                case 2: state.TryAddLoadoutItem(LoadoutItemId.UprightForm); state.TryAddLoadoutItem(LoadoutItemId.Sword); break;
                case 3: state.TryAddLoadoutItem(LoadoutItemId.UprightForm); break;
                case 4:
                    state.TryAddLoadoutItem(LoadoutItemId.Dash);
                    state.TryAddLoadoutItem(LoadoutItemId.DoubleJump);
                    state.TryAddLoadoutItem(LoadoutItemId.UprightForm);
                    state.TryAddLoadoutItem(LoadoutItemId.Sword);
                    break;
            }
        }
        private void ClearLoadout()
        {
            while (state.Items.Count > 0)
            {
                state.TryRemoveLoadoutItem(state.Items[0]);
            }
        }
        private T Field<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        private string Label(int i) => Field<TMP_Text>(slots[i], "title").text;
        private bool Unavailable(int i) => Field<GameObject>(slots[i], "unavailableView").activeSelf;
        private void Check(bool ok, string label)
        {
            if (ok) { passed++; } else { failed++; }
            Debug.Log($"[T02 CHECK {(ok ? "PASS" : "FAIL")}] {label}", this);
        }
        private void Verify()
        {
            Check(state.IsInitialized && healthText.text == "HP 100 / 100" && formText.text == "QUADRUPED", "initial real snapshot");
            Check(Label(0) == "EMPTY" && Label(3) == "EMPTY", "empty slots");
            Damage();
            Check(state.CurrentHealth == 90 && healthText.text == "HP 90 / 100", "real damage event");
            Heal();
            Check(state.CurrentHealth == 95 && healthText.text == "HP 95 / 100", "real heal event");
            state.TryAddLoadoutItem(LoadoutItemId.Sword);
            Check(Label(0) == "SWORD" && Unavailable(0) && formText.text == "QUADRUPED", "quad held sword unavailable");
            state.TryAddLoadoutItem(LoadoutItemId.UprightForm);
            Check(formText.text == "UPRIGHT" && !Unavailable(0), "form event enables held sword");
            state.TryRemoveLoadoutItem(LoadoutItemId.Sword);
            Check(formText.text == "UPRIGHT" && Label(0) == "UPRIGHT" && Label(1) == "EMPTY", "upright without sword");
            state.TryAddLoadoutItem(LoadoutItemId.Dash);
            state.TryAddLoadoutItem(LoadoutItemId.DoubleJump);
            state.TryAddLoadoutItem(LoadoutItemId.Sword);
            Check(state.Items.Count == 4 && Label(1) == "DASH" && Label(2) == "DOUBLE JUMP" && Label(3) == "SWORD", "four ordered real slots");
            state.TryRemoveLoadoutItem(LoadoutItemId.UprightForm);
            Check(state.Contains(LoadoutItemId.Sword) && Unavailable(2) && formText.text == "QUADRUPED", "remove upright retains unavailable sword");
            int hp = state.CurrentHealth;
            int count = state.Items.Count;
            hud.enabled = false;
            hud.enabled = true;
            Check(state.CurrentHealth == hp && state.Items.Count == count, "HUD lifecycle does not write real state");
            Check(hud.TryBind(probeA) && healthText.text == "HP 70 / 80", "new source snapshot");
            probeA.SetHealth(65, 120);
            Check(healthText.text == "HP 65 / 120", "local max health change event");
            for (int i = 0; i < 5; i++)
            {
                hud.enabled = false;
                Check(probeA.HealthListeners == 0 && probeA.LoadoutListeners == 0 && probeA.FormListeners == 0, "disabled removes all listeners " + i);
                hud.enabled = true;
                Check(probeA.HealthListeners == 1 && probeA.LoadoutListeners == 1 && probeA.FormListeners == 1, "enabled has exactly one listener " + i);
            }
            Check(hud.TryBind(probeB) && probeA.HealthListeners == 0 && probeA.FormListeners == 0, "rebind detaches old source");
            probeA.SetHealth(1, 2);
            Check(healthText.text == "HP 70 / 80", "old events cannot alter current view");
            Check(hud.TryBind(state) && probeB.HealthListeners == 0 && healthText.text == "HP 95 / 100", "restore real snapshot");
            Damage();
            Check(healthText.text == "HP 85 / 100", "current real events still update after rebind");
            status.text = $"T02 CHECKS: {passed} passed / {failed} failed\nReal PlayerState bound. TEST Damage / Heal / Scenario below.";
            Debug.Log($"[T02 CHECK SUMMARY] {passed} passed / {failed} failed", this);
        }
    }
}
