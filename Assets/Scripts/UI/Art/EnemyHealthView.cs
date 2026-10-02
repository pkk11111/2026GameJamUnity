// 职责：敌人头顶HP只读显示，跟随实体顶边；不执行伤害或控制时间。
// 维护：controller/enemy-health-ui；依赖Core/uGUI/TMP，生命由绑定敌人持有。
// 接线：HUD下独立视图绑定本敌人IHealth/Collider、唯一Run及游戏相机。
// 生命周期：启用订阅并读快照，停用退订；参数与视觉引用在Inspector调整。
// 交接：docs/handoffs/controller.handoff；规范：根AGENTS.md。
using Regrowth.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI.Art
{
    [DisallowMultipleComponent]
    public sealed class EnemyHealthView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour healthSource;
        [SerializeField] private MonoBehaviour runSource;
        [SerializeField] private Collider2D targetBounds;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0f), Tooltip("头顶间距，世界单位，实时生效。")]
        private float headOffset = 0.45f;

        private IHealth health;
        private IRunContext run;
        private RectTransform parent;
        private Canvas canvas;

        private void OnEnable()
        {
            health = healthSource as IHealth;
            run = runSource as IRunContext;
            parent = visualRoot ? visualRoot.parent as RectTransform : null;
            canvas = visualRoot ? visualRoot.GetComponentInParent<Canvas>() : null;
            if (health != null)
            {
                health.HealthChanged += RefreshHealth;
            }
            RefreshHealth();
            Hide();
        }

        private void Start()
        {
            if (health == null || run == null || !targetBounds || !worldCamera
                || !visualRoot || !fill || !label || !parent || !canvas)
            {
                Debug.LogError("[Enemy HP] Bind enemy health/collider, run, camera and HUD references.", this);
                enabled = false;
                return;
            }
            // Enemy OnEnable may initialize after this view; read its completed snapshot.
            RefreshHealth();
        }

        private void RefreshHealth()
        {
            if (health == null || !fill || !label)
            {
                return;
            }
            fill.fillAmount = health.MaximumHealth > 0
                ? Mathf.Clamp01((float)health.CurrentHealth / health.MaximumHealth) : 0f;
            label.text = health.CurrentHealth + " / " + health.MaximumHealth;
            if (!health.IsAlive)
            {
                Hide();
            }
        }

        private void LateUpdate()
        {
            if (!healthSource || !healthSource.isActiveAndEnabled || health == null || !health.IsAlive
                || !runSource || run == null || run.Phase == RunPhase.Dead || run.Phase == RunPhase.Won
                || !targetBounds || !worldCamera || !visualRoot || !parent || !canvas)
            {
                Hide();
                return;
            }
            Bounds bounds = targetBounds.bounds;
            Vector3 point = worldCamera.WorldToViewportPoint(
                new Vector3(bounds.center.x, bounds.max.y + headOffset, bounds.center.z));
            if (point.z <= 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f)
            {
                Hide();
                return;
            }
            Vector3 screen = worldCamera.ViewportToScreenPoint(point);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out local))
            {
                visualRoot.anchoredPosition = local;
                visualRoot.gameObject.SetActive(true);
            }
        }

        private void Hide()
        {
            if (visualRoot)
            {
                visualRoot.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.HealthChanged -= RefreshHealth;
            }
            Hide();
        }
    }
}
