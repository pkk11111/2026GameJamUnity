// Soap/T09：独测注册适配器，仅记录生命周期；不进入正式地图，不实现全局强化。
// 直接依赖Gameplay.EnemyBasic；交接docs/handoffs/Soap.handoff；规则AGENTS.md。
using System.Collections.Generic;
using Regrowth.Gameplay;
using UnityEngine;

namespace Regrowth.Tests.T09
{
    public sealed class T09RegistrationProbe : MonoBehaviour, IEnemyRegistrationAdapter
    {
        private readonly HashSet<EnemyBasic> live = new HashSet<EnemyBasic>();
        public int Registered { get; private set; }
        public int Unregistered { get; private set; }
        public bool Contains(EnemyBasic enemy) => live.Contains(enemy);
        public void Register(EnemyBasic enemy)
        {
            if (live.Add(enemy))
            {
                Registered++;
            }
        }
        public void Unregister(EnemyBasic enemy)
        {
            if (live.Remove(enemy))
            {
                Unregistered++;
            }
        }
    }
}
