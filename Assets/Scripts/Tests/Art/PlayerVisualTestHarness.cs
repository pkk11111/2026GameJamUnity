// 职责：独立测试场景的组合选择、Clip 采样、实时速度、静态 Idle 呼吸和会话记录。
// 模块：player-visual-art-test；依赖：本模块 Library、uGUI/TMP；不依赖 gameplay 程序集。
// 接线：所有跨对象引用由场景保存；监听 OnEnable 注册、OnDisable 清理。
// 交接：docs/handoffs/Dada.handoff；规范：根 AGENTS.md。
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace Regrowth.Tests.Art
{
    public sealed class PlayerVisualTestHarness : MonoBehaviour
    {
        [Header("场景引用（必填）")]
        [SerializeField, Tooltip("美术测试配置；运行时只读。")]
        private PlayerVisualLibrary library;
        [SerializeField, Tooltip("只对此节点施加 Idle 呼吸；不移动碰撞或正式角色根。")]
        private Transform playerVisualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField, Tooltip("与 SpriteRenderer 同节点，仅播放测试 Clip，不绑定正式控制器。")]
        private Animator previewAnimator;
        [SerializeField] private Toggle headToggle, bodyToggle, armsToggle, legsToggle, normalTailToggle, flameTailToggle;
        [SerializeField, Tooltip("按 Idle, Move, Jump, Bite, Attack, Fire 顺序绑定六个按钮。")]
        private Button[] actionButtons;
        [SerializeField] private Slider fpsSlider, holdSlider;
        [SerializeField] private TMP_Text debugText, statusText, fpsText, holdText, sessionText;
        [SerializeField] private Button passButton, failButton, replayButton, previousButton, nextButton, pauseButton;
        [Header("临时呼吸（实时生效）")]
        [SerializeField, Range(0f, 0.02f), Tooltip("缩放振幅；默认 0.01 即 0.99–1.01。")]
        private float breathingAmplitude = 0.01f;
        [SerializeField, Min(0.1f), Tooltip("呼吸周期，真实秒；默认 1 秒。")]
        private float breathingPeriod = 1f;
        [Header("单次动作（下次请求或滑条变化生效）")]
        [SerializeField, Tooltip("动作结束后显示该组合 Move 第1帧；Debug 保留请求动作与实际资源名。")]
        private bool returnToRest = true;
        [Header("状态颜色（实时生效）")]
        [SerializeField] private Color okColor = new Color(0.7f, 0.75f, 0.62f);
        [SerializeField] private Color missingColor = new Color(0.95f, 0.62f, 0.3f);
        [SerializeField] private Color invalidColor = new Color(0.9f, 0.3f, 0.27f);
        [SerializeField] private Color unavailableColor = new Color(0.7f, 0.7f, 0.7f);
        private readonly List<string> sessionResults = new List<string>();
        private VisualAction requestedAction;
        private VisualAnimation current;
        private VisualFamily family;
        private VisualStatus status;
        private string reason;
        private float cursor, breathingTime;
        private bool finished, paused, bound;
        private Vector3 baseScale, basePosition;
        private UnityEngine.Events.UnityAction[] actionCallbacks;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        public VisualStatus Status => status;
        public VisualAction RequestedAction => requestedAction;
        public VisualAnimation CurrentAnimation => current;
        public IReadOnlyList<string> SessionResults => sessionResults;
        public bool Finished => finished;
        public float Fps => fpsSlider.value;
        public float HoldSeconds => holdSlider.value;
        public int DisplayedFrame => current == null ? 0 : Mathf.Clamp(Mathf.FloorToInt(cursor), 0, current.FrameCount - 1) + 1;
        public VisualCombination Combination => new VisualCombination(headToggle.isOn, bodyToggle.isOn, armsToggle.isOn, legsToggle.isOn, normalTailToggle.isOn, flameTailToggle.isOn);

        private void OnEnable()
        {
            if (library == null || playerVisualRoot == null || spriteRenderer == null || previewAnimator == null || headToggle == null || bodyToggle == null || armsToggle == null || legsToggle == null || normalTailToggle == null || flameTailToggle == null || fpsSlider == null || holdSlider == null || debugText == null || statusText == null || fpsText == null || holdText == null || sessionText == null || actionButtons == null || actionButtons.Length != 6 || System.Array.Exists(actionButtons, item => item == null) || passButton == null || failButton == null || replayButton == null || previousButton == null || nextButton == null || pauseButton == null)
            {
                Debug.LogError("[ART TEST] Missing required scene reference on PlayerVisualTestHarness.", this);
                enabled = false;
                return;
            }
            baseScale = playerVisualRoot.localScale;
            basePosition = playerVisualRoot.localPosition;
            headToggle.onValueChanged.AddListener(OnBodyChanged);
            bodyToggle.onValueChanged.AddListener(OnBodyChanged);
            armsToggle.onValueChanged.AddListener(OnBodyChanged);
            legsToggle.onValueChanged.AddListener(OnBodyChanged);
            normalTailToggle.onValueChanged.AddListener(OnNormalTailChanged);
            flameTailToggle.onValueChanged.AddListener(OnFlameTailChanged);
            fpsSlider.onValueChanged.AddListener(OnSpeedChanged);
            holdSlider.onValueChanged.AddListener(OnHoldChanged);
            actionCallbacks = new UnityEngine.Events.UnityAction[6];
            for (int i = 0; i < actionButtons.Length; i++)
            {
                VisualAction action = (VisualAction)i;
                actionCallbacks[i] = () => Request(action);
                actionButtons[i].onClick.AddListener(actionCallbacks[i]);
            }
            passButton.onClick.AddListener(Pass);
            failButton.onClick.AddListener(Fail);
            replayButton.onClick.AddListener(Replay);
            previousButton.onClick.AddListener(PreviousFrame);
            nextButton.onClick.AddListener(NextFrame);
            pauseButton.onClick.AddListener(TogglePause);
            bound = true;
            Request(VisualAction.Idle);
        }

        private void OnDisable()
        {
            if (!bound)
            {
                return;
            }
            headToggle.onValueChanged.RemoveListener(OnBodyChanged);
            bodyToggle.onValueChanged.RemoveListener(OnBodyChanged);
            armsToggle.onValueChanged.RemoveListener(OnBodyChanged);
            legsToggle.onValueChanged.RemoveListener(OnBodyChanged);
            normalTailToggle.onValueChanged.RemoveListener(OnNormalTailChanged);
            flameTailToggle.onValueChanged.RemoveListener(OnFlameTailChanged);
            fpsSlider.onValueChanged.RemoveListener(OnSpeedChanged);
            holdSlider.onValueChanged.RemoveListener(OnHoldChanged);
            for (int i = 0; i < actionButtons.Length; i++)
            {
                actionButtons[i].onClick.RemoveListener(actionCallbacks[i]);
            }
            passButton.onClick.RemoveListener(Pass);
            failButton.onClick.RemoveListener(Fail);
            replayButton.onClick.RemoveListener(Replay);
            previousButton.onClick.RemoveListener(PreviousFrame);
            nextButton.onClick.RemoveListener(NextFrame);
            pauseButton.onClick.RemoveListener(TogglePause);
            if (graph.IsValid())
            {
                graph.Destroy();
            }
            RestoreTransform();
            bound = false;
        }

        private void OnBodyChanged(bool value)
        {
            Request(requestedAction);
        }
        private void OnNormalTailChanged(bool value)
        {
            if (value)
            {
                flameTailToggle.SetIsOnWithoutNotify(false);
            }
            Request(requestedAction);
        }
        private void OnFlameTailChanged(bool value)
        {
            if (value)
            {
                normalTailToggle.SetIsOnWithoutNotify(false);
            }
            Request(requestedAction);
        }

        /// <summary>仅主线程测试请求；返回状态通过 Status 查看，失败清空画面防止旧图冒充。</summary>
        public void Request(VisualAction action)
        {
            requestedAction = action;
            status = library.Resolve(Combination, action, out family, out current, out reason);
            cursor = 0f;
            breathingTime = 0f;
            finished = false;
            paused = false;
            if (graph.IsValid())
            {
                graph.Destroy();
            }
            RestoreTransform();
            spriteRenderer.sprite = null;
            if (status == VisualStatus.OK)
            {
                fpsSlider.SetValueWithoutNotify(Mathf.Clamp(current.Clip.frameRate, fpsSlider.minValue, fpsSlider.maxValue));
                if (current.FrameCount == 1 && action != VisualAction.Idle)
                {
                    holdSlider.SetValueWithoutNotify(Mathf.Clamp(1f / current.Clip.frameRate, holdSlider.minValue, holdSlider.maxValue));
                }
                if (action != VisualAction.Idle)
                {
                    graph = PlayableGraph.Create("PlayerVisualArtPreview");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    clipPlayable = AnimationClipPlayable.Create(graph, current.Clip);
                    clipPlayable.SetApplyFootIK(false);
                    var output = AnimationPlayableOutput.Create(graph, "Sprite Preview", previewAnimator);
                    output.SetSourcePlayable(clipPlayable);
                    graph.Play();
                }
                Sample();
            }
            RefreshText();
        }

        private void OnSpeedChanged(float value)
        {
            if (finished)
            {
                Replay();
            }
            RefreshText();
        }
        private void OnHoldChanged(float value)
        {
            if (current != null && current.FrameCount == 1 && requestedAction != VisualAction.Idle)
            {
                Replay();
            }
            RefreshText();
        }
        public void Replay()
        {
            cursor = 0f;
            finished = false;
            paused = false;
            Sample();
            RefreshText();
        }
        public void TogglePause()
        {
            paused = !paused; RefreshText();
        }
        public void PreviousFrame()
        {
            Step(-1);
        }
        public void NextFrame()
        {
            Step(1);
        }
        private void Step(int delta)
        {
            if (status != VisualStatus.OK)
            {
                return;
            }
            paused = true;
            finished = false;
            cursor = (Mathf.FloorToInt(cursor) + delta + current.FrameCount) % current.FrameCount;
            Sample();
            RefreshText();
        }
        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>真实秒推进本地预览，供独测使用；不设置全局时间，不产生玩法事件。</summary>
        public void Tick(float deltaSeconds)
        {
            if (!bound || status != VisualStatus.OK || paused)
            {
                return;
            }
            if (requestedAction == VisualAction.Idle)
            {
                breathingTime += deltaSeconds;
                float scale = 1f + Mathf.Sin(breathingTime * Mathf.PI * 2f / Mathf.Max(0.1f, breathingPeriod)) * breathingAmplitude;
                playerVisualRoot.localScale = baseScale * scale;
                return;
            }
            if (!finished)
            {
                float effectiveFps = current.FrameCount == 1 ? 1f / HoldSeconds : Fps;
                cursor += Mathf.Max(0f, deltaSeconds) * effectiveFps;
                if (current.Loop)
                {
                    cursor %= current.FrameCount;
                }
                else if (cursor >= current.FrameCount)
                {
                    cursor = current.FrameCount - 1; finished = true;
                }
                Sample();
                if (finished && returnToRest)
                {
                    VisualAnimation rest = family.Find(VisualAction.Idle);
                    if (rest != null && rest.IsReady)
                    {
                        spriteRenderer.sprite = rest.Frames[0];
                    }
                }
                RefreshText();
            }
        }
        private void Sample()
        {
            if (status != VisualStatus.OK || current == null)
            {
                return;
            }
            float sampleTime = Mathf.Clamp(cursor, 0f, current.FrameCount - 1) / current.Clip.frameRate;
            if (requestedAction == VisualAction.Idle)
            {
                spriteRenderer.sprite = current.Frames[0];
            }
            else if (graph.IsValid())
            {
                clipPlayable.SetTime(sampleTime);
                graph.Evaluate(0f);
            }
        }
        private void RestoreTransform()
        {
            if (playerVisualRoot == null)
            {
                return;
            }
            playerVisualRoot.localScale = baseScale;
            playerVisualRoot.localPosition = basePosition;
        }
        private void RefreshText()
        {
            bool single = status == VisualStatus.OK && current.FrameCount == 1 && requestedAction != VisualAction.Idle;
            fpsSlider.interactable = status == VisualStatus.OK && !single && requestedAction != VisualAction.Idle;
            holdSlider.interactable = single;
            fpsText.text = requestedAction == VisualAction.Idle && status == VisualStatus.OK ? "FPS: static Idle" : $"FPS: {(single ? 1f / HoldSeconds : Fps):0.##}";
            holdText.text = $"1F hold: {HoldSeconds:0.000}s" + (single ? "" : " (1F Bite / Fire)");
            statusText.color = status == VisualStatus.OK ? okColor : status == VisualStatus.MISSING ? missingColor : status == VisualStatus.INVALID ? invalidColor : unavailableColor;
            statusText.text = status == VisualStatus.MISSING && family == null ? "MISSING VISUAL" : status.ToString();
            string playback = paused ? "PAUSED / frame inspection" : finished ? (returnToRest ? "FINISHED / rest pose" : "FINISHED / last frame") : requestedAction == VisualAction.Idle ? "STATIC + BREATHING" : current != null && current.Loop ? "LOOP" : "PLAYING ONCE";
            debugText.text = $"Current Combination:\n{Combination.Label}\n\nState Key:\nHead={(Combination.Head ? 1 : 0)} {Combination.Key}\n\nRequested Action: {requestedAction}\nResolved Prefix: {family?.Prefix ?? "—"}\nResolved Clip: {current?.Clip?.name ?? "—"}\nSource PNG: {current?.SourceName ?? "—"}\nFrames: {(status == VisualStatus.OK ? current.FrameCount : 0)}    Frame: {(status == VisualStatus.OK ? DisplayedFrame : 0)}\n{fpsText.text}\nPlayback: {(status == VisualStatus.OK ? playback : "STOPPED / no substitute")}\n\n{reason}";
            sessionText.text = sessionResults.Count == 0 ? "Session results: 0\nPASS / FAIL writes to Console." : $"Session results: {sessionResults.Count}\n{sessionResults[sessionResults.Count - 1]}";
        }
        public void Pass()
        {
            Record(true);
        }
        public void Fail()
        {
            Record(false);
        }
        private void Record(bool passed)
        {
            string speed = current != null && current.FrameCount == 1 && requestedAction != VisualAction.Idle ? $"{HoldSeconds:0.000}s hold" : requestedAction == VisualAction.Idle ? "static" : $"{Fps:0.##}fps";
            string result = $"[ART TEST {(passed ? "PASS" : "FAIL")}] {family?.Prefix ?? Combination.Key} / {requestedAction} / {speed} / {status} / {current?.Clip?.name ?? "no clip"}";
            sessionResults.Add(result);
            Debug.Log(result, this);
            RefreshText();
        }
    }
}
