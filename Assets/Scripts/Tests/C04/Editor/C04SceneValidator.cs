// 职责：C04显式Bootstrap的只读配置审查；不接线、不修改场景/输入/时间倍率。
// 维护：controller；直接依赖：Core、Runtime、Input System、UnityEditor。
// 规范：根AGENTS.md；交接：docs/handoffs/controller.handoff；仅Editor程序集。
using System;
using System.Collections.Generic;
using Regrowth.Core;
using Regrowth.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Regrowth.Tests.C04
{
    /// <summary>选择场景Bootstrap后运行菜单；错误为接线阻塞，信息为配置快照，不代表Play或整局验收。</summary>
    public static class C04SceneValidator
    {
        [MenuItem("Tools/pawgatory/C04/Validate Selected Bootstrap")]
        private static void ValidateSelected()
        {
            var selected = Selection.activeGameObject;
            var bootstrap = selected != null ? selected.GetComponent<GameBootstrap>() : null;
            var report = Validate(bootstrap);
            var message = "C04 配置审查：" + report.Errors.Count + " 错误\n"
                + string.Join("\n", report.Errors) + "\n" + string.Join("\n", report.Notes);
            if (report.Errors.Count == 0)
            {
                Debug.Log(message, bootstrap);
            }
            else
            {
                Debug.LogWarning(message, bootstrap);
            }
        }

        /// <summary>只读选定入口所在场景；默认正式玩家必填，C01纯输入独测可显式传false。无效目标返回错误。</summary>
        public static Report Validate(GameBootstrap bootstrap, bool requirePlayer = true)
        {
            var report = new Report();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Errors.Add("请在非Play模式审查；此工具不验证运行事务。");
                return report;
            }
            if (bootstrap == null || !bootstrap.gameObject.scene.IsValid() || !bootstrap.gameObject.scene.isLoaded)
            {
                report.Errors.Add("请选择已加载场景中的GameBootstrap物体；不审查Prefab资产。");
                return report;
            }
            var scene = bootstrap.gameObject.scene;
            CheckComponent(bootstrap, bootstrap, "Bootstrap", report);
            CheckUnique<GameBootstrap>(bootstrap, report);
            using (var fields = new SerializedObject(bootstrap))
            {
                var run = fields.FindProperty("runController").objectReferenceValue as RunController;
                var input = fields.FindProperty("inputReader").objectReferenceValue as PlayerInputReader;
                var state = fields.FindProperty("playerState").objectReferenceValue as PlayerState;
                var interactor = fields.FindProperty("playerInteractor").objectReferenceValue as PlayerInteractor;
                var coordinator = fields.FindProperty("choiceCoordinator").objectReferenceValue as ChoiceCoordinator;
                var presenter = fields.FindProperty("choicePresenter").objectReferenceValue as MonoBehaviour;
                CheckComponent(run, bootstrap, "runController", report);
                CheckComponent(input, bootstrap, "inputReader", report);
                CheckUnique<RunController>(bootstrap, report);
                CheckUnique<PlayerInputReader>(bootstrap, report);
                if (requirePlayer || state != null || interactor != null || coordinator != null)
                {
                    CheckComponent(state, bootstrap, "playerState", report);
                    CheckUnique<PlayerState>(bootstrap, report);
                }
                if (run != null)
                {
                    CheckPositive(run, "gameplayTimeScale", report);
                }
                if (state != null)
                {
                    using (var data = new SerializedObject(state))
                    {
                        var hp = data.FindProperty("initialMaximumHealth").intValue;
                        var bite = data.FindProperty("initialBiteDamage").intValue;
                        var sword = data.FindProperty("initialSwordDamage").intValue;
                        var body = data.FindProperty("prototypeStartWithBodyCore").boolValue;
                        if (hp <= 0 || bite < 0 || sword < 0)
                        {
                            report.Errors.Add("PlayerState：最大生命须>0，咬击/剑伤害须>=0。");
                        }
                        report.Notes.Add("PlayerState 初始最大HP=" + hp + "，咬击=" + bite + "，剑击=" + sword
                            + "；prototypeStartWithBodyCore=" + body + "。这些是Inspector值，不是冻结平衡值。");
                        if (body)
                        {
                            report.Notes.Add("当前跳过头部教学，仅供白盒；正式T19接线需关闭该例外并验证教学。");
                        }
                    }
                }
                if (input != null)
                {
                    CheckInput(input, report);
                }
                if (interactor != null)
                {
                    CheckComponent(interactor, bootstrap, "playerInteractor", report);
                    CheckUnique<PlayerInteractor>(bootstrap, report);
                    CheckPositive(interactor, "interactionRadius", report);
                    using (var data = new SerializedObject(interactor))
                    {
                        var actor = data.FindProperty("actor").objectReferenceValue as GameObject;
                        if (state == null || actor != state.gameObject)
                        {
                            report.Errors.Add("PlayerInteractor.actor须绑定该PlayerState所在玩家根物体。");
                        }
                        if (data.FindProperty("interactionLayers").intValue == 0)
                        {
                            report.Errors.Add("PlayerInteractor.interactionLayers为空，无法检测任何目标。");
                        }
                        report.Notes.Add("交互检测：layers=" + data.FindProperty("interactionLayers").intValue
                            + "，includeTriggers=" + data.FindProperty("includeTriggers").boolValue + "。");
                    }
                }
                if (coordinator != null)
                {
                    CheckComponent(coordinator, bootstrap, "choiceCoordinator", report);
                    CheckUnique<ChoiceCoordinator>(bootstrap, report);
                    using (var data = new SerializedObject(coordinator))
                    {
                        var actual = data.FindProperty("presenter").objectReferenceValue as MonoBehaviour;
                        CheckPresenter(actual, bootstrap, report);
                        if (presenter != null && presenter != actual)
                        {
                            report.Errors.Add("Bootstrap.choicePresenter与Coordinator.presenter不一致。");
                        }
                    }
                }
                else if (presenter != null)
                {
                    CheckPresenter(presenter, bootstrap, report);
                }
            }
            report.Notes.Add("场景：" + scene.path + "；只审查本场景配置。通过不代表缺失的战斗/收费/支线/重开已经实现。");
            return report;
        }

        private static void CheckPresenter(MonoBehaviour presenter, GameBootstrap bootstrap, Report report)
        {
            CheckComponent(presenter, bootstrap, "IChoicePresenter", report);
            if (presenter != null && !(presenter is IChoicePresenter))
            {
                report.Errors.Add("菜单须实现Core.IChoicePresenter，不能绑定协调器自身。");
            }
        }

        private static void CheckComponent(MonoBehaviour value, GameBootstrap bootstrap, string field, Report report)
        {
            if (value == null || !value.enabled || !value.gameObject.activeInHierarchy
                || value.gameObject.scene != bootstrap.gameObject.scene)
            {
                report.Errors.Add(field + "：缺失、未启用或引用了其他场景。");
            }
        }

        private static void CheckUnique<T>(GameBootstrap bootstrap, Report report) where T : MonoBehaviour
        {
            var count = 0;
            foreach (var root in bootstrap.gameObject.scene.GetRootGameObjects())
            {
                foreach (var component in root.GetComponentsInChildren<T>(true))
                {
                    if (component.enabled && component.gameObject.activeInHierarchy)
                    {
                        count++;
                    }
                }
            }
            if (count != 1)
            {
                report.Errors.Add(typeof(T).Name + "：本场景启用实例须为1，实际=" + count + "。");
            }
        }

        private static void CheckPositive(MonoBehaviour component, string field, Report report)
        {
            using (var data = new SerializedObject(component))
            {
                var value = data.FindProperty(field).floatValue;
                if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                {
                    report.Errors.Add(component.GetType().Name + "." + field + "须为有限正数。");
                }
                report.Notes.Add(component.GetType().Name + "." + field + "=" + value);
            }
        }

        private static void CheckInput(PlayerInputReader reader, Report report)
        {
            CheckPositive(reader, "buttonBufferSeconds", report);
            if (InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
            {
                report.Errors.Add("Input System须使用ProcessEventsInDynamicUpdate；此工具不更改设置。");
            }
            using (var data = new SerializedObject(reader))
            {
                var asset = data.FindProperty("inputActions").objectReferenceValue as InputActionAsset;
                if (asset == null)
                {
                    report.Errors.Add("PlayerInputReader.inputActions必填。");
                    return;
                }
                var used = new HashSet<Guid>();
                var paths = new[] { "moveActionPath", "jumpActionPath", "attackActionPath", "interactActionPath",
                    "dashActionPath", "pauseActionPath", "prototypeResetActionPath" };
                foreach (var field in paths)
                {
                    var path = data.FindProperty(field).stringValue;
                    if (field == "prototypeResetActionPath" && string.IsNullOrWhiteSpace(path))
                    {
                        continue;
                    }
                    var action = string.IsNullOrWhiteSpace(path) ? null : asset.FindAction(path, false);
                    if (action == null)
                    {
                        report.Errors.Add("输入动作未找到：" + field + "=" + path);
                        continue;
                    }
                    if (!used.Add(action.id))
                    {
                        report.Errors.Add("多个输入职责绑定同一动作：" + path);
                    }
                    if (field == "moveActionPath")
                    {
                        if (action.type != InputActionType.Value || action.expectedControlType != "Vector2")
                        {
                            report.Errors.Add("Move须为Value/Vector2：" + path);
                        }
                    }
                    else
                    {
                        var plain = action.type == InputActionType.Button && string.IsNullOrEmpty(action.interactions);
                        foreach (var binding in action.bindings)
                        {
                            plain &= string.IsNullOrEmpty(binding.interactions);
                        }
                        if (!plain)
                        {
                            report.Errors.Add("按键须为普通Button，动作/绑定不得带Hold等Interactions：" + path);
                        }
                    }
                    report.Notes.Add(field + "=" + path);
                }
                report.Notes.Add("输入资产：" + AssetDatabase.GetAssetPath(asset) + "；未启停或复制动作。");
            }
        }

        /// <summary>单次审查结果；不持有场景对象或修改命令。Errors为空只表示上述静态配置检查通过。</summary>
        public sealed class Report
        {
            public List<string> Errors { get; } = new List<string>();
            public List<string> Notes { get; } = new List<string>();
        }
    }
}
