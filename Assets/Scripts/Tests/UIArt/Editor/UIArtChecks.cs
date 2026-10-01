// 职责：兼容旧验证入口，使用当前正式接入验证。交接docs/handoffs/Dada.handoff；规范AGENTS.md。
namespace Regrowth.Tests.UIArt.Editor
{
    public static class UIArtChecks
    {
        public static void BuildAndRun() { UIFinalChecks.BuildAndRun(); }
        public static void Run() { UIFinalChecks.Run(); }
    }
}
