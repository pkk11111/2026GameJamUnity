using UnityEngine;

// Keep this root at scale (1,1,1). Resize the detection using ellipseSize.
[DisallowMultipleComponent]
[RequireComponent(typeof(PolygonCollider2D))]
public class PrototypeSpike2D : MonoBehaviour
{
    [Header("Detection ellipse (local units)")]
    public Vector2 ellipseSize = new Vector2(3f, 1f);
    public Vector2 ellipseOffset = Vector2.zero;
    [Header("Knockback")]
    [Min(0.1f)] public float knockbackSpeed = 10f;
    [Min(0.02f)] public float controlLockTime = 0.25f;
    [Min(0f)] public float protectionTime = 0.6f;
    [Tooltip("For floor spikes, give side contacts some upward motion.")]
    [Range(0f, 1f)] public float minimumUpwardDirection = 0.35f;

    private const int Segments = 64;
    private PolygonCollider2D detection;

    private void Reset() { Rebuild(); }
    private void Awake() { Rebuild(); }
    private void OnValidate() { Rebuild(); }

    private void Rebuild()
    {
        ellipseSize.x = Mathf.Max(0.05f, ellipseSize.x);
        ellipseSize.y = Mathf.Max(0.05f, ellipseSize.y);
        detection = GetComponent<PolygonCollider2D>();
        if (detection == null) return;
        detection.isTrigger = true;
        detection.offset = ellipseOffset;
        detection.pathCount = 1;
        Vector2[] points = new Vector2[Segments];
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            points[i] = new Vector2(Mathf.Cos(angle) * ellipseSize.x * 0.5f,
                Mathf.Sin(angle) * ellipseSize.y * 0.5f);
        }
        detection.SetPath(0, points);
    }

    private void OnTriggerEnter2D(Collider2D other) { Hit(other); }
    private void OnTriggerStay2D(Collider2D other) { Hit(other); }

    private void Hit(Collider2D other)
    {
        WhiteboxPlayer2D player = other.GetComponentInParent<WhiteboxPlayer2D>();
        if (player == null || other.isTrigger) return;
        // The normal of an ellipse is proportional to (x/a^2, y/b^2).
        // Use the boundary point on the ray toward the player's collider center.
        Vector2 delta = (Vector2)transform.InverseTransformPoint(other.bounds.center) - ellipseOffset;
        float a = ellipseSize.x * 0.5f;
        float b = ellipseSize.y * 0.5f;
        Vector2 normal = new Vector2(delta.x / (a * a), delta.y / (b * b));
        if (normal.sqrMagnitude < 0.000001f) normal = Vector2.up;
        Vector2 direction = ((Vector2)transform.TransformDirection(normal.normalized)).normalized;
        // Optional floor-spike assist; zero keeps the ellipse normal unchanged.
        if (minimumUpwardDirection > 0f && direction.y < minimumUpwardDirection)
        {
            float horizontal = Mathf.Sqrt(1f - minimumUpwardDirection * minimumUpwardDirection);
            direction = new Vector2(Mathf.Sign(direction.x) * horizontal, minimumUpwardDirection);
        }
        player.TrySpikeKnockback(direction * knockbackSpeed, controlLockTime, protectionTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 previous = transform.TransformPoint((Vector3)(ellipseOffset + new Vector2(ellipseSize.x * 0.5f, 0f)));
        for (int i = 1; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector2 point = ellipseOffset + new Vector2(Mathf.Cos(angle) * ellipseSize.x * 0.5f,
                Mathf.Sin(angle) * ellipseSize.y * 0.5f);
            Vector3 next = transform.TransformPoint((Vector3)point);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
