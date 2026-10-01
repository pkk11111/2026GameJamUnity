using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Nearby E interaction; each unique target door toggles its OWN state.
[DisallowMultipleComponent]
public class PrototypeToggleButton2D : MonoBehaviour
{
    [Header("Connections")]
    public WhiteboxPlayer2D player;
    [Tooltip("Size 1 for one door; Size 2 for two doors. Drag scene Door roots here.")]
    public PrototypeDoor2D[] targetDoors = new PrototypeDoor2D[1];

    [Header("Interaction")]
    [Min(0.1f)] public float interactionRadius = 1.5f;
    [Min(0.05f)] public float interactionCooldown = 0.25f;
    public bool showPrompt = true;

    [Header("Optional Visual Feedback")]
    public SpriteRenderer buttonVisual;
    public Color defaultColor = new Color(0.25f, 0.8f, 1f);
    public Color alternateColor = new Color(0.4f, 1f, 0.3f);

    private static readonly List<PrototypeToggleButton2D> buttons = new List<PrototypeToggleButton2D>();
    private static int lastInteractionFrame = -1;
    private static float nextInteractionTime;
    private readonly HashSet<PrototypeDoor2D> uniqueDoors = new HashSet<PrototypeDoor2D>();
    private bool alternate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        buttons.Clear();
        lastInteractionFrame = -1;
        nextInteractionTime = 0f;
    }

    private void Awake()
    {
        if (buttonVisual == null) buttonVisual = GetComponentInChildren<SpriteRenderer>(true);
        if (buttonVisual != null) buttonVisual.color = defaultColor;
    }

    private void OnEnable()
    {
        if (!buttons.Contains(this)) buttons.Add(this);
    }

    private void OnDisable()
    {
        buttons.Remove(this);
    }

    private void LateUpdate()
    {
        if (Time.timeScale <= 0f || !Ready() || !InteractPressed()) return;
        if (Time.frameCount == lastInteractionFrame || Time.time < nextInteractionTime) return;
        if (FindNearest(player) != this) return;

        lastInteractionFrame = Time.frameCount;
        nextInteractionTime = Time.time + Mathf.Max(0.05f, interactionCooldown);
        uniqueDoors.Clear();
        foreach (PrototypeDoor2D door in targetDoors)
        {
            // Duplicate references cannot toggle the same door twice.
            if (door != null && uniqueDoors.Add(door)) door.Toggle();
        }
        alternate = !alternate;
        if (buttonVisual != null) buttonVisual.color = alternate ? alternateColor : defaultColor;
    }

    private bool Ready()
    {
        if (!isActiveAndEnabled || player == null || !player.isActiveAndEnabled || targetDoors == null)
            return false;
        foreach (PrototypeDoor2D door in targetDoors)
            if (door != null) return true;
        return false;
    }

    private static PrototypeToggleButton2D FindNearest(WhiteboxPlayer2D target)
    {
        if (target == null) return null;
        Rigidbody2D body = target.GetComponent<Rigidbody2D>();
        if (body == null) return null;
        PrototypeToggleButton2D nearest = null;
        float best = float.PositiveInfinity;
        foreach (PrototypeToggleButton2D button in buttons)
        {
            if (button == null || button.player != target || !button.Ready()) continue;
            float distance = ((Vector2)button.transform.position - body.position).sqrMagnitude;
            float radius = Mathf.Max(0.1f, button.interactionRadius);
            if (distance > radius * radius) continue;
            if (distance < best || (Mathf.Approximately(distance, best) && nearest != null &&
                button.GetInstanceID() < nearest.GetInstanceID()))
            {
                best = distance;
                nearest = button;
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
        if (!showPrompt || !Ready() || Time.timeScale <= 0f || Time.time < nextInteractionTime) return;
        if (FindNearest(player) != this) return;
        GUI.Box(new Rect(Screen.width * 0.5f - 130f, Screen.height - 120f, 260f, 40f), "E - Toggle doors");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, interactionRadius));
        if (targetDoors == null) return;
        Gizmos.color = Color.cyan;
        foreach (PrototypeDoor2D door in targetDoors)
            if (door != null) Gizmos.DrawLine(transform.position, door.transform.position);
    }
}
