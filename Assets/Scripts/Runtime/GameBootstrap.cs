// 职责：显式场景组装入口、唯一性校验与退出清理，不创建隐藏单例或实现重开。
// 模块/维护：controller，C01；直接依赖：RunController、PlayerInputReader、可选 IChoicePresenter、GameAudio。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    /// <summary>场景中恰好一份；OnEnable 启动、OnDisable 清理；不跨场景保留对象。</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField, Tooltip("必填，唯一运行控制器；启动/清理由本组件拥有。")]
        private RunController runController;
        [SerializeField, Tooltip("必填，唯一集中输入适配器；不得另挂 Unity PlayerInput。")]
        private PlayerInputReader inputReader;
        [SerializeField, Tooltip("可选，实现 IChoicePresenter 的菜单；卸载前取消当前事务。")]
        private MonoBehaviour choicePresenter;
        [SerializeField, Tooltip("可选，退出时停止这些发声对象；全局发声对象也会停止，不停整个音频引擎。")]
        private GameObject[] audioEmitters = new GameObject[0];

        private static GameBootstrap activeBootstrap;
        private bool ownsSession;

        public bool IsStarted { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwnership()
        {
            activeBootstrap = null;
        }

        private void OnEnable()
        {
            if (activeBootstrap != null && activeBootstrap != this)
            {
                Debug.LogError("C01 GameBootstrap 拒绝重复入口；当前场景已有运行会话。", this);
                return;
            }

            if (runController == null || inputReader == null || runController.IsInitialized || inputReader.IsInitialized
                || (choicePresenter != null && !(choicePresenter is IChoicePresenter)))
            {
                Debug.LogError("C01 GameBootstrap 接线失败：RunController/InputReader 必填且未被其他入口拥有，菜单须实现 IChoicePresenter。", this);
                return;
            }

            activeBootstrap = this;
            ownsSession = true;
            if (!runController.Initialize() || !inputReader.Initialize(runController))
            {
                ShutdownSession();
                return;
            }
            IsStarted = true;
        }

        /// <summary>幂等清理；先撤销菜单，再卸载输入/释放阶段，恢复启动前时间倍率。不能作为新局重开。</summary>
        private void ShutdownSession()
        {
            if (!ownsSession)
            {
                return;
            }

            IsStarted = false;
            try
            {
                if (choicePresenter != null)
                {
                    (choicePresenter as IChoicePresenter)?.CancelCurrent();
                }
            }
            finally
            {
                if (inputReader != null)
                {
                    inputReader.Shutdown();
                }
                if (runController != null)
                {
                    runController.Shutdown();
                }
                GameAudio.StopAll(null);
                if (audioEmitters != null)
                {
                    foreach (GameObject emitter in audioEmitters)
                    {
                        if (emitter != null)
                        {
                            GameAudio.StopAll(emitter);
                        }
                    }
                }
                ownsSession = false;
                if (activeBootstrap == this)
                {
                    activeBootstrap = null;
                }
            }
        }

        private void OnDisable()
        {
            ShutdownSession();
        }
    }
}
