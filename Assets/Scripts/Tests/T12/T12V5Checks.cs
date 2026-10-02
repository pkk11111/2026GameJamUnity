// 职责：T12/V5真实PlayerState与领取事务回归；仅手动Play检查，不挂入正式地图。
// 模块/维护：controller；依赖Core/Runtime/Chest，独立IRunContext/菜单替身隔离展示。
// 交接：docs/handoffs/controller.handoff；规范：根AGENTS.md。所有临时对象在finally销毁。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Runtime;
using UnityEngine;

namespace Regrowth.Tests.T12
{
    public static class T12V5Checks
    {
        private sealed class Context : IRunContext
        {
            public RunPhase Phase { get; set; } = RunPhase.Playing;
            public bool IsGameplayActive => Phase == RunPhase.Playing;
            public event Action<RunPhase> PhaseChanged { add { } remove { } }
        }
        private sealed class Menu : IChoiceFlow
        {
            internal Context Run;
            internal ChoiceRequest Request;
            internal Func<string, bool> Confirm;
            internal Action Cancelled;
            public bool IsOpen => Request != null;
            public string RequestId => Request?.Id ?? "";
            public bool TryBegin(ChoiceRequest request, Func<string, bool> confirm, Action cancel)
            {
                if (IsOpen)
                {
                    return false;
                }
                Run.Phase = RunPhase.Choosing;
                Request = request;
                Confirm = confirm;
                Cancelled = cancel;
                return true;
            }
            public bool TryReplace(ChoiceRequest request, Func<string, bool> confirm, Action cancel)
            {
                if (!IsOpen || request.Id != RequestId)
                {
                    return false;
                }
                Request = request;
                Confirm = confirm;
                Cancelled = cancel;
                return true;
            }
            public void Cancel()
            {
                var callback = Cancelled;
                Request = null;
                Confirm = null;
                Cancelled = null;
                Run.Phase = RunPhase.Playing;
                callback?.Invoke();
            }
            internal bool Submit(string id)
            {
                var callback = Confirm;
                bool success = callback != null && callback(id);
                if (success)
                {
                    Request = null;
                    Confirm = null;
                    Cancelled = null;
                    Run.Phase = RunPhase.Playing;
                }
                return success;
            }
        }

