// Soap/T09: one horizontal movement writer; owner remains the only health/death authority.
// Explicit owner/body/solid/player/base-speed bindings. No global lookup or player writes.
// Origin Soap/T09; controller/C09 wires the main map and preserves aggro during menus.
// 修复维护：structure-audit；原作者Soap。交接docs/handoffs/structure-audit.handoff；规范AGENTS.md。
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    public enum EnemyAIState { Patrol, Chase, Return }

    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class EnemyBasicAI : MonoBehaviour
    {
        [SerializeField] private EnemyBasic owner;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D solidCollider;
        [SerializeField, Tooltip("Actual player IHealth component; its transform is the target root.")]
        private MonoBehaviour playerSource;
        [SerializeField, Tooltip("Required IPlayerBaseMoveSpeedProvider. No numeric fallback.")]
        private MonoBehaviour baseMoveSpeedProvider;
        [SerializeField, Tooltip("Optional pair of world X boundaries; both or neither.")]
        private Transform leftBoundary;
        [SerializeField] private Transform rightBoundary;
        [SerializeField, Min(0.001f)] private float arrivalTolerance = 0.05f;
        private IHealth player;
        private IPlayerBaseMoveSpeedProvider speedProvider;
        private IRunContext subscribedRun;
        private bool spawnCaptured;
        private bool configCaptured;
        private float chaseFactor, aggroRadius, leftOffset, rightOffset, patrolSpeed, returnSpeed;
        private LayerMask obstacleLayers;
        private Vector2 spawn;
        private int patrolDirection = 1;
        private RigidbodyConstraints2D inactiveConstraints;
        private bool ownsMotion;
        private readonly List<RaycastHit2D> sightHits = new List<RaycastHit2D>();
        private readonly List<RaycastHit2D> movementHits = new List<RaycastHit2D>();
        public EnemyAIState State { get; private set; } = EnemyAIState.Patrol;
        public bool IsConfigured { get; private set; }
        public string ConfigurationError { get; private set; }
        public Vector2 SpawnPosition => spawn;
        public float LeftBound => leftBoundary != null ? leftBoundary.position.x : spawn.x - leftOffset;
        public float RightBound => rightBoundary != null ? rightBoundary.position.x : spawn.x + rightOffset;
        public float AggroRadius => aggroRadius;
        public float ChaseSpeed => IsConfigured ? speedProvider.BaseMoveSpeed * chaseFactor : 0f;
        public int PatrolDirection => patrolDirection;

        private void OnEnable()
        {
            player = playerSource as IHealth;
            speedProvider = baseMoveSpeedProvider as IPlayerBaseMoveSpeedProvider;
            if (!spawnCaptured && body != null && owner != null && body.gameObject == owner.gameObject)
            { spawn = owner.transform.position; spawnCaptured = true; }
            if (!configCaptured && owner != null && owner.Config != null && owner.Config.IsAIValid)
            {
                var settings = owner.Config;
                chaseFactor = settings.ChaseSpeedFactor; aggroRadius = settings.AggroRadius;
                leftOffset = settings.ActivityLeftOffset; rightOffset = settings.ActivityRightOffset;
                patrolSpeed = settings.PatrolSpeed; returnSpeed = settings.ReturnSpeed;
                obstacleLayers = settings.ObstacleLayers; configCaptured = true;
            }
            ConfigurationError = ValidateConfiguration();
            IsConfigured = ConfigurationError == null;
            if (!IsConfigured)
            {
                StopHorizontal();
                Debug.LogError("[T09 AI CONFIG] " + ConfigurationError, this);
                return; // Fail closed; Inspector must correct wiring and re-enable.
            }
            owner.Died += OnOwnerDied;
            player.Died += OnTargetDied;
            BindRun();
            inactiveConstraints = body.constraints;
            ownsMotion = true;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            if (!owner.IsAlive) OnOwnerDied();
        }
        private string ValidateConfiguration()
        {
            if (owner == null || body == null || solidCollider == null || body.gameObject != owner.gameObject
                || solidCollider.attachedRigidbody != body || solidCollider.isTrigger || body.bodyType != RigidbodyType2D.Dynamic)
                return "Bind local EnemyBasic, dynamic Rigidbody2D and its solid Collider2D.";
            if (!configCaptured) return "Invalid EnemyBasicConfig AI parameters.";
            if (!owner.IsInitialized || owner.RunContext == null) return "Owner must be initialized with the actual RunContext.";
            if (playerSource == null || player == null || playerSource.transform.IsChildOf(owner.transform)) return "Missing actual player IHealth target.";
            if (baseMoveSpeedProvider == null || speedProvider == null || !Positive(speedProvider.BaseMoveSpeed))
                return "Missing/invalid Player Base Move Speed Provider; no fallback speed is allowed.";
            if ((leftBoundary == null) != (rightBoundary == null)) return "Bind both activity boundaries or neither.";
            if (!Finite(LeftBound) || !Finite(RightBound) || LeftBound >= RightBound) return "Activity bounds require left < right; values are never swapped.";
            if (spawn.x < LeftBound || spawn.x > RightBound || RightBound - LeftBound <= solidCollider.bounds.size.x)
                return "Activity bounds must contain spawn and fit the solid collider.";
            if (!Positive(arrivalTolerance)) return "Arrival tolerance must be finite and positive.";
            return null;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0f;
        private void BindRun()
        {
            var current = owner != null ? owner.RunContext : null;
            if (ReferenceEquals(current, subscribedRun)) return;
            if (subscribedRun != null) subscribedRun.PhaseChanged -= OnPhaseChanged;
            subscribedRun = current;
            if (subscribedRun != null) subscribedRun.PhaseChanged += OnPhaseChanged;
        }
        private void OnPhaseChanged(RunPhase phase)
        {
            if (phase == RunPhase.Playing) return;
            if ((phase == RunPhase.Dead || phase == RunPhase.Won) && State == EnemyAIState.Chase)
            {
                State = EnemyAIState.Return;
            }
            StopHorizontal(); // Immediate even when timeScale=0 prevents FixedUpdate.
        }
        private void OnTargetDied()
        {
            if (State == EnemyAIState.Chase) State = EnemyAIState.Return;
            StopHorizontal();
        }
        private void OnOwnerDied()
        {
            if (body == null || owner == null || body.gameObject != owner.gameObject) return;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false; // Owner already disabled death colliders; corpse must not fall/move.
        }
        private bool TargetAlive => playerSource != null && playerSource.isActiveAndEnabled && player != null && player.IsAlive;
        private bool TargetWithinBounds => playerSource.transform.position.x >= LeftBound && playerSource.transform.position.x <= RightBound;
        private bool CanAcquire()
        {
            if (!TargetAlive || !TargetWithinBounds) return false;
            Vector2 from = solidCollider.bounds.center, to = playerSource.transform.position;
            if ((to - from).sqrMagnitude > aggroRadius * aggroRadius) return false;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
            sightHits.Clear();
            Physics2D.Raycast(from, (to - from).normalized, filter, sightHits, Vector2.Distance(from, to));
            foreach (var hit in sightHits)
                if (hit.collider != null && !hit.collider.transform.IsChildOf(owner.transform)
                    && !hit.collider.transform.IsChildOf(playerSource.transform)
                    && hit.collider.GetComponentInParent<EnemyBasic>() == null) return false;
            return true;
        }
        private void FixedUpdate()
        {
            if (!IsConfigured) { StopHorizontal(); return; }
            if (owner == null || body == null || solidCollider == null) { IsConfigured = false; StopHorizontal(); return; }
            BindRun();
            if (!owner.IsAlive) { OnOwnerDied(); return; }
            if (!owner.CanAct) { StopHorizontal(); return; }
            if (!TargetAlive) { OnTargetDied(); return; }
            if (baseMoveSpeedProvider == null || !baseMoveSpeedProvider.isActiveAndEnabled || !Positive(speedProvider.BaseMoveSpeed)
                || !Positive(ChaseSpeed) || !Finite(LeftBound) || !Finite(RightBound) || LeftBound >= RightBound)
            {
                IsConfigured = false; ConfigurationError = "AI provider/configuration became invalid; movement stopped.";
                StopHorizontal(); Debug.LogError("[T09 AI CONFIG] " + ConfigurationError, this); return;
            }
            if (State == EnemyAIState.Chase)
            {
                if (!TargetWithinBounds) State = EnemyAIState.Return;
                // Radius and LOS are acquisition-only. No chase-time aggro reset.
            }
            else if (CanAcquire()) State = EnemyAIState.Chase;

            float speed;
            if (State == EnemyAIState.Chase)
                speed = Toward(playerSource.transform.position.x, ChaseSpeed);
            else if (State == EnemyAIState.Return)
            {
                if (Mathf.Abs(body.position.x - spawn.x) <= arrivalTolerance)
                { State = EnemyAIState.Patrol; speed = 0f; }
                else speed = Toward(spawn.x, returnSpeed);
            }
            else
            {
                if (solidCollider.bounds.max.x >= RightBound - arrivalTolerance) patrolDirection = -1;
                else if (solidCollider.bounds.min.x <= LeftBound + arrivalTolerance) patrolDirection = 1;
                speed = patrolDirection * patrolSpeed;
            }
            // Bound the next commanded displacement, including collider extents, without teleporting.
            float dt = Time.fixedDeltaTime;
            speed = Mathf.Clamp(speed, Mathf.Min(0f, (LeftBound - solidCollider.bounds.min.x) / dt),
                Mathf.Max(0f, (RightBound - solidCollider.bounds.max.x) / dt));
            // Cast the real body to its next horizontal position; stop at solids instead of
            // continuously commanding motion into the player/wall. Physics remains authoritative.
            if (Mathf.Abs(speed) > 0f)
            {
                // 保留物理接触裕量；剩余距离小于接触裕量时，物理求解器可能已阻止位移。
                float skin = Mathf.Max(0.01f, Physics2D.defaultContactOffset);
                movementHits.Clear();
                var filter = new ContactFilter2D { useTriggers = false };
                solidCollider.Cast(Vector2.right * Mathf.Sign(speed), filter, movementHits, Mathf.Abs(speed) * dt + skin);
                float distance = Mathf.Abs(speed) * dt;
                bool blocked = false;
                foreach (var hit in movementHits)
                {
                    if (hit.collider != null && !hit.collider.transform.IsChildOf(owner.transform)
                        && Mathf.Abs(hit.normal.x) > 0.5f)
                    {
                        float allowed = Mathf.Max(0f, hit.distance - skin);
                        blocked |= allowed < distance;
                        distance = Mathf.Min(distance, allowed);
                    }
                }
                // Patrol turns away from a wall or another body instead of two guards blocking forever.
                if (blocked && distance <= skin)
                {
                    // 追击/返程等通路恢复后继续，不能持续写入求解器无法实现的微小速度。
                    distance = 0f;
                    if (State == EnemyAIState.Patrol)
                    {
                        patrolDirection = -patrolDirection;
                    }
                }
                speed = Mathf.Sign(speed) * distance / dt;
            }
            body.linearVelocity = new Vector2(speed, body.linearVelocity.y);
        }
        private float Toward(float x, float speed)
        {
            float dx = x - body.position.x;
            return Mathf.Abs(dx) <= arrivalTolerance ? 0f : Mathf.Sign(dx) * Mathf.Min(speed, Mathf.Abs(dx) / Time.fixedDeltaTime);
        }
        private void StopHorizontal()
        {
            if (body != null && owner != null && body.gameObject == owner.gameObject)
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        }
        private void OnDisable()
        {
            if (owner != null) owner.Died -= OnOwnerDied;
            if (player != null) player.Died -= OnTargetDied;
            if (subscribedRun != null) subscribedRun.PhaseChanged -= OnPhaseChanged;
            subscribedRun = null;
            StopHorizontal();
            if (ownsMotion && body != null) body.constraints = inactiveConstraints;
            ownsMotion = false;
            if (State == EnemyAIState.Chase) State = EnemyAIState.Return;
        }
    }
}
