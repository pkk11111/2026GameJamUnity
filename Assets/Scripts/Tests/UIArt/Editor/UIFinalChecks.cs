// 职责：兼容旧验证入口，转发当前真实UI检查；维护Dada；交接docs/handoffs/Dada.handoff；规范AGENTS.md。
namespace Regrowth.Tests.UIArt.Editor
{
    public static class UIFinalChecks
    {
        public static void Run() { UIRealIntegrationChecks.Run(); }
        public static void BuildAndRun() { UIRealIntegrationChecks.BuildAndRun(); }
    }
}
