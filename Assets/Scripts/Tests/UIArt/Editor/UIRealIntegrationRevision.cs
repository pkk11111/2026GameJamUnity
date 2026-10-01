// 职责：原生保存统一卡面、正式前端、Gameplay常驻边框与最新Ming场景显示引用。
// 维护Dada；不修改奖励/敌人/输入/Build Settings；交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using System.IO;
using System.Linq;
using Regrowth.UI;
using Regrowth.UI.Art;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Regrowth.Tests.UIArt.Editor
{
    public static class UIRealIntegrationRevision
    {
        public const string MainMenu = "Assets/Scenes/Frontend/MainMenu.unity";

        [MenuItem("PAWGATORY/UI Art/Apply real UI integration")]
        public static void Apply()
        {
            UICardIconsRevision.Apply();
            var preview = Object.FindFirstObjectByType<UIArtPreview>();
            foreach(var edge in preview.GetComponentsInChildren<GlobalEdgeAnimator>(true)) { ConfigureEdge(edge); }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            BuildMainMenu(preview);
            const string path="Assets/Prefabs/Hud/PlayerHud.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var edge=root.GetComponentInChildren<GlobalEdgeAnimator>(true);
                edge.name="GameplayEdgeOverlay";
                edge.gameObject.SetActive(true);
                ConfigureEdge(edge);
                edge.transform.SetAsFirstSibling();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var scene=EditorSceneManager.OpenScene(UIFinalRevision.Level);
            var hud=Object.FindFirstObjectByType<PlayerHud>();
            var body=hud.GetComponent<BodyHudArt>();
            UIFinalRevision.Set(body,"stateSource",UIFinalRevision.Ref<MonoBehaviour>(hud,"stateSource"));
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            var canvas=Object.FindFirstObjectByType<ChoicePanel>().GetComponent<Canvas>();
            canvas.sortingOrder=30;
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas);
            AlignExistingHint();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.OpenScene(UIFinalRevision.Level);
            Debug.Log("REAL_UI_MIGRATION_SAVED_AND_REOPENED");
        }

        public static void FinishHintLayout()
        {
            var scene=EditorSceneManager.OpenScene(UIFinalRevision.Level);
            AlignExistingHint();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static void AlignExistingHint()
        {
            var overlay=Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Single(o=>o.GetType().Name=="WhiteboxOverlay");
            var text=UIFinalRevision.Ref<TMP_Text>(overlay,"label");
            // Keep the real interaction/pause reader. Move only its existing text away from the body HUD.
            var rect=text.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);
            rect.anchoredPosition=new Vector2(-35,25); rect.sizeDelta=new Vector2(660,155);
            text.alignment=TextAlignmentOptions.BottomRight;
            text.raycastTarget=false;
            EditorUtility.SetDirty(rect); EditorUtility.SetDirty(text);
        }

        private static void ConfigureEdge(GlobalEdgeAnimator edge)
        {
            var so=new SerializedObject(edge);
            var frames=so.FindProperty("frames"); frames.arraySize=3;
            for(int i=0;i<3;i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/UI/Login/UI_Login_Edge_0"+(i+1)+".png");
            }
            so.FindProperty("frameDuration").floatValue=1.2f;
            so.FindProperty("breathPeriod").floatValue=8;
            so.FindProperty("animate").boolValue=true;
            so.ApplyModifiedPropertiesWithoutUndo();
            var image=edge.GetComponent<Image>(); image.raycastTarget=false;
            image.sprite=(Sprite)frames.GetArrayElementAtIndex(0).objectReferenceValue;
        }

        private static void BuildMainMenu(UIArtPreview preview)
        {
            var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var canvasObject=new GameObject("MainMenuCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            var background=Object.Instantiate(UIFinalRevision.Ref<Image>(preview,"background"),canvas.transform,false);
            background.name="Background"; background.gameObject.SetActive(true);
            background.rectTransform.anchorMin=Vector2.zero; background.rectTransform.anchorMax=Vector2.one;
            background.rectTransform.offsetMin=background.rectTransform.offsetMax=Vector2.zero;
            var login=Object.Instantiate(UIFinalRevision.Ref<GameObject>(preview,"login"),canvas.transform,false);
            login.name="MainMenuContent"; login.SetActive(true);
            var board=login.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name==UIFinalRevision.Ref<RectTransform>(preview,"loginBoard").name);
            var fit=board.GetComponent<UIBoardFit>() ?? board.gameObject.AddComponent<UIBoardFit>();
            UIFinalRevision.Set(fit,"canvasRect",canvas.transform);
            var edge=login.GetComponentInChildren<GlobalEdgeAnimator>(true);
            if (!edge) { edge=Object.Instantiate(UIFinalRevision.Ref<GlobalEdgeAnimator>(preview,"loginEdge"),canvas.transform,false); }
            edge.gameObject.SetActive(true); ConfigureEdge(edge);
            var start=login.GetComponentsInChildren<Button>(true).Single();
            var controller=canvasObject.AddComponent<MainMenuController>();
            UIFinalRevision.Set(controller,"startButton",start);
            start.onClick=new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(start.onClick,controller.StartGame);
            var events=Object.Instantiate(original.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<EventSystem>(true)).Single().gameObject);
            events.name="EventSystem";
            Directory.CreateDirectory("Assets/Scenes/Frontend"); AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene,MainMenu);
            EditorSceneManager.CloseScene(original,true);
        }
    }
}
