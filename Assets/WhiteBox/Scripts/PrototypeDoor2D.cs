// 职责：保留白板按钮/测试的门调用入口；唯一门状态、碰撞与表现由 T03 WorldDoor 持有。
// 模块/维护：controller / Level 适配；依赖：Regrowth.Gameplay.SwitchDoor。
// 接线：显式绑定同物体 WorldDoor，初始打开/可重新关闭在 WorldDoor Inspector 配置。
// 交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    [DisallowMultipleComponent]
    public sealed class PrototypeDoor2D : MonoBehaviour
    {
        [SerializeField, Tooltip("必填，同物体 T03 WorldDoor；它是本门唯一状态源。")]
        private WorldDoor door;

        public bool IsClosed => door != null && !door.IsOpen;

        private void Start()
        {
            if (door == null || door.gameObject != gameObject || !door.IsConfigured)
            {
                Debug.LogError("Level PrototypeDoor2D 缺少同物体有效 WorldDoor。", this);
                enabled = false;
            }
        }

        /// <summary>白板开关反转一次；停用、未接线或状态入口拒绝时 false。</summary>
        public bool TryToggle() => TrySetClosed(!IsClosed);

        /// <summary>适配旧白板调用；不再保存状态或第二次操作碰撞/音频。</summary>
        public bool TrySetClosed(bool value)
        {
            return isActiveAndEnabled && door != null && door.TrySetOpen(!value);
        }
    }
}