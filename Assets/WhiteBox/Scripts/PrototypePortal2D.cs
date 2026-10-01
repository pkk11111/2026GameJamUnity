using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Whitebox-only portal for the existing WhiteboxPlayer2D controller.
// Attach to an unscaled empty portal root. No collider, Rigidbody2D, tag,
// Input Action asset, or changes to the movement script are required.
[DisallowMultipleComponent]
public class PrototypePortal2D : MonoBehaviour
{
    [Header("Connections")]
    [Tooltip("Drag Test_Player here. Both portals use the same player.")]
    public WhiteboxPlayer2D player;
    [Tooltip("Drag the OTHER portal's ExitPoint here. This is the player's CENTER position.")]
    public Transform destination;

    [Header("Interaction")]
    [Min(0.1f)] public float interactionRadius = 1.5f;
    [Min(0.05f)] public float interactionCooldown = 0.35f;
    public bool showPrompt = true;

    private static readonly List<PrototypePortal2D> portals = new List<PrototypePortal2D>();
    // Shared across the single test player's portals: one key press, one trip.
    private static int lastTeleportFrame = -1;
    private static float nextInteractionTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        portals.Clear();
        lastTeleportFrame = -1;
        nextInteractionTime = 0f;
    }

    private void OnEnable()
    {
        if (!portals.Contains(this)) portals.Add(this);
    }

    private void OnDisable()
    {
        portals.Remove(this);
    }

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f || !IsReady() || !InteractPressed()) return;
        if (lastTeleportFrame == Time.frameCount || Time.time < nextInteractionTime) return;
        if (FindNearestPortal(player) != this) return;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body == null) return;

        // Claim the input BEFORE moving; the destination portal may update later
        // in this same frame, with exactly the same E key-down event.
        lastTeleportFrame = Time.frameCount;
        nextInteractionTime = Time.time + Mathf.Max(0.05f, interactionCooldown);

        // Compatibility with the supplied WhiteboxPlayer2D:
        // its OnDisable clears dash, queued jump, coyote time and input state.
        // Re-enabling does not run Awake again, so R keeps its original start point.
        // A production controller should expose a dedicated TeleportTo method.
        player.enabled = false;
        body.position = new Vector2(destination.position.x, destination.position.y);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = player.gravityScale;
        body.WakeUp();
        player.enabled = true;
    }

    private bool IsReady()
    {
        return isActiveAndEnabled && player != null && player.isActiveAndEnabled && destination != null;
    }

    private static PrototypePortal2D FindNearestPortal(WhiteboxPlayer2D targetPlayer)
    {
        if (targetPlayer == null) return null;
        Rigidbody2D body = targetPlayer.GetComponent<Rigidbody2D>();
        if (body == null) return null;

        PrototypePortal2D nearest = null;
        float bestDistance = float.PositiveInfinity;
        foreach (PrototypePortal2D portal in portals)
        {
            if (portal == null || portal.player != targetPlayer || !portal.IsReady()) continue;
            Vector2 offset = (Vector2)portal.transform.position - body.position;
            float distance = offset.sqrMagnitude;
            float radius = Mathf.Max(0.1f, portal.interactionRadius);
            if (distance > radius * radius) continue;
            if (distance < bestDistance ||
                (Mathf.Approximately(distance, bestDistance) && nearest != null &&
                 portal.GetInstanceID() < nearest.GetInstanceID()))
            {
                bestDistance = distance;
                nearest = portal;
            }
        }
        return nearest;
    }

    private static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.E);
#else
        return false;
#endif
    }

    private void OnGUI()
    {
        if (!showPrompt || !IsReady() || Time.timeScale <= 0f) return;
        if (Time.time < nextInteractionTime || FindNearestPortal(player) != this) return;
        GUI.Box(new Rect(Screen.width * 0.5f - 120f, Screen.height - 70f, 240f, 40f), "E - Use portal");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.75f, 0.3f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, interactionRadius));
        if (destination == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireCube(destination.position, new Vector3(0.8f, 1.2f, 0.1f));
    }
}
