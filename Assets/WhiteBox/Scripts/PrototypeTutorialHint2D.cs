using System.Collections.Generic;
using UnityEngine;

// Proximity-only whitebox hint. No collider or input package required.
[DisallowMultipleComponent]
public class PrototypeTutorialHint2D : MonoBehaviour
{
    public Transform player;
    [Min(0.1f)] public float detectionRadius = 3f;
    [TextArea(2, 6)] public string message = "按 Space 跳跃";
    [Header("Panel")]
    [Min(100f)] public float panelWidth = 600f;
    [Min(0f)] public float bottomMargin = 150f;
    [Range(12, 48)] public int fontSize = 24;
    public Font font;
    public Color textColor = Color.white;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.8f);

    private static readonly List<PrototypeTutorialHint2D> hints = new List<PrototypeTutorialHint2D>();
    private Texture2D background;

    private void OnEnable() { if (!hints.Contains(this)) hints.Add(this); }
    private void OnDisable() { hints.Remove(this); }

    private PrototypeTutorialHint2D Nearest()
    {
        if (player == null) return null;
        PrototypeTutorialHint2D nearest = null;
        float best = float.PositiveInfinity;
        foreach (PrototypeTutorialHint2D hint in hints)
        {
            if (hint == null || !hint.isActiveAndEnabled || hint.player != player ||
                string.IsNullOrWhiteSpace(hint.message)) continue;
            float distance = ((Vector2)hint.transform.position - (Vector2)player.position).sqrMagnitude;
            float radius = Mathf.Max(0.1f, hint.detectionRadius);
            if (distance > radius * radius) continue;
            if (distance < best || (Mathf.Approximately(distance, best) && nearest != null &&
                hint.GetInstanceID() < nearest.GetInstanceID()))
            {
                best = distance;
                nearest = hint;
            }
        }
        return nearest;
    }

    private void OnGUI()
    {
        if (Time.timeScale <= 0f || Nearest() != this) return;
        if (background == null)
        {
            background = new Texture2D(1, 1);
            background.hideFlags = HideFlags.HideAndDontSave;
            background.SetPixel(0, 0, Color.white);
            background.Apply();
        }
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        if (font != null) style.font = font;
        style.normal.textColor = textColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.wordWrap = true;
        const float padding = 16f;
        float width = Mathf.Min(Mathf.Max(100f, panelWidth), Mathf.Max(1f, Screen.width - 32f));
        float innerWidth = Mathf.Max(1f, width - padding * 2f);
        float height = Mathf.Max(fontSize + padding * 2f,
            style.CalcHeight(new GUIContent(message), innerWidth) + padding * 2f);
        float y = Mathf.Max(8f, Screen.height - bottomMargin - height);
        Rect panel = new Rect((Screen.width - width) * 0.5f, y, width, height);
        Color previous = GUI.color;
        GUI.color = backgroundColor;
        GUI.DrawTexture(panel, background);
        GUI.color = previous;
        GUI.Label(new Rect(panel.x + padding, panel.y + padding, innerWidth,
            panel.height - padding * 2f), message, style);
    }

    private void OnDestroy() { if (background != null) Destroy(background); }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Vector3 previous = center + Vector3.right * Mathf.Max(0.1f, detectionRadius);
        for (int i = 1; i <= 64; i++)
        {
            float angle = i * Mathf.PI * 2f / 64f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) *
                Mathf.Max(0.1f, detectionRadius);
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
