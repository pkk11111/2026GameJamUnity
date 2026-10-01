// Soap/T09：默认人工试玩；显式按钮用全新夹具运行原41项真实C01/C02/T07/Physics2D验收。
// 输入经InputSystem设备注入；反射只用于隔离夹具接线/运行Config副本，绝不复制玩家伤害逻辑。
// 直接依赖Core/Runtime/Gameplay/TMP/InputSystem；交接Soap.handoff；规范AGENTS.md。
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.T09
{
    public sealed class T09SmokeDriver : MonoBehaviour
    {
        [SerializeField] private EnemyBasic enemy;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private EnemyBasicConfig config;
        [SerializeField] private T09RegistrationProbe registration;
        [SerializeField] private PlayerState state;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerAttackRouter router;
        [SerializeField] private PlayerBiteAttack bite;
        [SerializeField] private BiteConfig biteConfig;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private RunController run;
        [SerializeField] private TMP_Text status;
        [SerializeField, Tooltip("默认false人工控制；true仅在新鲜夹具启动完整自动检查。")]
        private bool autoRun = false;
        [SerializeField] private Button autoChecksButton;
        [SerializeField, Tooltip("当前测试夹具根，切Auto时先停用本夹具唯一Bootstrap。")]
        private GameObject testRigRoot;
        [SerializeField, Tooltip("已保存的新鲜T09夹具Prefab；Auto不继承人工玩家/敌人状态。")]
        private GameObject testRigPrefab;
        private int passed, failed;
        private string stage = "Manual: A/D, Space, Enter/left click. Enemy is stationary.";

        private void OnEnable()
        {
            if (autoChecksButton != null)
            {
                autoChecksButton.onClick.AddListener(RunAutoChecks);
                autoChecksButton.interactable = !autoRun;
            }
        }
        private void OnDisable()
        {
            if (autoChecksButton != null)
            {
                autoChecksButton.onClick.RemoveListener(RunAutoChecks);
            }
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (autoRun)
            {
                yield return Verify();
            }
            else
            {
                // 仅测试初始躯干准备，走真实写口；不注入输入、不移动/冻结、不伤害或切阶段。
                state.TryAcquireBodyCore();
                Debug.Log("[T09 MANUAL] Ready: real input/motor/Bite/EnemyBasic; Auto Checks only by explicit button.", this);
            }
        }
        /// <summary>人工点击后先停旧入口，再启新Prefab。仅测试模式切换，不是正式重开或复活。</summary>
        public void RunAutoChecks()
        {
            if (autoRun || testRigRoot == null || testRigPrefab == null)
            {
                return;
            }
            autoRun = true; // 防同帧连点；停旧Bootstrap先释放唯一运行入口。
            var container = new GameObject("T09 Fresh Auto Checks Session");
            container.SetActive(false);
            GameObject fresh = Instantiate(testRigPrefab, container.transform);
            var driver = fresh.GetComponentInChildren<T09SmokeDriver>(true);
            driver.autoRun = true;
            Debug.Log("[T09 MODE] Explicit Auto Checks: fresh player/enemy/run/registration, manual session stopped.", this);
            testRigRoot.SetActive(false);
            container.SetActive(true);
        }
        private void Update()
        {
            if (!autoRun)
            {
                // 三行适配既有测试面板，避免扩展提示把第一行生命/模式顶出Game View。
                status.text = $"T09 MANUAL | {run.Phase} | Player HP {state.CurrentHealth}/{state.MaximumHealth} | Enemy HP {enemy.CurrentHealth}/{enemy.MaximumHealth}\n"
                    + "Move A/D or arrows | Jump Space | Bite Enter / left click\n"
                    + "Stop/Play resets Manual. Run T09 Auto Checks starts a fresh separate fixture.";
                return;
            }
            status.text = $"T09 AUTO CHECKS | {run.Phase} | Player HP {state.CurrentHealth}/{state.MaximumHealth} | Bite {state.BiteDamage}\n"
                + $"Enemy HP {enemy.CurrentHealth}/{enemy.MaximumHealth} | Attack {enemy.AttackDamage}\n"
                + $"{stage}\n{passed} passed / {failed} failed";
        }
        private void Check(bool ok, string label)
        {
            if (ok)
            {
                passed++;
            }
            else
            {
                failed++;
            }
            Debug.Log($"[T09 CHECK] {(ok ? "PASS" : "FAIL")} {label}", this);
        }
        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
        private IEnumerator PressAttack()
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Enter));
            yield return new WaitForSecondsRealtime(0.08f);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.08f);
        }
        private EnemyBasic Spawn(EnemyBasicConfig settings, Vector3 position)
        {
            var container = new GameObject("T09 runtime-only spawn fixture");
            container.SetActive(false);
            GameObject instance = Instantiate(enemyPrefab, container.transform);
            instance.transform.position = position;
            var result = instance.GetComponent<EnemyBasic>();
            Set(result, "config", settings); Set(result, "runContextSource", run);
            Set(result, "registrationSource", registration);
            Set(instance.GetComponentInChildren<EnemyContactAttack>(), "playerDamageableSource", state);
            container.SetActive(true);
            return result;
        }
        private IEnumerator Verify()
        {
            stage = "AUTO: real Bite -> enemy";
            body.linearVelocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            input.DiscardGameplayInput();
            Check(state.IsInitialized && run.IsInitialized && input.IsInitialized && router.IsWired && bite.IsWired,
                "real C01/C02/T07 wiring");
            Check(state.TryAcquireBodyCore() && state.CanBite && !state.Contains(LoadoutItemId.Arms),
                "real BodyCore, no Arms, CanBite");
            enemy.transform.position = body.transform.position + Vector3.right * 1.05f;
            var contact = enemy.GetComponentInChildren<EnemyContactAttack>();
            contact.enabled = false; Physics2D.SyncTransforms();
            Check(enemy.IsInitialized && enemy.IsAlive && enemy.CurrentHealth == config.MaximumHealth
                && enemy.AttackDamage == config.ContactDamage, "A Config initializes unique runtime HP/attack");
            Check(registration.Contains(enemy) && registration.Registered == 1, "N initial registration once");
            Check(enemy.GetComponentsInChildren<EnemyBasic>(true).Length == 1
                && System.Array.TrueForAll(enemy.GetComponentsInChildren<MonoBehaviour>(true), c => c != null),
                "M one HP owner / no missing scripts");
            var visual = enemy.GetComponent<SpriteRenderer>();
            Check(visual.enabled, "presentation live enemy SpriteRenderer enabled");
            var solid = enemy.GetComponent<BoxCollider2D>();
            var attackTrigger = contact.GetComponent<BoxCollider2D>();
            Check(solid.enabled && attackTrigger.enabled, "collision A live solid/attack colliders enabled");
            Check(!solid.isTrigger && Vector2.Distance(solid.bounds.size, visual.bounds.size) < 0.001f,
                "M physical collider matches visible placeholder");
            int deaths = 0, healthEvents = 0;
            enemy.Died += () => deaths++;
            enemy.HealthChanged += () => healthEvents++;
            int hp = enemy.CurrentHealth;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.BiteDamage && healthEvents == 1,
                "B InputSystem -> Reader -> Router -> Bite -> Physics2D -> real Enemy HP once");
            yield return new WaitForSeconds(biteConfig.CooldownSeconds + 0.05f);
            yield return PressAttack();
            yield return new WaitForSeconds(biteConfig.CooldownSeconds + 0.05f);
            yield return PressAttack();
            Check(enemy.CurrentHealth == 0 && !enemy.IsAlive && deaths == 1 && healthEvents == 3,
                "C three real bites kill with HealthChanged three / Died once");
            Check(!solid.enabled && !attackTrigger.enabled, "collision B lethal real Bite disables solid/attack colliders");
            Check(enemy.gameObject.activeSelf, "collision enemy object retained without destroying/deactivating root");
            Check(!visual.enabled && deaths == 1, "presentation lethal Bite immediately hides visual, Died once");
            Check(!registration.Contains(enemy) && registration.Unregistered == 1, "N death unregisters once");
            Check(!enemy.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)) && deaths == 1,
                "D dead direct damage rejected");
            yield return new WaitForSeconds(biteConfig.CooldownSeconds + 0.05f);
            yield return PressAttack();
            Check(enemy.CurrentHealth == 0 && deaths == 1 && healthEvents == 3, "D real dead-target Bite cannot damage/repeat death");
            enemy.enabled = false;
            solid.enabled = true; attackTrigger.enabled = true; // 模拟启停过程中配置被恢复。
            visual.enabled = true;
            enemy.enabled = true;
            Check(!solid.enabled && !attackTrigger.enabled && deaths == 1,
                "collision D component re-enable restores dead collision-off, Died still once");
            Check(!visual.enabled && !enemy.IsAlive && deaths == 1,
                "presentation dead component re-enable keeps visual hidden and death once");
            enemy.gameObject.SetActive(false);
            solid.enabled = true; attackTrigger.enabled = true;
            visual.enabled = true;
            enemy.gameObject.SetActive(true);
            Check(!solid.enabled && !attackTrigger.enabled && deaths == 1,
                "collision D/E object re-enable cannot restore corpse blockage or repeat death");
            Check(!visual.enabled && !enemy.IsAlive && deaths == 1,
                "presentation dead object re-enable keeps visual hidden and death once");
            Check(!enemy.IsAlive && enemy.CurrentHealth == 0 && deaths == 1 && !registration.Contains(enemy),
                "E disable/enable never revives/re-registers corpse");
            contact.enabled = true;
            int playerHp = state.CurrentHealth;
            yield return new WaitForSeconds(config.ContactInterval + 0.1f);
            Check(state.CurrentHealth == playerHp, "J dead enemy cannot contact damage");
            Check(!enemy.TrySetRuntimeStats(39, 39, 7), "N runtime adapter endpoint rejects resurrection");
            enemy.transform.position += Vector3.right * 8f;

            stage = "AUTO: contact -> real PlayerState / lifecycle / configuration";
            var settings = Instantiate(config);
            Set(settings, "maximumHealth", 26); Set(settings, "contactDamage", 9); Set(settings, "contactInterval", 1.2f);
            var live = Spawn(settings, body.transform.position + Vector3.right * 1.05f);
            var liveContact = live.GetComponentInChildren<EnemyContactAttack>();
            int hits = 0; DamageRequest last = default;
            liveContact.DamageApplied += request => { hits++; last = request; };
            var extra = new GameObject("T09 runtime-only extra player collider", typeof(CircleCollider2D));
            extra.transform.SetParent(state.transform, false);
            extra.GetComponent<CircleCollider2D>().isTrigger = true;
            extra.GetComponent<CircleCollider2D>().radius = 0.5f;
            Physics2D.SyncTransforms();
            Check(live.MaximumHealth == 26 && live.CurrentHealth == 26 && live.AttackDamage == 9,
                "L alternate Config HP/damage actually initialize");
            yield return new WaitForSeconds(0.12f);
            Check(state.CurrentHealth == playerHp - 9 && hits == 1, "F/G two real player colliders, one actual damage");
            Check(last.Kind == DamageKind.Enemy && last.Source == live.gameObject && last.Amount == 9,
                "F actual request Enemy kind / owner source / runtime damage");
            yield return new WaitForSeconds(0.72f);
            Check(hits == 1, "H/L alternate interval 1.2 blocks at original default 0.6");
            yield return new WaitForSeconds(0.5f);
            Check(hits == 2 && state.CurrentHealth == playerHp - 18, "H new cooldown period damages once");
            playerHp = state.CurrentHealth;
            Check(run.TryPause(), "I real Pause accepted");
            Check(!live.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)), "I enemy rejects new damage while paused");
            yield return new WaitForSecondsRealtime(1.5f);
            Check(hits == 2 && state.CurrentHealth == playerHp, "I Pause no player damage");
            run.TryResume();
            yield return new WaitForSeconds(0.15f);
            Check(hits == 2, "I resume no backlog/immediate burst");
            yield return new WaitForSeconds(1.15f);
            Check(hits == 3, "I one fresh period after resume");
            playerHp = state.CurrentHealth;
            run.TryBeginChoosing(this);
            Check(!live.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)), "I enemy rejects damage during Choosing");
            yield return new WaitForSecondsRealtime(1.5f);
            Check(hits == 3 && state.CurrentHealth == playerHp, "I Choosing no contact damage");
            run.TryEndChoosing(this);
            yield return new WaitForSeconds(0.15f);
            Check(hits == 3, "I Choosing resume no backlog");
            liveContact.enabled = false;
            int registered = registration.Registered, unregistered = registration.Unregistered;
            live.gameObject.SetActive(false);
            Check(!registration.Contains(live) && registration.Unregistered == unregistered + 1,
                "N live disable unregisters once");
            live.gameObject.SetActive(true); liveContact.enabled = false;
            Check(registration.Contains(live) && registration.Registered == registered + 1
                && live.CurrentHealth == 26, "N re-enable live registers without reinitializing");
            Check(!live.TryTakeDamage(default), "invalid default DamageRequest rejected without event");
            int changed = 0; live.HealthChanged += () => changed++;
            Check(live.TrySetRuntimeStats(26, 26, 11) && changed == 0 && live.AttackDamage == 11,
                "N explicit runtime attack snapshot no false HealthChanged");
            Check(!live.TrySetRuntimeStats(0, 1, 11) && !live.TrySetRuntimeStats(26, 27, 11)
                && live.MaximumHealth == 26, "N invalid runtime snapshot atomic refusal");
            live.TryTakeDamage(new DamageRequest(3, DamageKind.Enemy));
            int damagedHp = live.CurrentHealth;
            live.gameObject.SetActive(false); live.gameObject.SetActive(true); liveContact.enabled = false;
            Check(live.CurrentHealth == damagedHp && changed == 1, "live damaged disable/enable preserves HP");
            Check(config.MaximumHealth == 39 && config.ContactDamage == 7 && settings.ContactDamage == 9,
                "runtime HP/attack mutations never write Config");

            stage = "FINAL PLAY SANITY: real input Bite and contact";
            live.TrySetRuntimeStats(26, 26, 9);
            yield return new WaitForSeconds(biteConfig.CooldownSeconds + 0.05f);
            yield return PressAttack();
            Check(live.CurrentHealth == 26 - state.BiteDamage, "sanity fresh real input Bite reduces live enemy HP");
            playerHp = state.CurrentHealth; int before = hits;
            liveContact.enabled = true;
            yield return new WaitForSeconds(0.12f);
            Check(state.CurrentHealth == playerHp - live.AttackDamage && hits == before + 1,
                "sanity fresh contact reduces real player HP");
            liveContact.enabled = false;
            yield return new WaitForSeconds(biteConfig.CooldownSeconds + 0.05f);
            yield return PressAttack();
            Check(!live.IsAlive && live.CurrentHealth == 0, "sanity fresh enemy killed by real Bite");
            Debug.Log("[T09 PLAY SANITY] real input Bite damages/kills; physical contact damages real player.", this);

            stage = "AUTO: enemy contact can kill real player";
            var killer = Spawn(config, body.transform.position + Vector3.right * 1.05f);
            Check(killer.TrySetRuntimeStats(config.MaximumHealth, config.MaximumHealth, state.CurrentHealth),
                "test-only explicit runtime lethal contact snapshot");
            int deathCount = 0, killerHits = 0;
            state.Died += () => deathCount++;
            killer.GetComponentInChildren<EnemyContactAttack>().DamageApplied += request => killerHits++;
            Physics2D.SyncTransforms();
            yield return new WaitForSecondsRealtime(0.2f);
            Check(state.CurrentHealth == 0 && !state.IsAlive && run.Phase == RunPhase.Dead && deathCount == 1,
                "K real contact -> PlayerState HP0 -> Bootstrap -> Run Dead once");
            yield return new WaitForSecondsRealtime(1f);
            Check(killerHits == 1 && deathCount == 1 && !killer.CanAct, "K Dead player/run cannot take further contact");
            Check(!killer.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)), "Dead run rejects enemy damage");
            stage = "AUTO DONE (player death checked); stop Play to edit. No map integration.";
            Debug.Log($"[T09 RESULT] {passed} passed / {failed} failed; real T07/PlayerState, no T08/T17/map integration.", this);
        }
    }
}
