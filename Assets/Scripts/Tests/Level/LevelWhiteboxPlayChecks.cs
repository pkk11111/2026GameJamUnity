// 职责：Level 适配独立 Play 回归；真实输入/物理/场景交互，临时测试跑道不保存。
// 模块/维护：controller；依赖：WhiteBox、Runtime、Core、Input System；仅白板试走场景。
// 接线：Inspector 绑定真实实例；自动验证默认关闭，BeginVerification 手动启动，最终进入 Dead。
// 交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
using System.Collections;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Regrowth.Tests.Level
{
    public sealed class LevelWhiteboxPlayChecks : MonoBehaviour
    {
        [SerializeField, Tooltip("白板唯一运动实例。")] private WhiteboxPlayer2D player;
        [SerializeField, Tooltip("唯一真实运行控制器。")] private RunController run;
        [SerializeField, Tooltip("唯一真实输入适配器。")] private PlayerInputReader input;
        [SerializeField, Tooltip("唯一真实生命/构筑状态。")] private PlayerState state;
        [SerializeField, Tooltip("唯一正式交互选择器。")] private PlayerInteractor interactor;
        [SerializeField, Tooltip("唯一启动入口。")] private GameBootstrap bootstrap;
        [SerializeField, Tooltip("原地图 Portal_A。")] private PrototypePortal2D portalA;
        [SerializeField, Tooltip("原地图 Portal_B。")] private PrototypePortal2D portalB;
        [SerializeField, Tooltip("Portal_A 实際目的地。")] private Transform destinationA;
        [SerializeField, Tooltip("原地图 Button_A。")] private PrototypeToggleButton2D buttonA;
        [SerializeField, Tooltip("Button_A 目标门。")] private PrototypeDoor2D doorA;
        [SerializeField, Tooltip("原地图 Spike_A。")] private PrototypeSpike2D spikeA;
        [SerializeField, Tooltip("保留子物体相机。")] private Camera followCamera;
        [SerializeField, Tooltip("默认关闭方便手动试走；自动测试会最终进入 Dead。")] private bool autoVerify;
        private Keyboard keyboard;
        private GameObject testFloor;
        private GameObject testWall;
        private readonly List<string> checks = new List<string>();
        private readonly object choiceOwner = new object();
        private Vector2 spawn;
        private bool verifying;
        public int PassedChecks { get; private set; }
        public int FailedChecks { get; private set; }
        public bool IsVerifying => verifying;
        public string LastReport { get; private set; } = "Not run";

        private void Start()
        {
            if (autoVerify)
            {
                BeginVerification();
            }
        }

        /// <summary>只在本场景 Playing 手动调用；会创建临时跑道并在完成后清理，最后验死亡停止。</summary>
        public void BeginVerification()
        {
            if (verifying || !Application.isPlaying)
            {
                return;
            }
            if (player == null || run == null || input == null || state == null || interactor == null
                || bootstrap == null || portalA == null || portalB == null || destinationA == null
                || buttonA == null || doorA == null || spikeA == null || followCamera == null)
            {
                Debug.LogError("LevelWhiteboxPlayChecks 测试引用不完整。", this);
                return;
            }
            if (run.IsGameplayActive)
            {
                StartCoroutine(Verify());
            }
        }

        private IEnumerator Verify()
        {
            verifying = true;
            PassedChecks = FailedChecks = 0;
            checks.Clear();
            LastReport = "Running";
            spawn = new Vector2(-12.34f, -5.7f);
            keyboard = InputSystem.AddDevice<Keyboard>("LevelWhiteboxTestKeyboard");
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            try
            {
                yield return Sample(0.04f);
                Check(bootstrap.IsStarted && input.IsInitialized && player.IsGameplayActive, "unique real runtime starts");
                Check(((ILoadoutState)state).Items.Count == 0, "test skills do not forge formal loadout");
                Check(followCamera.transform.parent == player.transform, "original child camera retained");
                Check(!player.TryTeleportTo(new Vector2(float.NaN, 0f)), "invalid relocation rejected");

                testFloor = new GameObject("TEMP Level Test Floor");
                testFloor.transform.position = new Vector3(1000f, -1f, 0f);
                testFloor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
                Check(player.TryTeleportTo(new Vector2(1000f, 1f)), "movement owns queued relocation");
                Check(!player.TryTeleportTo(new Vector2(1001f, 1f)), "duplicate pending relocation rejected");
                yield return Sample(0.6f);
                Check(player.IsGrounded && Mathf.Abs(body.linearVelocity.y) < 0.05f, "G33 rays land on real 2D floor");
                float x = player.Position.x;
                yield return Sample(0.2f, Key.D);
                Check(player.Position.x > x + 0.7f, "real D input drives FixedUpdate movement");
                yield return Sample(0.06f);
                Check(Mathf.Abs(body.linearVelocity.x) < 0.05f, "release stops horizontal movement");
                float y = player.Position.y;
                yield return Sample(0.06f, Key.Space);
                Check(player.Position.y > y + 0.2f && !player.IsGrounded, "first jump succeeds");
                yield return Sample(0.12f);
                float beforeSecond = body.linearVelocity.y;
                yield return Sample(0.04f, Key.Space);
                Check(body.linearVelocity.y > beforeSecond, "whitebox double jump preserved");
                yield return Sample(0.05f);
                float beforeThird = body.linearVelocity.y;
                yield return Sample(0.04f, Key.Space);
                Check(body.linearVelocity.y < beforeThird, "third airborne jump rejected");
                yield return Sample(1.5f);
                Check(player.IsGrounded, "landing resets shared grounded state");

                yield return Sample(0.04f, Key.RightShift);
                Check(player.IsDashing, "original right Shift binding still works");
                Vector2 pausePosition = player.Position;
                float pauseVelocity = body.linearVelocity.x;
                Check(run.TryPause(), "pause accepted during dash");
                yield return Sample(0.12f, Key.R, Key.Space);
                Check(Vector2.Distance(player.Position, pausePosition) < 0.01f, "pause freezes actual physics");
                Check(!player.TryTeleportTo(Vector2.zero) && !portalA.TryInteract(player.gameObject), "pause rejects relocation and interaction");
                Check(run.TryResume(), "resume accepted");
                yield return Sample(0.2f, Key.R, Key.Space);
                Check(player.Position.x > pausePosition.x && Mathf.Abs(pauseVelocity) > 1f, "dash resumes without R or jump leaking");
                yield return Sample(0.08f);

                Check(run.TryBeginChoosing(choiceOwner), "real Choosing lock acquired");
                Vector2 choicePosition = player.Position;
                yield return Sample(0.1f, Key.D, Key.E, Key.LeftShift);
                Check(Vector2.Distance(player.Position, choicePosition) < 0.01f && !buttonA.TryInteract(player.gameObject),
                    "Choosing blocks movement and switches");
                Check(run.TryEndChoosing(choiceOwner), "choice owner releases pause");
                yield return Sample(0.08f);

                Check(player.TryTeleportTo(new Vector2(1000f, 1f)), "return to isolated physics lane");
                yield return Sample(0.6f);
                testWall = new GameObject("TEMP Level Test Wall");
                testWall.transform.position = new Vector3(1003f, 2f, 0f);
                testWall.AddComponent<BoxCollider2D>().size = new Vector2(1f, 6f);
                yield return Sample(0.04f, Key.D);
                yield return Sample(0.8f, Key.D, Key.LeftShift);
                Check(player.Position.x < 1002.1f, "movement and dash respect solid wall");
                yield return Sample(0.06f);
                Destroy(testWall);
                testWall = null;

                Check(player.TryTeleportTo(buttonA.transform.position), "move to original switch");
                yield return Sample(0.08f);
                bool wasClosed = doorA.IsClosed;
                interactor.RefreshTarget();
                Check(interactor.InteractionId == buttonA.InteractionId, "real interactor selects saved stable switch ID");
                yield return Sample(0.08f, Key.E);
                Check(doorA.IsClosed != wasClosed, "E toggles original target door");
                yield return Sample(0.4f, Key.E);
                Check(doorA.IsClosed != wasClosed, "held E does not toggle again");
                yield return Sample(0.06f);
                yield return Sample(0.08f, Key.E);
                Check(doorA.IsClosed == wasClosed, "release and press toggles door back");
                yield return Sample(0.06f);
                Check(!doorA.TrySetClosed(doorA.IsClosed), "same door state is no-op");

                Check(player.TryTeleportTo(portalA.transform.position), "move to original portal");
                yield return Sample(0.08f);
                interactor.RefreshTarget();
                Check(interactor.InteractionId == portalA.InteractionId, "real interactor selects portal");
                int hp = state.CurrentHealth;
                yield return Sample(0.08f, Key.E);
                Check(Vector2.Distance(player.Position, destinationA.position) < 1f, "E follows original portal destination");
                Check(state.CurrentHealth == hp && ((ILoadoutState)state).Items.Count == 0, "whitebox portal remains free");
                Check(!portalB.TryInteract(player.gameObject), "shared cooldown prevents instant return");
                yield return Sample(0.4f, Key.E);
                Check(Mathf.Abs(player.Position.x - destinationA.position.x) < 0.2f, "holding E does not bounce back after cooldown");
                yield return Sample(0.06f);

                yield return Sample(0.08f, Key.E);
                Check(Vector2.Distance(player.Position, portalA.transform.position) < 1f, "standing second press returns through adjusted Portal_B trigger");
                yield return Sample(0.06f);

                Check(player.TryTeleportTo(spikeA.transform.position), "move to original spike ellipse");
                yield return Sample(0.1f);
                Check(Vector2.Distance(player.Position, spikeA.transform.position) > 0.1f, "actual spike trigger applies knockback");
                Check(state.CurrentHealth == hp, "prototype spikes preserve no-damage behavior");
                Check(!player.TrySpikeKnockback(Vector2.up * 10f, 0.25f, 0.6f), "protection suppresses duplicate spike hit");

                yield return Sample(0.06f, Key.R);
                Check(Vector2.Distance(player.Position, spawn) < 0.2f, "central R resets original spawn");
                Check(!player.IsDashing && state.CurrentHealth == hp, "R clears movement without resetting HP/world");
                yield return Sample(0.08f);
                Check(player.TryTeleportTo(new Vector2(1000f, 1f)), "move to death test lane");
                yield return Sample(0.4f);
                Check(state.TryTakeDamage(new DamageRequest(state.CurrentHealth, DamageKind.Enemy)), "real health death accepted");
                Check(run.Phase == RunPhase.Dead && Time.timeScale == 0f, "death enters real Dead and freezes physics");
                Check(!player.TryResetToStart() && !player.TrySpikeKnockback(Vector2.up, 0.25f, 0.6f)
                    && !buttonA.TryInteract(player.gameObject), "Dead blocks reset, knockback and interaction");
                LastReport = PassedChecks + " passed / " + FailedChecks + " failed";
                Debug.Log("[LevelWhitebox] " + LastReport + "\n" + string.Join("\n", checks), this);
            }
            finally
            {
                verifying = false;
                Cleanup();
            }
        }

        private IEnumerator Sample(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            yield return new WaitForSecondsRealtime(seconds);
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
            if (keyboard != null && keyboard.added)
            {
                InputSystem.RemoveDevice(keyboard);
            }
            keyboard = null;
            if (testFloor != null)
            {
                Destroy(testFloor);
            }
            if (testWall != null)
            {
                Destroy(testWall);
            }
            if (input != null)
            {
                input.DiscardGameplayInput();
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            verifying = false;
            Cleanup();
        }
    }
}
