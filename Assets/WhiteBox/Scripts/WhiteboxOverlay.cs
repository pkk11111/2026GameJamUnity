// 职责：白板统一 uGUI 提示与测试暂停策略；不读取键盘或维护真实状态。
// 模块/维护：controller / Level 适配；依赖：Runtime、TMP；引用在 Inspector 显式绑定。
// 交接：docs/handoffs/controller.handoff；规范：根 AGENTS.md。
using Regrowth.Core;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    public sealed class WhiteboxOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("必填，唯一交互器。")] private PlayerInteractor interactor;
        [SerializeField, Tooltip("必填，唯一输入适配器。")] private PlayerInputReader inputReader;
        [SerializeField, Tooltip("必填，唯一阶段。")] private RunController runController;
        [SerializeField, Tooltip("必填，uGUI TMP 文本。")] private TMP_Text label;
        [SerializeField, TextArea, Tooltip("白板操作说明；实时读取。")]
        private string instructions = "WHITEBOX TEST\nA/D - Move | Space - Jump / Double jump\nShift - Dash | E - Interact | R - Return | Esc - Pause";
        private void OnEnable()
        {
            if (interactor == null || inputReader == null || runController == null || label == null)
            {
                Debug.LogError("Level WhiteboxOverlay 缺少交互/输入/阶段/TMP 引用。", this);
                enabled = false;
                return;
            }
            inputReader.PauseRequested += TogglePause;
        }

        private void Update()
        {
            label.text = instructions + "\n" + runController.Phase + "\n" + interactor.Prompt;
        }

        private void TogglePause()
        {
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
