// Soap: single player-facing source for all directional actions. Reads only the unique IPlayerInput.
// Dependencies: Core / UnityEngine. Default +1; release/drift/disable preserve the last direction.
// Integration and future Dash/Flame consumers: Soap.handoff. Rules: AGENTS.md.
using System;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class PlayerFacing2D : MonoBehaviour
    {
        [SerializeField, Tooltip("Explicit unique IPlayerInput; never polls devices or consumes buttons.")]
        private MonoBehaviour inputSource;
        [SerializeField, Range(0f, 1f), Tooltip("Small horizontal input dead zone; default 0.05. Inside it retain facing.")]
        private float deadZone = 0.05f;
        private IPlayerInput input;
        public int FacingSign { get; private set; } = 1;
        public bool IsFacingRight => FacingSign > 0;
        public Vector2 Direction => new Vector2(FacingSign, 0f);
        public bool IsWired => inputSource != null && input != null && deadZone >= 0f && deadZone <= 1f;
        public event Action<int> FacingChanged;
        private void OnEnable()
        {
            input = inputSource as IPlayerInput;
            if (!IsWired) Debug.LogWarning("[Player Facing] Bind unique input and finite dead zone in [0,1].", this);
        }
        private void Update()
        {
            // Reader (-200) samples first. Facing (-150) updates before presentation and the next physics attack.
            if (!IsWired || !inputSource.isActiveAndEnabled) return;
            float move = input.MoveX;
            if (float.IsNaN(move) || float.IsInfinity(move)) return;
            int next = move > deadZone ? 1 : move < -deadZone ? -1 : FacingSign;
            if (next == FacingSign) return;
            FacingSign = next;
            FacingChanged?.Invoke(next);
        }
    }
}
