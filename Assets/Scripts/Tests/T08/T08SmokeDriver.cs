// Soap/T08: Manual by default; explicit Auto button switches to a fresh real C01/C02/T07/T09 rig.
// Device injection and reflection are confined to Auto setup, never the manual session.
// Dependencies: Core/Runtime/Combat/Bite/Sword/EnemyBasic/Audio/InputSystem/TMP/UI.
// Wiring, unverified Play status and test defaults: docs/handoffs/Soap.handoff; rules: AGENTS.md.
using System.Collections;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.Gameplay;
using Regrowth.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Regrowth.Tests.T08
{
    public sealed class T08SmokeDriver : MonoBehaviour
    {
        [SerializeField] private EnemyBasic enemy;
        [SerializeField] private EnemyBasic enemyLeft;
        [SerializeField] private PlayerFacing2D facing;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform groundOrigin;
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private PlayerState state;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerAttackRouter router;
        [SerializeField] private PlayerBiteAttack bite;
        [SerializeField] private BiteConfig biteConfig;
        [SerializeField] private PlayerSwordAttack sword;
        [SerializeField] private SwordConfig swordConfig;
        [SerializeField] private SpriteRenderer slashVisual;
        [SerializeField] private SpriteRenderer biteVisual;
        [SerializeField] private Transform biteOrigin;
        [SerializeField] private Transform swordOrigin;
        [SerializeField] private float rangeOnlyDistance = 2.8f;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private RunController run;
        [SerializeField] private TMP_Text status;
        [SerializeField, Tooltip("False = default Manual. True only on fresh Auto rig.")]
        private bool autoRun;
        [SerializeField] private Button autoChecksButton;
        [SerializeField] private Button removeArmsButton;
        [SerializeField] private Button addArmsButton;
        [SerializeField] private GameObject testRigRoot;
        [SerializeField] private GameObject testRigPrefab;
        private int passed, failed;
        private string stage = "Preparing real fixture";
        private AudioRecorder audioRecorder;
        private IAudioBackend previousBackend;
        private Keyboard addedKeyboard;
        private Gamepad addedGamepad;

        private sealed class AudioRecorder : IAudioBackend
        {
            public int Swords, Bites;
            public GameObject SwordEmitter;
            public void Play(AudioCue cue, GameObject emitter)
            {
                if (cue == AudioCue.PlayerWeaponAttack) { Swords++; SwordEmitter = emitter; }
                if (cue == AudioCue.PlayerBite) Bites++;
            }
            public void StopAll(GameObject emitter) { }
        }
        private void OnEnable()
        {
            autoChecksButton.onClick.AddListener(RunAutoChecks);
            removeArmsButton.onClick.AddListener(RemoveArms);
            addArmsButton.onClick.AddListener(AddArms);
            autoChecksButton.interactable = !autoRun;
            removeArmsButton.interactable = !autoRun;
            addArmsButton.interactable = !autoRun;
        }
        private void OnDisable()
        {
            autoChecksButton.onClick.RemoveListener(RunAutoChecks);
            removeArmsButton.onClick.RemoveListener(RemoveArms);
            addArmsButton.onClick.RemoveListener(AddArms);
            RestoreTestBackend();
            if (addedKeyboard != null) { InputSystem.RemoveDevice(addedKeyboard); addedKeyboard = null; }
            if (addedGamepad != null) { InputSystem.RemoveDevice(addedGamepad); addedGamepad = null; }
        }
        private void RestoreTestBackend()
        {
            if (audioRecorder == null) return;
            GameAudio.UninstallBackend(audioRecorder);
            if (previousBackend != null) GameAudio.InstallBackend(previousBackend);
            audioRecorder = null;
        }
        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            if (autoRun) yield return Verify();
            else
            {
                state.TryAcquireBodyCore();
                state.TryAddLoadoutItem(LoadoutItemId.Arms);
                stage = "Manual ready";
                Debug.Log("[T08 MANUAL] Body + Arms, no Legs. Move to face left/right; release retains facing. Auto only by button.", this);
            }
        }
        public void RemoveArms() { if (!autoRun) state.TryRemoveLoadoutItem(LoadoutItemId.Arms); }
        public void AddArms() { if (!autoRun) state.TryAddLoadoutItem(LoadoutItemId.Arms); }
        public void RunAutoChecks()
        {
            if (autoRun || testRigPrefab == null || testRigRoot == null) return;
            autoRun = true;
            var container = new GameObject("T08 Fresh Auto Checks Session");
            container.SetActive(false);
            Instantiate(testRigPrefab, container.transform);
            testRigRoot.SetActive(false); // Release the old unique Bootstrap before activating the new one.
            container.SetActive(true);
        }
        private void Update()
        {
            string mode = state.CanUseSword ? "SWORD" : state.CanBite ? "BITE" : "NONE";
            status.text = $"T08 {(autoRun ? "AUTO" : "MANUAL")} | Current Attack: {mode} | Arms: {(state.Contains(LoadoutItemId.Arms) ? "Yes" : "No")}\n"
                + $"Bite Damage: {state.BiteDamage} | Bite Range: {biteConfig.Radius:0.##} | Sword Damage: {state.SwordDamage} | Sword Range: {swordConfig.Range:0.##}\n"
                + $"Facing: {(facing.IsFacingRight ? "RIGHT" : "LEFT")} | Player HP: {state.CurrentHealth}/{state.MaximumHealth} | Enemy R: {enemy.CurrentHealth} L: {enemyLeft.CurrentHealth}\n"
                + (autoRun ? $"{stage} | {passed} passed / {failed} failed"
                    : "A/D Move + Face | Space Jump | Enter / LMB Attack | Remove/Add Arms: Bite/Sword. Release keeps facing.");
        }
        private void Check(bool ok, string label)
        {
            if (ok) passed++; else failed++;
            Debug.Log($"[T08 CHECK] {(ok ? "PASS" : "FAIL")} {label}", this);
        }
        private static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
        private IEnumerator PressAttack()
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Enter));
            yield return new WaitForSecondsRealtime(0.04f);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.04f);
        }
        private IEnumerator Ready()
        {
            yield return new WaitForSeconds(Mathf.Max(biteConfig.CooldownSeconds,
                swordConfig.CooldownSeconds) + 0.08f);
        }
        private IEnumerator PressMove(Key key)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(0.08f);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.08f);
        }
        private IEnumerator VerifyFacing()
        {
            stage = "Single facing source / real two-sided attacks";
            var collider = state.GetComponent<BoxCollider2D>();
            Bounds bounds = collider.bounds;
            Vector3 scale = state.transform.localScale, ground = groundOrigin.position;
            Vector2 size = collider.size, offset = collider.offset;
            Check(ReferenceEquals(bite.Facing, facing) && ReferenceEquals(sword.Facing, facing), "Bite/Sword share the explicit neutral facing instance");
            Check(state.GetComponents<PlayerFacing2D>().Length == 1 && facing.FacingSign == 1, "one facing component, default right +1");
            int hp = enemy.CurrentHealth, leftHp = enemyLeft.CurrentHealth;
            int cues = audioRecorder.Swords + audioRecorder.Bites;
            yield return PressMove(Key.A);
            Check(facing.FacingSign == -1 && input.MoveX == 0f, "left input then release retains -1");
            facing.enabled = false; facing.enabled = true;
            Check(facing.FacingSign == -1, "disable/re-enable facing retains last direction");
            Check(visualRoot.localScale.x < 0f, "left facing mirrors visual child only");
            Check(enemy.CurrentHealth == hp && enemyLeft.CurrentHealth == leftHp && audioRecorder.Swords + audioRecorder.Bites == cues, "turning alone never attacks");
            visualRoot.localScale = new Vector3(Mathf.Abs(visualRoot.localScale.x), visualRoot.localScale.y, visualRoot.localScale.z);
            Check(facing.FacingSign == -1, "presentation scale cannot write or infer facing");
            yield return null;
            Check(facing.FacingSign == -1 && visualRoot.localScale.x < 0f, "presentation resumes reading the same facing");
            addedGamepad = InputSystem.AddDevice<Gamepad>();
            Set(facing, "deadZone", 0.5f); // Auto only: make processed, real small stick input observable.
            InputSystem.QueueStateEvent(addedGamepad, new GamepadState { leftStick = new Vector2(0.3f, 0f) });
            yield return new WaitForSecondsRealtime(0.08f);
            Check(input.MoveX > 0f && input.MoveX < 0.5f && facing.FacingSign == -1, "real stick input inside configured dead zone preserves facing");
            InputSystem.QueueStateEvent(addedGamepad, new GamepadState());
            yield return new WaitForSecondsRealtime(0.08f);
            Set(facing, "deadZone", 0.05f);
            InputSystem.RemoveDevice(addedGamepad); addedGamepad = null;
            yield return PressMove(Key.D);
            Check(facing.FacingSign == 1 && input.MoveX == 0f && visualRoot.localScale.x > 0f, "right input then release retains +1 and right visual");
            Check(facing.Direction == Vector2.right && facing.IsFacingRight, "readonly neutral Direction supports future consumers");
            Vector3 rightPosition = enemy.transform.position, leftPosition = enemyLeft.transform.position;
            enemyLeft.GetComponentInChildren<EnemyContactAttack>().enabled = false;
            enemyLeft.TrySetRuntimeStats(500, 500, enemyLeft.AttackDamage);
            enemy.transform.position = state.transform.position + Vector3.right * 1.05f;
            enemyLeft.transform.position = state.transform.position + Vector3.left * 1.05f;
            Physics2D.SyncTransforms();
            state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
            yield return Ready(); hp = enemy.CurrentHealth; leftHp = enemyLeft.CurrentHealth;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.BiteDamage && enemyLeft.CurrentHealth == leftHp, "right Bite hits real right enemy, never close opposite enemy");
            Check(bite.HitCenter.x > state.transform.position.x && Vector3.Distance(biteVisual.transform.position, bite.HitCenter) < 0.001f && !biteVisual.flipX, "right Bite center and flash agree");
            yield return PressMove(Key.A); yield return Ready(); hp = enemy.CurrentHealth; leftHp = enemyLeft.CurrentHealth;
            yield return PressAttack();
            Check(enemyLeft.CurrentHealth == leftHp - state.BiteDamage && enemy.CurrentHealth == hp, "released left Bite hits real left enemy only");
            Check(bite.HitCenter.x < state.transform.position.x && Vector3.Distance(biteVisual.transform.position, bite.HitCenter) < 0.001f && biteVisual.flipX, "left Bite center and flash agree");
            state.TryAddLoadoutItem(LoadoutItemId.Arms);
            yield return Ready(); hp = enemy.CurrentHealth; leftHp = enemyLeft.CurrentHealth;
            yield return PressAttack();
            Check(enemyLeft.CurrentHealth == leftHp - state.SwordDamage && enemy.CurrentHealth == hp, "released left Sword hits real left enemy only");
            Check(sword.HitCenter.x < state.transform.position.x && Vector3.Distance(slashVisual.transform.position, sword.HitCenter) < 0.001f && slashVisual.flipX, "left Sword center and slash agree");
            yield return PressMove(Key.D); yield return Ready(); hp = enemy.CurrentHealth; leftHp = enemyLeft.CurrentHealth;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.SwordDamage && enemyLeft.CurrentHealth == leftHp, "released right Sword hits real right enemy only");
            Check(sword.HitCenter.x > state.transform.position.x && Vector3.Distance(slashVisual.transform.position, sword.HitCenter) < 0.001f && !slashVisual.flipX, "right Sword center and slash agree");
            Check(state.transform.localScale == scale && collider.bounds == bounds && collider.size == size && collider.offset == offset && groundOrigin.position == ground, "turning and both attacks preserve root/collider/ground anchor");
            Check(run.TryPause(), "facing regression enters Paused through actual controller");
            yield return PressMove(Key.A);
            Check(facing.FacingSign == 1 && !sword.TryAttack() && !bite.TryAttack(), "Paused input cannot change facing or attack");
            run.TryResume();
            Check(run.TryBeginChoosing(this), "facing regression enters Choosing through actual controller");
            yield return PressMove(Key.A);
            Check(facing.FacingSign == 1 && !sword.TryAttack() && !bite.TryAttack(), "Choosing input cannot change facing or attack");
            run.TryEndChoosing(this);
            enemy.transform.position = rightPosition; enemyLeft.transform.position = leftPosition;
            Physics2D.SyncTransforms();
        }
        private EnemyBasic SpawnLiveTarget()
        {
            var container = new GameObject("T08 runtime-only real EnemyBasic fixture");
            container.SetActive(false);
            GameObject instance = Instantiate(enemyPrefab, container.transform);
            instance.transform.position = body.position + Vector2.right * 1.05f;
            var target = instance.GetComponent<EnemyBasic>();
            Set(target, "runContextSource", run);
            Set(instance.GetComponentInChildren<EnemyContactAttack>(), "playerDamageableSource", state);
            instance.GetComponentInChildren<EnemyContactAttack>().enabled = false;
            container.SetActive(true);
            Physics2D.SyncTransforms();
            return target;
        }
        private IEnumerator Verify()
        {
            if (Keyboard.current == null) addedKeyboard = InputSystem.AddDevice<Keyboard>();
            previousBackend = (IAudioBackend)typeof(GameAudio).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            audioRecorder = new AudioRecorder();
            GameAudio.InstallBackend(audioRecorder);
            body.linearVelocity = Vector2.zero;
            body.constraints = RigidbodyConstraints2D.FreezeAll; // Auto only.
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            input.DiscardGameplayInput();
            var playerCollider = state.GetComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();
            Bounds originalBounds = playerCollider.bounds;
            var contact = enemy.GetComponentInChildren<EnemyContactAttack>();
            contact.enabled = false;
            var solid = enemy.GetComponent<BoxCollider2D>();
            var trigger = contact.GetComponent<BoxCollider2D>();
            var visual = enemy.GetComponent<SpriteRenderer>();
            Check(state.IsInitialized && run.IsInitialized && input.IsInitialized && router.IsWired && bite.IsWired && sword.IsWired,
                "real C01/C02/Router/Bite/Sword wiring");
            Check(ReferenceEquals(sword.CombatState, state) && ReferenceEquals(bite.CombatState, state), "both actions share actual PlayerState");
            Check(state.SwordDamage > state.BiteDamage, "test configuration SwordDamage > BiteDamage");
            Check(swordConfig.Range > biteConfig.Radius, "test configuration SwordRange > BiteRange");
            Check(Mathf.Approximately(swordConfig.CooldownSeconds, biteConfig.CooldownSeconds), "same test cooldown, no cadence advantage");
            Check(Vector3.Distance(biteOrigin.position, swordOrigin.position) < 0.001f, "equal explicit anchors, range is the geometry difference");
            Check(!state.HasBodyCore && !state.CanBite && !state.CanUseSword, "head has no attack permissions");
            int hp = enemy.CurrentHealth;
            bool sawBiteFlash = false;
            bite.AttackStarted += () => sawBiteFlash |= biteVisual.enabled;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp && audioRecorder.Swords == 0 && audioRecorder.Bites == 0,
                "head Router input causes no damage/cue");
            Check(!sword.TryAttack() && !bite.TryAttack() && !biteVisual.enabled && !slashVisual.enabled,
                "rejected head attacks do not display flashes");
            Check(state.TryAcquireBodyCore() && state.CanBite && !state.CanUseSword, "Body without Arms selects Bite");
            Check(enemy.TrySetRuntimeStats(500, 500, enemy.AttackDamage), "Auto-only real live enemy HP snapshot");
            Check(solid.enabled && trigger.enabled && visual.enabled, "live real enemy solid/trigger/visual enabled");

            stage = "Real Physics2D range advantage";
            enemy.transform.position = body.transform.position + Vector3.right * rangeOnlyDistance;
            Physics2D.SyncTransforms();
            float biteDistance = Vector2.Distance(biteOrigin.position, trigger.ClosestPoint(biteOrigin.position));
            float swordDistance = Vector2.Distance(swordOrigin.position, solid.ClosestPoint(swordOrigin.position));
            Check(biteDistance > biteConfig.Radius && swordDistance < swordConfig.Range,
                "real gap excludes even enemy trigger from Bite, includes solid in Sword");
            hp = enemy.CurrentHealth;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp && audioRecorder.Bites == 1 && audioRecorder.Swords == 0,
                "Sword-only gap: accepted real Bite misses");
            Check(sawBiteFlash && !slashVisual.enabled, "accepted Bite displays distinct cyan jaws only");
            yield return new WaitForSeconds(0.18f);
            Check(!biteVisual.enabled, "Bite flash ends without delayed damage");
            Check(state.TryAddLoadoutItem(LoadoutItemId.Arms) && state.CanUseSword && !state.CanBite
                && !state.Contains(LoadoutItemId.Legs), "Arms without Legs selects Sword");
            Physics2D.SyncTransforms();
            Check(playerCollider.bounds == originalBounds, "adding Arms preserves player collider bounds");
            int events = 0, deaths = 0;
            enemy.HealthChanged += () => events++;
            enemy.Died += () => deaths++;
            yield return Ready();
            int bites = audioRecorder.Bites;
            Check(sword.TryAttack() && enemy.CurrentHealth == hp - state.SwordDamage,
                "Sword-only gap: immediate real Sword settlement in TryAttack");
            Check(slashVisual.enabled && !biteVisual.enabled && audioRecorder.SwordEmitter == state.gameObject,
                "accepted Sword shows gold slash and uses real player emitter");
            Check(audioRecorder.Swords == 1 && audioRecorder.Bites == bites, "Sword cue only, no Bite");
            int cues = audioRecorder.Swords;
            Check(!sword.TryAttack() && audioRecorder.Swords == cues, "cooldown rejects direct spam without cue");
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.SwordDamage && events == 1 && audioRecorder.Swords == cues,
                "Router spam during cooldown cannot hit or emit again");
            yield return new WaitForSeconds(0.18f);
            Check(!slashVisual.enabled, "Sword flash ends, no recovery or delayed settlement");

            stage = "Near range, actual Arms commands and damage";
            state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
            Physics2D.SyncTransforms();
            Check(state.CanBite && !state.CanUseSword && playerCollider.bounds == originalBounds,
                "remove Arms switches next attack to Bite without collider changes");
            enemy.transform.position = body.transform.position + Vector3.right * 1.05f;
            Physics2D.SyncTransforms();
            hp = enemy.CurrentHealth; int beforeEvents = events; cues = audioRecorder.Swords;
            int playerHp = state.CurrentHealth;
            yield return Ready(); yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.BiteDamage && events == beforeEvents + 1 && audioRecorder.Swords == cues,
                "near real Bite uses BiteDamage, multi-collider once, no Sword");
            Check(state.CurrentHealth == playerHp, "Bite excludes actual player");
            bites = audioRecorder.Bites;
            Check(!bite.TryAttack() && audioRecorder.Bites == bites, "Bite cooldown rejects without presentation/cue restart");
            Check(state.TryAddLoadoutItem(LoadoutItemId.Arms) && state.CanUseSword, "add Arms restores next Sword");
            yield return Ready(); hp = enemy.CurrentHealth; beforeEvents = events;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.SwordDamage && events == beforeEvents + 1 && audioRecorder.Bites == bites,
                "near Router Sword uses SwordDamage, multi-collider once, no Bite");
            Check(state.CurrentHealth == playerHp, "Sword excludes actual player");

            stage = "Run phase and component gates";
            yield return Ready(); hp = enemy.CurrentHealth;
            Check(run.TryPause() && !sword.TryAttack() && !bite.TryAttack(), "Pause rejects both attacks");
            cues = audioRecorder.Swords; bites = audioRecorder.Bites;
            yield return PressAttack(); yield return new WaitForSecondsRealtime(0.2f);
            Check(enemy.CurrentHealth == hp && audioRecorder.Swords == cues && audioRecorder.Bites == bites
                && !slashVisual.enabled && !biteVisual.enabled, "Pause input no hit/cue/leftover visual");
            run.TryResume(); yield return new WaitForSeconds(0.2f);
            Check(enemy.CurrentHealth == hp, "resume cannot produce an unrequested old hit");
            yield return Ready(); yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.SwordDamage, "fresh attack after resume works");
            hp = enemy.CurrentHealth;
            Check(run.TryBeginChoosing(this) && !sword.TryAttack() && !bite.TryAttack(), "Choosing rejects both attacks");
            yield return PressAttack(); yield return new WaitForSecondsRealtime(0.2f);
            Check(enemy.CurrentHealth == hp && !slashVisual.enabled && !biteVisual.enabled, "Choosing input no hit/visual");
            run.TryEndChoosing(this); yield return new WaitForSeconds(0.2f);
            Check(enemy.CurrentHealth == hp, "Choosing end does not invent a hit");
            yield return Ready(); sword.enabled = false;
            Check(!sword.TryAttack() && !slashVisual.enabled, "disabled Sword rejects and hides presentation");
            sword.enabled = true; yield return new WaitForSeconds(0.2f);
            Check(enemy.CurrentHealth == hp, "re-enable without Attack cannot damage");

            Check(state.TryApplyReward(new PlayerReward(attackIncrease: 3)), "actual reward updates combat damage");
            yield return Ready(); hp = enemy.CurrentHealth;
            yield return PressAttack();
            Check(enemy.CurrentHealth == hp - state.SwordDamage, "Sword reads current actual damage after reward");
            yield return VerifyFacing();
            stage = "Real enemy death regression";
            enemy.TrySetRuntimeStats(state.SwordDamage * 2, state.SwordDamage * 2, enemy.AttackDamage);
            yield return Ready(); yield return PressAttack();
            Check(enemy.IsAlive && enemy.CurrentHealth == state.SwordDamage, "first Sword really lowers live enemy HP");
            yield return Ready(); yield return PressAttack();
            Check(!enemy.IsAlive && enemy.CurrentHealth == 0 && deaths == 1, "real Sword kills, HP0 / Died once");
            Check(!solid.enabled && !trigger.enabled && !visual.enabled && enemy.gameObject.activeSelf,
                "dead enemy colliders/visual off, runtime object retained");
            playerHp = state.CurrentHealth; contact.enabled = true;
            yield return new WaitForSeconds(0.8f);
            Check(state.CurrentHealth == playerHp, "dead enemy cannot contact damage");
            enemy.gameObject.SetActive(false); enemy.gameObject.SetActive(true);
            Check(!enemy.IsAlive && !solid.enabled && !trigger.enabled && !visual.enabled && deaths == 1,
                "dead re-enable cannot revive/block/show/repeat Died");
            Check(!enemy.TryTakeDamage(new DamageRequest(1, DamageKind.Enemy)), "dead direct damage rejected");
            yield return Ready(); yield return PressAttack();
            Check(enemy.CurrentHealth == 0 && deaths == 1, "Sword against corpse cannot repeat death");

            EnemyBasic survivor = SpawnLiveTarget(); hp = survivor.CurrentHealth;
            Check(state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Enemy, survivor.gameObject))
                && !state.IsAlive && run.Phase == RunPhase.Dead, "actual lethal player damage enters Bootstrap Dead");
            Check(!sword.TryAttack() && !bite.TryAttack(), "Dead rejects both actual actions");
            cues = audioRecorder.Swords; bites = audioRecorder.Bites;
            yield return PressAttack(); yield return new WaitForSecondsRealtime(0.2f);
            Check(survivor.CurrentHealth == hp && audioRecorder.Swords == cues && audioRecorder.Bites == bites
                && !slashVisual.enabled && !biteVisual.enabled, "Dead input cannot damage/cue/display");
            yield return PressMove(Key.A);
            Check(facing.FacingSign == 1, "Dead movement input cannot turn facing");
            stage = "DONE; Stop/Play returns to fresh Manual";
            Debug.Log($"[T08 RESULT] {passed} passed / {failed} failed; immediate real Bite/Sword/EnemyBasic. Manual acceptance is separate.", this);
            RestoreTestBackend();
        }
    }
}
