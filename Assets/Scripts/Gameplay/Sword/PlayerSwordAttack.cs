// Soap/T08: immediate one-pass settlement, then cooldown. No windup/pending/recovery state.
// Router alone consumes Attack. Damage/permission come from the actual player; presentation never damages.
// Dependencies: Core/Audio.Core/Combat/UnityEngine. Rules/wiring: AGENTS.md / Soap.handoff.
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
        [SerializeField, Tooltip("Explicit hit center; test mount faces right, matching Bite.")]
        private Transform swordOrigin;
        [SerializeField] private SwordConfig config;
        [SerializeField] private SpriteRenderer slashVisual;
        [SerializeField, Min(0.01f), Tooltip("Presentation only; never delays settlement. Test default 0.12s.")]
        private float flashSeconds = 0.12f;
        private IPlayerCombatState combat;
        private double nextAttackTime, hideVisualTime;
        private bool attacking;
        private readonly List<Collider2D> overlaps = new List<Collider2D>();
        private readonly HashSet<IDamageable> damaged = new HashSet<IDamageable>();
        public IPlayerCombatState CombatState => combat;
        public bool IsWired => combatStateSource != null && combat != null && swordOrigin != null && config != null && config.IsValid;
        public float CooldownRemaining => Mathf.Max(0f, (float)(nextAttackTime - Time.timeAsDouble));
        private bool CanAttack => IsWired && isActiveAndEnabled && combatStateSource.isActiveAndEnabled && combat.CanUseSword;
        private void OnEnable()
        {
            combat = combatStateSource as IPlayerCombatState;
            HideVisual();
            if (!IsWired) Debug.LogWarning("[T08 Sword] Bind actual combat state/origin/valid config.", this);
        }
        private void OnDisable() { HideVisual(); }
        private void Update()
        {
            if (!CanAttack || Time.timeAsDouble >= hideVisualTime) HideVisual();
        }
        private void HideVisual() { if (slashVisual != null) slashVisual.enabled = false; }
        public bool TryAttack()
        {
            if (!CanAttack || attacking || Time.timeAsDouble < nextAttackTime) return false;
            attacking = true; // Synchronous reentrancy guard, not an attack lifecycle.
            try
            {
                nextAttackTime = Time.timeAsDouble + config.CooldownSeconds;
                hideVisualTime = Time.timeAsDouble + (float.IsNaN(flashSeconds) || float.IsInfinity(flashSeconds)
                    ? 0.12f : Mathf.Max(0.01f, flashSeconds));
                if (slashVisual != null) slashVisual.enabled = true;
                try { GameAudio.Play(AudioCue.PlayerWeaponAttack, combatStateSource.gameObject); }
                catch (Exception ex) { Debug.LogWarning("[T08 Sword] Audio backend failed; attack continues: " + ex.Message, this); }
                var filter = new ContactFilter2D { useLayerMask = true, layerMask = config.TargetLayers,
                    useTriggers = config.IncludeTriggers };
                overlaps.Clear(); damaged.Clear();
                Physics2D.OverlapCircle(swordOrigin.position, config.Range, filter, overlaps);
                foreach (Collider2D collider in overlaps)
                {
                    if (!CanAttack) break;
                    if (collider == null || collider.transform.IsChildOf(combatStateSource.transform)) continue;
                    IDamageable target = Receiver(collider.transform);
                    if (target == null || ReferenceEquals(target, combatStateSource) || !damaged.Add(target)) continue;
                    if (target is Component component && (component.transform.IsChildOf(combatStateSource.transform)
                        || combatStateSource.transform.IsChildOf(component.transform))) continue;
                    int amount = combat.SwordDamage;
                    if (amount > 0) target.TryTakeDamage(new DamageRequest(amount, DamageKind.Enemy, combatStateSource.gameObject));
                }
                return true;
            }
            finally { attacking = false; }
        }
        private static IDamageable Receiver(Transform node)
        {
            for (Transform current = node; current != null; current = current.parent)
                foreach (MonoBehaviour behaviour in current.GetComponents<MonoBehaviour>())
                    if (behaviour is IDamageable receiver) return receiver;
            return null;
        }
        private void OnDrawGizmosSelected()
        {
            if (swordOrigin != null && config != null) Gizmos.DrawWireSphere(swordOrigin.position, config.Range);
        }
    }
}
