using UnityEngine;

// Attach to an empty Door root at scale (1,1,1).
// Visual is a child Square sprite; default collision size is 2 x 5 units.
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class PrototypeDoor2D : MonoBehaviour
{
    [Header("Initial State")]
    [Tooltip("Checked: initially visible and solid. Unchecked: initially hidden and passable.")]
    public bool startClosed = true;

    [Header("Door")]
    public SpriteRenderer visual;
    public Vector2 doorSize = new Vector2(2f, 5f);

    private BoxCollider2D doorCollider;
    private bool initialized;
    private bool closed;

    public bool IsClosed
    {
        get { Initialize(); return closed; }
    }

    private void Reset()
    {
        GetComponent<BoxCollider2D>().size = new Vector2(2f, 5f);
        GetComponent<BoxCollider2D>().isTrigger = false;
        visual = GetComponentInChildren<SpriteRenderer>(true);
    }

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized) return;
        doorCollider = GetComponent<BoxCollider2D>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>(true);
        doorCollider.size = new Vector2(Mathf.Max(0.01f, doorSize.x), Mathf.Max(0.01f, doorSize.y));
        doorCollider.offset = Vector2.zero;
        doorCollider.isTrigger = false;
        initialized = true;
        SetClosed(startClosed);
    }

    public void Toggle()
    {
        Initialize();
        SetClosed(!closed);
    }

    public void SetClosed(bool value)
    {
        Initialize();
        closed = value;
        // Keep the root active so every button can still reference this door.
        doorCollider.enabled = value;
        if (visual != null) visual.enabled = value;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Application.isPlaying && !closed ? Color.gray : Color.cyan;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(doorSize.x, doorSize.y, 0.1f));
    }
}
