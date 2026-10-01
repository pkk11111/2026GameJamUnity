// 职责：V5回归的明确Editor入口；旧四槽驱动仅历史，当前测试从本入口运行。
// 模块/维护：controller；依赖T12V5Checks/UnityEditor；交接docs/handoffs/controller.handoff；规范根AGENTS.md。
using UnityEditor;
using UnityEngine;

namespace Regrowth.Tests.T12.Editor
{
    public static class T12V5Menu
    {
        [MenuItem("Tools/pawgatory/T12/Run V5 Checks (Play)")]
        public static void Run()
        {
            Debug.Log(T12V5Checks.Run());
        }
    }
}