// 职责：保留旧菜单入口，委托当前卡面正文版本；交接docs/handoffs/Dada.handoff；规范AGENTS.md。
namespace Regrowth.Tests.UIArt.Editor { public static class UIArtRevision { public static void Apply() { UIFinalRevision.ApplyArtTest(); UICardIconsRevision.Apply(); } } }
