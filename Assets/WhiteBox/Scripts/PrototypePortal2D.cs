// 职责：普通门E交互、固定三代价与原子支付/迁移；保留已有脚本GUID和场景连接。
// 维护controller/C08；依赖Core/Runtime/EnemyBasic/WhiteboxPlayer2D；不轮询输入或控制时间。
// 显式绑定唯一状态/选择流/全图强化及代价配置；交接portal-rules.handoff；规范AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    [DisallowMultipleComponent]
    public sealed class PrototypePortal2D : MonoBehaviour, IInteractable
    {
        [Header("连接")]
        [SerializeField] private WhiteboxPlayer2D player;
        [SerializeField, Tooltip("玩家中心的目标锚点；打开、确认和物理提交都检查落点。")]
        private Transform destination;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private string interactionId;
        [SerializeField, Min(0.1f)] private float interactionRadius = 1.5f;
        [SerializeField, Min(0.05f)] private float interactionCooldown = 0.35f;
        [SerializeField] private bool showPrompt = true;
        [SerializeField] private string prompt = "E - Choose teleport cost";
        [Header("收费接线（与宝箱共用选择流与抽取器）")]
        [SerializeField] private PlayerState state;
        [SerializeField] private RunController run;
        [SerializeField] private ChoiceCoordinator choiceFlow;
        [SerializeField] private PortalCostConfig costConfig;
        [SerializeField] private EnemyEnhancementService enemies;
        private PortalCostDefinition[] cached;
        private bool pending;
        private bool queued;
        private int revision;
        private string stableId;
        private static int lastTeleportFrame = -1;
        private static float nextInteractionTime;
        public string InteractionId => stableId ?? (stableId = interactionId);
        public string Prompt => showPrompt ? prompt : string.Empty;
        public InteractionKind Kind => InteractionKind.Portal;
        private Vector2 InteractionPosition => interactionPoint != null ? (Vector2)interactionPoint.position : (Vector2)transform.position;
        public bool IsWired => player != null && state != null && run != null && choiceFlow != null
            && enemies != null && costConfig != null && costConfig.IsValid && destination != null
            && player.gameObject == state.gameObject && !string.IsNullOrWhiteSpace(InteractionId);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            lastTeleportFrame = -1;
            nextInteractionTime = 0f;
        }

        private void Start()
        {
            if (!IsWired)
            {
                Debug.LogError("[Portal] Bind player/state/run/choiceFlow/enemies/costConfig/destination and unique ID.", this);
            }
        }

        public bool CanInteract(GameObject actor)
        {
            return isActiveAndEnabled && IsWired && actor == state.gameObject && state.HasBodyCore && state.IsAlive
                && run.IsGameplayActive && choiceFlow.isActiveAndEnabled && !choiceFlow.IsOpen && !pending
                && player.IsGameplayActive && !player.HasPendingRelocation && Time.time >= nextInteractionTime
                && interactionRadius > 0f && (InteractionPosition - player.Position).sqrMagnitude <= interactionRadius * interactionRadius;
        }

        /// <summary>true表示三张菜单被接受，确认后仍须物理步复验；不再提供免费路径。</summary>
        public bool TryInteract(GameObject actor)
        {
            if (!CanInteract(actor) || !player.CanLandAt(destination.position))
            {
                return false;
            }
            var selected = FixedChoiceDeck.Select(cached, costConfig.Costs, 3, value => value.Id,
                Available, count => UnityEngine.Random.Range(0, count), value => value.Copy());
            if (selected == null || !RepairAllDisabled(selected))
            {
                Debug.LogWarning("[Portal] Cannot provide three different costs with an executable choice; no free teleport.", this);
                return false;
            }
            int token = ++revision;
            pending = true;
            if (!choiceFlow.TryBegin(Request(selected), id => Confirm(token, id), () => Cancel(token)))
            {
                pending = false;
                return false;
            }
            cached = selected;
            return true;
        }

        private bool Available(PortalCostDefinition cost)
        {
            var item = PlayerState.CostItem(cost.Kind);
            return cost.IsValid && (!item.HasValue || state.Contains(item.Value));
        }

        private bool CanPay(PortalCostDefinition cost, out string reason)
        {
            if (!state.CanPayTeleportCost(cost.Kind, cost.Amount, costConfig.MinimumAttackDamage, out reason))
            {
                return false;
            }
            if ((cost.Kind == TeleportCostKind.EnemyHealth || cost.Kind == TeleportCostKind.EnemyAttack)
                && !enemies.CanIncrease(cost.Kind, cost.Amount))
            {
                reason = "Enemy enhancement is unavailable or would overflow.";
                return false;
            }
            return true;
        }

        private bool RepairAllDisabled(PortalCostDefinition[] selected)
        {
            foreach (var cost in selected)
            {
                if (CanPay(cost, out _))
                {
                    return true;
                }
            }
            var displayed = new HashSet<string>();
            foreach (var cost in selected)
            {
                displayed.Add(cost.Id);
            }
            var replacements = new List<PortalCostDefinition>();
            foreach (var cost in costConfig.Costs)
            {
                if (!displayed.Contains(cost.Id) && (cost.Kind == TeleportCostKind.Attack
                    || cost.Kind == TeleportCostKind.EnemyHealth || cost.Kind == TeleportCostKind.EnemyAttack)
                    && CanPay(cost, out _))
                {
                    replacements.Add(cost);
                }
            }
            if (replacements.Count == 0)
            {
                return false;
            }
            selected[UnityEngine.Random.Range(0, 3)] = replacements[UnityEngine.Random.Range(0, replacements.Count)].Copy();
            return true;
        }

        private ChoiceRequest Request(PortalCostDefinition[] selected)
        {
            var options = new List<ChoiceOption>();
            foreach (var cost in selected)
            {
                bool enabled = CanPay(cost, out string reason);
                string text;
                switch (cost.Kind)
                {
                    case TeleportCostKind.CurrentHealth:
                        long loss = ((long)state.CurrentHealth * cost.Amount + 99) / 100;
                        text = "Pay " + loss + " HP (" + cost.Amount + "% current). HP: " + state.CurrentHealth + " -> " + (state.CurrentHealth - loss); break;
                    case TeleportCostKind.MaximumHealth:
                        long maximum = state.MaximumHealth - ((long)state.MaximumHealth * cost.Amount + 99) / 100;
                        text = "Maximum HP: " + state.MaximumHealth + " -> " + maximum + "; current: " + state.CurrentHealth + " -> " + Math.Min(state.CurrentHealth, maximum); break;
                    case TeleportCostKind.Attack:
                        text = "Damage -" + cost.Amount + ": bite " + state.BiteDamage + " -> " + (state.BiteDamage - cost.Amount)
                            + ", sword " + state.SwordDamage + " -> " + (state.SwordDamage - cost.Amount)
                            + ", fire/tick " + state.FireDamage + " -> " + (state.FireDamage - cost.Amount) + "."; break;
                    case TeleportCostKind.EnemyHealth:
                        text = "All living and future enemies: +" + cost.Amount + " maximum HP. Keep current HP ratio."; break;
                    case TeleportCostKind.EnemyAttack:
                        text = "All living and future enemies: +" + cost.Amount + " contact damage."; break;
                    default:
                        text = "Lose this body part and its ability. Free one slot; it may be regrown later."; break;
                }
                options.Add(new ChoiceOption(cost.Id, cost.Title, text + (enabled ? "" : "\nUnavailable: " + reason), enabled));
            }
            return new ChoiceRequest(InteractionId, costConfig.ChoiceTitle, options);
        }

        private bool Confirm(int token, string id)
        {
            if (!pending || queued || revision != token || !isActiveAndEnabled || !IsWired
                || run.Phase != RunPhase.Choosing || lastTeleportFrame == Time.frameCount)
            {
                return false;
            }
            var cost = Array.Find(cached, value => value.Id == id);
            if (cost == null || !Available(cost) || !CanPay(cost, out _) || !player.CanLandAt(destination.position))
            {
                return false;
            }
            Vector2 target = destination.position;
            queued = player.TryQueuePaidTeleport(target, move =>
            {
                if (!isActiveAndEnabled || !IsWired || revision != token || !pending
                    || (Vector2)destination.position != target || !Available(cost) || !CanPay(cost, out _))
                {
                    return false;
                }
                return state.TryCommitTeleportCost(cost.Kind, cost.Amount, costConfig.MinimumAttackDamage,
                    costConfig.ArrivalProtection,
                    () => cost.Kind == TeleportCostKind.EnemyHealth || cost.Kind == TeleportCostKind.EnemyAttack
                        ? enemies.TryCommit(cost.Kind, cost.Amount, move) : move(),
                    () => { cached = null; enemies.PublishCommitted(); });
            }, success =>
            {
                pending = queued = false;
                revision++;
                if (success)
                {
                    lastTeleportFrame = Time.frameCount;
                    nextInteractionTime = Time.time + interactionCooldown;
                }
                else
                {
                    Debug.LogWarning("[Portal] Teleport revalidation failed; no payment or movement. Cached cards retained.", this);
                }
            });
            return queued;
        }

        private void Cancel(int token)
        {
            if (revision != token || queued)
            {
                return;
            }
            pending = false;
            revision++;
        }

        private void OnDisable()
        {
            if (queued && player != null)
            {
                player.CancelPaidTeleport();
            }
            if (pending && choiceFlow != null && choiceFlow.RequestId == InteractionId)
            {
                choiceFlow.Cancel();
            }
            pending = queued = false;
            revision++;
        }
    }
}
