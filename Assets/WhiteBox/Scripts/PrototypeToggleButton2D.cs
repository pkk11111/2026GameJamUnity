// 职责：白板一个开关反复切换多个门，保留谜题联动；不是 T03 一次性开关。
// 模块/维护：controller / Level 适配；依赖：IInteractable、白板玩家/门、Audio Core。
// 接线：InteractionTarget 绑定本组件；唯一 PlayerInteractor 消费 E，不再各自抢同一按键。
// 交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。引用/半径/冷却实时读取。
using System.Collections.Generic;
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    [DisallowMultipleComponent]
    public sealed class PrototypeToggleButton2D : MonoBehaviour, IInteractable
    {
        [Header("连接")]
        [SerializeField, Tooltip("必填，唯一白板玩家。")]
        private WhiteboxPlayer2D player;
        [SerializeField, Tooltip("必填，至少一个门；重复引用只切换一次。")]
        private PrototypeDoor2D[] targetDoors = new PrototypeDoor2D[1];
        [Header("交互")]
        [SerializeField, Tooltip("同场景唯一且运行中不修改。")]
        private string interactionId;
        [SerializeField, Min(0.1f), Tooltip("玩家中心到开关的交互半径，单位。")]
        private float interactionRadius = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("所有白板按钮共用的冷却，游戏秒。")]
        private float interactionCooldown = 0.25f;
        [SerializeField, Tooltip("是否显示统一 uGUI 提示。")]
        private bool showPrompt = true;
        [SerializeField, Tooltip("白板交互提示文案。")]
        private string prompt = "E - Toggle doors";
        [Header("可选表现")]
        [SerializeField, Tooltip("可选；显式绑定按钮 Sprite，不搜索其他物体。")]
        private SpriteRenderer buttonVisual;
        [SerializeField, Tooltip("初始/偶数次切换颜色，下次状态变化生效。")]
        private Color defaultColor = new Color(0.25f, 0.8f, 1f);
        [SerializeField, Tooltip("奇数次切换颜色，下次状态变化生效。")]
        private Color alternateColor = new Color(0.4f, 1f, 0.3f);
        private static int lastInteractionFrame = -1;
        private static float nextInteractionTime;
        private readonly HashSet<PrototypeDoor2D> uniqueDoors = new HashSet<PrototypeDoor2D>();
        private bool alternate;

        public string InteractionId => interactionId;
        public InteractionKind Kind => InteractionKind.Switch;
        public string Prompt => showPrompt ? prompt : string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastInteractionFrame = -1;
            nextInteractionTime = 0f;
        }

        private void Start()
        {
            if (player == null || string.IsNullOrWhiteSpace(interactionId) || !HasDoor())
            {
                Debug.LogError("Level PrototypeToggleButton2D 缺少 player/门/稳定 interactionId。", this);
            }
            if (buttonVisual != null)
            {
                buttonVisual.color = defaultColor;
            }
        }

        /// <summary>只判断当前是否可用，不改变任一门。</summary>
        public bool CanInteract(GameObject actor)
        {
            return isActiveAndEnabled && player != null && actor == player.gameObject && player.IsGameplayActive
                && !player.HasPendingRelocation && !string.IsNullOrWhiteSpace(interactionId) && HasDoor()
                && interactionRadius > 0f && interactionCooldown >= 0f && Time.time >= nextInteractionTime
                && ((Vector2)transform.position - player.Position).sqrMagnitude <= interactionRadius * interactionRadius;
        }

        /// <summary>主线程请求；至少一扇门改变才 true。每个有效门切换自身状态，不把所有门设为同一个值。</summary>
        public bool TryInteract(GameObject actor)
        {
            if (!CanInteract(actor) || lastInteractionFrame == Time.frameCount)
            {
                return false;
            }
            lastInteractionFrame = Time.frameCount;
            nextInteractionTime = Time.time + Mathf.Max(0.05f, interactionCooldown);
            uniqueDoors.Clear();
            bool changed = false;
            foreach (PrototypeDoor2D door in targetDoors)
            {
                if (door != null && uniqueDoors.Add(door))
                {
                    changed |= door.TryToggle();
                }
            }
            if (!changed)
            {
                return false;
            }
            alternate = !alternate;
            if (buttonVisual != null)
            {
                buttonVisual.color = alternate ? alternateColor : defaultColor;
            }
            GameAudio.Play(AudioCue.SwitchActivated, gameObject);
            return true;
        }

        private bool HasDoor()
        {
            if (targetDoors != null)
            {
                foreach (PrototypeDoor2D door in targetDoors)
                {
                    if (door != null && door.isActiveAndEnabled)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, interactionRadius));
            if (targetDoors == null)
            {
                return;
            }
            Gizmos.color = Color.cyan;
            foreach (PrototypeDoor2D door in targetDoors)
            {
                if (door != null)
                {
                    Gizmos.DrawLine(transform.position, door.transform.position);
                }
            }
        }
    }
}
