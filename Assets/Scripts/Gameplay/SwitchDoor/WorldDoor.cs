// 职责：持有本门唯一已开状态；打开禁用显式碰撞体，白板可显式授权重新关闭，同场景启停/往返不重置。
// 模块/维护：Soap / T03；依赖Unity2D、GameAudio；只改自己的门，不写玩家/阶段。
// 接线：blockingColliders必填，Closed/Open子视图可替换；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System;
using Regrowth.Audio;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class WorldDoor : MonoBehaviour
    {
        [SerializeField, Tooltip("必填：本门阻挡Collider2D（非Trigger），打开全部禁用；不扫描名字或全场景。")]
        private Collider2D[] blockingColliders;
        [SerializeField, Tooltip("可选：关闭外观子对象；Sprite/颜色/布局/Animator在Prefab替换，不能指向门根。")]
        private GameObject closedView;
        [SerializeField, Tooltip("可选：打开外观子对象，不参与通行判断，不能指向门根。")]
        private GameObject openView;

        [SerializeField, Tooltip("初始打开；本局只初始化一次。白板 Door_2 使用此配置。")]
        private bool startOpen;
        [SerializeField, Tooltip("仅白板试走允许重新关闭；默认关闭，正式 T03 仍为一次开门。")]
        private bool allowPrototypeReclose;
        private bool initialized;

        public bool IsOpen { get; private set; }
        public bool IsConfigured
        {
            get
            {
                if (blockingColliders == null || blockingColliders.Length == 0 || closedView == gameObject || openView == gameObject)
                {
                    return false;
                }
                foreach (Collider2D collider in blockingColliders)
                {
                    if (collider == null || collider.isTrigger)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>成功从关闭变为打开后通知；启用时另读IsOpen快照，订阅者停用退订。</summary>
        public event Action Opened;

        private void OnEnable()
        {
            if (!IsConfigured)
            {
                Debug.LogWarning("[T03 WorldDoor] 缺有效blockingColliders或视图误绑根节点；拒绝打开。", this);
                return;
            }
            if (!initialized)
            {
                IsOpen = startOpen;
                initialized = true;
            }
            ApplyView();
        }

        /// <summary>Unity主线程由开关调用。重复/停用/缺引用false且无音频或事件；成功立即可通行。</summary>
        public bool TryOpen() => TrySetOpen(true);

        /// <summary>白板适配入口；只有 Inspector 明确授权才允许关闭。状态只有本组件持有。</summary>
        public bool TrySetOpen(bool value)
        {
            if (!initialized || IsOpen == value || !isActiveAndEnabled || !IsConfigured
                || (!value && !allowPrototypeReclose))
            {
                return false;
            }
            IsOpen = value;
            ApplyView();
            if (value)
            {
                GameAudio.Play(AudioCue.DoorOpened, gameObject);
                Opened?.Invoke();
            }
            return true;
        }
        private void ApplyView()
        {
            foreach (Collider2D collider in blockingColliders)
            {
                collider.enabled = !IsOpen;
            }
            if (closedView != null)
            {
                closedView.SetActive(!IsOpen);
            }
            if (openView != null)
            {
                openView.SetActive(IsOpen);
            }
        }
    }
}