        private static void Set(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        /// <summary>仅Play可调用；使用真实状态与事务，结果不代表场景UI/路线已验证。</summary>
        public static string Run()
        {
            if (!Application.isPlaying)
            {
                return "REFUSED: Play mode required";
            }
            var cleanup = new List<UnityEngine.Object>();
            var failures = new List<string>();
            int passed = 0;
            Action<bool, string> check = (valid, name) =>
            {
                if (valid)
                {
                    passed++;
                }
                else
                {
                    failures.Add(name);
                }
            };
            Func<Context, bool, PlayerState> makeState = (context, hasBody) =>
            {
                var go = new GameObject("TEST ONLY T12 V5 State");
                cleanup.Add(go);
                var state = go.AddComponent<PlayerState>();
                Set(state, "prototypeStartWithBodyCore", hasBody);
                typeof(PlayerState).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(state, new object[] { context });
                return state;
            };
            Func<string, ChestRewardKind, LoadoutItemId, int, ChestRewardDefinition> definition = (id, kind, item, extraHeal) =>
            {
                var reward = new ChestRewardDefinition();
                Set(reward, "id", id);
                Set(reward, "title", id);
                Set(reward, "description", "V5 isolated test");
                Set(reward, "kind", kind);
                Set(reward, "item", item);
                Set(reward, "bonusHeal", extraHeal);
                return reward;
            };
            Func<ChestRewardDefinition[], ChestRewardConfig> config = pool =>
            {
                var asset = ScriptableObject.CreateInstance<ChestRewardConfig>();
                cleanup.Add(asset);
                Set(asset, "rewards", pool);
                return asset;
            };
            try
            {
                var context = new Context();
                var state = makeState(context, false);
                check(state.IsAlive && !state.HasBodyCore && state.CurrentHealth == 0 && !state.CanBite, "head safe/alive/no attack");
                check(!state.TryTakeDamage(new DamageRequest(100, DamageKind.Terrain)), "head rejects damage");
                check(!state.TryAddLoadoutItem(LoadoutItemId.Legs), "head rejects regular reward");
                check(state.TryAcquireBodyCore() && state.CurrentHealth == 100 && state.CanBite, "body initializes health/bite");
                check(!state.TryAcquireBodyCore() && state.Items.Count == 0, "body one time/no slot");
                check(!state.TryAddLoadoutItem(LoadoutItemId.Sword) && !state.TryAddLoadoutItem(LoadoutItemId.UprightForm), "legacy identities rejected");
                check(state.TryAddLoadoutItem(LoadoutItemId.Arms) && state.CanUseSword && !state.CanBite, "arms sword without legs");
                check(state.TryAddLoadoutItem(LoadoutItemId.Legs) && state.TryAddLoadoutItem(LoadoutItemId.Tail)
                    && state.Capacity == 3 && state.Items.Count == 3, "three shared slots");
                check(!state.TryAddLoadoutItem(LoadoutItemId.FlameBreath) && state.Items.Count == 3, "fourth item rejected");
                check(state.TryRemoveLoadoutItem(LoadoutItemId.Arms) && state.CanBite && state.WasEverOwned(LoadoutItemId.Arms), "loss preserves history/bite");
                state.TryTakeDamage(new DamageRequest(30, DamageKind.Terrain));
                bool ownerCommitted = false;
                bool observedAtomic = false;
                bool reentryRejected = false;
                state.HealthChanged += () =>
                {
                    observedAtomic = ownerCommitted && state.Contains(LoadoutItemId.Arms) && state.CurrentHealth == 90;
                    reentryRejected = !state.TryRemoveLoadoutItem(LoadoutItemId.Arms);
                };
                check(state.TryApplyReward(new PlayerReward(LoadoutItemId.Arms, 20, 10, 4), null, () => ownerCommitted = true)
                    && state.MaximumHealth == 110 && state.BiteDamage == 14 && state.SwordDamage == 24, "bundle all effects");
                check(observedAtomic && reentryRejected, "notifications see owner/items/stats together and reject reentry");
                int hp = state.CurrentHealth;
                check(!state.TryApplyReward(new PlayerReward(heal: 10, maximumHealthIncrease: int.MaxValue))
                    && state.CurrentHealth == hp && state.MaximumHealth == 110, "overflow no partial healing");
                check(state.TryReplaceLoadoutItem(LoadoutItemId.Tail, LoadoutItemId.FlameTail)
                    && !state.Contains(LoadoutItemId.Tail), "tail atomic swap");
                check(!state.TryReplaceLoadoutItem(LoadoutItemId.Legs, LoadoutItemId.FlameBreath), "flame duplicate source rejected");
                check(!state.TryReplaceLoadoutItem(LoadoutItemId.Legs, LoadoutItemId.Tail), "two tails rejected");

                var menu = new Menu { Run = context };
                var second = makeState(context, true);
                var legs = definition("legs", ChestRewardKind.Loadout, LoadoutItemId.Legs, 7);
                var arms = definition("arms", ChestRewardKind.Loadout, LoadoutItemId.Arms, 0);
                var tail = definition("tail", ChestRewardKind.Loadout, LoadoutItemId.Tail, 0);
                var heal = definition("heal", ChestRewardKind.Heal, default, 0);
                var max = definition("max", ChestRewardKind.MaximumHealth, default, 0);
                var attack = definition("attack", ChestRewardKind.Attack, default, 0);
                var pool = config(new[] { legs, arms, tail, heal, max, attack });
                var transaction = new ChestClaimTransaction(second, second, second, context, menu, pool, "fixed", () => { }, () => true);
                check(transaction.TryBegin() && menu.Request.Options.Count == 3
                    && menu.Request.Options.Select(x => x.Id).Distinct().Count() == 3, "ordinary three distinct");
                var oldCards = transaction.Cached.ToArray();
                menu.Cancel();
                check(transaction.TryBegin() && oldCards.Select(x => JsonUtility.ToJson(x)).SequenceEqual(transaction.Cached.Select(x => JsonUtility.ToJson(x))), "cancel keeps exact cards");
                menu.Cancel();
                var oldItemCard = oldCards.FirstOrDefault(x => x.Kind == ChestRewardKind.Loadout);
                if (oldItemCard == null)
                {
                    // 固定随机种子独立夹具确保有可失效身体项，不修改正式抽样策略。
                    pool = config(new[] { legs, arms, tail });
                    transaction = new ChestClaimTransaction(second, second, second, context, menu, pool, "repair", () => { }, () => true);
                    transaction.TryBegin();
                    oldCards = transaction.Cached.ToArray();
                    menu.Cancel();
                    oldItemCard = oldCards[0];
                    Set(pool, "rewards", new[] { legs, arms, tail, heal, max, attack });
                }
                second.TryAddLoadoutItem(oldItemCard.Item);
                check(transaction.TryBegin() && !transaction.Cached.Any(x => x.Kind == ChestRewardKind.Loadout && second.Contains(x.Item)),
                    "reopen excludes newly owned card");
                check(oldCards.Where(x => x != oldItemCard).All(x => JsonUtility.ToJson(transaction.Cached[Array.IndexOf(oldCards, x)]) == JsonUtility.ToJson(x)),
                    "repair preserves other positions");
                menu.Cancel();

                var full = makeState(context, true);
                full.TryAddLoadoutItem(LoadoutItemId.Arms);
                full.TryAddLoadoutItem(LoadoutItemId.Tail);
                // 只在独测授予未实现喷火身份，地图池无喷火；覆盖部件缺失但三槽已满的真实状态。
                full.TryAddLoadoutItem(LoadoutItemId.FlameBreath);
                full.TryTakeDamage(new DamageRequest(30, DamageKind.Terrain));
                int completed = 0;
                var replacementPool = config(new[] { legs, heal, max });
                var replacement = new ChestClaimTransaction(full, full, full, context, menu, replacementPool, "replace", () => completed++, () => true);
                check(replacement.TryBegin() && !menu.Submit("legs") && menu.Request.Options.Count == 3, "full slot stage has three old items");
                check(menu.Request.Options.Any(x => x.Id == ((int)LoadoutItemId.FlameBreath).ToString()), "standalone skill replaceable");
                menu.Cancel();
                check(full.CurrentHealth == 70 && !full.Contains(LoadoutItemId.Legs) && !replacement.Claimed
                    && !full.WasEverOwned(LoadoutItemId.Legs), "cancel no bundle/history/chest change");
                replacement.TryBegin();
                menu.Submit("legs");
                check(menu.Submit(((int)LoadoutItemId.Tail).ToString()) && full.Contains(LoadoutItemId.Legs)
                    && !full.Contains(LoadoutItemId.Tail) && full.CurrentHealth == 77 && completed == 1, "replacement bundle committed once");
                check(replacement.Claimed && !replacement.TryBegin() && !menu.Submit("legs"), "claimed cannot repeat");
                full.TryRemoveLoadoutItem(LoadoutItemId.Legs);
                // 相同当前构筑/随机状态下，历史持有不改变抽样；仍可改变再生展示文案。
                var plain = makeState(context, true);
                var historical = makeState(context, true);
                historical.TryAddLoadoutItem(LoadoutItemId.Legs);
                historical.TryRemoveLoadoutItem(LoadoutItemId.Legs);
                var uniformPool = config(new[] { legs, arms, tail, heal, max, attack });
                var randomState = UnityEngine.Random.state;
                bool historyIndependent = true;
                bool canOmitPreviousPart = false;
                try
                {
                    for (int seed = 0; seed < 32; seed++)
                    {
                        UnityEngine.Random.InitState(seed);
                        var first = new ChestClaimTransaction(plain, plain, plain, context, menu, uniformPool, "plain", () => { }, () => true);
                        bool firstOpened = first.TryBegin();
                        var firstIds = first.Cached.Select(x => x.Id).ToArray();
                        menu.Cancel();
                        UnityEngine.Random.InitState(seed);
                        var secondDraw = new ChestClaimTransaction(historical, historical, historical, context, menu, uniformPool, "history", () => { }, () => true);
                        bool secondOpened = secondDraw.TryBegin();
                        historyIndependent &= firstOpened && secondOpened && firstIds.SequenceEqual(secondDraw.Cached.Select(x => x.Id));
                        canOmitPreviousPart |= !secondDraw.Cached.Any(x => x.Item == LoadoutItemId.Legs && x.Kind == ChestRewardKind.Loadout);
                        menu.Cancel();
                    }
                }
                finally
                {
                    UnityEngine.Random.state = randomState;
                }
                check(historyIndependent, "same random state and current loadout draw identical cards regardless of ownership history");
                check(canOmitPreviousPart, "previously held missing part is not reserved in every draw");
                var buffs = makeState(context, true);
                check(buffs.TryApplyReward(new PlayerReward(heal: 20)) && buffs.CurrentHealth == 100, "full health no-op reward accepted");
                check(buffs.TryApplyReward(new PlayerReward(maximumHealthIncrease: 10)) && buffs.MaximumHealth == 110
                    && buffs.CurrentHealth == 100, "max increase does not silently heal");
                check(buffs.TryApplyReward(new PlayerReward(attackIncrease: 5)) && buffs.BiteDamage == 15
                    && buffs.SwordDamage == 25 && buffs.Items.Count == 0, "attack buff no slot");
                var onlyBuffs = config(new[] { heal, max, attack });
                var buffChest = new ChestClaimTransaction(buffs, buffs, buffs, context, menu, onlyBuffs, "buff", () => { }, () => true);
                check(buffChest.TryBegin() && menu.Submit("max") && buffChest.Claimed && buffs.MaximumHealth == 120, "stat reward uses real command");
                var head = makeState(context, false);
                var bodyConfig = config(new[] { definition("body", ChestRewardKind.BodyCore, default, 0) });
                Set(bodyConfig, "bodyTutorial", true);
                var tutorial = new ChestClaimTransaction(head, head, head, context, menu, bodyConfig, "body", () => { }, () => true);
                check(tutorial.TryBegin() && menu.Request.Options.Count == 1, "tutorial fixed single card");
                menu.Cancel();
                check(!head.HasBodyCore && !tutorial.Claimed, "tutorial cancel safe");
                tutorial.TryBegin();
                check(menu.Submit("body") && head.HasBodyCore && head.CurrentHealth == 100 && tutorial.Claimed, "tutorial atomic body grant");
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
            }
            finally
            {
                foreach (var value in cleanup)
                {
                    if (value != null)
                    {
                        UnityEngine.Object.Destroy(value);
                    }
                }
            }
            return "T12 V5: " + passed + " passed, " + failures.Count + " failed\n" + string.Join("\n", failures);
        }
    }
}
