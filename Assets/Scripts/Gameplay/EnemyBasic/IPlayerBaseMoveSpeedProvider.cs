// Soap/Enemy AI integration seam. Module-local, not a new Core/player contract.
// Bind a component exposing the player's configured base speed, never instantaneous velocity.
namespace Regrowth.Gameplay
{
    public interface IPlayerBaseMoveSpeedProvider
    {
        float BaseMoveSpeed { get; }
    }
}
