// Soap: explicit presentation-only mirror. Never flips the player root or writes FacingSign.
// The child visualRoot may contain sprites/nose art, but no physics/ground/attack anchors.
// Dependencies: UnityEngine / PlayerFacing2D in this neutral assembly. See Soap.handoff.
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayerFacingPresentation : MonoBehaviour
    {
        [SerializeField] private PlayerFacing2D facingSource;
        [SerializeField] private Transform visualRoot;
        private Vector3 rightScale;
        public bool IsWired => facingSource != null && visualRoot != null && visualRoot != facingSource.transform
            && visualRoot.IsChildOf(facingSource.transform);
        private void OnEnable()
        {
            if (!IsWired) { Debug.LogWarning("[Facing Visual] Bind an explicit presentation child, never the player root.", this); return; }
            rightScale = visualRoot.localScale;
            rightScale.x = Mathf.Abs(rightScale.x);
            Apply();
        }
        private void Update() { if (IsWired) Apply(); }
        private void Apply()
        {
            visualRoot.localScale = new Vector3(rightScale.x * facingSource.FacingSign, rightScale.y, rightScale.z);
        }
    }
}
