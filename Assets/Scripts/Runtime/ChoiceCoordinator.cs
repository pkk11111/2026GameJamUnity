// 职责：唯一选择事务接线，暂停/提交保护/同事务替换/取消与清理；不生成卡组或执行业务。
// 模块/维护：controller，C03；依赖：Core、RunController、PlayerInputReader。
// 接线：presenter显式绑定IChoicePresenter实现，Bootstrap注入运行/输入/生命；正式UI归T01。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    /// <summary>教学/指定舍弃单项或奖励三项打开；V5替换三项，保留四项展示兼容独测；所有选择按确认规则暂停。</summary>
    [DisallowMultipleComponent]
    public sealed class ChoiceCoordinator : MonoBehaviour, IChoiceFlow
    {
        [SerializeField, Tooltip("必填，实现IChoicePresenter的菜单组件；不能绑定协调器自身。")]
        private MonoBehaviour presenter;
        private RunController run;
        private PlayerInputReader input;
        private IHealth health;
        private Session session;
        private bool opening;
        private bool submitting;
        private bool closing;
        private bool cancelRequested;
        private bool shutdownRequested;

        public bool IsInitialized => run != null;
        public bool IsOpen => session != null;
        public int LastClosedFrame { get; private set; } = -1;
        public string RequestId => session != null ? session.Request.Id : string.Empty;
        internal MonoBehaviour PresenterComponent => presenter;
        private IChoicePresenter Presenter => presenter != null ? presenter as IChoicePresenter : null;

        internal bool Initialize(RunController context, PlayerInputReader reader, IHealth playerHealth)
        {
            if (IsInitialized || !enabled || !gameObject.activeInHierarchy || context == null || reader == null
                || playerHealth == null || presenter == null || !(presenter is IChoicePresenter)
                || !presenter.enabled || !presenter.gameObject.activeInHierarchy || Presenter.IsOpen)
            {
                Debug.LogError("C03 ChoiceCoordinator 接线失败：阶段/输入/生命及启用的IChoicePresenter必填，菜单须关闭。", this);
                return false;
            }
            run = context;
            input = reader;
            health = playerHealth;
            shutdownRequested = false;
            run.PhaseChanged += OnPhaseChanged;
            return true;
        }

        public bool TryBegin(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel)
        {
            if (!IsInitialized || !isActiveAndEnabled || opening || submitting || closing || IsOpen
                || request == null || (request.Options.Count != 1 && request.Options.Count != 3) || tryConfirm == null
                || !health.IsAlive || !run.IsGameplayActive || Presenter == null || !presenter.isActiveAndEnabled || Presenter.IsOpen)
            {
                return false;
            }
            var context = run;
            opening = true;
            bool accepted = false;
            try
            {
                if (!context.TryBeginChoosing(this))
                {
                    return false;
                }
                if (!isActiveAndEnabled || run != context || shutdownRequested || !health.IsAlive)
                {
                    context.TryEndChoosing(this);
                    return false;
                }
                var created = new Session(request, tryConfirm, onCancel);
                session = created;
                accepted = Presenter.TryShow(request, id => Confirm(created, 0, id), () => OnPresenterCancelled(created, 0));
                if (!accepted)
                {
                    AbortOpening(created);
                }
                return accepted;
            }
            catch
            {
                if (session != null)
                {
                    AbortOpening(session);
                }
                throw;
            }
            finally
            {
                opening = false;
                if (cancelRequested && session != null)
                {
                    Cancel();
                }
                if (shutdownRequested && !submitting)
                {
                    Disconnect();
                }
            }
        }

        public bool TryReplace(ChoiceRequest request, Func<string, bool> tryConfirm, Action onCancel)
        {
            var current = session;
            if (!IsInitialized || !isActiveAndEnabled || current == null || opening || closing || Presenter == null || !presenter.isActiveAndEnabled
                || request == null || request.Id != current.Request.Id || tryConfirm == null
                || (request.Options.Count != 1 && request.Options.Count != 3 && request.Options.Count != 4) || run.Phase != RunPhase.Choosing || !health.IsAlive)
            {
                return false;
            }
            int nextVersion = current.Version + 1;
            if (!Presenter.TryReplaceCurrent(request, id => Confirm(current, nextVersion, id),
                () => OnPresenterCancelled(current, nextVersion)))
            {
                return false;
            }
            current.Request = request;
            current.Confirm = tryConfirm;
            current.Cancel = onCancel;
            current.Version = nextVersion;
            return true;
        }

        public void Cancel()
        {
            if (session == null || closing)
            {
                return;
            }
            if (submitting || opening)
            {
                cancelRequested = true;
                return;
            }
            var ended = session;
            session = null;
            closing = true;
            cancelRequested = false;
            try
            {
                Presenter?.CancelCurrent();
            }
            finally
            {
                try
                {
                    ReleaseStage();
                    ended.Cancel?.Invoke();
                }
                finally
                {
                    closing = false;
                }
            }
        }

        internal void Shutdown()
        {
            shutdownRequested = true;
            Cancel();
            if (!submitting && !opening)
            {
                Disconnect();
            }
        }

        private bool Confirm(Session expected, int version, string id)
        {
            if (!ReferenceEquals(session, expected) || expected.Version != version || opening || closing || submitting
                || !isActiveAndEnabled || !IsInitialized || run.Phase != RunPhase.Choosing || !health.IsAlive)
            {
                return false;
            }
            bool found = false;
            foreach (var option in expected.Request.Options)
            {
                if (option.Id == id)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
            submitting = true;
            try
            {
                bool committed = expected.Confirm(id);
                if (!committed || expected.Version != version)
                {
                    return false;
                }
                session = null;
                cancelRequested = false;
                ReleaseStage();
                return true;
            }
            finally
            {
                submitting = false;
                if (cancelRequested && session != null)
                {
                    Cancel();
                }
                if (shutdownRequested)
                {
                    Disconnect();
                }
            }
        }

        private void OnPresenterCancelled(Session expected, int version)
        {
            if (ReferenceEquals(session, expected) && expected.Version == version)
            {
                Cancel();
            }
        }

        private void AbortOpening(Session expected)
        {
            if (!ReferenceEquals(session, expected))
            {
                return;
            }
            // 拒绝/异常打开不属于已接受事务，不通知业务取消；撤下已存回调让迟到提交失效。
            session = null;
            cancelRequested = false;
            try
            {
                Presenter?.CancelCurrent();
            }
            finally
            {
                ReleaseStage();
            }
        }

        private void ReleaseStage()
        {
            LastClosedFrame = Time.frameCount;
            input?.DiscardGameplayInput();
            if (run != null && run.IsInitialized && run.Phase == RunPhase.Choosing && !run.TryEndChoosing(this))
            {
                Debug.LogError("C03 选择锁释放失败，检查运行倍率/生命周期；不会擅自覆盖其他阶段。", this);
            }
        }

        private void Disconnect()
        {
            if (run != null)
            {
                run.PhaseChanged -= OnPhaseChanged;
            }
            run = null;
            input = null;
            health = null;
            shutdownRequested = false;
            cancelRequested = false;
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            if (session != null && phase != RunPhase.Choosing)
            {
                Cancel();
            }
        }

        private void Update()
        {
            // Presenter被停用/销毁或违约自行关闭时也释放本事务，不能留下无限选择锁。
            if (session != null && !opening && !submitting && !closing
                && (Presenter == null || !presenter.isActiveAndEnabled || !Presenter.IsOpen))
            {
                Cancel();
            }
        }

        private void OnDisable() => Shutdown();

        private sealed class Session
        {
            internal ChoiceRequest Request;
            internal Func<string, bool> Confirm;
            internal Action Cancel;
            internal int Version;

            internal Session(ChoiceRequest request, Func<string, bool> confirm, Action cancel)
            {
                Request = request;
                Confirm = confirm;
                Cancel = cancel;
            }
        }
    }
}
