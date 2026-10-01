// Soap/T07纯测试受伤靶；不是T09敌人或玩家状态。自身HP与多Collider由同一接收者持有。
using Regrowth.Core;
using UnityEngine;
namespace Regrowth.Tests.T07
{
    public sealed class T07DamageDummy : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private int initialHealth = 1000;
        public int Health { get; private set; }
        public int HitCount { get; private set; }
        public DamageRequest LastRequest { get; private set; }
        private void Awake() { Health = initialHealth; }
        public bool TryTakeDamage(DamageRequest request)
        {
            if (Health <= 0 || request.Amount <= 0 || (request.Kind != DamageKind.Enemy && request.Kind != DamageKind.Terrain)) return false;
            Health = Mathf.Max(0, Health - request.Amount);
            HitCount++;
            LastRequest = request;
            return true;
        }
    }
}
