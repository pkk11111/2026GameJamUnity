// 职责：C03独测的IInteractable替身，用于观察请求次数/优先级；不是正式世界物件。
// 模块/维护：controller，C03测试；依赖：Core/UnityEngine；只挂C03_Smoke。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Tests.C03
{
    public sealed class C03ProbeTarget : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("测试稳定Id，非Play保存；启用期间不改变。")] private string interactionId;
        [SerializeField, Tooltip("测试类别。")] private InteractionKind kind;
        [SerializeField, Tooltip("测试提示。")] private string prompt = "TEST interact";
        [SerializeField, Tooltip("测试能否交互，实时读取。")] private bool available = true;
        [SerializeField, Tooltip("测试是否接受请求，实时读取。")] private bool accepts = true;

        public string InteractionId => interactionId;
        public InteractionKind Kind => kind;
        public string Prompt => prompt;
        public bool Available { get => available; set => available = value; }
        public bool Accepts { get => accepts; set => accepts = value; }
        public int Attempts { get; private set; }
        public int Accepted { get; private set; }
        public Func<GameObject, bool> OnTry { get; set; }

        public bool CanInteract(GameObject actor) => isActiveAndEnabled && actor != null && available;

        public bool TryInteract(GameObject actor)
        {
            if (!CanInteract(actor))
            {
                return false;
            }
            Attempts++;
            bool accepted = OnTry != null ? OnTry(actor) : accepts;
            if (accepted)
            {
                Accepted++;
            }
            return accepted;
        }
    }
}
