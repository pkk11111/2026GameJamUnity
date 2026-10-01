// 职责：IInteractable一次开关，通过显式WorldDoor引用打开门；不读取设备/消费输入。
// 模块/维护：Soap / T03；依赖Core/WorldDoor/GameAudio；阶段归唯一RunController，生命只读actor。
// 接线：InteractionTarget在根绑定本组件，ID场景唯一，Collider区域由C03扫描；runSource/door必填。
// 交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System;
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class WorldSwitch : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("必填：同场景唯一稳定ID，Awake读取后不再改变；Prefab实例分别填写。")]
        private string interactionId;
        [SerializeField, Tooltip("交互提示，实时读取；按键绑定仍由Input Actions维护。")]
        private string prompt = "Open door";
        [SerializeField, Tooltip("必填：唯一IRunContext组件（RunController），运行时只读。")]
        private MonoBehaviour runSource;
        [SerializeField, Tooltip("必填：仅打开此门。Prefab/Scene Inspector显式绑定，不Find。")]
        private WorldDoor door;
        [SerializeField, Tooltip("可选：未使用外观子物体；替换Sprite/Animator不改逻辑。")]
        private GameObject idleView;
        [SerializeField, Tooltip("可选：已使用外观子物体，不要绑定开关根。")]
        private GameObject activatedView;

        private IRunContext run;
        private string stableId;
        private bool identityCaptured;
        private bool submitting;
        // C03桥接OnEnable可能早于本组件Awake；首次读取即固定Inspector身份。
        public string InteractionId
        {
            get
            {
                if (!identityCaptured)
                {
                    stableId = interactionId;
                    identityCaptured = true;
                }
                return stableId;
            }
        }
        public InteractionKind Kind => InteractionKind.Switch;
        public string Prompt => prompt;
        public bool IsActivated { get; private set; }

        /// <summary>只在本开关首次实际成功后通知；OnEnable读取快照/OnDisable退订。</summary>
        public event Action Activated;

        private void Awake()
        {
            _ = InteractionId;
        }
        private void OnEnable()
        {
            run = runSource as IRunContext;
            if (string.IsNullOrWhiteSpace(InteractionId) || run == null || door == null || !door.IsConfigured
                || idleView == gameObject || activatedView == gameObject)
            {
                Debug.LogWarning("[T03 WorldSwitch] 缺Door/runSource/稳定ID或视图引用无效；检查此组件Inspector。", this);
            }
            ApplyView();
        }

        /// <summary>纯只读可用性判断：不播放、开门、修改状态或记录日志。C03负责范围/唯一ID验证。</summary>
        public bool CanInteract(GameObject actor)
        {
            return !IsActivated && !submitting && isActiveAndEnabled && actor != null && actor.activeInHierarchy
                && runSource != null && run != null && run.IsGameplayActive
                && !string.IsNullOrWhiteSpace(stableId) && door != null && door.isActiveAndEnabled
                && door.IsConfigured && !door.IsOpen && idleView != gameObject && activatedView != gameObject
                && actor.GetComponent<IHealth>() is IHealth health && health.IsAlive;
        }

        /// <summary>主线程由唯一PlayerInteractor调用；再次验证，只有实际打开门返回true；重入/重复false。</summary>
        public bool TryInteract(GameObject actor)
        {
            if (!CanInteract(actor))
            {
                return false;
            }
            submitting = true;
            try
            {
                if (!door.TryOpen())
                {
                    return false;
                }
                IsActivated = true;
                ApplyView();
                GameAudio.Play(AudioCue.SwitchActivated, gameObject);
                Activated?.Invoke();
                return true;
            }
            finally
            {
                submitting = false;
            }
        }

        private void ApplyView()
        {
            if (idleView != null && idleView != gameObject)
            {
                idleView.SetActive(!IsActivated);
            }
            if (activatedView != null && activatedView != gameObject)
            {
                activatedView.SetActive(IsActivated);
            }
        }
    }
}
