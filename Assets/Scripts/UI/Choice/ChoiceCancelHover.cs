// 职责：Cancel按钮鼠标进入音效；点击由ChoicePanel接受取消后统一通知。
// 维护：audio-rewards；规范：根AGENTS.md。
using Regrowth.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Regrowth.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ChoiceCancelHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private bool inside;
        public void OnPointerEnter(PointerEventData data)
        {
            if (inside) return;
            inside = true;
            if (isActiveAndEnabled && GetComponent<Button>().IsInteractable()) GameAudio.Play(AudioCue.UIHovered);
        }
        public void OnPointerExit(PointerEventData data) => inside = false;
        private void OnDisable() => inside = false;
    }
}
