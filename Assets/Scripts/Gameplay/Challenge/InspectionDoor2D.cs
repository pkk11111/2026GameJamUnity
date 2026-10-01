// 职责：两条支线共用入口门，靠近后单卡确认、显式舍弃、实体阻挡与本次入场许可。
// 维护：controller / C05、T20入口适配；依赖Core/Runtime/Audio、白盒唯一运动器的迁移通知。
// 跨物体引用全部Inspector绑定。这里只拥有入口许可，不实现落坑扣血、终点目标或专属领奖。
// 交接：docs/handoffs/controller.handoff；规范：根AGENTS.md。
using Regrowth.Audio;
using Regrowth.Core;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Gameplay.Challenge
{
    [DisallowMultipleComponent]
    public sealed class InspectionDoor2D : MonoBehaviour
    {
        [Header("入口身份（开局读取，运行中不修改）")]
        [SerializeField, Tooltip("场景唯一支线ID，同时作为选择事务ID。")]
        private string challengeId;
        [SerializeField, Tooltip("只允许Arms或Legs；手包含剑。")]
        private LoadoutItemId requiredMissingPart = LoadoutItemId.Arms;
        [Header("唯一运行接线")]
        [SerializeField, Tooltip("必填，唯一真实玩家状态。")]
        private PlayerState player;
        [SerializeField, Tooltip("必填，该玩家的根实体Collider；不用Sprite判断。")]
        private Collider2D playerCollider;
        [SerializeField, Tooltip("必填，唯一运行阶段。")]
        private RunController run;
        [SerializeField, Tooltip("必填，统一单卡选择协调器。")]
        private ChoiceCoordinator choices;
        [SerializeField, Tooltip("必填，当前白盒唯一运动器；R/传送后撤销未完成入口许可。")]
        private WhiteboxPlayer2D movement;
        [Header("地图接线（轴对齐矩形）")]
        [SerializeField, Tooltip("必填，实体BoxCollider2D，isTrigger=false；尺寸与门视觉一致。")]
        private BoxCollider2D blocker;
        [SerializeField, Tooltip("必填，外侧靠近检测Trigger；不能与blocker是同一个Collider。")]
        private BoxCollider2D approachZone;
        [SerializeField, Tooltip("必填，门的独立显示子物体；开门隐藏，不停用脚本根。")]
        private GameObject closedView;
        [SerializeField, Tooltip("从门指向外侧的世界方向；门1向右，门2向左。重新进入Play生效。")]
        private Vector2 outsideDirection = Vector2.right;
        [SerializeField, Min(0.01f), Tooltip("越过门平面后判内外的距离，单位；避免边缘抖动。")]
        private float crossingMargin = 0.1f;
        [Header("菜单文案（下次打开读取）")]
        [SerializeField, Tooltip("对应支线名称。")]
        private string choiceTitle = "Armless challenge";
        [SerializeField, Tooltip("持有指定部件时的确认卡名。")]
        private string discardTitle = "Discard arms and enter";
        [SerializeField, TextArea, Tooltip("明确只舍弃指定部件；取消无变化。")]
        private string discardDescription = "Give up arms and sword for this attempt. Basic bite remains available.";
        [SerializeField, Tooltip("已缺部件时仍须主动确认。")]
        private string enterTitle = "Start challenge";
        [SerializeField, TextArea, Tooltip("已缺部件时不移除其他部件或扣血。")]
        private string enterDescription = "Start this attempt without giving up anything else.";

        private bool ready;
        private bool waitingForExit;
        private bool pending;
        private bool offeredWithPart;
        private bool entered;
        private bool leavingInvalidAttempt;
        private bool completed;
        private Vector2 outside;
        public string ChallengeId => challengeId;
        public bool HasAdmission { get; private set; }
        public bool HasEntered => entered;
        public bool IsCompleted => completed;
        public bool IsOpen => blocker != null && !blocker.enabled;

        private void OnEnable()
        {
            ready = ValidConfiguration();
            if (!ready)
            {
                Debug.LogError("InspectionDoor2D 接线无效：检查唯一玩家/阶段/菜单/运动器、门实体、外侧Trigger、方向及Arms/Legs。", this);
                return;
            }
            outside = outsideDirection.normalized;
            player.LoadoutChanged += RecheckPart;
            player.Died += ResetAdmission;
            movement.Relocated += OnRelocated;
            RecheckPart();
        }

        private bool ValidConfiguration()
        {
            return player != null && playerCollider != null && playerCollider.gameObject == player.gameObject
                && !playerCollider.isTrigger && run != null && choices != null && movement != null
                && movement.gameObject == player.gameObject && blocker != null && approachZone != null
                && blocker != approachZone && !blocker.isTrigger && approachZone.isTrigger
                && closedView != null && closedView != gameObject && closedView.transform.IsChildOf(transform)
                && !string.IsNullOrWhiteSpace(challengeId)
                && (requiredMissingPart == LoadoutItemId.Arms || requiredMissingPart == LoadoutItemId.Legs)
                && Finite(outsideDirection.x) && Finite(outsideDirection.y) && outsideDirection.sqrMagnitude > 0f
                && Finite(crossingMargin) && crossingMargin > 0f;
        }

        private void Update()
        {
            if (!ready)
            {
                return;
            }
            bool near = approachZone.bounds.Intersects(playerCollider.bounds);
            if (!near)
            {
                waitingForExit = false;
                CancelOwnedChoice();
            }
            if (completed)
            {
                SetOpen(true);
                return;
            }
            if (HasAdmission && player.Contains(requiredMissingPart))
            {
                ResetAdmission();
            }
            float side = SignedSide();
            float extent = Mathf.Abs(outside.x) * playerCollider.bounds.extents.x
                + Mathf.Abs(outside.y) * playerCollider.bounds.extents.y + crossingMargin;
            if (HasAdmission && side < -extent)
            {
                entered = true;
            }
            if (HasAdmission && ((entered && side > extent) || (!entered && !near && side > extent)))
            {
                ResetAdmission();
            }
            // 补回部件时撤资格，但玩家仍在门内侧时留出退路；回到外侧且不占门后才恢复实体。
            if (!HasAdmission && leavingInvalidAttempt && side > extent && !OccupiesDoor())
            {
                leavingInvalidAttempt = false;
            }
            SetOpen(HasAdmission || leavingInvalidAttempt || OccupiesDoor());
            if (!HasAdmission && !leavingInvalidAttempt && !waitingForExit && !pending && near
                && side > 0f && run.IsGameplayActive && player.IsAlive && player.HasBodyCore
                && player.isActiveAndEnabled && playerCollider.enabled && !choices.IsOpen)
            {
                offeredWithPart = player.Contains(requiredMissingPart);
                pending = true;
                if (!choices.TryBegin(CreateRequest(), Confirm, Cancelled))
                {
                    pending = false;
                }
            }
        }

        private ChoiceRequest CreateRequest()
        {
            return new ChoiceRequest(challengeId, choiceTitle, new[]
            {
                new ChoiceOption("enter", offeredWithPart ? discardTitle : enterTitle,
                    offeredWithPart ? discardDescription : enterDescription)
            });
        }

        private bool Confirm(string id)
        {
            if (!ready || !isActiveAndEnabled || !pending || id != "enter" || !player.IsAlive
                || !player.HasBodyCore || !player.isActiveAndEnabled || !playerCollider.enabled
                || run.Phase != RunPhase.Choosing || !approachZone.bounds.Intersects(playerCollider.bounds)
                || SignedSide() <= 0f || choices.RequestId != challengeId)
            {
                return false;
            }
            bool currentlyOwned = player.Contains(requiredMissingPart);
            if (currentlyOwned != offeredWithPart)
            {
                // 显示期间状态变化必须重新展示/确认，不能把“免费开启”静默变成舍弃。
                offeredWithPart = currentlyOwned;
                choices.TryReplace(CreateRequest(), Confirm, Cancelled);
                return false;
            }
            if (currentlyOwned && !player.TryRemoveLoadoutItem(requiredMissingPart))
            {
                return false;
            }
            pending = false;
            waitingForExit = true;
            entered = false;
            HasAdmission = true;
            leavingInvalidAttempt = false;
            SetOpen(true);
            return true;
        }

        private void Cancelled()
        {
            pending = false;
            waitingForExit = true;
        }

        private void CancelOwnedChoice()
        {
            if (pending && choices != null && choices.IsOpen && choices.RequestId == challengeId)
            {
                choices.Cancel();
            }
            pending = false;
        }

        private void RecheckPart()
        {
            if (HasAdmission && player.Contains(requiredMissingPart))
            {
                ResetAdmission();
            }
        }

        private void OnRelocated()
        {
            ResetAdmission();
            // 已经实际迁出，无须保留穿门退路；下次Update仍做占位保护。
            leavingInvalidAttempt = false;
        }

        /// <summary>主线程；退出/死亡/白盒R或传送及未来失败服务调用。只撤入口许可，不扣血/移动/返部件。</summary>
        public void ResetAdmission()
        {
            CancelOwnedChoice();
            if (completed)
            {
                return;
            }
            leavingInvalidAttempt = IsOpen && SignedSide() <= 0f;
            HasAdmission = false;
            entered = false;
            waitingForExit = true;
        }

        /// <summary>预留给未来已验证目标与终点的挑战服务；入口/身体/生命仍复验。当前地图没有调用者，不会自行判完成或发奖。</summary>
        public bool TryCompleteChallenge()
        {
            if (!ready || !isActiveAndEnabled || completed || !HasAdmission || !entered
                || !run.IsGameplayActive || !player.IsAlive || player.Contains(requiredMissingPart))
            {
                return false;
            }
            completed = true;
            SetOpen(true);
            return true;
        }

        private float SignedSide()
        {
            return Vector2.Dot((Vector2)playerCollider.bounds.center - (Vector2)blocker.transform.TransformPoint(blocker.offset), outside);
        }

        private bool OccupiesDoor()
        {
            // disabled Collider.bounds为空；用序列化Box构造世界AABB，避免恢复实体时夹住玩家。
            Vector3 x = blocker.transform.TransformVector(new Vector3(blocker.size.x, 0f, 0f));
            Vector3 y = blocker.transform.TransformVector(new Vector3(0f, blocker.size.y, 0f));
            var bounds = new Bounds(blocker.transform.TransformPoint(blocker.offset),
                new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y), 1f));
            return playerCollider.enabled && bounds.Intersects(playerCollider.bounds);
        }

        private void SetOpen(bool open)
        {
            bool opened = open && blocker.enabled;
            blocker.enabled = !open;
            closedView.SetActive(!open);
            if (opened) GameAudio.Play(AudioCue.DoorOpened, gameObject);
        }

        private void OnDisable()
        {
            CancelOwnedChoice();
            if (player != null)
            {
                player.LoadoutChanged -= RecheckPart;
                player.Died -= ResetAdmission;
            }
            if (movement != null)
            {
                movement.Relocated -= OnRelocated;
            }
            ready = false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
