// 职责：从同一组件读取生命/构筑/姿态，事件刷新三槽HUD；绝不调用状态写口。
// 模块/维护：controller适配Soap / T02-V5；直接依赖：Core、TMP、HudSlotView；真实状态归PlayerState。
// 接线：stateSource须同时实现三个只读接口；启用先读快照并订阅，停用/换源退订。
// 交接：docs/handoffs/controller.handoff；规范：根AGENTS.md。
using System;
using Regrowth.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerHud : MonoBehaviour
    {
        [Serializable]
        private sealed class ItemVisual
        {
            public LoadoutItemId item;
            public string label;
            public Sprite icon;
        }

        [Header("唯一状态源与展示引用")]
        [SerializeField, Tooltip("必填：同一个PlayerState提供IHealth/ILoadoutState/IFormState；更换请用TryBind。")]
        private MonoBehaviour stateSource;
        [SerializeField, Tooltip("必填：当前/上限生命文字。")]
        private TMP_Text healthText;
        [SerializeField, Tooltip("必填：姿态文字。")]
        private TMP_Text formText;
        [SerializeField, Tooltip("必填：至少三个有序槽位；布局和外观在Prefab编辑。")]
        private HudSlotView[] slots;
        [SerializeField, Tooltip("可选：Filled Image；颜色/Sprite/方向在Inspector，不依赖占位图。")]
        private Image healthFill;
        [Header("可替换显示配置（刷新时读取）")]
        [SerializeField, Tooltip("每个构筑身份的标题和Sprite映射。")]
        private ItemVisual[] itemVisuals;
        [SerializeField] private string healthFormat = "HP {0} / {1}";
        [SerializeField] private string quadrupedLabel = "QUADRUPED";
        [SerializeField] private string uprightLabel = "UPRIGHT";
        [SerializeField] private string emptyLabel = "EMPTY";
        [SerializeField] private string unavailableLabel = "HELD / UNAVAILABLE";
        [SerializeField] private string occupiedLabel = "HELD";

        private IHealth health;
        private ILoadoutState loadout;
        private IFormState form;
        private IPlayerBodyState body;
        private bool subscribed;

        private void OnEnable()
        {
            if (!TryBind(stateSource))
            {
                Debug.LogError("[T02 PlayerHud] 检查同一状态源、HP/Form文字和三个槽位引用。", this);
            }
        }

        private void OnDisable()
        {
            Detach();
        }

        /// <summary>主线程换源；拒绝非法源并保留旧绑定。成功先退订旧源，再读新快照；禁用时只保存引用。</summary>
        public bool TryBind(MonoBehaviour source)
        {
            if (source == null || !(source is IHealth nextHealth) || !(source is ILoadoutState nextLoadout)
                || !(source is IFormState nextForm) || !ValidView())
            {
                return false;
            }
            Detach();
            stateSource = source;
            health = nextHealth;
            loadout = nextLoadout;
            form = nextForm;
            body = source as IPlayerBodyState;
            if (isActiveAndEnabled)
            {
                health.HealthChanged += Refresh;
                loadout.LoadoutChanged += Refresh;
                form.FormChanged += OnFormChanged;
                if (body != null)
                {
                    body.BodyChanged += Refresh;
                }
                subscribed = true;
                Refresh();
            }
            return true;
        }

        private bool ValidView()
        {
            if (healthText == null || formText == null || slots == null || slots.Length < LoadoutRules.Capacity)
            {
                return false;
            }
            foreach (HudSlotView slot in slots)
            {
                if (slot == null || !slot.IsConfigured)
                {
                    return false;
                }
            }
            return true;
        }

        private void Detach()
        {
            if (subscribed)
            {
                health.HealthChanged -= Refresh;
                loadout.LoadoutChanged -= Refresh;
                form.FormChanged -= OnFormChanged;
                if (body != null)
                {
                    body.BodyChanged -= Refresh;
                }
                subscribed = false;
            }
        }

        private void OnFormChanged(PlayerForm value) => Refresh();

        private void Refresh()
        {
            if (!subscribed || stateSource == null)
            {
                return;
            }
            healthText.gameObject.SetActive(body == null || body.HasBodyCore);
            if (healthFill != null)
            {
                healthFill.gameObject.SetActive(body == null || body.HasBodyCore);
            }
            healthText.text = string.Format(healthFormat, health.CurrentHealth, health.MaximumHealth);
            formText.text = form.CurrentForm == PlayerForm.Upright ? uprightLabel : quadrupedLabel;
            if (healthFill != null)
            {
                healthFill.fillAmount = health.MaximumHealth > 0 ? (float)health.CurrentHealth / health.MaximumHealth : 0f;
            }
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].gameObject.SetActive(i < loadout.Capacity);
                bool occupied = i < loadout.Items.Count;
                LoadoutItemId item = occupied ? loadout.Items[i] : default;
                ItemVisual visual = FindVisual(item);
                bool unavailable = occupied && (item == LoadoutItemId.FlameBreath);
                slots[i].Display(occupied ? (visual != null ? visual.label : item.ToString()) : emptyLabel,
                    visual?.icon, occupied, unavailable, occupied ? (unavailable ? unavailableLabel : occupiedLabel) : string.Empty);
            }
        }

        private ItemVisual FindVisual(LoadoutItemId item)
        {
            if (itemVisuals != null)
            {
                foreach (ItemVisual visual in itemVisuals)
                {
                    if (visual != null && visual.item == item)
                    {
                        return visual;
                    }
                }
            }
            return null;
        }
    }
}
