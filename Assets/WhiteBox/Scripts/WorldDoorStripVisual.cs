using Regrowth.Gameplay;
using UnityEngine;

// Presentation only. WorldDoor remains the sole owner of state/collision/audio.
[DisallowMultipleComponent]
public sealed class WorldDoorStripVisual : MonoBehaviour
{
    public enum ShrinkAxis { Height, Width }
    public WorldDoor door;
    [Tooltip("An always-active child containing only the door artwork.")]
    public Transform visual;
    public ShrinkAxis shrinkAxis = ShrinkAxis.Height;
    [Min(0.01f)] public float animationDuration = 0.3f;

    private Vector3 closedScale;
    private SpriteRenderer[] renderers;
    private bool[] originalEnabled;
    private float visibleAmount;
    private bool ready;

    private void Start()
    {
        if (door == null || visual == null || visual == door.transform ||
            !visual.IsChildOf(door.transform) || visual == transform || transform.IsChildOf(visual))
        {
            Debug.LogError("Door visual: assign WorldDoor and a separate artwork child; attach this component to the door root.", this);
            enabled = false;
            return;
        }
        if (!visual.gameObject.activeInHierarchy)
        {
            Debug.LogError("Door visual must stay active. Clear WorldDoor Closed View/Open View when using this visual controller.", this);
            enabled = false;
            return;
        }
        closedScale = visual.localScale;
        renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
        originalEnabled = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originalEnabled[i] = renderers[i].enabled;
        ready = true;
        visibleAmount = door.IsOpen ? 0f : 1f;
        Apply();
    }

    private void Update()
    {
        if (!ready) return;
        float target = door.IsOpen ? 0f : 1f;
        visibleAmount = Mathf.MoveTowards(visibleAmount, target,
            Time.deltaTime / Mathf.Max(0.01f, animationDuration));
        Apply();
    }

    private void Apply()
    {
        Vector3 scale = closedScale;
        if (shrinkAxis == ShrinkAxis.Height) scale.y *= visibleAmount;
        else scale.x *= visibleAmount;
        visual.localScale = scale;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = originalEnabled[i] && visibleAmount > 0f;
    }

    private void OnDisable()
    {
        if (!ready || visual == null) return;
        // Leave a correct static view if the animation component is disabled.
        visibleAmount = door != null && door.IsOpen ? 0f : 1f;
        Apply();
    }

    private void OnDestroy()
    {
        if (!ready || visual == null) return;
        visual.localScale = closedScale;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = originalEnabled[i];
    }
}
