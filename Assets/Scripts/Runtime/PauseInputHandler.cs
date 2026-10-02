// 职责：将唯一输入的暂停请求转发到RunController；不依赖文字/UI，也不直接设置时间。
// 维护：postjam-maintenance；依赖Runtime/Core，状态归RunController，菜单事务归ChoiceCoordinator。
// 接线：显式绑定Reader/Run/Choices；启用订阅、停用退订，菜单取消帧不再次暂停。
// 交接：docs/handoffs/postjam-maintenance.handoff；规范：根AGENTS.md。
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PauseInputHandler : MonoBehaviour
    {
        [SerializeField, Tooltip("必填，唯一输入适配器；重新启用时读取绑定。")]
        private PlayerInputReader inputReader;
        [SerializeField, Tooltip("必填，唯一运行阶段/时间持有者。")]
        private RunController runController;
        [SerializeField, Tooltip("必填，统一选择事务；取消菜单的同一帧不再次暂停。")]
        private ChoiceCoordinator choiceCoordinator;

        private void OnEnable()
        {
            if (inputReader == null || runController == null || choiceCoordinator == null)
            {
                Debug.LogError("PauseInputHandler 缺少 PlayerInputReader、RunController 或 ChoiceCoordinator 绑定。", this);
                enabled = false;
                return;
            }
            inputReader.PauseRequested += TogglePause;
        }

        private void TogglePause()
        {
            if (choiceCoordinator.IsOpen || choiceCoordinator.LastClosedFrame == Time.frameCount)
            {
                return;
            }
            if (runController.Phase == RunPhase.Playing)
            {
                runController.TryPause();
            }
            else if (runController.Phase == RunPhase.Paused)
            {
                runController.TryResume();
            }
        }

        private void OnDisable()
        {
            if (inputReader != null)
            {
                inputReader.PauseRequested -= TogglePause;
            }
        }
    }
}
