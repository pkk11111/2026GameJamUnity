// 职责：可选特殊攻击输入端口，不改变已有 IPlayerInput 实现；由唯一适配器提供。
// 维护：controller；依赖：无；规范：AGENTS.md；交接：docs/handoffs/controller.handoff。
namespace Regrowth.Core
{
    public interface IPlayerFireInput
    {
        /// <summary>单次取走火焰请求；无请求/过期/非 Playing 返回 false。</summary>
        bool TryConsumeFire();
    }
}
