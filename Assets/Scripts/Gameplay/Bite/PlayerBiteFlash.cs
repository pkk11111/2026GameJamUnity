// Soap/T07: accepted-Bite presentation only. No physics, input, HP or animation damage callback.
// Bind the real Bite action and explicit cyan jaw renderer. See Soap.handoff / AGENTS.md.
using UnityEngine;

namespace Regrowth.Gameplay
{
    public sealed class PlayerBiteFlash : MonoBehaviour
    {
        [SerializeField] private PlayerBiteAttack source;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField, Min(0.01f), Tooltip("Presentation duration only; test default, final art not frozen.")]
        private float flashSeconds = 0.12f;
        private double hideTime;
        private void OnEnable() { Hide(); if (source != null) source.AttackStarted += Show; }
        private void OnDisable() { if (source != null) source.AttackStarted -= Show; Hide(); }
        private void Show()
        {
            PlaceVisual();
            hideTime = Time.timeAsDouble + (float.IsNaN(flashSeconds) || float.IsInfinity(flashSeconds)
                ? 0.12f : Mathf.Max(0.01f, flashSeconds));
            if (visual != null) visual.enabled = true;
        }
        private void Update()
        {
            if (source != null && source.IsWired) PlaceVisual();
            if (source == null || !source.isActiveAndEnabled || source.CombatState == null
                || !source.CombatState.CanBite || Time.timeAsDouble >= hideTime) Hide();
        }
        private void PlaceVisual()
        {
            if (visual == null) return;
            visual.transform.position = source.HitCenter;
            visual.flipX = source.Facing.FacingSign < 0;
        }
        private void Hide() { if (visual != null) visual.enabled = false; }
    }
}
