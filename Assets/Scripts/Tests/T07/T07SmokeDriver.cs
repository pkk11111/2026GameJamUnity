// Soap/T07：测试按钮、设备状态注入与自动Play验收；不进入正式地图。
// Attack经InputSystem→真实PlayerInputReader→唯一Router→Bite；反射仅用于隔离接线/读口夹具。
using System;
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Audio;
using Regrowth.Runtime;
using Regrowth.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.T07
{
    public sealed class T07SmokeDriver : MonoBehaviour
    {
        [SerializeField] private PlayerBiteAttack attack;
        [SerializeField] private PlayerAttackRouter router;
        [SerializeField] private BiteConfig config;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private PlayerLocomotion motor;
        [SerializeField] private PlayerState state;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private RunController run;
        [SerializeField] private T07DamageDummy dummy;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button pauseButton, permissionButton, deathButton, checksButton;
        private bool running, finished;
        private int passed, failed;
        private Coroutine checks;
        private readonly Recorder recorder = new Recorder();
        private sealed class Recorder : IAudioBackend
        {
            public int Bites;
            public int Weapons;
            public bool ThrowNext;
            public Action DuringPlay;
            public void Play(AudioCue cue, GameObject emitter)
            {
                if (cue == AudioCue.PlayerWeaponAttack) Weapons++;
                if (cue != AudioCue.PlayerBite) return;
                Bites++;
                DuringPlay?.Invoke();
                if (ThrowNext) { ThrowNext = false; throw new InvalidOperationException("T07 expected test backend fault"); }
            }
            public void StopAll(GameObject emitter) { }
        }
        private void OnEnable()
        {
            pauseButton.onClick.AddListener(Pause);
            permissionButton.onClick.AddListener(ToggleV5Body);
            deathButton.onClick.AddListener(Die);
            checksButton.onClick.AddListener(Begin);
        }
        private void OnDisable()
        {
            pauseButton.onClick.RemoveListener(Pause);
            permissionButton.onClick.RemoveListener(ToggleV5Body);
            deathButton.onClick.RemoveListener(Die);
            checksButton.onClick.RemoveListener(Begin);
            if (checks != null) StopCoroutine(checks);
            GameAudio.UninstallBackend(recorder);
        }
        private void Update()
        {
            status.text = $"T07 V5 | {run.Phase} | Body={state.HasBodyCore} | Bite={state.CanBite} | Sword={state.CanUseSword} | damage={state.BiteDamage}\n"
                + $"Dummy HP={dummy.Health} hits={dummy.HitCount} | cooldown={attack.CooldownRemaining:F2} | grounded={motor.IsGrounded}\n"
                + (running ? $"AUTO: {passed} passed / {failed} failed" : finished ? $"AUTO DONE: {passed} passed / {failed} failed - Stop/Play for fresh manual test"
                : "MANUAL: Body/Arms button acquires body, then toggles Arms. A/D move, Space jump, Enter / left mouse Attack.");
        }
        private void Pause() { if (!running) { if (run.Phase == RunPhase.Paused) run.TryResume(); else run.TryPause(); } }
        private void ToggleV5Body()
        {
            if (running) return;
            if (!state.HasBodyCore) state.TryAcquireBodyCore();
            else if (state.Contains(LoadoutItemId.Arms)) state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
            else state.TryAddLoadoutItem(LoadoutItemId.Arms);
        }
        private void Die() { if (!running && state.IsAlive) state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Enemy)); }
        private void Begin()
        {
            if (running || finished || run.Phase != RunPhase.Playing) return;
            running = true;
            checks = StartCoroutine(Verify());
        }
        private void Check(bool ok, string name)
        {
            if (ok) passed++; else failed++;
            Debug.Log($"[T07 CHECK] {(ok ? "PASS" : "FAIL")} {name}", this);
        }
        private static void KeyState(params Key[] keys) { InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys)); }
        private IEnumerator Press(Key key)
        {
            KeyState(key); yield return new WaitForSecondsRealtime(0.08f);
            KeyState(); yield return new WaitForSecondsRealtime(0.08f);
        }
        private IEnumerator Ready() { yield return new WaitForSeconds(config.CooldownSeconds + 0.1f); }
        private static void Bind(MonoBehaviour component, string field, UnityEngine.Object value)
        {
            component.enabled = false;
            component.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
            component.enabled = true;
        }
        private void BindCombat(MonoBehaviour source)
        {
            Bind(attack, "combatStateSource", source);
            Bind(router, "combatStateSource", source);
        }
        private IEnumerator Verify()
        {
            GameAudio.InstallBackend(recorder);
            KeyState(); input.DiscardGameplayInput();
            yield return new WaitForSecondsRealtime(0.4f);
            Check(input.IsInitialized && state.IsInitialized && run.IsInitialized && attack.IsWired && router.IsWired, "real C01/C02/T06/Router/Bite wiring");
            // 只在测试中冻结位置，避免设备模拟/移动导致靶子范围漂移；不新增运动写入口。
            body.linearVelocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Vector3 near = body.transform.position + Vector3.right;
            dummy.transform.position = near;
            Physics2D.SyncTransforms();
            yield return Ready();
            int hp = dummy.Health, hits = dummy.HitCount, cues = recorder.Bites, ownHp = state.CurrentHealth;
            Check(!state.HasBodyCore && !state.CanBite && !state.CanUseSword, "V5 Head only has no attack permission");
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && dummy.HitCount == hits && recorder.Bites == cues && recorder.Weapons == 0,
                "Head Attack consumes without damage or attack Cue");
            Check(state.TryAcquireBodyCore() && state.HasBodyCore && state.CanBite && !state.CanUseSword,
                "real BodyCore acquisition enables Bite without Arms");
            ownHp = state.CurrentHealth;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp - state.BiteDamage, "Attack reader chain uses real PlayerState BiteDamage");
            Check(dummy.HitCount == hits + 1, "two overlapping colliders damage one receiver once");
            Check(dummy.LastRequest.Kind == DamageKind.Enemy && dummy.LastRequest.Source == state.gameObject, "legal DamageRequest kind/source");
            Check(state.CurrentHealth == ownHp, "own collider / IDamageable excluded");
            Check(recorder.Bites == cues + 1, "actual start requests PlayerBite once");
            hp = dummy.Health; cues = recorder.Bites;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && recorder.Bites == cues, "cooldown consumes fast repeat without damage/recorder");
            yield return Ready();
            Check(dummy.Health == hp && recorder.Bites == cues, "rejected cooldown input never plays later");
            yield return Press(Key.Enter);
            Check(dummy.Health == hp - state.BiteDamage && recorder.Bites == cues + 1, "new request after cooldown accepted");
            yield return Ready();
            dummy.transform.position = near + Vector3.right * 10f; Physics2D.SyncTransforms();
            hp = dummy.Health; cues = recorder.Bites;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && recorder.Bites == cues + 1, "miss still starts one action without damage");
            dummy.transform.position = near; Physics2D.SyncTransforms();
            yield return Ready();
            // 参数试验只改运行时Config副本，退出Play不污染持久资产。
            var configProbe = Instantiate(config);
            Bind(attack, "config", configProbe);
            typeof(BiteConfig).GetField("biteRadius", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configProbe, 0.01f);
            hp = dummy.Health; cues = recorder.Bites;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && recorder.Bites == cues + 1, "Inspector radius controls hit reach");
            yield return Ready();
            typeof(BiteConfig).GetField("biteRadius", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configProbe, config.Radius);
            typeof(BiteConfig).GetField("includeTriggers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configProbe, false);
            yield return Press(Key.Enter);
            Check(dummy.Health == hp, "includeTriggers=false excludes trigger dummy");
            yield return Ready();
            typeof(BiteConfig).GetField("includeTriggers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configProbe, true);
            typeof(BiteConfig).GetField("targetLayers", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(configProbe, (LayerMask)2);
            yield return Press(Key.Enter);
            Check(dummy.Health == hp, "target LayerMask excludes Default dummy");
            Bind(attack, "config", config);
            Destroy(configProbe);
            yield return Ready();
            var fixtureObject = new GameObject("T07 runtime-only external combat fixture");
            var fixture = fixtureObject.AddComponent<T07CombatFixture>();
            fixture.Damage = 7; fixture.Allowed = false;
            BindCombat(fixture);
            hp = dummy.Health; cues = recorder.Bites;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && recorder.Bites == cues, "external CanBite=false refuses damage and recorder");
            fixture.Allowed = true;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp - 7 && recorder.Bites == cues + 1, "external CanBite=true accepted without interpreting form");
            yield return Ready();
            fixture.Damage = 0; hp = dummy.Health; hits = dummy.HitCount; cues = recorder.Bites;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && dummy.HitCount == hits && recorder.Bites == cues + 1, "zero damage creates no request but action/recorder valid");
            yield return Ready();
            fixture.Damage = 7; fixture.Allowed = true;
            recorder.DuringPlay = () => fixture.Allowed = false;
            hp = dummy.Health; cues = recorder.Bites;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && recorder.Bites == cues + 1, "permission rechecked at hit after action start");
            recorder.DuringPlay = null;
            BindCombat(state);
            Destroy(fixtureObject);
            yield return Ready();
            recorder.ThrowNext = true; hp = dummy.Health;
            yield return Press(Key.Enter);
            Check(dummy.Health == hp - state.BiteDamage, "fault recorder backend cannot block damage");
            yield return Ready();
            Check(state.TryAddLoadoutItem(LoadoutItemId.Arms) && !state.Contains(LoadoutItemId.Legs)
                && !state.CanBite && state.CanUseSword, "V5 Arms without Legs selects Sword permission");
            hp = dummy.Health; cues = recorder.Bites;
            var probeObject = new GameObject("T07 runtime-only input consumption probe");
            var probe = probeObject.AddComponent<T07InputProbe>();
            probe.Source = input;
            Bind(router, "inputSource", probe);
            // 第二个Bite没有输入读口或FixedUpdate消费者，不应抢请求或补打。
            var secondObject = new GameObject("T07 runtime-only second Bite action");
            secondObject.transform.SetParent(state.transform, false);
            var secondBite = secondObject.AddComponent<PlayerBiteAttack>();
            Bind(secondBite, "combatStateSource", state);
            Bind(secondBite, "biteOrigin", typeof(PlayerBiteAttack).GetField("biteOrigin", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack) as Transform);
            Bind(secondBite, "config", config);
            yield return Press(Key.Enter);
            Check(!state.CanBite && state.CanUseSword && dummy.Health == hp && recorder.Bites == cues && recorder.Weapons == 0,
                "Arms Attack with unimplemented T08 does not fall back to Bite or fake Sword");
            Check(probe.SuccessfulConsumes == 1, "one Arms request consumed once with second Bite present");
            Check(state.TryRemoveLoadoutItem(LoadoutItemId.Arms) && state.CanBite && !state.CanUseSword,
                "real V5 Arms remove restores Bite permission");
            yield return new WaitForSeconds(0.2f);
            Check(dummy.Health == hp && recorder.Bites == cues, "Arms request is not replayed after removal");
            yield return Press(Key.Enter);
            Check(state.CanBite && dummy.Health == hp - state.BiteDamage && recorder.Bites == cues + 1, "next fresh Attack after Arms removal bites once");
            Check(probe.SuccessfulConsumes == 2, "two requests consumed exactly once each; second Bite never polls input");
            Destroy(secondObject);
            Bind(router, "inputSource", input);
            Destroy(probeObject);
            yield return Ready();
            // 在动作已发Cue、尚未判定期间，通过真实状态取得Arms，必须拒绝旧咬击伤害。
            hp = dummy.Health; cues = recorder.Bites;
            recorder.DuringPlay = () => state.TryAddLoadoutItem(LoadoutItemId.Arms);
            yield return Press(Key.Enter);
            Check(state.CanUseSword && !state.CanBite && dummy.Health == hp && recorder.Bites == cues + 1,
                "real Arms gained during Bite invalidates hit before damage");
            recorder.DuringPlay = null;
            state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
            yield return Ready();
            hp = dummy.Health; cues = recorder.Bites;
            Check(run.TryPause(), "formal pause accepted");
            KeyState(Key.Enter); yield return new WaitForSecondsRealtime(0.15f);
            Check(dummy.Health == hp && recorder.Bites == cues && !state.CanBite, "Paused formal state denies attack");
            run.TryResume(); yield return new WaitForSecondsRealtime(0.2f);
            Check(dummy.Health == hp && recorder.Bites == cues, "resume does not replay held pause input");
            KeyState(); yield return new WaitForSecondsRealtime(0.1f);
            yield return Press(Key.Enter);
            Check(dummy.Health == hp - state.BiteDamage, "fresh post-resume request works");
            yield return Ready();
            hp = dummy.Health; cues = recorder.Bites;
            run.TryBeginChoosing(this);
            yield return Press(Key.Enter);
            Check(dummy.Health == hp && recorder.Bites == cues && !state.CanBite, "Choosing formal state denies attack");
            run.TryEndChoosing(this); yield return new WaitForSecondsRealtime(0.2f);
            Check(dummy.Health == hp && recorder.Bites == cues, "Choosing input does not accumulate");
            foreach (string field in new[] { "combatStateSource", "biteOrigin", "config" })
            {
                UnityEngine.Object original = (UnityEngine.Object)typeof(PlayerBiteAttack).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(attack);
                Bind(attack, field, null);
                yield return Press(Key.Enter);
                Check(!attack.IsWired && dummy.Health == hp, "missing " + field + " safely refuses; contextual warning expected");
                Bind(attack, field, original);
            }
            Bind(router, "inputSource", null);
            yield return Press(Key.Enter);
            Check(!router.IsWired && dummy.Health == hp, "missing router input safely refuses; contextual warning expected");
            Bind(router, "inputSource", input);
            // T06短回归仍使用正式输入；身体写入仅此测试夹具恢复约束。
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            float x = body.position.x;
            yield return Press(Key.D);
            Check(body.position.x > x + 0.05f, "T06 real Move input short regression");
            yield return new WaitForSeconds(0.3f);
            yield return Press(Key.Space);
            Check(body.linearVelocity.y > 0f && !motor.IsGrounded, "T06 real Jump input short regression");
            hp = dummy.Health; cues = recorder.Bites;
            state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Enemy));
            yield return Press(Key.Enter);
            Check(run.Phase == RunPhase.Dead && !state.CanBite && dummy.Health == hp && recorder.Bites == cues, "real death freezes gameplay and rejects attack");
            KeyState();
            finished = true; running = false;
            GameAudio.UninstallBackend(recorder);
            Debug.Log($"[T07 V5 RESULT] {passed} passed / {failed} failed. T08 unimplemented; main map not integrated; physical input not certified.", this);
        }
    }
}
