// 职责：正式前端Start入口，只加载主场景；不创建PlayerState或修改玩法状态。
// 依赖：Unity场景管理/uGUI；维护Dada；交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Regrowth.UI.Art
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private string gameplayScenePath = "Assets/WhiteBox/Scenes/Level_Whitebox.unity";
        private bool loading;

        public void StartGame()
        {
            if (loading) { return; }
            loading = true;
            startButton.interactable = false;
#if UNITY_EDITOR
            if (SceneUtility.GetBuildIndexByScenePath(gameplayScenePath) < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    gameplayScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            if (!Application.CanStreamedLevelBeLoaded(gameplayScenePath))
            {
                Debug.LogError("Register MainMenu and Level_Whitebox in the build scene list before building.", this);
                loading = false;
                startButton.interactable = true;
                return;
            }
            SceneManager.LoadSceneAsync(gameplayScenePath, LoadSceneMode.Single);
        }
    }
}
