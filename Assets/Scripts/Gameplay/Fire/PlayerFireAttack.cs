// 职责：火焰尾发射缓慢移动的持续伤害团；唯一 Fire 输入消费者，不写刚体或构筑。
// 维护：controller/C07；依赖 Runtime/Player/Core/Unity；规范 AGENTS.md，交接 controller.handoff。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Runtime;
using UnityEngine;
namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-120)]
    public sealed class PlayerFireAttack : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerState state;
        [SerializeField] private RunController run;
        [SerializeField] private PlayerFacing2D facing;
        [SerializeField] private PlayerActionGate actionGate;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private SpriteRenderer fireVisual;
        [SerializeField] private LineRenderer fireTrail;
        [SerializeField, Min(0.1f), Tooltip("起手至消失，游戏秒。")]
        private float duration = 2f;
        [SerializeField, Min(0.02f)] private float tickInterval = 0.2f;
        [SerializeField, Min(0.1f)] private float cooldown = 8f;
        [SerializeField, Min(0.01f), Tooltip("慢速飞行；默认1.5单位/秒，2秒约3单位。")]
        private float speed = 1.5f;
        [SerializeField, Min(0.01f)] private float radius = 0.6f;
        [SerializeField] private LayerMask hitLayers = ~0;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<MonoBehaviour> receiverComponents = new List<MonoBehaviour>();
        private readonly List<RaycastHit2D> walls = new List<RaycastHit2D>();
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        private double nextStart, started, nextTick;
        private int direction, tickCount;
        private Vector2 position;
        public bool IsFiring
        {
            get;
            private set;
        }
        public Vector2 Position => position;
        public int TickCount => tickCount;
        public float CooldownRemaining => Mathf.Max(0f, (float)(nextStart - Time.timeAsDouble));
        public bool IsWired => input != null && state != null && run != null && facing != null
        && actionGate != null && playerCollider != null && fireVisual != null && fireTrail != null
        && Valid(duration) && Valid(tickInterval) && Valid(cooldown) && Valid(speed) && Valid(radius);
        public event Action AttackStarted;
        public event Action<IDamageable> HitAccepted;
        private static bool Valid(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnEnable()
        {
            if (!IsWired)
            {
                Debug.LogWarning("[Fire] Bind input/state/run/facing/gate/collider/visual/trail and finite positive parameters.", this);
            }
            if (state != null)
            {
                state.LoadoutChanged += OnLoadoutChanged;
                state.Died += Cancel;
            }
            if (run != null)
            {
                run.PhaseChanged += OnPhaseChanged;
            }
            SetVisible(false);
        }
        private void OnDisable()
        {
            if (state != null)
            {
                state.LoadoutChanged -= OnLoadoutChanged;
                state.Died -= Cancel;
            }
            if (run != null)
            {
                run.PhaseChanged -= OnPhaseChanged;
            }
            Cancel();
        }
        private void OnLoadoutChanged()
        {
            if (!state.Contains(LoadoutItemId.FlameTail))
            {
                Cancel();
            }
        }
        private void OnPhaseChanged(RunPhase phase)
        {
            if (phase == RunPhase.Dead || phase == RunPhase.Won)
            {
                Cancel();
            }
        }
        /// <summary>唯一动作起点，拒绝无尾/死亡/暂停/冲刺或普攻中/冷却。不重置既有冷却。</summary>
        public bool TryAttack()
        {
            if (!isActiveAndEnabled || !IsWired || !facing.IsWired || !state.CanUseFire
            || IsFiring || Time.timeAsDouble < nextStart || actionGate.IsBusy)
            {
                return false;
            }
            // A gate can expire between the last Update and this physics tick. Refresh BEFORE locking it.
            direction = facing.RefreshFromInput();
            if (!actionGate.TryBegin(this, duration))
            {
                return false;
            }
            var bounds = playerCollider.bounds;
            position = new Vector2(bounds.center.x + direction * (bounds.extents.x + radius * 0.4f), bounds.center.y + 0.15f);
            started = Time.timeAsDouble;
            nextStart = started + cooldown;
            nextTick = started;
            tickCount = 0;
            IsFiring = true;
            SetVisible(true);
            try
            {
                AttackStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Fire] Presentation failed: " + ex.Message, this);
            }
            // Spawn path is checked too: standing against a closed door cannot spawn damage behind it.
            if (Blocked(bounds.center, position))
            {
                Cancel();
                return true;
            }
            TickDamage();
            return true;
        }
        private void FixedUpdate()
        {
            if (input != null && input.TryConsumeFire())
            {
                TryAttack();
            }
            if (!IsFiring)
            {
                return;
            }
            if (!IsWired || !state.Contains(LoadoutItemId.FlameTail) || !state.IsAlive)
            {
                Cancel();
                return;
            }
            if (!run.IsGameplayActive)
            {
                return;
            }
            if (Time.timeAsDouble - started >= duration - 0.00001)
            {
                Cancel();
                return;
            }
            Vector2 next = position + Vector2.right * (direction * speed * Time.fixedDeltaTime);
            if (Blocked(position, next))
            {
                Cancel();
                return;
            }
            position = next;
            if (Time.timeAsDouble + 0.00001 >= nextTick)
            {
                TickDamage();
            }
        }
        private void TickDamage()
        {
            tickCount++;
            // Long frames skip past slots, never burst historical damage at the current position.
            nextTick = started + (Math.Floor((Time.timeAsDouble - started + 0.00001) / tickInterval) + 1) * tickInterval;
            overlaps.Clear();
            damaged.Clear();
            var filter = new ContactFilter2D
            {
                useLayerMask = true, layerMask = hitLayers, useTriggers = true
            };
            Physics2D.OverlapCircle(position, radius, filter, overlaps);
            foreach (Collider2D hit in overlaps)
            {
                if (!IsFiring || !state.CanUseFire)
                {
                    break;
                }
                if (hit == null || hit.transform.IsChildOf(state.transform))
                {
                    continue;
                }
                IDamageable target = DamageableLookup.FindInParents(hit.transform, receiverComponents);
                if (target == null || ReferenceEquals(target, state) || damaged.Contains(target))
                {
                    continue;
                }
                Vector2 closest = hit.ClosestPoint(position);
                if (Occluded(position, closest))
                {
                    continue;
                }
                damaged.Add(target);
                if (target.TryTakeDamage(new DamageRequest(state.FireDamage, DamageKind.Enemy, state.gameObject)))
                {
                    try { HitAccepted?.Invoke(target); }
                    catch (Exception ex) { Debug.LogException(ex, this); }
                }
            }
        }
        private bool Occluded(Vector2 from, Vector2 to)
        {
            walls.Clear();
            var filter = new ContactFilter2D
            {
                useLayerMask = true, layerMask = hitLayers, useTriggers = false
            };
            Physics2D.Linecast(from, to, filter, walls);
            foreach (var hit in walls)
            {
                if (IsWall(hit.collider))
                {
                    return true;
                }
            }
            return false;
        }
        private bool Blocked(Vector2 from, Vector2 to)
        {
            walls.Clear();
            var filter = new ContactFilter2D
            {
                useLayerMask = true, layerMask = hitLayers, useTriggers = false
            };
            Physics2D.CircleCast(from, radius * 0.35f, (to - from).normalized, filter, walls, Vector2.Distance(from, to));
            foreach (var hit in walls)
            {
                if (IsWall(hit.collider))
                {
                    return true;
                }
            }
            return false;
        }
        private bool IsWall(Collider2D hit) => hit != null && !hit.isTrigger
        && !hit.transform.IsChildOf(state.transform) && DamageableLookup.FindInParents(hit.transform, receiverComponents) == null;
        private void Update()
        {
            if (!IsFiring || fireVisual == null)
            {
                return;
            }
            fireVisual.transform.position = position;
            fireVisual.flipX = direction < 0;
            float pulse = 1f + 0.08f * Mathf.Sin((float)(Time.timeAsDouble - started) * 30f);
            Vector3 parentScale = fireVisual.transform.parent.lossyScale;
            fireVisual.transform.localScale = new Vector3(radius * 2f * pulse / Mathf.Abs(parentScale.x), radius * 2f * pulse / Mathf.Abs(parentScale.y), 1f);
            fireTrail.SetPosition(0, position - Vector2.right * direction * radius * 1.2f);
            fireTrail.SetPosition(1, position);
        }
        private void SetVisible(bool value)
        {
            if (fireVisual != null)
            {
                fireVisual.enabled = value;
            }
            if (fireTrail != null)
            {
                fireTrail.enabled = value;
            }
        }
        public void Cancel()
        {
            IsFiring = false;
            if (actionGate != null)
            {
                actionGate.Release(this);
            }
            SetVisible(false);
        }
    }
}
