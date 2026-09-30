// 职责：唯一运行阶段和 Time.timeScale 持有者；提供显式暂停及归属校验的选择锁。
// 模块/维护：controller，C01；直接依赖：UnityEngine、Regrowth.Core；由 GameBootstrap 初始化/清理。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    /// <summary>
    /// 主线程命令入口。当前仅 Playing/Paused/Choosing；Dead/Won/重开等待 C04 仲裁。
    /// UI 不设置时间倍率；奖励是否调用选择锁仍未冻结。
    /// </summary>
    public sealed class RunController : MonoBehaviour, IRunContext
    {
        [SerializeField, Min(0.01f)]
        [Tooltip("Playing 的时间倍率；测试默认 1，下一次切入 Playing 生效。暂停/选择为 0。")]
        private float gameplayTimeScale = 1f;

        private RunPhase phase = RunPhase.Paused;
        private object choiceOwner;
        private float previousTimeScale;
        private bool changingPhase;

        public RunPhase Phase => phase;
        public bool IsGameplayActive => IsInitialized && phase == RunPhase.Playing;
        public bool IsInitialized { get; private set; }

        /// <summary>真实阶段改变后同步通知；订阅时另读快照，回调不重入阶段命令。</summary>
        public event Action<RunPhase> PhaseChanged;

        // Bootstrap 早于本组件 OnEnable；启动校验用 enabled/activeInHierarchy，避免误判未就绪。
        internal bool Initialize()
        {
            if (IsInitialized || (!enabled || !gameObject.activeInHierarchy) || !IsValidTimeScale())
            {
                Debug.LogError("C01 RunController 初始化失败：组件必须启用，倍率须为正有限数且不可重复初始化。", this);
                return false;
            }

            previousTimeScale = Time.timeScale;
            IsInitialized = true;
            ChangePhase(RunPhase.Playing);
            return true;
        }

        /// <summary>显式暂停请求；只有 Playing 接受。PauseRequested 尚不绑定正式菜单策略。</summary>
        public bool TryPause()
        {
            if (!CanChange() || phase != RunPhase.Playing)
            {
                return false;
            }

            ChangePhase(RunPhase.Paused);
            return true;
        }

        /// <summary>只恢复 Paused；不能绕过选择事务锁。</summary>
        public bool TryResume()
        {
            if (!CanChange() || phase != RunPhase.Paused || !IsValidTimeScale())
            {
                return false;
            }

            ChangePhase(RunPhase.Playing);
            return true;
        }

        /// <summary>
        /// Playing 时取得唯一选择锁；owner 须非 null，结束时必须传回同一对象。
        /// G29 传送可使用；不意味着所有奖励菜单自动暂停。
        /// </summary>
        public bool TryBeginChoosing(object owner)
        {
            if (owner == null || !CanChange() || phase != RunPhase.Playing)
            {
                return false;
            }

            choiceOwner = owner;
            ChangePhase(RunPhase.Choosing);
            return true;
        }

        /// <summary>归属错误/无锁返回 false；成功恢复 Playing，不结算费用或移动玩家。</summary>
        public bool TryEndChoosing(object owner)
        {
            if (!CanChange() || phase != RunPhase.Choosing || !ReferenceEquals(choiceOwner, owner)
                || !IsValidTimeScale())
            {
                return false;
            }

            choiceOwner = null;
            ChangePhase(RunPhase.Playing);
            return true;
        }

        internal void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            IsInitialized = false;
            choiceOwner = null;
            Time.timeScale = previousTimeScale;
            if (phase != RunPhase.Paused)
            {
                phase = RunPhase.Paused;
                PhaseChanged?.Invoke(phase);
            }
        }

        private bool CanChange()
        {
            return IsInitialized && isActiveAndEnabled && !changingPhase;
        }

        private bool IsValidTimeScale()
        {
            return gameplayTimeScale > 0f && !float.IsNaN(gameplayTimeScale) && !float.IsInfinity(gameplayTimeScale);
        }

        private void ChangePhase(RunPhase nextPhase)
        {
            changingPhase = true;
            phase = nextPhase;
            Time.timeScale = nextPhase == RunPhase.Playing ? gameplayTimeScale : 0f;
            try
            {
                PhaseChanged?.Invoke(phase);
            }
            finally
            {
                changingPhase = false;
            }
        }

        private void OnDisable()
        {
            Shutdown();
        }
    }
}
