// 职责：主界面Image播放既有Walk Clip中导出的原Sprite序列，保持原帧序和帧率。
// 依赖：uGUI；维护Dada，交接docs/handoffs/Dada.handoff；不驱动角色玩法；规范AGENTS.md。
using UnityEngine;
using UnityEngine.UI;
namespace Regrowth.UI.Art
{
    [RequireComponent(typeof(Image))]
    public sealed class UIWalkPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("来源Clip留作核对；Image引用为其原始Sprite曲线顺序。")]
        private AnimationClip sourceClip;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(.01f)] private float frameDuration = .125f;
        [SerializeField] private bool playing = true;
        private double elapsed;
        private Image image;
        public int FrameIndex => frames == null || frames.Length == 0 ? 0 : (int)(elapsed/frameDuration)%frames.Length;
        private void Awake() { image=GetComponent<Image>(); }
        private void Update()
        {
            if (!playing || frames == null || frames.Length == 0) { return; }
            elapsed+=Time.unscaledDeltaTime; image.sprite=frames[FrameIndex];
        }
        public void SetPlaying(bool value) { playing=value; }
    }
}
