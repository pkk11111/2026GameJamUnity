// 职责：仅T02上限/监听生命周期测试替身；正式HUD默认接真实PlayerState，不进入主场景。
// 模块/维护：Soap / T02；依赖Core；交接docs/handoffs/Soap.handoff；规范根AGENTS.md。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.Tests.T02
{
    public sealed class HudProbeState : MonoBehaviour, IHealth, ILoadoutState, IFormState
    {
        private Action healthChanged;
        private Action loadoutChanged;
        private Action<PlayerForm> formChanged;
        private static readonly IReadOnlyList<LoadoutItemId> Empty = Array.AsReadOnly(Array.Empty<LoadoutItemId>());
        public int CurrentHealth { get; private set; } = 70;
        public int MaximumHealth { get; private set; } = 80;
        public bool IsAlive => CurrentHealth > 0;
        public int Capacity => 4;
        public IReadOnlyList<LoadoutItemId> Items => Empty;
        public PlayerForm CurrentForm => PlayerForm.Quadruped;
        public bool Contains(LoadoutItemId item) => false;
        public event Action Died { add { } remove { } }
        public event Action HealthChanged { add => healthChanged += value; remove => healthChanged -= value; }
        public event Action LoadoutChanged { add => loadoutChanged += value; remove => loadoutChanged -= value; }
        public event Action<PlayerForm> FormChanged { add => formChanged += value; remove => formChanged -= value; }
        public int HealthListeners => healthChanged?.GetInvocationList().Length ?? 0;
        public int LoadoutListeners => loadoutChanged?.GetInvocationList().Length ?? 0;
        public int FormListeners => formChanged?.GetInvocationList().Length ?? 0;

        /// <summary>仅测试上限事件和旧源通知；不提供正式玩家写端口。</summary>
        public void SetHealth(int current, int maximum)
        {
            CurrentHealth = current;
            MaximumHealth = maximum;
            healthChanged?.Invoke();
        }
    }
}
