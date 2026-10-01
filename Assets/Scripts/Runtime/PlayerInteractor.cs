// 职责：唯一消费Interact输入，2D区域检测/去重，按类别→距离→稳定Id选可用对象。
// 模块/维护：controller，C03；依赖：Core、InteractionTarget、UnityEngine.Physics2D。
// 接线：GameBootstrap显式注入输入/阶段/生命，actor必填；不轮询键盘、不结算奖励。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    /// <summary>Update刷新提示；按下时重新检测/验证，只请求一个对象，不在失败后再请求第二对象。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour, IInteractionState
    {
        [SerializeField, Tooltip("必填，唯一玩家根物体，传给IInteractable作为actor。")]
        private GameObject actor;
        [SerializeField, Tooltip("可选扫描中心，为空用actor.transform；跨物体引用显式绑定。")]
        private Transform interactionOrigin;
        [SerializeField, Min(0.01f), Tooltip("交互圆半径，单位；暂定2，实时读取。检测区域Collider，不是地面射线。")]
        private float interactionRadius = 2f;
        [SerializeField, Tooltip("可交互区域Collider的LayerMask，暂定Default；正式层由总控协调。")]
        private LayerMask interactionLayers = 1;
        [SerializeField, Tooltip("是否检测Trigger交互区域；默认true，实时读取，不改全局Physics2D设置。")]
        private bool includeTriggers = true;

        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly HashSet<InteractionTarget> seen = new HashSet<InteractionTarget>();
        private IPlayerInput input;
        private IRunContext run;
        private IHealth health;
        private InteractionTarget selected;
        private string selectedId = string.Empty;
        private string selectedPrompt = string.Empty;
        private bool requesting;
        private int lastRequestFrame = -1;

        public bool IsInitialized => run != null;
        public bool HasTarget => CanInteractNow && selected != null && selectedId.Length > 0;
        public string InteractionId => HasTarget ? selectedId : string.Empty;
        public string Prompt => HasTarget ? selectedPrompt : string.Empty;
        private bool CanInteractNow => IsInitialized && isActiveAndEnabled && actor != null
            && actor.activeInHierarchy && run.IsGameplayActive && health != null && health.IsAlive;

        /// <summary>候选或显示内容实际变化才通知；OnEnable读当前快照/OnDisable退订。</summary>
        public event Action TargetChanged;

        internal bool Initialize(IPlayerInput playerInput, IRunContext context, IHealth playerHealth)
        {
            if (IsInitialized || !enabled || !gameObject.activeInHierarchy || actor == null
                || playerInput == null || context == null || playerHealth == null || !ValidRadius())
            {
                Debug.LogError("C03 PlayerInteractor 初始化失败：检查唯一绑定、actor、生命、输入和正数半径。", this);
                return false;
            }
            input = playerInput;
            run = context;
            health = playerHealth;
            run.PhaseChanged += OnPhaseChanged;
            health.Died += ClearTarget;
            lastRequestFrame = -1;
            return true;
        }

        /// <summary>刷新只读提示，不消费输入、不调用TryInteract；半径非法时安全清空。</summary>
        public void RefreshTarget()
        {
            if (!CanInteractNow || requesting || !ValidRadius())
            {
                SetTarget(null, null);
                return;
            }
            Vector2 origin = interactionOrigin != null ? (Vector2)interactionOrigin.position : (Vector2)actor.transform.position;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(interactionLayers);
            filter.useTriggers = includeTriggers;
            hits.Clear();
            Physics2D.OverlapCircle(origin, interactionRadius, filter, hits);
            seen.Clear();
            InteractionTarget best = null;
            IInteractable bestSource = null;
            float bestDistance = 0f;
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }
                var target = hit.GetComponentInParent<InteractionTarget>();
                if (target == null || !seen.Add(target) || !target.TryGetInteractable(out var source)
                    || !source.CanInteract(actor))
                {
                    continue;
                }
                float distance = (target.Position - origin).sqrMagnitude;
                if (float.IsNaN(distance) || float.IsInfinity(distance))
                {
                    continue;
                }
                if (best == null || (int)source.Kind < (int)bestSource.Kind
                    || (source.Kind == bestSource.Kind && (distance < bestDistance
                        || (distance == bestDistance && string.CompareOrdinal(source.InteractionId, bestSource.InteractionId) < 0))))
                {
                    best = target;
                    bestSource = source;
                    bestDistance = distance;
                }
            }
            SetTarget(best, bestSource);
        }

        /// <summary>测试/显式总控请求口；正常入口由本组件消费Interact。拒绝重入/同帧重复/暂停/死亡。</summary>
        public bool TryInteractCurrent()
        {
            if (!CanInteractNow || requesting || lastRequestFrame == Time.frameCount)
            {
                return false;
            }
            RefreshTarget();
            if (selected == null || !selected.TryGetInteractable(out var source) || !source.CanInteract(actor))
            {
                ClearTarget();
                return false;
            }
            lastRequestFrame = Time.frameCount;
            requesting = true;
            try
            {
                // 接收端再次验证业务；false仍不回退给第二对象，避免一次按键启动多个事务。
                return source.TryInteract(actor);
            }
            finally
            {
                requesting = false;
                RefreshTarget();
            }
        }

        internal void Shutdown()
        {
            if (run != null)
            {
                run.PhaseChanged -= OnPhaseChanged;
            }
            if (health != null)
            {
                health.Died -= ClearTarget;
            }
            input = null;
            run = null;
            health = null;
            hits.Clear();
            seen.Clear();
            ClearTarget();
        }

        private void Update()
        {
            if (requesting)
            {
                return;
            }
            RefreshTarget();
            if (CanInteractNow && input.TryConsumeInteract())
            {
                TryInteractCurrent();
            }
        }

        private bool ValidRadius() => interactionRadius > 0f && !float.IsNaN(interactionRadius) && !float.IsInfinity(interactionRadius);

        private void SetTarget(InteractionTarget target, IInteractable source)
        {
            string id = source != null ? source.InteractionId : string.Empty;
            string prompt = source != null ? source.Prompt ?? string.Empty : string.Empty;
            if (selected == target && selectedId == id && selectedPrompt == prompt)
            {
                return;
            }
            selected = target;
            selectedId = id;
            selectedPrompt = prompt;
            TargetChanged?.Invoke();
        }

        private void ClearTarget() => SetTarget(null, null);
        private void OnPhaseChanged(RunPhase phase)
        {
            if (phase != RunPhase.Playing)
            {
                ClearTarget();
            }
        }

        private void OnDisable() => Shutdown();

        private void OnDrawGizmosSelected()
        {
            if (actor == null || !ValidRadius())
            {
                return;
            }
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(interactionOrigin != null ? interactionOrigin.position : actor.transform.position, interactionRadius);
        }
    }
}
