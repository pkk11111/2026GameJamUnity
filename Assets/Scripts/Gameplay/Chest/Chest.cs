// 职责：IInteractable宝箱接收端、显式接线与本箱表现；唯一领取逻辑由ChestClaimTransaction持有。
// controller适配Soap / T12-V5；依赖Core/Audio.Core；不读设备/不直调UI/不写Time.timeScale。
// 交接docs/handoffs/controller.handoff；规范根AGENTS.md。必填同一状态源、唯一Run/ChoiceFlow与Config。
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Chest : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("非空场景唯一ID，首次读取固定，不能运行时更改。")] private string interactionId;
        [SerializeField] private string prompt = "Open chest";
        [SerializeField, Tooltip("同一PlayerState提供生命/构筑/写口；重新启用不重置箱子。")] private MonoBehaviour stateSource;
        [SerializeField, Tooltip("唯一IRunContext组件。")] private MonoBehaviour runSource;
        [SerializeField, Tooltip("唯一IChoiceFlow组件；禁止绑定ChoicePanel。")] private MonoBehaviour choiceFlowSource;
        [SerializeField, Tooltip("首次成功打开复制候选；本局缓存不写回资产。")] private ChestRewardConfig rewardConfig;
        [SerializeField, Tooltip("可选未领取子视图，Sprite/Animator可替换，不能绑根。")] private GameObject closedView;
        [SerializeField] private GameObject claimedView;
        private string stableId;
        private bool identityCaptured;
        private ChestClaimTransaction transaction;
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
        public string Prompt => prompt;
        public InteractionKind Kind => InteractionKind.Chest;
        public bool IsClaimed => transaction != null && transaction.Claimed;
        internal ChestClaimTransaction Transaction => transaction;

        private bool ValidReferences()
        {
            // 启动时其他组件可能尚未执行OnEnable；enabled/activeInHierarchy与C01启动资格一致。
            return stateSource != null && stateSource.enabled && stateSource.gameObject.activeInHierarchy && stateSource is IHealth
                && stateSource is ILoadoutState && stateSource is IPlayerStateCommands
                && stateSource is IPlayerRewardCommands && stateSource is IPlayerBodyState
                && runSource != null && runSource.enabled && runSource.gameObject.activeInHierarchy && runSource is IRunContext
                && choiceFlowSource != null && choiceFlowSource.enabled && choiceFlowSource.gameObject.activeInHierarchy && choiceFlowSource is IChoiceFlow
                && rewardConfig != null && rewardConfig.IsValid && !string.IsNullOrWhiteSpace(InteractionId)
                && closedView != gameObject && claimedView != gameObject;
        }
        private void OnEnable()
        {
            if (!ValidReferences())
            {
                Debug.LogWarning("[T12 Chest] 缺状态/Run/ChoiceFlow/有效Config/稳定ID；检查此组件Inspector。", this);
            }
            ApplyView();
        }
        private void OnDisable()
        {
            transaction?.CancelCurrent();
        }
        private void EnsureTransaction()
        {
            if (transaction == null)
            {
                transaction = new ChestClaimTransaction((IHealth)stateSource, (ILoadoutState)stateSource,
                    (IPlayerStateCommands)stateSource, (IRunContext)runSource, (IChoiceFlow)choiceFlowSource,
                    rewardConfig, InteractionId, OnClaimed, () => isActiveAndEnabled && ValidReferences());
            }
        }
        /// <summary>只读资格判断，不抽卡/打开/发音频；actor必须为显式状态源的玩家根。</summary>
        public bool CanInteract(GameObject actor)
        {
            return ValidReferences() && isActiveAndEnabled && actor != null && actor == stateSource.gameObject
                && actor.activeInHierarchy && ((IHealth)stateSource).IsAlive && ((IRunContext)runSource).IsGameplayActive
                && (rewardConfig.BodyTutorial != ((IPlayerBodyState)stateSource).HasBodyCore)
                && !((IChoiceFlow)choiceFlowSource).IsOpen && !IsClaimed && (transaction == null || !transaction.IsPending);
        }
        /// <summary>true仅表示成功打开事务；只有最终奖励完成才消耗本箱。主线程调用。</summary>
        public bool TryInteract(GameObject actor)
        {
            if (!CanInteract(actor))
            {
                return false;
            }
            EnsureTransaction();
            if (!transaction.TryBegin())
            {
                Debug.LogWarning("[T12 Chest] 拒绝打开：合法候选不足（普通3/教学1）或选择入口拒绝；不会重抽已缓存卡组。", this);
                return false;
            }
            GameAudio.Play(AudioCue.ChestOpened, gameObject);
            return true;
        }
        private void OnClaimed()
        {
            ApplyView();
        }
        private void ApplyView()
        {
            if (closedView != null && closedView != gameObject)
            {
                closedView.SetActive(!IsClaimed);
            }
            if (claimedView != null && claimedView != gameObject)
            {
                claimedView.SetActive(IsClaimed);
            }
        }
    }
}
