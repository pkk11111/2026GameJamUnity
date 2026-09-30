// 职责：仅向 C02 独测程序集开放内部四槽容器，正式 gameplay 不可绕过 PlayerState。
// 模块/维护：controller，C02；直接依赖：System.Runtime.CompilerServices。
// 交接：docs/handoffs/controller.handoff；规范：根目录 AGENTS.md。
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Regrowth.Tests.C02")]
