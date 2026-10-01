// 职责：显式C05测试入口，校验当前地图与全新状态后注入真实组件；不是生产自动组装器。
// 维护：controller；依赖C05/Runtime/Chest/Challenge/Choice。仅Editor或专用检查包编译。
// 规范：根AGENTS.md；交接：docs/handoffs/controller.handoff。检查会消耗本次Play状态。
#if UNITY_EDITOR || C05_PLAYER_CHECKS
using System;
using Regrowth.Gameplay;
using Regrowth.Gameplay.Challenge;
using Regrowth.Runtime;
using Regrowth.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Regrowth.Tests.C05
{
    public static class NewMapCheckLauncher
    {
        public static NewMapPlayChecks Current { get; private set; }

        public const string ScenePath = "Assets/WhiteBox/Scenes/Level_Whitebox.unity";

        /// <summary>仅显式测试调用；非目标地图、重复检查或非全新状态抛出异常，校验通过才建临时驱动。</summary>
        public static NewMapPlayChecks StartChecks()
        {
            if (!Application.isPlaying || SceneManager.GetActiveScene().path != ScenePath)
            {
                throw new InvalidOperationException("请打开Level_Whitebox并进入全新Play后执行C05。");
            }
            if (Current != null || UnityEngine.Object.FindObjectsByType<NewMapPlayChecks>(FindObjectsSortMode.None).Length != 0)
            {
                throw new InvalidOperationException("本次Play已有C05驱动；退出并重新Play再测。");
            }
            var player = Unique<PlayerState>();
            var run = Unique<RunController>();
            var flow = Unique<ChoiceCoordinator>();
            var panel = Unique<ChoicePanel>();
            var gates = UnityEngine.Object.FindObjectsByType<InspectionDoor2D>(FindObjectsSortMode.None);
            var boxes = UnityEngine.Object.FindObjectsByType<Chest>(FindObjectsSortMode.None);
            if (!run.IsGameplayActive || flow.IsOpen || !player.HasBodyCore || !player.IsAlive
                || player.Items.Count != 0 || player.EverOwnedItems.Count != 0
                || gates.Length != 2 || boxes.Length != 9
                || Array.Exists(boxes, box => box.IsClaimed)
                || Array.Exists(gates, gate => gate.HasAdmission || gate.HasEntered || gate.IsCompleted))
            {
                throw new InvalidOperationException("C05要求全新地图：Playing、已有躯干、空构筑/历史、9未领箱、2未开始入口。");
            }
            Array.Sort(gates, (a, b) => string.CompareOrdinal(a.name, b.name));
            Array.Sort(boxes, (a, b) => string.CompareOrdinal(a.name, b.name));
            var host = new GameObject("C05 Temporary Checks - Do Not Save");
            host.hideFlags = HideFlags.DontSave;
            var driver = host.AddComponent<NewMapPlayChecks>();
            Current = driver;
            driver.Begin(player, run, flow, panel, gates, boxes);
            return driver;
        }

        private static T Unique<T>() where T : UnityEngine.Object
        {
            var values = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            if (values.Length != 1)
            {
                throw new InvalidOperationException("C05 requires exactly one " + typeof(T).Name);
            }
            return values[0];
        }
    }
}
#endif
