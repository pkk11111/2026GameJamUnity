// 职责：Won时立刻全白，短暂停留后用不受暂停影响的时间淡入用户结局图。
// 维护controller/C14；依赖Core/uGUI；只读阶段，不控制时间、胜负或输入。
// Inspector绑定唯一Run与全屏白底/结局图；停用退订。交接docs/handoffs/victory-ending.handoff；规范AGENTS.md。
using Regrowth.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI
{
    [DisallowMultipleComponent]
    public sealed class VictoryEndingView : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour runSource;
        [SerializeField] private GameObject screenRoot;
        [SerializeField] private Image endingImage;
        [SerializeField, Min(0f), Tooltip("完整白屏停留秒数，unscaled时间。")]
        private float whiteHoldSeconds = 0.4f;
        [SerializeField, Min(0.01f), Tooltip("结局画面从白色中淡入的秒数，unscaled时间。")]
        private float fadeSeconds = 2f;
        private IRunContext run;
        private float elapsed;
        private bool revealing;

        private void OnEnable()
        {
            run = runSource as IRunContext;
            if (run == null || screenRoot == null || screenRoot == gameObject
                || endingImage == null || endingImage.sprite == null)
            {
                Debug.LogError("[Victory Ending] Bind run, child screen and ending image.", this);
                enabled = false;
                return;
            }
            run.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(run.Phase);
        }

        private void OnPhaseChanged(RunPhase phase)
        {
            if (phase != RunPhase.Won)
            {
                revealing = false;
                screenRoot.SetActive(false);
                return;
            }
            if (revealing)
            {
                return;
            }
            elapsed = 0f;
            endingImage.color = new Color(1f, 1f, 1f, 0f);
            screenRoot.SetActive(true);
            revealing = true;
        }

        private void Update()
        {
            if (!revealing)
            {
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01((elapsed - whiteHoldSeconds) / Mathf.Max(0.01f, fadeSeconds));
            endingImage.color = new Color(1f, 1f, 1f, alpha);
        }

        private void OnDisable()
        {
            if (run != null)
            {
                run.PhaseChanged -= OnPhaseChanged;
            }
            revealing = false;
            if (screenRoot != null && screenRoot != gameObject)
            {
                screenRoot.SetActive(false);
            }
        }
    }
}
