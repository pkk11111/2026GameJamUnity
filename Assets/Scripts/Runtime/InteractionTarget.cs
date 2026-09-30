// 职责：交互区域到 IInteractable 的显式桥接，校验场景唯一稳定Id；不执行业务。
// 模块/维护：controller，C03；依赖：Core/UnityEngine；接收端由T03/T12/T13实现。
// 接线：本物体/子物体2D Collider；interactionSource必填，距离锚点可选。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    /// <summary>一个接收端一个桥接，可有多个子Collider；稳定Id由接收端持有。</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionTarget : MonoBehaviour
    {
        private static readonly Dictionary<Tuple<int, string>, HashSet<InteractionTarget>> identities
            = new Dictionary<Tuple<int, string>, HashSet<InteractionTarget>>();

        [SerializeField, Tooltip("必填，实现IInteractable的组件，不在桥接重复维护Id/Kind/Prompt。")]
        private MonoBehaviour interactionSource;
        [SerializeField, Tooltip("同类距离排序锚点；为空使用本物体位置，扫描范围由交互器配置。")]
        private Transform interactionPoint;
        private Tuple<int, string> registeredKey;

        public Vector2 Position => interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)transform.position;

        /// <summary>引用/启用/身份正确才返回；重复Id的所有桥接均拒绝，不用实例编号掩盖冲突。</summary>
        public bool TryGetInteractable(out IInteractable interactable)
        {
            interactable = null;
            if (!isActiveAndEnabled || interactionSource == null || !interactionSource.isActiveAndEnabled
                || registeredKey == null || !identities.TryGetValue(registeredKey, out var peers) || peers.Count != 1)
            {
                return false;
            }
            var source = interactionSource as IInteractable;
            if (source == null || source.InteractionId != registeredKey.Item2
                || !Enum.IsDefined(typeof(InteractionKind), source.Kind))
            {
                return false;
            }
            interactable = source;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            identities.Clear();
        }

        private void OnEnable()
        {
            var source = interactionSource as IInteractable;
            if (interactionSource == null || source == null || string.IsNullOrWhiteSpace(source.InteractionId)
                || !Enum.IsDefined(typeof(InteractionKind), source.Kind))
            {
                Debug.LogError("C03 InteractionTarget 接线/身份无效：检查interactionSource、稳定Id和Kind。", this);
                return;
            }
            registeredKey = Tuple.Create(gameObject.scene.handle, source.InteractionId);
            if (!identities.TryGetValue(registeredKey, out var peers))
            {
                peers = new HashSet<InteractionTarget>();
                identities.Add(registeredKey, peers);
            }
            peers.Add(this);
            if (peers.Count > 1)
            {
                Debug.LogError("C03 场景重复InteractionId：" + source.InteractionId + "；相关对象均禁用交互，修正Id后重新启用。", this);
            }
        }

        private void OnDisable()
        {
            if (registeredKey != null && identities.TryGetValue(registeredKey, out var peers))
            {
                peers.Remove(this);
                if (peers.Count == 0)
                {
                    identities.Remove(registeredKey);
                }
            }
            registeredKey = null;
        }
    }
}
