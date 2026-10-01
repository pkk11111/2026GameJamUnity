// 职责：唯一真实身体状态 + 运动快照 + 成功动作通知驱动 Dada 角色帧；绝不提交伤害。
// 维护 controller/C07；依赖 Runtime/Player/WhiteBox/Bite/Sword/Fire；规范 AGENTS.md。
// 移动音频只读帧快照说明：docs/handoffs/audio-player-movement.handoff；不在表现层直接调用Wwise。
using Regrowth.Core;
using Regrowth.Runtime;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using UnityEngine;
namespace Regrowth.Presentation
{
    [DisallowMultipleComponent]
    public sealed class PlayerCharacterPresentation : MonoBehaviour
    {
        [SerializeField] private PlayerState state;
        [SerializeField] private RunController run;
        [SerializeField] private WhiteboxPlayer2D motor;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D rootCollider;
        [SerializeField] private PlayerFacing2D facing;
        [SerializeField] private PlayerBiteAttack bite;
        [SerializeField] private PlayerSwordAttack sword;
        [SerializeField] private PlayerFireAttack fire;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField] private PlayerAnimationSet animations;
        [SerializeField, Min(0.01f)] private float worldScale = 0.42f;
        private PlayerVisualAction requested, currentAction;
        private PlayerAnimationEntry current;
        private int currentMask = -1;
        private double actionUntil, sampledSince;
        private int lastHealth;
        private double hurtUntil;
        public string CurrentFamily => current == null ? "" : current.family;
        public PlayerVisualAction CurrentAction => currentAction;
        /// <summary>Read-only sampled sprite timing for presentation consumers such as movement audio.</summary>
        public PlayerAnimationEntry CurrentAnimation => current;
        public int CurrentFrameIndex { get; private set; }
        /// <summary>Unwrapped frame number; distinguishes the same sprite in consecutive movement loops.</summary>
        public int CurrentFrameSequence { get; private set; }
        public bool MissingAction
        {
            get;
            private set;
        }
        public bool IsWired => state != null && run != null && motor != null && body != null && rootCollider != null
        && facing != null && bite != null && sword != null && fire != null && visual != null && animations != null;
        private void OnEnable()
        {
            if (!IsWired)
            {
                Debug.LogWarning("[Player Art] Bind state/run/motor/body/collider/facing/actions/renderer/set.", this);
                return;
            }
            state.LoadoutChanged += ResetAction;
            state.BodyChanged += ResetAction;
            state.HealthChanged += OnHealth;
            state.Died += ResetAction;
            bite.AttackStarted += OnBite;
            sword.AttackStarted += OnSword;
            fire.AttackStarted += OnFire;
            motor.Relocated += OnRelocated;
            lastHealth = state.CurrentHealth;
            ResetAction();
        }
        private void OnDisable()
        {
            if (state != null)
            {
                state.LoadoutChanged -= ResetAction;
                state.BodyChanged -= ResetAction;
                state.HealthChanged -= OnHealth;
                state.Died -= ResetAction;
            }
            if (bite != null)
            {
                bite.AttackStarted -= OnBite;
            }
            if (sword != null)
            {
                sword.AttackStarted -= OnSword;
            }
            if (fire != null)
            {
                fire.AttackStarted -= OnFire;
            }
            if (motor != null)
            {
                motor.Relocated -= OnRelocated;
            }
            ResetAction();
        }
        private void OnHealth()
        {
            if (state.CurrentHealth < lastHealth)
            {
                hurtUntil = Time.timeAsDouble + 0.18;
            }
            lastHealth = state.CurrentHealth;
        }
        private int BodyMask => !state.HasBodyCore ? 0 : 1 | (state.Contains(LoadoutItemId.Arms) ? 2 : 0)
        | (state.Contains(LoadoutItemId.Legs) ? 4 : 0) | (state.Contains(LoadoutItemId.Tail) ? 8 : 0)
        | (state.Contains(LoadoutItemId.FlameTail) ? 16 : 0);
        private void OnBite()
        {
            Begin(PlayerVisualAction.Bite);
        }
        private void OnSword()
        {
            Begin(PlayerVisualAction.Attack);
        }
        private void OnFire()
        {
            Begin(PlayerVisualAction.Fire);
        }
        private void OnRelocated()
        {
            fire.Cancel();
            ResetAction();
        }
        private void Begin(PlayerVisualAction action)
        {
            requested = action;
            var entry = animations.Find(BodyMask, action);
            double seconds = entry == null || entry.clip == null ? 0.18 : Mathf.Max(0.18f, entry.frames.Length / entry.clip.frameRate);
            actionUntil = Time.timeAsDouble + seconds;
            current = null;
            Sample();
        }
        private void ResetAction()
        {
            actionUntil = 0;
            currentMask = -1;
            current = null;
        }
        private void LateUpdate()
        {
            if (IsWired)
            {
                Sample();
            }
        }
        private void Sample()
        {
            int mask = BodyMask;
            PlayerVisualAction action = !state.IsAlive ? PlayerVisualAction.Idle
            : Time.timeAsDouble < actionUntil ? requested
            : !motor.IsGrounded ? PlayerVisualAction.Jump
            : Mathf.Abs(body.linearVelocity.x) > 0.1f ? PlayerVisualAction.Move : PlayerVisualAction.Idle;
            if (current == null || currentMask != mask || currentAction != action)
            {
                currentMask = mask;
                currentAction = action;
                sampledSince = Time.timeAsDouble;
                current = animations.Find(mask, action);
                MissingAction = current == null;
                // Preserve the correct body on missing artwork; never borrow another combination or Fire as Bite.
                if (current == null)
                {
                    current = animations.Find(mask, PlayerVisualAction.Idle);
                }
            }
            if (current == null || current.frames == null || current.frames.Length == 0)
            {
                visual.enabled = false;
                return;
            }
            visual.enabled = true;
            float fps = current.clip != null ? current.clip.frameRate : 6f;
            int frame = Mathf.FloorToInt((float)(Time.timeAsDouble - sampledSince) * fps);
            CurrentFrameSequence = frame;
            frame = action == PlayerVisualAction.Move ? frame % current.frames.Length : Mathf.Min(frame, current.frames.Length - 1);
            CurrentFrameIndex = frame;
            visual.sprite = current.frames[frame];
            visual.flipX = facing.FacingSign < 0;
            float breathing = action == PlayerVisualAction.Idle && state.IsAlive ? 1f + 0.01f * Mathf.Sin(Time.time * Mathf.PI * 2f) : 1f;
            Vector3 parentScale = visual.transform.parent.lossyScale;
            visual.transform.localScale = new Vector3(worldScale / Mathf.Abs(parentScale.x), worldScale * breathing / Mathf.Abs(parentScale.y), 1f);
            visual.transform.position = new Vector3(rootCollider.bounds.center.x - current.centerOffset * worldScale * facing.FacingSign,
            rootCollider.bounds.min.y + current.footOffset * worldScale * breathing, transform.position.z);
            visual.color = !state.IsAlive ? new Color(0.5f, 0.5f, 0.5f, 0.7f)
            : Time.timeAsDouble < hurtUntil ? new Color(1f, 0.35f, 0.35f)
            : motor.IsDashing ? new Color(0.5f, 0.9f, 1f) : Color.white;
        }
    }
}
