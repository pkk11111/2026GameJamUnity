// 职责：仅本模块测试场景的画布适配和独立相机预览；不改项目设置。
// 模块：player-visual-art-test；依赖：uGUI、现有 URP、UnityEditor。
// 交接：docs/handoffs/Dada.handoff；规范：根 AGENTS.md。
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Regrowth.Tests.Art.Editor
{
    public static class PlayerVisualArtLayout
    {
        [MenuItem("Tools/pawgatory/Art Test/Fit Test Preview Layout")]
        public static void FitTestPreview()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                throw new InvalidOperationException("Stop Play and save scene before fitting the test preview.");
            }
            if (scene.path != PlayerVisualArtSetup.ScenePath)
            {
                scene = EditorSceneManager.OpenScene(PlayerVisualArtSetup.ScenePath);
            }
            Canvas canvas = null;
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out Canvas foundCanvas)) { canvas = foundCanvas; }
                if (root.name == "Art Preview Camera" && root.TryGetComponent(out Camera foundCamera)) { camera = foundCamera; }
            }
            if (canvas == null || camera == null)
            {
                throw new InvalidOperationException("Test scene must contain its own Canvas and preview Camera.");
            }
            Apply(canvas, camera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            PlayerVisualArtChecks.ValidateAssets();
        }

        /// <summary>编辑模式调用，在本测试画布内完整缩放 UI；相机画面独立于 Game 视图宽高比。</summary>
        public static void Apply(Canvas canvas, Camera camera)
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("Stop Play before changing art test layout.");
            }
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referenceResolution = new Vector2(1440, 900);
            RectTransform board = canvas.transform.Find("ArtBoard") as RectTransform;
            if (board == null)
            {
                board = new GameObject("ArtBoard", typeof(RectTransform)).GetComponent<RectTransform>();
                board.SetParent(canvas.transform, false);
                board.anchorMin = board.anchorMax = board.pivot = new Vector2(0.5f, 0.5f);
                board.sizeDelta = new Vector2(1440, 900);
                board.anchoredPosition = Vector2.zero;
                for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = canvas.transform.GetChild(i);
                    if (child != board)
                    {
                        child.SetParent(board, false);
                        child.SetAsFirstSibling();
                    }
                }
            }
            const string path = PlayerVisualArtSetup.ArtRoot + "/Controllers/ArtPreview.renderTexture";
            RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (texture == null)
            {
                texture = new RenderTexture(1024, 1024, 24) { name = "ArtPreview", antiAliasing = 1 };
                AssetDatabase.CreateAsset(texture, path);
            }
            Transform existing = board.Find("Character Preview");
            RawImage preview = existing == null ? new GameObject("Character Preview", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>() : existing.GetComponent<RawImage>();
            preview.transform.SetParent(board, false);
            preview.transform.SetAsFirstSibling();
            preview.rectTransform.anchorMin = preview.rectTransform.anchorMax = preview.rectTransform.pivot = new Vector2(0, 1);
            preview.rectTransform.anchoredPosition = new Vector2(383, -162);
            preview.rectTransform.sizeDelta = new Vector2(576, 556);
            preview.texture = texture;
            preview.raycastTarget = false;
            camera.targetTexture = texture;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.aspect = 576f / 556f;
            if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            // 预览相机输出纹理；另一个空相机负责屏幕底色，Overlay Canvas 显示控制面板。
            Camera background = null;
            foreach (GameObject root in canvas.gameObject.scene.GetRootGameObjects())
            {
                if (root.name == "Art UI Background Camera")
                {
                    background = root.GetComponent<Camera>();
                }
            }
            if (background == null)
            {
                background = new GameObject("Art UI Background Camera").AddComponent<Camera>();
                background.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }
            background.cullingMask = 0;
            background.depth = -10;
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = new Color(.08f, .08f, .075f);
            AssetDatabase.SaveAssets();
        }
    }
}
