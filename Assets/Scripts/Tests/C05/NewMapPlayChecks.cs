#if UNITY_EDITOR || C05_PLAYER_CHECKS
// 职责：C05新地图真实组件Play验收；独立Tests程序集，临时驱动不得保存进主场景。
// 维护：controller；依赖Core/Runtime/Challenge/WhiteBox/Chest/Choice与uGUI；不创建第二份玩家状态。
// 交接：docs/handoffs/controller.handoff；规范根AGENTS.md。测试会修改本次Play状态，结束后退出Play复原。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Regrowth.Core;
using Regrowth.Gameplay;
using Regrowth.Gameplay.Challenge;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using Regrowth.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Regrowth.Tests.C05
{
    public sealed class NewMapPlayChecks : MonoBehaviour
    {
        private PlayerState player;
        private RunController run;
        private ChoiceCoordinator flow;
        private ChoicePanel panel;
        private WhiteboxPlayer2D movement;
        private Rigidbody2D body;
        private PlayerInteractor interactor;
        private InspectionDoor2D[] gates;
        private Chest[] chests;
        private readonly List<string> results = new List<string>();
        public bool Finished { get; private set; }
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public IReadOnlyList<string> Results => results;

        /// <summary>只在Play调用，所有依赖显式注入；从全新场景状态开始，测试结束Dead，不保存测试对象。</summary>
        public void Begin(PlayerState state, RunController context, ChoiceCoordinator choices, ChoicePanel presenter,
            InspectionDoor2D[] doors, Chest[] boxes)
        {
            player = state;
            run = context;
            flow = choices;
            panel = presenter;
            gates = doors;
            chests = boxes;
            movement = player.GetComponent<WhiteboxPlayer2D>();
            body = player.GetComponent<Rigidbody2D>();
            interactor = player.GetComponent<PlayerInteractor>();
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return null;
            Check("bootstrap Playing / 9 boxes / 2 gates", run.IsGameplayActive && chests.Length == 9 && gates.Length == 2);
            movement.enabled = false;
            body.gravityScale = 0f;
            Check("loadout setup", player.TryAddLoadoutItem(LoadoutItemId.Arms)
                && player.TryAddLoadoutItem(LoadoutItemId.Legs) && player.TryAddLoadoutItem(LoadoutItemId.Tail));
            foreach (var gate in gates)
            {
                var part = Read<LoadoutItemId>(gate, "requiredMissingPart");
                var direction = Read<Vector2>(gate, "outsideDirection").normalized;
                Vector2 near = (Vector2)gate.transform.position + direction * 2.2f + Vector2.down * 1.75f;
                Vector2 far = near + direction * 6f;
                Vector2 inside = near - direction * 4.4f;
                Move(far);
                gate.ResetAdmission();
                yield return Delay();
                // 自动菜单关掉时，单独测试真实实体碰撞，不依赖检测器停止玩家。
                gate.enabled = false;
                Move(near);
                body.linearVelocity = -direction * 12f;
                yield return new WaitForSecondsRealtime(0.25f);
                Check(gate.name + " physical block", Vector2.Dot(body.position - (Vector2)gate.transform.position, direction) > 0.9f);
                Move(far);
                gate.enabled = true;
                yield return Delay();
                Move(near);
                yield return Delay();
                Check(gate.name + " approach discard card pauses", flow.IsOpen && run.Phase == RunPhase.Choosing
                    && Time.timeScale == 0f && Request().Options.Count == 1 && Request().Options[0].Title.StartsWith("Discard"));
                var stale = Read<Func<string, bool>>(panel, "confirm");
                int health = player.CurrentHealth;
                Cancel();
                yield return Delay();
                Check(gate.name + " cancel stays closed / no repeated popup", !flow.IsOpen && !gate.IsOpen
                    && player.Contains(part) && player.CurrentHealth == health && player.Items.Count == 3);
                Move(far);
                yield return Delay();
                Move(near);
                yield return Delay();
                Check(gate.name + " reapproach / stale callback rejected", flow.IsOpen && !stale("enter") && player.Contains(part));
                Confirm();
                yield return Delay();
                Check(gate.name + " confirm removes only selected part", !flow.IsOpen && !player.Contains(part)
                    && player.Items.Count == 2 && player.CurrentHealth == health && player.WasEverOwned(part));
                Check(gate.name + " collider and visual disappear", gate.HasAdmission && gate.IsOpen
                    && !Read<GameObject>(gate, "closedView").activeSelf && run.IsGameplayActive);
                Check(gate.name + " no premature completion", !gate.TryCompleteChallenge());
                body.linearVelocity = -direction * 12f;
                yield return new WaitForSecondsRealtime(0.25f);
                Check(gate.name + " opened physical passage", Vector2.Dot(body.position - (Vector2)gate.transform.position, direction) < -0.6f);
                Move(inside);
                yield return Delay();
                Check(gate.name + " crossing records entry", gate.HasEntered && gate.HasAdmission);
                Check(gate.name + " regain part accepted", player.TryAddLoadoutItem(part));
                yield return Delay();
                Check(gate.name + " regain invalidates but allows retreat", !gate.HasAdmission && gate.IsOpen);
                Move((Vector2)gate.transform.position + Vector2.down * 1.75f);
                yield return Delay();
                Check(gate.name + " closing does not embed player", gate.IsOpen);
                Move(far);
                yield return Delay();
                Check(gate.name + " exit rearms solid door", !gate.HasAdmission && !gate.IsOpen);
                player.TryRemoveLoadoutItem(part);
                Move(near);
                yield return Delay();
                Check(gate.name + " absent part still asks start", flow.IsOpen && Request().Options[0].Title == "Start challenge");
                Cancel();
                yield return Delay();
                Check(gate.name + " absent cancel has no effects", !gate.IsOpen && !flow.IsOpen && player.Items.Count == 2);
                Move(far);
                yield return Delay();
                Move(near);
                yield return Delay();
                player.TryAddLoadoutItem(part);
                Confirm();
                yield return Delay();
                Check(gate.name + " changed state requires fresh consent", flow.IsOpen && player.Contains(part)
                    && !gate.IsOpen && Request().Options[0].Title.StartsWith("Discard"));
                Confirm();
                yield return Delay();
                Check(gate.name + " renewed consent commits once", !player.Contains(part) && gate.HasAdmission && !flow.IsOpen);
                Move(inside);
                yield return Delay();
                Move(far);
                yield return Delay();
                Check(gate.name + " entered then exited requires new attempt", !gate.HasAdmission && !gate.IsOpen);
                Move(near);
                yield return Delay();
                int count = player.Items.Count;
                Confirm();
                yield return Delay();
                Check(gate.name + " absent confirmation is free and explicit", gate.HasAdmission && gate.IsOpen
                    && player.Items.Count == count && player.CurrentHealth == health);
                Move(far);
                yield return Delay();
                player.TryAddLoadoutItem(part);
                Check(gate.name + " state restored / other gate independent", player.Items.Count == 3);
            }
            foreach (var chest in chests)
            {
                Move((Vector2)chest.transform.position + new Vector2(-1.8f, -0.75f));
                yield return Delay();
                interactor.RefreshTarget();
                bool target = interactor.InteractionId == chest.InteractionId;
                bool opened = target && interactor.TryInteractCurrent();
                Check(chest.name + " real interactor opens 3 cards", opened && flow.IsOpen
                    && Request().Options.Count == 3 && Time.timeScale == 0f);
                Cancel();
                yield return Delay();
                Check(chest.name + " cancel preserves box and restores Playing", !chest.IsClaimed && !flow.IsOpen && run.IsGameplayActive);
            }
            var claimed = chests[0];
            Move((Vector2)claimed.transform.position + new Vector2(-1.8f, -0.75f));
            yield return Delay();
            claimed.TryInteract(player.gameObject);
            yield return Delay();
            Confirm();
            yield return Delay();
            Check("real reward claim consumes exactly one box", claimed.IsClaimed && !flow.IsOpen && !claimed.TryInteract(player.gameObject));
            var complete = gates[1];
            var completeDirection = Read<Vector2>(complete, "outsideDirection").normalized;
            Move((Vector2)complete.transform.position + completeDirection * 8f);
            yield return Delay();
            Move((Vector2)complete.transform.position + completeDirection * 2.2f + Vector2.down * 1.75f);
            yield return Delay();
            Confirm();
            yield return Delay();
            Move((Vector2)complete.transform.position - completeDirection * 2.2f + Vector2.down * 1.75f);
            yield return Delay();
            Check("completion hook accepts entered eligible attempt once", complete.TryCompleteChallenge() && !complete.TryCompleteChallenge());
            player.TryAddLoadoutItem(Read<LoadoutItemId>(complete, "requiredMissingPart"));
            Move((Vector2)complete.transform.position + completeDirection * 8f);
            complete.ResetAdmission();
            yield return Delay();
            Check("completed gate remains open after regeneration and exit", complete.IsCompleted && complete.IsOpen);
            var first = gates[0];
            Vector2 outside = Read<Vector2>(first, "outsideDirection").normalized;
            Move((Vector2)first.transform.position + outside * 8f);
            yield return Delay();
            run.TryPause();
            Move((Vector2)first.transform.position + outside * 2.2f + Vector2.down * 1.75f);
            yield return Delay();
            Check("Paused approach cannot open", !flow.IsOpen);
            run.TryResume();
            yield return Delay();
            Confirm();
            yield return Delay();
            movement.enabled = true;
            Check("whitebox R relocation accepted", movement.TryResetToStart());
            yield return new WaitForSecondsRealtime(0.15f);
            Check("actual R revokes entry permission", !first.HasAdmission && !first.IsOpen);
            movement.enabled = false;
            body.gravityScale = 0f;
            Move((Vector2)first.transform.position + outside * 8f);
            yield return Delay();
            Move((Vector2)first.transform.position + outside * 2.2f + Vector2.down * 1.75f);
            yield return Delay();
            Check("death test starts with pending choice", flow.IsOpen);
            Check("Choosing rejects damage and keeps choice", !player.TryTakeDamage(new DamageRequest(1000000, DamageKind.Enemy)) && flow.IsOpen && player.IsAlive);
            Confirm();
            yield return Delay();
            player.TryTakeDamage(new DamageRequest(1000000, DamageKind.Enemy));
            yield return Delay();
            Check("Playing death revokes admission and leaves no menu", run.Phase == RunPhase.Dead && !flow.IsOpen && !first.HasAdmission);
            Finished = true;
            Debug.Log("C05 NewMapPlayChecks: " + Passed + " passed / " + Failed + " failed.");
        }

        private void Move(Vector2 position)
        {
            body.position = position;
            player.transform.position = position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }

        private ChoiceRequest Request() => Read<ChoiceRequest>(panel, "currentRequest");
        private static WaitForSecondsRealtime Delay() => new WaitForSecondsRealtime(0.06f);
        private void Cancel() => Read<Button>(panel, "cancelButton").onClick.Invoke();
        private void Confirm()
        {
            var cards = Read<List<ChoiceCardView>>(panel, "cards");
            if (cards.Count > 0)
            {
                Read<Button>(cards[0], "selectButton").onClick.Invoke();
            }
        }
        private static T Read<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }
        private void Check(string name, bool success)
        {
            results.Add((success ? "PASS " : "FAIL ") + name);
            if (success)
            {
                Passed++;
            }
            else
            {
                Failed++;
                Debug.LogError("C05 FAIL: " + name, this);
            }
        }
    }
}

#endif
