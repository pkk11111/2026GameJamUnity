// 职责：Level 白板免费传送交互，保留双向连接与共用冷却；不是 T13 收费业务。
// 模块/维护：controller / Level 适配；依赖：IInteractable、WhiteboxPlayer2D。
// 接线：唯一 PlayerInteractor 经 InteractionTarget 请求；player/destination/稳定 Id 必填。
// 交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。引用下次交互读取，冷却为游戏秒。
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    [DisallowMultipleComponent]
    public sealed class PrototypePortal2D : MonoBehaviour, IInteractable
    {
        [Header("连接")]
        [SerializeField, Tooltip("必填，唯一白板玩家。")]
        private WhiteboxPlayer2D player;
        [SerializeField, Tooltip("必填，另一端 ExitPoint；表示玩家中心位置。")]
        private Transform destination;
        [SerializeField, Tooltip("可选交互距离锚点；为空使用门根。与 InteractionTarget 和 Trigger 配在同一位置，不改变显示或传送落点。")]
        private Transform interactionPoint;
        [Header("交互（实时读取）")]
        [SerializeField, Tooltip("同场景唯一且运行时不修改，由总控在场景保存。")]
        private string interactionId;
        [SerializeField, Min(0.1f), Tooltip("玩家中心到交互锚点的半径，单位；实时读取。")]
        private float interactionRadius = 1.5f;
        [SerializeField, Min(0.05f), Tooltip("所有白板传送门共用交互冷却，游戏秒。")]
        private float interactionCooldown = 0.35f;
        [SerializeField, Tooltip("显示统一 uGUI 交互提示，不自行轮询 E。")]
        private bool showPrompt = true;
        [SerializeField, Tooltip("白板提示文案，按键配置见专用输入资产。")]
        private string prompt = "E - Use portal";
        private static int lastTeleportFrame = -1;
        private static float nextInteractionTime;

        private Vector2 InteractionPosition => interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)transform.position;
        public string InteractionId => interactionId;
        public InteractionKind Kind => InteractionKind.Portal;
        public string Prompt => showPrompt ? prompt : string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastTeleportFrame = -1;
            nextInteractionTime = 0f;
        }

        private void Start()
        {
            if (player == null || destination == null || string.IsNullOrWhiteSpace(interactionId))
            {
                Debug.LogError("Level PrototypePortal2D 缺少 player/destination/稳定 interactionId。", this);
            }
        }

        /// <summary>无副作用；仅指定玩家、Playing、有效引用、范围与共用冷却满足才可用。</summary>
        public bool CanInteract(GameObject actor)
        {
            return isActiveAndEnabled && player != null && actor == player.gameObject && player.IsGameplayActive
                && destination != null && !string.IsNullOrWhiteSpace(interactionId) && !player.HasPendingRelocation
                && interactionRadius > 0f && interactionCooldown >= 0f && Time.time >= nextInteractionTime
                && (InteractionPosition - player.Position).sqrMagnitude <= interactionRadius * interactionRadius;
        }

        /// <summary>主线程调用；再次验证后排队迁移，false 不收费、不移动。白板不提供正式落点/代价策略。</summary>
        public bool TryInteract(GameObject actor)
        {
            if (!CanInteract(actor) || lastTeleportFrame == Time.frameCount
                || !player.TryTeleportTo(destination.position))
            {
                return false;
            }
            lastTeleportFrame = Time.frameCount;
            nextInteractionTime = Time.time + Mathf.Max(0.05f, interactionCooldown);
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.75f, 0.3f, 1f);
            Gizmos.DrawWireSphere(InteractionPosition, Mathf.Max(0.1f, interactionRadius));
            if (destination != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, destination.position);
            }
        }
    }
}
