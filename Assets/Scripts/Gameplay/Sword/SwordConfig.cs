// Soap/T08: Inspector test defaults, not final balance. Dependencies: UnityEngine.
// Wiring and validation: docs/handoffs/Soap.handoff; rules: AGENTS.md.
using UnityEngine;

namespace Regrowth.Gameplay
{
    [CreateAssetMenu(menuName = "pawgatory/Sword Config")]
    public sealed class SwordConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f), Tooltip("World radius from the explicit origin. Test default; final balance not frozen.")]
        private float attackRange = 1.8f;
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.6f;
        [SerializeField] private LayerMask targetLayers = 1;
        [SerializeField] private bool includeTriggers = true;

        public float Range => attackRange;
        public float CooldownSeconds => cooldownSeconds;
        public LayerMask TargetLayers => targetLayers;
        public bool IncludeTriggers => includeTriggers;
        public bool IsValid => Positive(attackRange) && NonNegative(cooldownSeconds) && targetLayers.value != 0;
        private static bool NonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static bool Positive(float value) => NonNegative(value) && value > 0f;
    }
}
