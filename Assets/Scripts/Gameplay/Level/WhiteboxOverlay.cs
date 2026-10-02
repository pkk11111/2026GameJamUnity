// 职责：白板只读调试文字；暂停由Runtime/PauseInputHandler独立处理。
// 模块/维护：controller / Level 适配；依赖：Runtime、TMP；引用在 Inspector 显式绑定。
// 交接：docs/handoffs/postjam-maintenance.handoff；规范：根 AGENTS.md。
using Regrowth.Core;
using Regrowth.Runtime;
using TMPro;
using UnityEngine;

namespace Regrowth.Gameplay.WhiteBox
{
    public sealed class WhiteboxOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("必填，唯一交互器。")] private PlayerInteractor interactor;
        [SerializeField, Tooltip("必填，唯一阶段。")] private RunController runController;
        [SerializeField, Tooltip("必填，uGUI TMP 文本。")] private TMP_Text label;
        [SerializeField, TextArea, Tooltip("白板操作说明；实时读取。")]
        private string instructions = "WHITEBOX TEST\nA/D - Move | Space - Jump / Double jump\nShift - Dash | E - Interact | Esc - Pause";
        private string displayedInstructions;
        private string displayedPrompt;
        private RunPhase displayedPhase;
        private bool hasDisplaySnapshot;

        private void OnEnable()
        {
            if (interactor == null || runController == null || label == null)
            {
                Debug.LogError("Level WhiteboxOverlay 缺少交互/阶段/TMP 引用。", this);
                enabled = false;
                return;
            }
            hasDisplaySnapshot = false;
        }

        private void Update()
        {
            // 隐藏的调试说明不拼字符串；暂停不依赖本组件。
            if (!label.isActiveAndEnabled)
            {
                hasDisplaySnapshot = false;
                return;
            }
            string currentPrompt = interactor.Prompt;
            RunPhase currentPhase = runController.Phase;
            if (!hasDisplaySnapshot || displayedInstructions != instructions
                || displayedPrompt != currentPrompt || displayedPhase != currentPhase)
            {
                label.text = instructions + "\n" + currentPhase + "\n" + currentPrompt;
                displayedInstructions = instructions;
                displayedPrompt = currentPrompt;
                displayedPhase = currentPhase;
                hasDisplaySnapshot = true;
            }
        }

    }
}
