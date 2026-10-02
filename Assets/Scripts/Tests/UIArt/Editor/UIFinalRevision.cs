// 职责：原地更新ArtTest并迁移同一视觉至三个正式UI Prefab及Level的显示接线。
// 依赖：UnityEditor、UI.Art、既有UI业务；维护Dada，交接docs/handoffs/Dada.handoff；规范AGENTS.md。
// 仅显式编辑器入口；保留ChoicePanel/CardView/Button业务组件，不修改奖励或输入资产。
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
    public static class UIFinalRevision
    {
        public const string Level = "Assets/Scenes/Gameplay/MainLevel.unity";
        public const string ArtScene = "Assets/Scenes/Tests/Art/UI_ArtTest.unity";
        public const string StylePath = "Assets/Art/UI/Cards/CardVisualStyle.asset";
        private const string Root = "Assets/Art/UI/";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"Common/CascadiaMono_UI_SDF.asset");
        private static CardVisualStyle Style => AssetDatabase.LoadAssetAtPath<CardVisualStyle>(StylePath);
        [MenuItem("PAWGATORY/UI Art/Apply current visual revision")]
        public static void ApplyAll()
        {
            ApplyArtTest();
            MigrateCard(); MigrateMenu(); MigrateHud(); WireLevel();
            UICardIconsRevision.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("UI_FINAL_MIGRATION_DONE");
        }
        public static void ApplyArtTest()
        {
            EnsureStyle();
            var scene=EditorSceneManager.OpenScene(ArtScene);
            var p=Object.FindFirstObjectByType<UIArtPreview>();
            if (!p) { throw new Exception("Existing ArtTest required"); }
            var tool=p.transform.Find("TooltipLayer"); if (tool) { Object.DestroyImmediate(tool.gameObject); }
            foreach (var edge in p.GetComponentsInChildren<GlobalEdgeAnimator>(true))
            { Float(edge,"frameDuration",.8f); Float(edge,"breathPeriod",8); }
            string[] titles={"Grow Your Legs","Grow Your Arms","Grow Your Tail"};
            string[] descriptions={"Double jump. Legs occupy one slot.","Arms and sword share one slot. Basic attack becomes sword.","Dash. Tail occupies one slot."};
            var cards=Refs<CardVisual>(p,"cardVisuals");
            for (int i=0;i<cards.Length;i++)
            {
                var c=cards[i]; var title=Ref<TMP_Text>(c,"title");
                var desc=c.DescriptionText;
                if (!desc) { desc=Text(c.transform,"DescriptionText",22); }
                ConfigureText(title,24,Color.white,true); ConfigureText(desc,22,new Color(.14f,.105f,.065f),false);
                desc.alignment=TextAlignmentOptions.TopLeft;
                Set(c,"style",Style); Set(c,"descriptionText",desc); Bool(c,"followFocus",false);
                Set(c,"visualGroup",Ensure<CanvasGroup>(c.gameObject));
                var anchor=c.transform.Find("TooltipAnchor"); if (anchor) { Object.DestroyImmediate(anchor.gameObject); }
                var art=Ref<RectTransform>(c,"art");
                if (art.GetComponent<TMP_Text>()) { art.GetComponent<TMP_Text>().text=""; }
                Fixed(art,new Vector2(0,100),new Vector2(240,250));
                c.ApplyVariant(i); c.SetContent(titles[i],descriptions[i]); EditorUtility.SetDirty(c);
                foreach (var layout in c.GetComponentsInChildren<LayoutGroup>(true)) { Object.DestroyImmediate(layout); }
                foreach (var fit in c.GetComponentsInChildren<ContentSizeFitter>(true)) { Object.DestroyImmediate(fit); }
            }
            Strings(p,"cardTitles",titles); Strings(p,"cardDescriptions",descriptions);
            String(p,"longTitle","Regrow Your Arms and Sword");
            String(p,"longDescription","Arms and sword share one slot. Basic attack becomes sword. Replace one held item when all three slots are occupied.");
            var board=Ref<RectTransform>(p,"loginBoard");
            var placeholder=board.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="CharacterPlaceholder");
            if (placeholder) { placeholder.gameObject.SetActive(false); }
            var walk=board.Find("WalkingCharacter") as RectTransform;
            if (!walk) { walk=Rect(board,"WalkingCharacter",new Vector2(440,390)); }
            Fixed(walk,new Vector2(0,-160),new Vector2(440,390));
            var img=walk.GetComponent<Image>() ?? walk.gameObject.AddComponent<Image>(); img.preserveAspect=true; img.raycastTarget=false;
            var player=walk.GetComponent<UIWalkPlayer>() ?? walk.gameObject.AddComponent<UIWalkPlayer>();
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Characters/Player/Animations/Dog2_Move_Sword.anim");
            var curve=AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var keys=AnimationUtility.GetObjectReferenceCurve(clip,curve);
            Set(player,"sourceClip",clip); Array(player,"frames",keys.Select(k=>k.value).ToArray()); Float(player,"frameDuration",1/clip.frameRate);
            img.sprite=(Sprite)keys[0].value; Set(p,"loginWalk",player);
            ExpandControls(p);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void EnsureStyle()
        {
            if (Style) { return; }
            var style=ScriptableObject.CreateInstance<CardVisualStyle>();
            style.frame=Sprite("Cards/UI_Card_Frame_Center.png");
            AssetDatabase.CreateAsset(style,StylePath);
        }
        private static void ExpandControls(UIArtPreview p)
        {
            var panel=Ref<GameObject>(p,"debugPanel").transform;
            if (panel.Find("CopyNormal")) { return; }
            var rt=(RectTransform)panel; float bottom=0;
            foreach (RectTransform child in panel) { bottom=Mathf.Max(bottom,-child.anchoredPosition.y+child.sizeDelta.y); }
            bottom+=16;
            ButtonAt(panel,"CopyNormal","Normal text",15,bottom,p.NormalCopy);
            ButtonAt(panel,"CopyLong","Long text",200,bottom,p.LongCopy);
            ButtonAt(panel,"OneCard","1 card",15,bottom+46,p.SingleCard);
            ButtonAt(panel,"ThreeCards","3 cards",200,bottom+46,p.ThreeCards);
            ButtonAt(panel,"WalkPlay","Walk play",15,bottom+92,()=>p.SetWalk(true));
            ButtonAt(panel,"WalkPause","Walk pause",200,bottom+92,()=>p.SetWalk(false));
            // Lambda cannot persist in UnityEvent; use supported static bool persistent listeners.
            var play=panel.Find("WalkPlay").GetComponent<Button>(); play.onClick=new Button.ButtonClickedEvent(); UnityEventTools.AddBoolPersistentListener(play.onClick,p.SetWalk,true);
            var pause=panel.Find("WalkPause").GetComponent<Button>(); pause.onClick=new Button.ButtonClickedEvent(); UnityEventTools.AddBoolPersistentListener(pause.onClick,p.SetWalk,false);
            ButtonAt(panel,"CardsEnable","Cards enabled",15,bottom+138,null); ButtonAt(panel,"CardsDisable","Cards disabled",200,bottom+138,null);
            UnityEventTools.AddBoolPersistentListener(panel.Find("CardsEnable").GetComponent<Button>().onClick,p.SetCardsEnabled,true);
            UnityEventTools.AddBoolPersistentListener(panel.Find("CardsDisable").GetComponent<Button>().onClick,p.SetCardsEnabled,false);
            rt.sizeDelta=new Vector2(Mathf.Max(390,rt.sizeDelta.x),bottom+195);
            // 保留面板内原有坐标；按高度缩放使完整控件面板落在1080参考画布。
            rt.localScale=Vector3.one*Mathf.Min(1,950/rt.sizeDelta.y);
        }
        private static void MigrateCard()
        {
            const string path="Assets/Prefabs/Choice/ChoiceCard.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<ChoiceCardView>(); var button=Ref<Button>(view,"selectButton");
                var title=Ref<TMP_Text>(view,"titleText"); var desc=Ref<TMP_Text>(view,"descriptionText");
                ConfigureText(title,24,Color.white,true); ConfigureText(desc,22,new Color(.14f,.105f,.065f),false); desc.alignment=TextAlignmentOptions.TopLeft;
                title.text=""; desc.text="";
                var icon=Ref<Image>(view,"icon"); icon.color=Color.clear; icon.raycastTarget=false;
                foreach (var label in icon.GetComponentsInChildren<TMP_Text>(true)) { label.text=""; }
                Fixed(icon.rectTransform,new Vector2(0,100),new Vector2(240,250));
                var hit=root.GetComponent<Image>(); hit.sprite=null; hit.color=Color.clear; hit.raycastTarget=true;
                button.targetGraphic=hit; button.transition=Selectable.Transition.None;
                var frame=Image(root.transform,"Frame",Style.frame);
                var glow=Image(root.transform,"SelectedGlow",Sprite("Cards/UI_Card_Selected_Glow.png")); glow.transform.SetAsFirstSibling(); glow.gameObject.SetActive(false);
                frame.transform.SetSiblingIndex(1);
                var visual=root.GetComponent<CardVisual>() ?? root.AddComponent<CardVisual>();
                Set(visual,"style",Style); Set(visual,"frame",frame); Set(visual,"title",title); Set(visual,"descriptionText",desc);
                Set(visual,"art",icon.rectTransform); Set(visual,"selectedGlow",glow.gameObject); Bool(visual,"followFocus",true); visual.ApplyVariant(1);
                Set(visual,"visualGroup",Ensure<CanvasGroup>(root));
                foreach(var layout in root.GetComponentsInChildren<LayoutGroup>(true)) { Object.DestroyImmediate(layout); }
                foreach(var fitter in root.GetComponentsInChildren<ContentSizeFitter>(true)) { Object.DestroyImmediate(fitter); }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void MigrateMenu()
        {
            const string path="Assets/Prefabs/Choice/ChoiceMenu.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var panel=root.GetComponent<ChoicePanel>(); var view=Ref<GameObject>(panel,"viewRoot"); view.SetActive(true);
                root.GetComponent<Canvas>().sortingOrder=30; root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1920,1080);
                var overlay=view.GetComponent<Image>(); overlay.sprite=null; overlay.color=new Color(0,0,0,.8f); overlay.raycastTarget=true; Stretch((RectTransform)view.transform);
                var board=view.transform.Find("ChoiceBoard") as RectTransform;
                if (!board) { board=Rect(view.transform,"ChoiceBoard",new Vector2(1920,1080)); }
                var fit=board.GetComponent<UIBoardFit>() ?? board.gameObject.AddComponent<UIBoardFit>(); Set(fit,"canvasRect",root.transform);
                var title=Ref<TMP_Text>(panel,"requestTitle"); title.transform.SetParent(board,false); ConfigureText(title,48,Color.white,true);
                Fixed(title.rectTransform,new Vector2(0,360),new Vector2(1300,90));
                var container=(RectTransform)Ref<Transform>(panel,"cardContainer"); container.SetParent(board,false);
                foreach(var layout in container.GetComponents<LayoutGroup>()) { Object.DestroyImmediate(layout); }
                foreach(var fitter in container.GetComponents<ContentSizeFitter>()) { Object.DestroyImmediate(fitter); }
                Fixed(container,new Vector2(0,-65),new Vector2(1650,700));
                if (!container.GetComponent<ChoiceCardLayout>()) { container.gameObject.AddComponent<ChoiceCardLayout>(); }
                var feedback=Ref<TMP_Text>(panel,"feedbackText"); feedback.transform.SetParent(board,false); ConfigureText(feedback,20,Color.white,false);
                Fixed(feedback.rectTransform,new Vector2(0,-415),new Vector2(1400,50));
                var cancel=Ref<Button>(panel,"cancelButton"); cancel.transform.SetParent(board,false); Fixed((RectTransform)cancel.transform,new Vector2(0,-480),new Vector2(220,52));
                cancel.GetComponent<Image>().color=new Color(.14f,.125f,.11f,1);
                var label=cancel.GetComponentInChildren<TMP_Text>(); ConfigureText(label,24,Color.white,false); label.text="Cancel"; Stretch(label.rectTransform);
                view.SetActive(false); PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void MigrateHud()
        {
            EditorSceneManager.OpenScene(ArtScene); var art=Object.FindFirstObjectByType<UIArtPreview>();
            const string path="Assets/Prefabs/Hud/PlayerHud.prefab"; var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var hud=root.GetComponent<PlayerHud>(); var oldHealth=Ref<TMP_Text>(hud,"healthText");
                root.GetComponent<Canvas>().sortingOrder=0; root.GetComponent<CanvasScaler>().referenceResolution=new Vector2(1920,1080);
                var legacy=root.transform.Find("LegacyReadout");
                if (!legacy)
                {
                    var children=root.transform.Cast<Transform>().ToArray(); legacy=Rect(root.transform,"LegacyReadout",Vector2.zero); Stretch((RectTransform)legacy);
                    foreach(var child in children) { child.SetParent(legacy,false); }
                    legacy.gameObject.SetActive(false);
                }
                Transform hp=root.transform.Find("HPGroup");
                if (!hp) { hp=Object.Instantiate(Ref<RectTransform>(art,"hpGroup").gameObject,root.transform,false).transform; hp.name="HPGroup"; }
                var clonedText=hp.GetComponentInChildren<TMP_Text>(true); var pos=clonedText.rectTransform;
                if (clonedText!=oldHealth)
                {
                    oldHealth.transform.SetParent(hp,false); CopyRect(pos,oldHealth.rectTransform); Object.DestroyImmediate(clonedText.gameObject);
                }
                ConfigureText(oldHealth,30,Color.black,true); oldHealth.fontSharedMaterial=Font.material; oldHealth.gameObject.SetActive(true);
                var fill=hp.GetComponentsInChildren<Image>(true).Single(i=>i.type==UnityEngine.UI.Image.Type.Filled); Set(hud,"healthFill",fill); String(hud,"healthFormat","{0}");
                Transform body=root.transform.Find("BodyStatus");
                if (!body) { body=Object.Instantiate(Ref<RectTransform>(art,"bodyGroup").gameObject,root.transform,false).transform; body.name="BodyStatus"; }
                foreach(var corner in new[]{hp,body})
                {
                    corner.localScale=Vector3.one;
                    var fit=corner.GetComponent<UIBoardFit>() ?? corner.gameObject.AddComponent<UIBoardFit>();
                    Set(fit,"canvasRect",root.transform); Bool(fit,"shrinkOnly",true);
                }
                var display=root.GetComponent<BodyHudArt>() ?? root.AddComponent<BodyHudArt>();
                Set(display,"hpGroup",hp.gameObject);
                foreach(string field in new[]{"torso","armsBase","legs","tail","flame"}) { Set(display,field,FindClone(body,Ref<GameObject>(art,field).name).gameObject); }
                Set(display,"arms",FindClone(body,Ref<GameObject>(art,"armsActive").name).gameObject);
                foreach(string field in new[]{"swordLabel","legsLabel","fireLabel"}) { Set(display,field,FindClone(body,Ref<TMP_Text>(art,field).name).gameObject); }
                var dash=body.Find("DashLabel")?.GetComponent<TMP_Text>() ?? Text(body,"DashLabel",26);
                CopyRect(Ref<TMP_Text>(art,"fireLabel").rectTransform,dash.rectTransform); dash.fontSize=Ref<TMP_Text>(art,"fireLabel").fontSize;
                dash.text="Dash"; dash.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"Common/CascadiaMono_Outline.mat"); Set(display,"dashLabel",dash.gameObject);
                var edge=root.transform.Find("GameplayEdge");
                if (!edge) { edge=Object.Instantiate(Ref<GlobalEdgeAnimator>(art,"gameplayEdge").gameObject,root.transform,false).transform; edge.name="GameplayEdge"; }
                edge.gameObject.SetActive(true); Stretch((RectTransform)edge); Float(edge.GetComponent<GlobalEdgeAnimator>(),"breathPeriod",8);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static void WireLevel()
        {
            var scene=EditorSceneManager.OpenScene(Level);
            var hud=Object.FindFirstObjectByType<PlayerHud>();
            var display=hud.GetComponent<BodyHudArt>(); Set(display,"stateSource",Ref<MonoBehaviour>(hud,"stateSource"));
            PrefabUtility.RecordPrefabInstancePropertyModifications(display);
            var hp=Ref<TMP_Text>(hud,"healthText");
            // 原场景保存了旧HP文字的颜色/坐标覆写，只重置该显示节点的视觉字段。
            var source=PrefabUtility.GetCorrespondingObjectFromSource(hp);
            CopyRect(source.rectTransform,hp.rectTransform); hp.color=Color.black; hp.font=Font; hp.fontSharedMaterial=Font.material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(hp); PrefabUtility.RecordPrefabInstancePropertyModifications(hp.rectTransform);
            var menu=Object.FindFirstObjectByType<ChoicePanel>(); menu.GetComponent<Canvas>().sortingOrder=30;
            PrefabUtility.RecordPrefabInstancePropertyModifications(menu.GetComponent<Canvas>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        public static T Ref<T>(Object o,string name) where T:Object => (T)new SerializedObject(o).FindProperty(name).objectReferenceValue;
        private static T Ensure<T>(GameObject o) where T:Component { var c=o.GetComponent<T>(); if(!c) { c=o.AddComponent<T>(); } return c; }
        public static T[] Refs<T>(Object o,string name) where T:Object { var a=new SerializedObject(o).FindProperty(name); return Enumerable.Range(0,a.arraySize).Select(i=>(T)a.GetArrayElementAtIndex(i).objectReferenceValue).ToArray(); }
        public static void Set(Object o,string name,Object value) { var s=new SerializedObject(o); s.FindProperty(name).objectReferenceValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Float(Object o,string n,float v) { var s=new SerializedObject(o); s.FindProperty(n).floatValue=v; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Bool(Object o,string n,bool v) { var s=new SerializedObject(o); s.FindProperty(n).boolValue=v; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void String(Object o,string n,string v) { var s=new SerializedObject(o); s.FindProperty(n).stringValue=v; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Strings(Object o,string n,string[] values) { var s=new SerializedObject(o); var a=s.FindProperty(n); a.arraySize=values.Length; for(int i=0;i<values.Length;i++) { a.GetArrayElementAtIndex(i).stringValue=values[i]; } s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Array(Object o,string n,Object[] values) { var s=new SerializedObject(o); var a=s.FindProperty(n); a.arraySize=values.Length; for(int i=0;i<values.Length;i++) { a.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; } s.ApplyModifiedPropertiesWithoutUndo(); }
        private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(Root+path);
        private static Transform FindClone(Transform root,string name) => root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        private static RectTransform Rect(Transform parent,string name,Vector2 size) { var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false); Fixed(r,Vector2.zero,size); return r; }
        public static void Fixed(RectTransform r,Vector2 pos,Vector2 size) { r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=pos; r.sizeDelta=size; r.localRotation=Quaternion.identity; r.localScale=Vector3.one; }
        private static void Stretch(RectTransform r) { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; }
        private static void CopyRect(RectTransform source,RectTransform dest) { dest.anchorMin=source.anchorMin; dest.anchorMax=source.anchorMax; dest.pivot=source.pivot; dest.sizeDelta=source.sizeDelta; dest.anchoredPosition=source.anchoredPosition; dest.localRotation=source.localRotation; dest.localScale=source.localScale; }
        private static Image Image(Transform parent,string name,Sprite sprite) { var r=parent.Find(name) as RectTransform; if (!r) { r=Rect(parent,name,sprite.rect.size); } var img=r.GetComponent<Image>() ?? r.gameObject.AddComponent<Image>(); img.sprite=sprite; img.color=Color.white; img.raycastTarget=false; return img; }
        private static TMP_Text Text(Transform parent,string name,float size) { var t=Rect(parent,name,Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>(); ConfigureText(t,size,Color.white,false); return t; }
        private static void ConfigureText(TMP_Text t,float size,Color color,bool bold)
        { t.font=Font; t.fontSharedMaterial=Font.material; t.fontSize=size; t.enableAutoSizing=false; t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal; t.color=color; t.alignment=TextAlignmentOptions.Center; t.textWrappingMode=TextWrappingModes.Normal; t.overflowMode=TextOverflowModes.Overflow; t.raycastTarget=false; }
        private static void ButtonAt(Transform parent,string name,string label,float x,float y,UnityAction action)
        {
            var r=Rect(parent,name,new Vector2(175,38)); r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y);
            var img=r.gameObject.AddComponent<Image>(); img.color=new Color(.19f,.18f,.17f,1); var b=r.gameObject.AddComponent<Button>(); b.targetGraphic=img;
            if(action!=null && action.Target is UIArtPreview) { UnityEventTools.AddPersistentListener(b.onClick,action); }
            var t=Text(r,"Label",18);t.text=label;Stretch(t.rectTransform);
        }
    }
}
