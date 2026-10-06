// 剑击成功后通过Core可选IWeaponHitReceiver排队轻击退；维护enemy-ai，docs/handoffs/enemy-ai.handoff。
// 遮挡修复：structure-audit；交接docs/handoffs/structure-audit.handoff；不修改现有即时动作和冷却。
// Soap/T08: immediate one-pass settlement, then cooldown. No windup/pending/recovery state.
// Router alone consumes Attack. Damage/permission come from the actual player; presentation never damages.
// Dependencies: Core/Audio.Core/Combat/Player/UnityEngine. Rules/wiring: AGENTS.md / Soap.handoff.
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Audio;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerSwordAttack : MonoBehaviour, IPlayerAttackAction
    {
        [SerializeField, Tooltip("Same actual player component as Router.")]
        private MonoBehaviour combatStateSource;
        [SerializeField, Tooltip("Explicit right-facing reference offset; resolved center mirrors through the unique facing source.")]
        private Transform swordOrigin;
        [SerializeField] private PlayerFacing2D facingSource;
        [SerializeField] private SwordConfig config;
        [SerializeField] private SpriteRenderer slashVisual;
        [SerializeField, Min(0.01f), Tooltip("Presentation only; never delays settlement. Test default 0.12s.")]
        private float flashSeconds = 0.12f;
        [SerializeField] private PlayerActionGate actionGate;
        private IPlayerCombatState combat;
        private double nextAttackTime, hideVisualTime;
        private bool attacking;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly List<RaycastHit2D> obstructionHits = new List<RaycastHit2D>();
        private readonly List<MonoBehaviour> receiverComponents = new List<MonoBehaviour>();
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        public event Action AttackStarted;
        public event Action<IDamageable> HitAccepted;
        public IPlayerCombatState CombatState => combat;
        public PlayerFacing2D Facing => facingSource;
        public Vector3 HitCenter
        {
            get
            {
                Vector3 offset = combatStateSource.transform.InverseTransformPoint(swordOrigin.position);
                offset.x *= facingSource.FacingSign;
                return combatStateSource.transform.TransformPoint(offset);
            }
        }
        public bool IsWired => combatStateSource != null && combat != null && swordOrigin != null && config != null && config.IsValid
            && facingSource != null && facingSource.IsWired;
        public float CooldownRemaining => Mathf.Max(0f, (float)(nextAttackTime - Time.timeAsDouble));
        private bool CanAttack => IsWired && isActiveAndEnabled && combatStateSource.isActiveAndEnabled
            && facingSource.isActiveAndEnabled && combat.CanUseSword;
        private void OnEnable()
        {
            combat = combatStateSource as IPlayerCombatState;
            HideVisual();
            if (!IsWired) Debug.LogWarning("[T08 Sword] Bind actual combat state/origin/valid config.", this);
        }
        private void OnDisable() { HideVisual(); }
        private void Update()
        {
            if (CanAttack) PlaceVisual();
            if (!CanAttack || Time.timeAsDouble >= hideVisualTime) HideVisual();
        }
        private void PlaceVisual()
        {
            if (slashVisual == null) return;
            slashVisual.transform.position = HitCenter;
            slashVisual.flipX = facingSource.FacingSign < 0;
        }
        private void HideVisual() { if (slashVisual != null) slashVisual.enabled = false; }
        public bool TryAttack()
        {
            if (!CanAttack || attacking || Time.timeAsDouble < nextAttackTime) return false;
            if (actionGate != null && !actionGate.TryBegin(this, 0.35f)) { return false; }
            attacking = true; // Synchronous reentrancy guard, not an attack lifecycle.
            try
            {
                nextAttackTime = Time.timeAsDouble + config.CooldownSeconds;
                try { AttackStarted?.Invoke(); }
                catch (Exception ex) { Debug.LogWarning("[Sword] Presentation failed: " + ex.Message, this); }
                PlaceVisual();
                hideVisualTime = Time.timeAsDouble + (float.IsNaN(flashSeconds) || float.IsInfinity(flashSeconds)
                    ? 0.12f : Mathf.Max(0.01f, flashSeconds));
                if (slashVisual != null) slashVisual.enabled = true;
                try { GameAudio.Play(AudioCue.PlayerWeaponAttack, combatStateSource.gameObject); }
                catch (Exception ex) { Debug.LogWarning("[T08 Sword] Audio backend failed; attack continues: " + ex.Message, this); }
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = config.TargetLayers,
                    useTriggers = config.IncludeTriggers };
                overlaps.Clear(); damaged.Clear();
                Physics2D.OverlapCircle(HitCenter, config.Range, filter, overlaps);
                foreach (Collider2D collider in overlaps)
                {
                    if (!CanAttack) break;
                    if (collider == null || collider.transform.IsChildOf(combatStateSource.transform)) continue;
                    IDamageable target = DamageableLookup.FindInParents(collider.transform, receiverComponents);
                    if (target == null || ReferenceEquals(target, combatStateSource) || damaged.Contains(target)) continue;
                    if (target is Component component && (component.transform.IsChildOf(combatStateSource.transform)
                        || combatStateSource.transform.IsChildOf(component.transform)
                        || (component.transform.position.x - combatStateSource.transform.position.x) * facingSource.FacingSign < 0f)) continue;
                    if (DamageableLookup.IsOccluded(combatStateSource.transform.position, collider, target,
                        combatStateSource.transform, config.ObstructionLayers, obstructionHits, receiverComponents))
                    {
                        continue;
                    }
                    damaged.Add(target);
                    int amount = combat.SwordDamage;
                    if (amount > 0 && target.TryTakeDamage(new DamageRequest(amount, DamageKind.Enemy, combatStateSource.gameObject)))
                    {
                        // 业务反馈先于音频/表现通知；死亡目标由接收者拒绝，未命中/被墙挡住不调用。
                        if (target is Component hitComponent)
                        {
                            hitComponent.GetComponent<IWeaponHitReceiver>()?.TryApplyWeaponHit(facingSource.FacingSign);
                        }
                        try { HitAccepted?.Invoke(target); }
                        catch (Exception ex) { Debug.LogException(ex, this); }
                    }
                }
                return true;
            }
            finally { attacking = false; }
        }
        private void OnDrawGizmosSelected()
        {
            if (swordOrigin != null && config != null && facingSource != null && combatStateSource != null)
                Gizmos.DrawWireSphere(HitCenter, config.Range);
        }
    }
}
