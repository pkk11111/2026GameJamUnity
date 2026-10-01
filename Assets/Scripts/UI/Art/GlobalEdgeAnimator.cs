// 职责：UI Art 的全局三帧边缘与轻微呼吸；时间相位不依赖任何页面。
// 依赖：uGUI；维护：UI art test / docs/handoffs/Dada.handoff；规则：AGENTS.md。
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.UI.Art
{
    /// <summary>每个生命周期持有独立时钟；单帧HUD素材只做呼吸，多帧Login素材循环。</summary>
    [RequireComponent(typeof(Image))]
    public sealed class GlobalEdgeAnimator : MonoBehaviour
    {
        [SerializeField, Tooltip("按顺序循环的三张边框；必须在场景持久绑定。")]
        private Sprite[] frames;
        [SerializeField, Min(.1f), Tooltip("每帧秒数；暂定0.8，Play实时可调。")]
        private float frameDuration = .8f;
        [SerializeField, Range(0, .005f), Tooltip("缩放呼吸幅度；实时生效。")]
        private float breathScale = .003f;
        [SerializeField, Min(.1f), Tooltip("完整呼吸周期，单位秒。")]
        private float breathPeriod = 8f;
        [SerializeField, Tooltip("最大位置扰动，Canvas参考像素。")]
        private Vector2 positionAmplitude = new Vector2(.6f, 0);
        [SerializeField, Range(0, 1), Tooltip("呼吸最低Alpha；最高为1。")]
        private float minimumAlpha = .97f;
        [SerializeField, Tooltip("由测试面板开关控制，不随切页改变。")]
        private bool animate = true;
        private Image image;
        private double elapsed;
        public double Elapsed => elapsed;
        public bool IsAnimating => animate;
        public int FrameIndex => frames == null || frames.Length == 0 ? 0 : (int)(elapsed / Mathf.Max(.1f, frameDuration)) % frames.Length;

        private void Awake()
        {
            image = GetComponent<Image>();
            image.raycastTarget = false;
            if (frames == null || frames.Length == 0 || System.Array.Exists(frames, f => !f))
            {
                Debug.LogError("GlobalEdgeAnimator: missing frame references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!animate) { return; }
            elapsed += Time.unscaledDeltaTime;
            image.sprite = frames[FrameIndex];
            float wave = Mathf.Sin((float)(elapsed * Mathf.PI * 2 / Mathf.Max(.1f, breathPeriod)));
            image.rectTransform.localScale = Vector3.one * (1 + breathScale * (wave + 1));
            image.rectTransform.anchoredPosition = positionAmplitude * wave;
            image.color = new Color(1, 1, 1, Mathf.Lerp(minimumAlpha, 1, (wave + 1) * .5f));
        }

        /// <summary>只改播放开关；页面切换不调用此方法，也不重置时间。</summary>
        public void SetAnimating(bool value) { animate = value; }

        /// <summary>仅开始新Gameplay会话时调用；开关Choice Modal不得调用。</summary>
        public void BeginSession()
        {
            elapsed = 0;
            if (!image) { image = GetComponent<Image>(); }
            image.sprite = frames[0];
            image.rectTransform.localScale = Vector3.one;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.color = Color.white;
        }

        /// <summary>面板指定帧。保持累计时间，清除呼吸偏移便于比较原图。</summary>
        public void ShowFrame(int index)
        {
            if (!image) { image = GetComponent<Image>(); }
            if (frames == null || index < 0 || index >= frames.Length) { return; }
            animate = false;
            image.sprite = frames[index];
            image.rectTransform.localScale = Vector3.one;
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.color = Color.white;
        }
    }
}
