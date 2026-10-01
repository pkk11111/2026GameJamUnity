// 职责：仅C05显式构建时清理构建中的场景副本测试组件，不写磁盘场景。
// 维护：controller；依赖UnityEditor构建回调。普通封装流程不被此工具隐式更改。
// 规范：根AGENTS.md；交接：docs/handoffs/controller.handoff。
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Regrowth.Tests.C05
{
    public sealed class C05BuildSceneFilter : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || !C05EditorTools.BuildInProgress)
            {
                return;
            }
            int removed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component != null && component.GetType().Assembly.GetName().Name.StartsWith("Regrowth.Tests.", System.StringComparison.Ordinal))
                    {
                        Object.DestroyImmediate(component);
                        removed++;
                    }
                }
            }
            Debug.Log("C05 build scene: removed " + removed + " test components from temporary scene copy.");
        }
    }
}
