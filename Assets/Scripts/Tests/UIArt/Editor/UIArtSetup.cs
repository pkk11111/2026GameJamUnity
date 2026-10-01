// 职责：打开既有 UI 美术测试场景，兼容旧显式批处理入口；不重新生成场景。
// 依赖：UnityEditor / UIArtRevision；维护：docs/handoffs/Dada.handoff；规范：AGENTS.md。
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Regrowth.Tests.UIArt.Editor
{
    public static class UIArtSetup
    {
        public const string ScenePath = "Assets/Scenes/Tests/Art/UI_ArtTest.unity";
        public const string Root = "Assets/Art/UI";
        [MenuItem("PAWGATORY/UI Art/Open test scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { EditorSceneManager.OpenScene(ScenePath); }
        }
        /// <summary>旧入口现在仅迁移已存在场景；不导入图片、字体或新建场景。</summary>
        public static void Build() { UIArtRevision.Apply(); }
    }
}
