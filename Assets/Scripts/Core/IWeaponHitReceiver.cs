// 职责：已成功武器伤害后的可选运动反馈端口；不提交伤害、不赋予无敌。
// 维护：enemy-ai；直接依赖：无；交接docs/handoffs/enemy-ai.handoff；规范AGENTS.md。
namespace Regrowth.Core
{
    /// <summary>由伤害实体同物体上的运动组件实现；只有武器伤害成功后调用。</summary>
    public interface IWeaponHitReceiver
    {
        /// <summary>
        /// 主线程请求水平击退。方向为有限非零数（只取正负）；false表示未接收。
        /// 接收者验证存活/运行状态，缓冲到自身FixedUpdate，不允许第二个刚体写入者。
        /// 不用于咬击、火焰或未成功的伤害；死亡命中不得推动尸体。
        /// </summary>
        bool TryApplyWeaponHit(float horizontalDirection);
    }
}
