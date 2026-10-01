// 职责：稳定音频身份；不实现奖励或传送规则。
// 依赖：无。维护：总控；规范：根目录 AGENTS.md。
namespace Regrowth.Audio
{
    /// <summary>调用身份；真实事件由 Wwise 后端映射，数值不得重排。</summary>
    public enum AudioCue
    {
        PlayerJump = 1,
        PlayerBite = 2,
        PlayerHurt = 3,
        PlayerDied = 4,
        ChestOpened = 5,
        AbilityGained = 6,
        AbilityLost = 7,
        SwitchActivated = 8,
        DoorOpened = 9,
        Teleported = 10,
        UIConfirm = 11,
        BossStarted = 12,
        RunWon = 13,
        PlayerWeaponAttack = 14,
        PlayerDash = 15,
        PlayerDoubleJump = 16,
        CardsPresented = 17,
        CardHovered = 18,
        CardSelected = 19,
        UIHovered = 20,
    }
}

