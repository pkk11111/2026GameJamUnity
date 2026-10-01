// 职责：仅修正卡面文字对齐和正式只读交互提示，保留业务绑定。
// 维护Dada；依赖UnityEditor/UI；交接docs/handoffs/Dada.handoff。
using System.Linq;
using Regrowth.Runtime;
using Regrowth.UI;
using Regrowth.UI.Art;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Regrowth.Tests.UIArt.Editor
{
    public static class UIAlignmentPromptRevision
    {
        public static void Apply()
        {
            const string cardPath = "Assets/Prefabs/Choice/ChoiceCard.prefab";
            var cardRoot = PrefabUtility.LoadPrefabContents(cardPath);
            try
            {
                Align(cardRoot.GetComponent<CardVisual>());
                PrefabUtility.SaveAsPrefabAsset(cardRoot, cardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(cardRoot); }
            var artScene = EditorSceneManager.OpenScene(UIFinalRevision.ArtScene);
            foreach (var card in Object.FindObjectsByType<CardVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { Align(card); }
            EditorSceneManager.MarkSceneDirty(artScene); EditorSceneManager.SaveScene(artScene);

            const string hudPath = "Assets/Prefabs/Hud/PlayerHud.prefab";
            var hudRoot = PrefabUtility.LoadPrefabContents(hudPath);
            try
            {
                var view = hudRoot.GetComponent<InteractionHintView>();
                if (!view) { view = hudRoot.AddComponent<InteractionHintView>(); }
                var existing = hudRoot.transform.Find("InteractionHint");
                var panel = existing ? existing.gameObject : new GameObject("InteractionHint", typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(hudRoot.transform, false);
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(0, 116); rect.sizeDelta = new Vector2(800, 68);
                var background = panel.GetComponent<Image>(); background.color = new Color(0, 0, 0, .88f); background.raycastTarget = false;
                var label = panel.GetComponentInChildren<TextMeshProUGUI>(true);
                if (!label)
                {
                    label = new GameObject("Prompt", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    label.transform.SetParent(panel.transform, false);
                }
                var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(18, 4); lr.offsetMax = new Vector2(-18, -4);
                label.font = UIFinalRevision.Ref<TMP_Text>(hudRoot.GetComponent<PlayerHud>(), "healthText").font;
                label.fontSize = 32; label.enableAutoSizing = false; label.color = new Color(1, .98f, .92f, 1);
                label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold;
                label.raycastTarget = false; label.text = string.Empty;
                UIFinalRevision.Set(view, "hintRoot", panel); UIFinalRevision.Set(view, "label", label);
                panel.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(hudRoot, hudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hudRoot); }
            var level = EditorSceneManager.OpenScene(UIFinalRevision.Level);
            var hint = Object.FindFirstObjectByType<InteractionHintView>();
            UIFinalRevision.Set(hint, "interactionSource", Object.FindFirstObjectByType<PlayerInteractor>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(hint);
            EditorSceneManager.MarkSceneDirty(level); EditorSceneManager.SaveScene(level);
            AssetDatabase.SaveAssets();
            Debug.Log("UI_ALIGNMENT_PROMPT_SAVED");
        }

        private static void Align(CardVisual card)
        {
            card.ApplyVariant(card.Variant);
            EditorUtility.SetDirty(card.Title); EditorUtility.SetDirty(card.DescriptionText);
        }
    }
}
