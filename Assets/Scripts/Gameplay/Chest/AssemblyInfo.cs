// Soap / T12：仅本任务测试可访问内部领取事务，不扩Core契约。
// 交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Regrowth.Tests.T12")]
