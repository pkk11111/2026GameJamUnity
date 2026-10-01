// 职责：原地导入卡图、统一CardRoot布局、接线真实Choice与现有ArtTest；不写奖励配置。
// 依赖UnityEditor、既有UI表现；维护Dada；交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Regrowth.UI;
using Regrowth.UI.Art;
using Object = UnityEngine.Object;

namespace Regrowth.Tests.UIArt.Editor
{
    public static class UICardIconsRevision
    {
        public const string CatalogPath = "Assets/Art/UI/Cards/CardPresentationCatalog.asset";
        private const string Icons = "Assets/Art/UI/Cards/Icons/";
        private static CardPresentationCatalog catalog;
        private static CardVisualStyle style;
        public static void RefreshCopy() { MakeCatalog(); AssetDatabase.SaveAssets(); }

        [MenuItem("PAWGATORY/UI Art/Apply card icons and unified layout")]
        public static void Apply()
        {
            ImportIcons();
            MakeCatalog();
            style = AssetDatabase.LoadAssetAtPath<CardVisualStyle>(UIFinalRevision.StylePath);
            var straightFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Cards/UI_Card_Frame_Center.png");
            style.frame = straightFrame;
            style.size = new Vector2(415,614);
            style.rootAngles = new Vector3(4,0,-4);
            style.titlePosition = new Vector2(0,-220);
            style.descriptionPosition = new Vector2(0,-119);
            style.titleSize = new Vector2(280, 58);
            style.descriptionSize = new Vector2(280, 108);
            style.iconPosition = new Vector2(0, 45);
            style.iconSize = new Vector2(280, 430.5f);
            EditorUtility.SetDirty(style);

            const string prefab = "Assets/Prefabs/Choice/ChoiceCard.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var view = root.GetComponent<ChoiceCardView>();
                Configure(root.GetComponent<CardVisual>(), 1);
                UIFinalRevision.Set(view, "presentation", catalog);
                PrefabUtility.SaveAsPrefabAsset(root, prefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var scene = EditorSceneManager.OpenScene(UIFinalRevision.ArtScene);
            var preview = Object.FindFirstObjectByType<UIArtPreview>();
            var cards = UIFinalRevision.Refs<CardVisual>(preview, "cardVisuals");
            for (int i = 0; i < cards.Length; i++)
            {
                Configure(cards[i], i);
            }
            UIFinalRevision.Set(preview, "cardCatalog", catalog);
            var serialized = new SerializedObject(preview);
            string[] keys = { "GrowLegs", "GrowArms", "GrowTail" };
            SetStrings(serialized, "cardTitles", keys.Select(key => catalog.FindArtwork(key).previewTitle).ToArray());
            SetStrings(serialized, "cardDescriptions", keys.Select(key => catalog.FindArtwork(key).previewDescription).ToArray());
            serialized.FindProperty("longTitle").stringValue = "Regrow Legs\n+ Heal";
            serialized.FindProperty("longDescription").stringValue = "Regrow legs.\nGain a double jump.\nRestore HP.\nUses 1 slot.";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var layout = cards[0].transform.parent.GetComponent<ChoiceCardLayout>();
            if (!layout) { layout = cards[0].transform.parent.gameObject.AddComponent<ChoiceCardLayout>(); }
            layout.Refresh();
            AddGalleryControls(preview);
            preview.NormalCopy();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("UI_CARD_ICONS_MIGRATION_DONE");
        }

        private static void ImportIcons()
        {
            AssetDatabase.Refresh();
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { Icons.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            if (paths.Length != 20) { throw new Exception("Expected exactly 20 card icons"); }
            foreach (var path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static CardPresentationCatalog.Artwork Art(string key, string title, string description)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + "UI_Card_Icon_" + key + ".png");
            if (!sprite) { throw new Exception("Missing explicit icon: " + key); }
            return new CardPresentationCatalog.Artwork
            { keyword = key, sprite = sprite, previewTitle = title, previewDescription = description };
        }

        private static CardPresentationCatalog.Binding Bind(string id, string key, string original, string title, string description, string regrowth = "")
        {
            return new CardPresentationCatalog.Binding
            { optionId = id, artworkKeyword = key, sourceDescription = original, shortTitle = title, shortDescription = description, regrowthTitle = regrowth };
        }

        private static void MakeCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<CardPresentationCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<CardPresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.artworks = new[]
            {
                Art("RestoreBody", "Restore Body", "Regrow your body.\nUnlock HP and bite."),
                Art("GrowLegs", "Grow Legs", "Double jump.\nUses 1 slot."),
                Art("GrowTail", "Grow Tail", "Gain a dash.\nUses 1 slot."),
                Art("GrowArms", "Grow Arms", "Arms and sword.\nSlash attack.\nUses 1 slot."),
                Art("GrowFlameTail", "Grow Flame Tail", "Fire orb (Q / RMB).\nReplaces tail / dash.\nUses 1 slot."),
                Art("RestoreHP", "Restore HP", "Restore 20% max HP.\nFull HP: no healing."),
                Art("MaxHPUp", "Max HP Up", "Max and current HP\n+10% max HP."),
                Art("AttackUp", "Attack Up", "All attacks +10.\nIncludes fire ticks."),
                Art("LoseLegs", "Lose Legs", "Lose legs\nand double jump."),
                Art("LoseTail", "Lose Tail", "Lose tail\nand dash."),
                Art("LoseFlameTail", "Lose Flame Tail", "Lose flame tail\nand fire breath."),
                Art("LoseArms", "Lose Arms", "Lose arms and sword.\nReturn to bite."),
                Art("PayHP", "Pay HP", "Pay HP."),
                Art("MaxHPDown", "Max HP Down", "Max HP down."),
                Art("AttackDown", "Attack Down", "Attack down."),
                Art("EnemyHPUp", "Enemy HP Up", "Enemies gain HP."),
                Art("EnemyAttackUp", "Enemy Attack Up", "Enemies hit harder."),
                Art("RegrowLegsAndHeal", "Regrow Legs\n+ Heal", "Regrow legs.\nGain a double jump.\nRestore HP.\nUses 1 slot."),
                Art("MaxHPUpEnhanced", "Greater Max HP", "Gain more max HP."),
                Art("AttackUpEnhanced", "Greater Attack", "Gain more attack.")
            };
            var config = AssetDatabase.LoadAssetAtPath<Regrowth.Gameplay.ChestRewardConfig>("Assets/Configs/Chest/V5_WhiteboxRewards.asset");
            string[] ids={"legs","arms","tail","flame-tail","heal","max-health","attack"};
            string[] keys={"GrowLegs","GrowArms","GrowTail","GrowFlameTail","RestoreHP","MaxHPUp","AttackUp"};
            var bindings=new System.Collections.Generic.List<CardPresentationCatalog.Binding>();
            for(int i=0;i<ids.Length;i++)
            {
                var reward=config.Rewards.Single(r=>r.Id==ids[i]); var artwork=catalog.FindArtwork(keys[i]);
                string copy=artwork.previewDescription;
                // Legacy fixed-value requests retain their own semantics; current map numbers are formatted from the runtime option.
                if(ids[i]=="heal") { copy="Restore 25 HP.\nFull HP: no healing."; }
                if(ids[i]=="max-health") { copy="Max HP +15.\nNo healing."; }
                bindings.Add(Bind(ids[i],keys[i],reward.Description,artwork.previewTitle,copy,artwork.previewTitle.Replace("Grow ","Regrow ")));
            }
            string[] heldIds={"201","202","203","204"}; string[] lose={"LoseLegs","LoseArms","LoseTail","LoseFlameTail"};
            string[] parts={"Legs","Arms","Tail","Flame Tail"};
            for(int i=0;i<4;i++) { bindings.Add(Bind(heldIds[i],lose[i],"Replace this held item.","Replace "+parts[i],"Lose "+parts[i].ToLowerInvariant()+".\nGain chosen reward.")); }
            bindings.Add(Bind("swap-tail","","Lose the old tail and its ability; gain the selected tail.","Swap Tail","Lose current tail.\nGain selected tail."));
            string[] costIds={"COST_SHED_LEGS","COST_SHED_ARMS","COST_SHED_TAIL","COST_SHED_FLAME_TAIL","COST_CURRENT_HP","COST_MAX_HP","COST_ATTACK","COST_ENEMY_HP","COST_ENEMY_ATTACK"};
            string[] costKeys={"LoseLegs","LoseArms","LoseTail","LoseFlameTail","PayHP","MaxHPDown","AttackDown","EnemyHPUp","EnemyAttackUp"};
            for(int i=0;i<costIds.Length;i++)
            {
                var a=catalog.FindArtwork(costKeys[i]);
                bindings.Add(Bind(costIds[i],costKeys[i],i<4 ? "Lose this body part and its ability. Free one slot; it may be regrown later." : "",a.previewTitle,a.previewDescription));
            }
            catalog.bindings = bindings.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        private static void Configure(CardVisual visual, int variant)
        {
            visual.name = "CardRoot";
            UIFinalRevision.Set(visual, "style", style);
            visual.Title.name = "TitleBannerText";
            visual.DescriptionText.name = "DescriptionText";
            visual.Art.name = "Icon";
            var oldText = visual.Art.GetComponent<TMP_Text>();
            if (oldText) { Object.DestroyImmediate(oldText); }
            foreach (var child in visual.Art.GetComponentsInChildren<TMP_Text>(true)) { Object.DestroyImmediate(child.gameObject); }
            var icon = visual.Art.GetComponent<Image>();
            if (!icon) { icon = visual.Art.gameObject.AddComponent<Image>(); }
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = icon.sprite;
            var content = Area(visual.transform,"ContentArea");
            var iconArea = Area(content,"IconArea");
            var descriptionArea = Area(content,"DescriptionArea");
            var titleArea = Area(content,"TitleArea");
            visual.Title.transform.SetParent(titleArea, false);
            visual.DescriptionText.transform.SetParent(descriptionArea, false);
            visual.Art.SetParent(iconArea, false);
            UIFinalRevision.Set(visual,"contentArea",content);
            UIFinalRevision.Set(visual,"iconArea",iconArea);
            UIFinalRevision.Set(visual,"descriptionArea",descriptionArea);
            UIFinalRevision.Set(visual,"titleArea",titleArea);
            visual.transform.Find("SelectedGlow").SetSiblingIndex(0);
            visual.transform.Find("Frame").SetSiblingIndex(1);
            content.SetSiblingIndex(2);
            foreach(var layout in visual.GetComponentsInChildren<LayoutGroup>(true)) { Object.DestroyImmediate(layout); }
            foreach(var fitter in visual.GetComponentsInChildren<ContentSizeFitter>(true)) { Object.DestroyImmediate(fitter); }
            visual.Title.fontSize = 24;
            visual.DescriptionText.fontSize = 22;
            visual.Title.enableAutoSizing = visual.DescriptionText.enableAutoSizing = false;
            visual.Title.alignment = TextAlignmentOptions.Center;
            visual.DescriptionText.alignment = TextAlignmentOptions.Top;
            visual.Title.textWrappingMode = visual.DescriptionText.textWrappingMode = TextWrappingModes.Normal;
            visual.Title.overflowMode = visual.DescriptionText.overflowMode = TextOverflowModes.Truncate;
            visual.ApplyVariant(variant);
            EditorUtility.SetDirty(visual);
        }

        private static RectTransform Area(Transform parent, string name)
        {
            var area = parent.Find(name) as RectTransform;
            if (!area) { area = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); area.SetParent(parent,false); }
            return area;
        }

        private static void AddGalleryControls(UIArtPreview preview)
        {
            var panel = UIFinalRevision.Ref<GameObject>(preview, "debugPanel").transform;
            var existing = panel.Find("IconPrevious");
            if (!existing)
            {
                var sample = panel.Find("CopyNormal").GetComponent<Button>();
                float bottom = panel.Cast<RectTransform>().Max(child => -child.anchoredPosition.y + child.sizeDelta.y) + 12;
                AddButton(sample, panel, "IconPrevious", "Prev icons", 15, bottom, preview.PreviousIcons);
                AddButton(sample, panel, "IconNext", "Next icons", 200, bottom, preview.NextIcons);
                var label = Object.Instantiate(sample.GetComponentInChildren<TMP_Text>(), panel);
                label.name = "IconGalleryLabel";
                var rt = label.rectTransform;
                UIFinalRevision.Fixed(rt, new Vector2(195, -bottom - 65), new Vector2(370, 36));
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                label.fontSize = 16;
                UIFinalRevision.Set(preview, "catalogLabel", label);
                var panelRect = (RectTransform)panel;
                panelRect.sizeDelta = new Vector2(390, bottom + 92);
                panelRect.localScale = Vector3.one * Mathf.Min(1, 950 / panelRect.sizeDelta.y);
            }
            panel.Find("CopyNormal").GetComponentInChildren<TMP_Text>().text = "Live copy";
            panel.Find("CopyLong").GetComponentInChildren<TMP_Text>().text = "4-line sample";
        }

        private static void AddButton(Button sample, Transform parent, string name, string label, float x, float y, UnityAction action)
        {
            var button = Object.Instantiate(sample, parent);
            button.name = name;
            ((RectTransform)button.transform).anchoredPosition = new Vector2(x, -y);
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick, action);
            button.GetComponentInChildren<TMP_Text>().text = label;
        }

        private static void SetStrings(SerializedObject owner, string field, string[] values)
        {
            var array = owner.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) { array.GetArrayElementAtIndex(i).stringValue = values[i]; }
        }
    }
}
