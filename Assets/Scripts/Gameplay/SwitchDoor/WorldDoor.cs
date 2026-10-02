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
        [SerializeField, Tooltip("允许按钮反复切换；开启时须绑定closingGuard，门碰撞必须为BoxCollider2D。")]
        private bool allowPrototypeReclose;
        [SerializeField, Tooltip("允许重新关闭时必填：玩家实体Collider；占用门区域时拒绝关门，离开后可再次按开关。")]
        private Collider2D closingGuard;
        private bool initialized;

        public bool IsOpen { get; private set; }
        public bool IsConfigured
        {
            get
            {
                if (blockingColliders == null || blockingColliders.Length == 0 || closedView == gameObject || openView == gameObject || (allowPrototypeReclose && closingGuard == null))
                {
                    return false;
                }
                foreach (Collider2D collider in blockingColliders)
                {
                    if (collider == null || collider.isTrigger || (allowPrototypeReclose && !(collider is BoxCollider2D)))
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

        /// <summary>白板适配入口；只有Inspector明确授权且玩家未占门才允许关闭；拒绝不改变状态/碰撞/音频。</summary>
        public bool TrySetOpen(bool value)
        {
            if (!initialized || IsOpen == value || !isActiveAndEnabled || !IsConfigured
                || (!value && (!allowPrototypeReclose || IsClosingAreaOccupied())))
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
        private bool IsClosingAreaOccupied()
        {
            if (closingGuard == null)
            {
                return true;
            }
            if (!closingGuard.enabled || !closingGuard.gameObject.activeInHierarchy)
            {
                return false;
            }
            Physics2D.SyncTransforms();
            foreach (Collider2D collider in blockingColliders)
            {
                // 已打开门的Collider.bounds为空，使用保存的Box形状计算保守世界包围盒。
                var box = collider as BoxCollider2D;
                if (box == null)
                {
                    return true;
                }
                Vector3 x = box.transform.TransformVector(new Vector3(box.size.x, 0f, 0f));
                Vector3 y = box.transform.TransformVector(new Vector3(0f, box.size.y, 0f));
                var bounds = new Bounds(box.transform.TransformPoint(box.offset),
                    new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), 1f));
                if (bounds.Intersects(closingGuard.bounds))
                {
                    return true;
                }
            }
            return false;
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
