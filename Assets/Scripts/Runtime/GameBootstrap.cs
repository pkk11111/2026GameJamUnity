// 职责：显式场景组装入口、唯一性校验、退出清理及死亡后整场景重开。
// 模块/维护：controller；直接依赖：运行/输入/玩家状态、可选交互/选择、GameAudio、SceneManager。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using Regrowth.Audio;
using Regrowth.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField, Tooltip("C02 玩家状态；旧 C01 输入独测可留空。正式玩家必填，本入口统一初始化/解绑，不靠启停重置生命。")]
        private PlayerState playerState;
        [SerializeField, Tooltip("C03正式交互器；启用交互功能时绑定，旧C01/C02独测可留空。唯一消费Interact。")]
        private PlayerInteractor playerInteractor;
        [SerializeField, Tooltip("C03选择事务协调器；有选择功能时绑定，其presenter须与下方菜单一致或下方留空。")]
        private ChoiceCoordinator choiceCoordinator;
        [SerializeField, Tooltip("可选，实现 IChoicePresenter 的菜单；卸载前取消当前事务。")]
        private MonoBehaviour choicePresenter;
        [SerializeField, Tooltip("可选，退出时停止这些发声对象；全局发声对象也会停止，不停整个音频引擎。")]
        private GameObject[] audioEmitters = new GameObject[0];

        [SerializeField, Tooltip("玩家死亡后自动重载本入口所在场景；默认开启。独立死亡状态测试可关闭。")]
        private bool restartOnDeath = true;

        [SerializeField, Min(0f), Tooltip("死亡后保持场景的真实秒数；默认2.1覆盖当前最长1.964秒死亡声。不等待音频回调，缺音频仍自动重开。")]
        private float deathRestartDelaySeconds = 2.1f;
        private double restartAt;

        private static GameBootstrap activeBootstrap;
        private bool ownsSession;
        private bool restartPending;

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
                || (playerState != null && playerState.IsBound)
                || ((playerInteractor != null || choiceCoordinator != null) && playerState == null)
                || (playerInteractor != null && playerInteractor.IsInitialized)
                || (choiceCoordinator != null && (choiceCoordinator.IsInitialized
                    || (choicePresenter != null && choiceCoordinator.PresenterComponent != choicePresenter)))
                || (choicePresenter != null && !(choicePresenter is IChoicePresenter)))
            {
                Debug.LogError("GameBootstrap 接线失败：检查阶段/输入唯一绑定，C03须有PlayerState，交互/选择未被其他入口拥有，菜单绑定一致且实现IChoicePresenter。", this);
                return;
            }

            activeBootstrap = this;
            ownsSession = true;
            if (!runController.Initialize()
                || (playerState != null && !playerState.Initialize(runController))
                || !inputReader.Initialize(runController, playerState)
                || (choiceCoordinator != null && !choiceCoordinator.Initialize(runController, inputReader, playerState))
                || (playerInteractor != null && !playerInteractor.Initialize(inputReader, runController, playerState)))
            {
                ShutdownSession();
                return;
            }
            if (playerState != null)
            {
                playerState.Died += OnPlayerDied;
                if (!playerState.IsAlive)
                {
                    // 重新绑定已死亡的同一生命周期，只同步 Dead，不复活或重复播放死亡音频。
                    runController.RequestPlayerDeath(playerState);
                    restartPending = restartOnDeath;
                    restartAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, deathRestartDelaySeconds);
                }
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
            restartPending = false;
            try
            {
                if (playerInteractor != null)
                {
                    playerInteractor.Shutdown();
                }
                if (choiceCoordinator != null)
                {
                    choiceCoordinator.Shutdown();
                }
                if (choicePresenter != null && (choiceCoordinator == null || choiceCoordinator.PresenterComponent != choicePresenter))
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
                if (playerState != null)
                {
                    playerState.Died -= OnPlayerDied;
                    playerState.Shutdown();
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

        private void OnPlayerDied()
        {
            inputReader.DiscardGameplayInput();
            if (runController.RequestPlayerDeath(playerState))
            {
                restartPending = restartOnDeath;
                restartAt = Time.realtimeSinceStartupAsDouble + Mathf.Max(0f, deathRestartDelaySeconds);
                GameAudio.Play(AudioCue.PlayerDied, playerState.gameObject);
            }
        }

        // 死亡立即生效；按真实时间保留场景和Bank尾音，再清理并重载。
        // LateUpdate 不依赖 timeScale，Dead 暂停时仍可重开；每次死亡仅发起一次。
        private void LateUpdate()
        {
            if (!restartPending || !ownsSession || runController.Phase != RunPhase.Dead
                || Time.realtimeSinceStartupAsDouble < restartAt)
            {
                return;
            }

            restartPending = false;
            Scene scene = gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)
                || !Application.CanStreamedLevelBeLoaded(scene.path))
            {
                Debug.LogError("GameBootstrap 无法死亡重开：请保存当前场景并加入启用的构建场景。", this);
                return;
            }

            string scenePath = scene.path;
            ShutdownSession();
            SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
        }

        private void OnDisable()
        {
            ShutdownSession();
        }
    }
}
