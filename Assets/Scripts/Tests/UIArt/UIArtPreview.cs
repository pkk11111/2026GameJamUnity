// 职责：独立 UI 美术测试的页面、边框、HP、身体显隐与卡牌选择；不写玩法状态。
// 依赖：uGUI / TMP；维护：UI art test，docs/handoffs/Dada.handoff；规则：AGENTS.md。
using TMPro;
using Regrowth.UI.Art;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Regrowth.Tests.UIArt
{
    /// <summary>仅供 UI_ArtTest 场景。按钮通过持久化 UnityEvent 接线，无设备轮询或业务依赖。</summary>
    public sealed class UIArtPreview : MonoBehaviour
    {
        [Header("页面与定位（生成场景时接线）")]
        [SerializeField] private GameObject login, hud, cards, debugPanel;
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private RectTransform canvasRect, loginBoard, cardBoard, hpGroup, bodyGroup;
        [SerializeField] private Image background, hpFill, overlay;
        [SerializeField] private Sprite overlayPng;
        [FormerlySerializedAs("globalEdge")]
        [SerializeField] private GlobalEdgeAnimator loginEdge;
        [SerializeField] private GlobalEdgeAnimator gameplayEdge;
        [SerializeField] private CardVisual[] cardVisuals;
        [SerializeField] private GameObject torso, armsBase, armsActive, legs, tail, flame;
        [SerializeField] private TMP_Text hpText, resolutionText, pageText, swordLabel, fireLabel, legsLabel;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Toggle headToggle, torsoToggle, armsToggle, legsToggle, tailToggle, flameToggle, motionToggle, pngToggle;
        [Header("暂定美术参数，Play 中实时生效")]
        [Tooltip("HP 数字的视觉最大值，不连接游戏生命")]
        [SerializeField, Min(1)] private int previewMaxHp = 200;
        [SerializeField] private UIWalkPlayer loginWalk;
        [SerializeField] private string[] cardTitles;
        [SerializeField, TextArea] private string[] cardDescriptions;
        [SerializeField] private string longTitle;
        [SerializeField, TextArea] private string longDescription;
        [SerializeField] private CardPresentationCatalog cardCatalog;
        [SerializeField] private TMP_Text catalogLabel;
        private int catalogStart;
        private int page, selected = 2;
        public void NormalCopy()
        {
            string[] keywords = { "GrowLegs", "GrowArms", "GrowTail" };
            for (int i = 0; i < cardVisuals.Length; i++)
            {
                cardVisuals[i].SetContent(cardTitles[i], cardDescriptions[i]);
                cardVisuals[i].SetArtwork(cardCatalog ? cardCatalog.FindArtwork(keywords[i])?.sprite : null);
            }
            if (catalogLabel) { catalogLabel.text = "VISUAL ONLY — REWARD PREVIEW"; }
        }
        public void PreviousIcons() { ShowCatalog(catalogStart - 3); }
        public void NextIcons() { ShowCatalog(catalogStart + 3); }
        public void ShowCatalog(int start)
        {
            if (!cardCatalog || cardCatalog.artworks.Length == 0) { return; }
            catalogStart = (start % cardCatalog.artworks.Length + cardCatalog.artworks.Length) % cardCatalog.artworks.Length;
            ThreeCards();
            CardsPage();
            for (int i = 0; i < cardVisuals.Length; i++)
            {
                var entry = cardCatalog.artworks[(catalogStart + i) % cardCatalog.artworks.Length];
                cardVisuals[i].SetArtwork(entry.sprite);
                cardVisuals[i].SetContent(entry.previewTitle, entry.previewDescription);
            }
            if (catalogLabel) { catalogLabel.text = "VISUAL ONLY — ICON GALLERY  " + (catalogStart + 1) + " / " + cardCatalog.artworks.Length; }
        }
        public void LongCopy() { foreach (var c in cardVisuals) { c.SetContent(longTitle,longDescription); } }
        public void SingleCard() { for (int i=0;i<cardVisuals.Length;i++) { cardVisuals[i].gameObject.SetActive(i==1); } }
        public void ThreeCards() { foreach (var c in cardVisuals) { c.gameObject.SetActive(true); } }
        public void SetWalk(bool value) { loginWalk.SetPlaying(value); }
        public void SetCardsEnabled(bool value) { foreach(var c in cardVisuals) { c.GetComponent<Button>().interactable=value; } }
        public int Page => page;
        public int Selected => selected;

        private void Start() { NormalCopy(); ApplyBody(false); SetHp(hpSlider.value); SetOverlay(pngToggle.isOn); Select(2); Show(0); }
        private void Update()
        {
            Layout();
            if (!Application.isPlaying) return;
            resolutionText.text = Screen.width + " x " + Screen.height + "\nGame View > Resolution";
        }

        /// <summary>按实际 Canvas 尺寸等比布局。中央画板完整适配，HUD 始终贴安全边距。</summary>
        public void Layout()
        {
            if (!canvasRect) return;
            Vector2 size = canvasRect.rect.size;
            float fit = Mathf.Min(size.x / 1920, size.y / 1080);
            loginBoard.localScale = cardBoard.localScale = Vector3.one * fit;
            hpGroup.localScale = bodyGroup.localScale = Vector3.one * Mathf.Min(1, fit);
            hpGroup.anchoredPosition = new Vector2(35, -25);
            bodyGroup.anchoredPosition = new Vector2(37, 25);
            // 背景覆盖而不拉扁；边框单独全屏拉伸。
            float cover = Mathf.Max(size.x / 1920, size.y / 1080);
            background.rectTransform.sizeDelta = new Vector2(1920, 1080) * cover;
        }
        private void Show(int value)
        {
            page = value;
            if (value == 0)
            {
                cards.SetActive(false);
                gameplayRoot.SetActive(false);
                login.SetActive(true);
            }
            else
            {
                login.SetActive(false);
                if (!gameplayRoot.activeSelf)
                {
                    gameplayRoot.SetActive(true);
                    gameplayEdge.BeginSession();
                }
                // HUD与GameplayEdge属于会话，Choice只切自己的Modal显隐。
                cards.SetActive(value == 2);
            }
            pageText.text = new[] { "LOGIN", "HUD", "CARDS" }[value];
            Layout();
        }
        public void LoginPage() => Show(0);
        public void HudPage() => Show(1);
        public void CardsPage() => Show(2);
        public void TogglePanel() => debugPanel.SetActive(!debugPanel.activeSelf);
        private void Edge(int index) { motionToggle.isOn = false; loginEdge.ShowFrame(index); }
        public void SetEdgeAnimation(bool value) { loginEdge.SetAnimating(value); gameplayEdge.SetAnimating(value); }
        public void Edge01() => Edge(0);
        public void Edge02() => Edge(1);
        public void Edge03() => Edge(2);
        /// <summary>只改变 Image 填充与 TMP 数字，保持原始填充框尺寸。</summary>
        public void SetHp(float value)
        {
            hpFill.fillAmount = Mathf.Clamp01(value / 100);
            hpText.text = Mathf.RoundToInt(previewMaxHp * hpFill.fillAmount).ToString();
        }
        public void SetTail(bool value) { if (value) flameToggle.SetIsOnWithoutNotify(false); ApplyBody(false); }
        public void SetFlame(bool value) { if (value) tailToggle.SetIsOnWithoutNotify(false); ApplyBody(false); }
        /// <summary>所有部件均可独立检查，头固定；火焰尾包含尾巴，无手状态没有剑标签。</summary>
        public void ApplyBody(bool unused)
        {
            headToggle.SetIsOnWithoutNotify(true);
            torso.SetActive(torsoToggle.isOn);
            armsBase.SetActive(torsoToggle.isOn);
            armsActive.SetActive(armsToggle.isOn);
            legs.SetActive(legsToggle.isOn);
            tail.SetActive(tailToggle.isOn || flameToggle.isOn);
            flame.SetActive(flameToggle.isOn);
            swordLabel.gameObject.SetActive(armsToggle.isOn);
            fireLabel.gameObject.SetActive(flameToggle.isOn);
            legsLabel.gameObject.SetActive(legsToggle.isOn);
        }
        private void Select(int index)
        {
            selected = index;
            for (int i = 0; i < cardVisuals.Length; i++)
            {
                cardVisuals[i].SetSelected(i == index);
            }
        }
        public void SelectLeft() => Select(0);
        public void SelectCenter() => Select(1);
        public void SelectRight() => Select(2);
        public void SelectNone() => Select(-1);
        public void SetOverlay(bool png)
        {
            overlay.sprite = png ? overlayPng : null;
            overlay.color = png ? Color.white : new Color(0, 0, 0, .8f);
        }
    }
}
