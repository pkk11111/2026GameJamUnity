// 职责：卡面固定标题/正文、整卡旋转及焦点高光；不提交选择或修改奖励。
// 依赖：uGUI、TMP、CardVisualStyle；维护：Dada；交接：docs/handoffs/Dada.handoff；规范：AGENTS.md。
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Regrowth.UI.Art
{
    [RequireComponent(typeof(Button))]
    public sealed class CardVisual : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField, Tooltip("共享三种卡面参数；更改后重新布局生效。")]
        private CardVisualStyle style;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private RectTransform art;
        [SerializeField] private RectTransform contentArea, iconArea, descriptionArea, titleArea;
        [SerializeField] private GameObject selectedGlow;
        [SerializeField, Tooltip("左0、中1、右2。只改变根旋转，卡内坐标完全一致。")]
        private int variant = 1;
        [SerializeField, Tooltip("真实菜单开启；ArtTest由测试选择按钮控制高光。")]
        private bool followFocus = true;
        [SerializeField, Range(0,1), Tooltip("不可交互卡的整体透明度；只显示Button当前状态，不决定业务资格。")]
        private float disabledAlpha = .45f;
        [SerializeField] private CanvasGroup visualGroup;
        private bool focused, manualSelected;
        public TMP_Text Title => title;
        public TMP_Text DescriptionText => descriptionText;
        public RectTransform Art => art;
        public Image Icon => art ? art.GetComponent<Image>() : null;
        public bool IsSelected => selectedGlow && selectedGlow.activeSelf;
        public int Variant => variant;
        private void OnEnable() { ApplyVariant(variant); }
        private void LateUpdate()
        {
            bool available = GetComponent<Button>().interactable;
            if (visualGroup) { visualGroup.alpha = available ? 1 : disabledAlpha; }
            if (followFocus) { SetSelected(focused && available); }
            else if (selectedGlow) { selectedGlow.SetActive(manualSelected && available); }
        }
        /// <summary>只更新两个TMP文本，绝不重算标题坐标。正式ChoiceCardView仍直接填写原TMP引用。</summary>
        public void SetContent(string heading, string body) { title.text = heading; descriptionText.text = body; }
        public void SetArtwork(Sprite sprite)
        {
            if (Icon)
            {
                Icon.sprite = sprite;
                Icon.enabled = sprite;
                Icon.color = Color.white;
            }
        }
        /// <summary>卡列布局调用；固定锚点/尺寸，无AutoSize或文字驱动布局。</summary>
        public void ApplyVariant(int index)
        {
            if (!style || !frame || !title || !descriptionText || !selectedGlow) { return; }
            variant = Mathf.Clamp(index,0,2);
            var root = (RectTransform)transform;
            root.sizeDelta = style.size; root.localRotation = Quaternion.Euler(0,0,style.rootAngles[variant]);
            frame.sprite = style.frame;
            Fixed(frame.rectTransform,Vector2.zero,style.size);
            // 三列复用同一张正卡框，只有根旋转；所有子节点始终共享同一局部坐标。
            Fixed(contentArea,Vector2.zero,style.size);
            Fixed(titleArea,style.titlePosition,style.titleSize);
            Fixed(descriptionArea,style.descriptionPosition,style.descriptionSize);
            Fixed(iconArea,style.iconPosition,style.iconSize);
            Fixed(title.rectTransform,Vector2.zero,style.titleSize);
            Fixed(descriptionText.rectTransform,Vector2.zero,style.descriptionSize);
            if (art) { Fixed(art,Vector2.zero,style.iconSize); }
            title.enableAutoSizing = descriptionText.enableAutoSizing = false;
            title.fontSize = style.titlePointSize; descriptionText.fontSize = style.descriptionPointSize;
            var glow = (RectTransform)selectedGlow.transform;
            Fixed(glow,style.glowPosition,style.glowSize);
        }
        private static void Fixed(RectTransform r,Vector2 pos,Vector2 size)
        {
            if (!r) { return; }
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f);
            r.anchoredPosition = pos; r.sizeDelta = size; r.localRotation = Quaternion.identity; r.localScale = Vector3.one;
        }
        public void SetSelected(bool value)
        {
            manualSelected=value;
            if (selectedGlow) { selectedGlow.SetActive(value && GetComponent<Button>().interactable); }
        }
        public void OnSelect(BaseEventData e) { focused = true; RefreshGlow(); }
        public void OnDeselect(BaseEventData e) { focused = false; RefreshGlow(); }
        public void OnPointerEnter(PointerEventData e) { if (followFocus && GetComponent<Button>().interactable) { GetComponent<Button>().Select(); } }
        public void OnPointerExit(PointerEventData e) { }
        private void RefreshGlow() { if (followFocus) { SetSelected(focused && GetComponent<Button>().interactable); } }
        private void OnDisable() { focused = false; if (followFocus) { SetSelected(false); } }
    }
}
