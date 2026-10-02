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
            var bootstrap = player.gameObject.scene.GetRootGameObjects();
            foreach (var root in bootstrap)
            {
                foreach (var entry in root.GetComponentsInChildren<GameBootstrap>(true))
                {
                    typeof(GameBootstrap).GetField("restartOnDeath", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(entry, false);
                }
            }
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
                Vector2 near = OutsidePoint(gate);
                Vector2 far = near + direction * 6f;
                Vector2 inside = (Vector2)gate.transform.position - (near - (Vector2)gate.transform.position) + Vector2.down * 3.5f;
                Move(far);
                gate.ResetAdmission();
                yield return Delay();
                Check(gate.name + " fresh scene locked", !gate.IsUnlocked && !gate.IsOpen);
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
                    && Request().Options.Count == 1 && Request().Options[0].Title.StartsWith("Discard"));
                var stale = Read<Func<string, bool>>(panel, "confirm");
                int health = player.CurrentHealth;
                Cancel();
                yield return Delay();
                Check(gate.name + " cancel stays locked", !flow.IsOpen && !gate.IsUnlocked && !gate.IsOpen
                    && player.Contains(part) && player.CurrentHealth == health && player.Items.Count == 3);
                Move(far);
                player.TryRemoveLoadoutItem(part);
                yield return Delay();
                Move(near);
                yield return Delay();
                Check(gate.name + " absent part still requires first consent", flow.IsOpen
                    && Request().Options[0].Title == "Start challenge" && !stale("enter"));
                // 门1验证展示期间补回部件后的再次确认；门2验证已缺部件的免费首次确认。
                if (gate == gates[0])
                {
                    player.TryAddLoadoutItem(part);
                    Confirm();
                    yield return Delay();
                    Check(gate.name + " changed state requires fresh consent", flow.IsOpen && player.Contains(part)
                        && !gate.IsUnlocked && Request().Options[0].Title.StartsWith("Discard"));
                }
                Confirm();
                yield return Delay();
                Check(gate.name + " confirmation commits once", !flow.IsOpen && !player.Contains(part)
                    && player.Items.Count == 2 && player.CurrentHealth == health && player.WasEverOwned(part));
                Check(gate.name + " permanently unlocked and hidden", gate.IsUnlocked && gate.HasAdmission && gate.IsOpen
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
                Check(gate.name + " regain only revokes attempt", !gate.HasAdmission && gate.IsUnlocked && gate.IsOpen);
                Move(far);
                yield return Delay();
                Check(gate.name + " exit never recreates door", !gate.HasAdmission && gate.IsOpen);
                Move(near);
                yield return Delay();
                Check(gate.name + " revisit with part is free passage without admission", !flow.IsOpen && gate.IsOpen
                    && !gate.HasAdmission && player.Contains(part));
                gate.enabled = false;
                gate.enabled = true;
                yield return Delay();
                Check(gate.name + " component reenable preserves unlock", gate.IsUnlocked && gate.IsOpen);
                player.TryRemoveLoadoutItem(part);
                yield return Delay();
                Check(gate.name + " eligible reentry restores attempt without menu", gate.HasAdmission && !flow.IsOpen);
                Move(inside);
                yield return Delay();
                Check(gate.name + " new attempt records crossing", gate.HasEntered);
                Move(far);
                yield return Delay();
                Check(gate.name + " exit resets only attempt", !gate.HasAdmission && !gate.HasEntered && gate.IsOpen);
                player.TryAddLoadoutItem(part);
                Check(gate.name + " other gate independent", player.Items.Count == 3);
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
            if (flow.IsOpen)
            {
                Confirm();
                yield return Delay();
            }
            Check("real reward claim consumes exactly one box", claimed.IsClaimed && !flow.IsOpen && !claimed.TryInteract(player.gameObject));
            var complete = gates[1];
            player.TryRemoveLoadoutItem(Read<LoadoutItemId>(complete, "requiredMissingPart"));
            var completeDirection = Read<Vector2>(complete, "outsideDirection").normalized;
            Move((Vector2)complete.transform.position + completeDirection * 8f);
            yield return Delay();
            Move(OutsidePoint(complete));
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
            Move(OutsidePoint(first));
            yield return Delay();
            Check("Paused revisit remains open without choice", !flow.IsOpen && first.IsOpen);
            run.TryResume();
            yield return Delay();
            movement.enabled = true;
            Check("explicit test relocation accepted", movement.TryResetToStart());
            yield return new WaitForSecondsRealtime(0.15f);
            Check("relocation preserves unlocked door", !first.HasAdmission && first.IsUnlocked && first.IsOpen);
            movement.enabled = false;
            body.gravityScale = 0f;
            player.TryTakeDamage(new DamageRequest(1000000, DamageKind.Enemy));
            yield return Delay();
            Check("death revokes attempt without recreating door", run.Phase == RunPhase.Dead && !flow.IsOpen
                && !first.HasAdmission && first.IsOpen);
            Finished = true;
            Debug.Log("C05 NewMapPlayChecks: " + Passed + " passed / " + Failed + " failed.");
        }

        private Vector2 OutsidePoint(InspectionDoor2D gate)
        {
            // 地图已调整外侧Trigger；靠近点按真实门/玩家宽度取门外接触处，不复用旧2.2单位偏移。
            Vector2 direction = Read<Vector2>(gate, "outsideDirection").normalized;
            var box = Read<BoxCollider2D>(gate, "blocker");
            var playerBox = player.GetComponent<Collider2D>();
            Vector3 x = box.transform.TransformVector(new Vector3(box.size.x, 0f, 0f));
            Vector3 y = box.transform.TransformVector(new Vector3(0f, box.size.y, 0f));
            float extent = Mathf.Abs(direction.x) * ((Mathf.Abs(x.x) + Mathf.Abs(y.x)) * 0.5f + playerBox.bounds.extents.x)
                + Mathf.Abs(direction.y) * ((Mathf.Abs(x.y) + Mathf.Abs(y.y)) * 0.5f + playerBox.bounds.extents.y);
            return (Vector2)box.transform.TransformPoint(box.offset) + direction * (extent + 0.02f) + Vector2.down * 1.75f;
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
