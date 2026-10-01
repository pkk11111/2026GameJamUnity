// Soap/T07隔离攻击读口；只在自动Play测试中创建，绝不代替正式PlayerState。
using Regrowth.Core;
using UnityEngine;
namespace Regrowth.Tests.T07
{
    public sealed class T07CombatFixture : MonoBehaviour, IPlayerCombatState
    {
        public bool Allowed;
        public int Damage;
        public bool CanBite => Allowed;
        public int BiteDamage => Damage;
        public bool CanUseSword => false;
        public int SwordDamage => 0;
    }
}
