// 职责：Start接受后播放确认音，留出短暂尾音时间再加载主场景；不写玩家状态。
// 原维护Dada；音频修复controller；依赖uGUI/Audio.Core；规范AGENTS.md。
// 交接：docs/handoffs/audio-fixes.handoff；Start Button与目标场景必须显式配置。
using System.Collections;
using Regrowth.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.UI.Art
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string gameplayScenePath = "Assets/Scenes/Gameplay/MainLevel.unity";
        [SerializeField, Min(0f), Tooltip("点击确认后切场景的真实秒数；默认0.35覆盖当前0.308秒UI音。不等待音频回调，缺音频也能开始。")]
        private float startTransitionSeconds = 0.35f;
        private bool loading;

        /// <summary>只接受一次有效Start；禁用或目标无效不播放确认音，不启动加载。</summary>
        public void StartGame()
        {
            if (!isActiveAndEnabled || loading)
            {
                return;
            }
            if (startButton == null)
            {
                Debug.LogError("[MainMenu] Bind Start Button.", this);
                return;
            }
#if UNITY_EDITOR
            bool editorFallback = SceneUtility.GetBuildIndexByScenePath(gameplayScenePath) < 0;
            if (editorFallback && UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(gameplayScenePath) == null)
            {
                Debug.LogError("[MainMenu] Gameplay scene does not exist: " + gameplayScenePath, this);
                return;
            }
#else
            bool editorFallback = false;
#endif
            if (!editorFallback && !Application.CanStreamedLevelBeLoaded(gameplayScenePath))
            {
                Debug.LogError("Register MainMenu and Level_Whitebox in the build scene list before building.", this);
                return;
            }
            loading = true;
            startButton.interactable = false;
            GameAudio.Play(AudioCue.UIConfirm);
            StartCoroutine(LoadAfterFeedback(editorFallback));
        }

        private IEnumerator LoadAfterFeedback(bool editorFallback)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, startTransitionSeconds));
#if UNITY_EDITOR
            if (editorFallback)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    gameplayScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield break;
            }
#endif
            SceneManager.LoadSceneAsync(gameplayScenePath, LoadSceneMode.Single);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            loading = false;
            if (startButton != null)
            {
                startButton.interactable = true;
            }
        }
    }
}
