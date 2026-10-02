// 职责：通过 Unity 序列化 API 安装显式音频接线，不修改地图/奖励池。
// 维护：audio-rewards；规范：根 AGENTS.md。
using System;
using Regrowth.Gameplay;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.UI;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Regrowth.Audio.Editor
{
    public static class RewardAudioSetup
    {
        [MenuItem("Tools/Audio/Install Whitebox Reward Audio")]
        public static void Install()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Gameplay/MainLevel.unity")
                throw new InvalidOperationException("请在非 Play 模式打开 Level_Whitebox。");
            var owners = UnityEngine.Object.FindObjectsByType<ExplorationMusicZones>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (owners.Length != 1) throw new InvalidOperationException("需要唯一 Bank Owner。");
            var owner = owners[0];
            var backends = UnityEngine.Object.FindObjectsByType<RewardAudioBackend>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (backends.Length > 1) throw new InvalidOperationException("存在重复音频后端。");
            RewardAudioBackend backend;
            if (backends.Length == 1) backend = backends[0];
            else
            {
                var root = new GameObject("Audio_RewardBackend");
                Undo.RegisterCreatedObjectUndo(root, "Install reward audio");
                root.transform.SetParent(owner.transform.parent, false);
                backend = Undo.AddComponent<RewardAudioBackend>(root);
            }
            var so = new SerializedObject(backend);
            so.FindProperty("bankOwner").objectReferenceValue = owner;
            var listeners = UnityEngine.Object.FindObjectsByType<AkAudioListener>(FindObjectsSortMode.None);
            if (listeners.Length != 1) throw new InvalidOperationException("UI音频需要明确的唯一Listener。");
            so.FindProperty("uiListener").objectReferenceValue = listeners[0];
            // 已有场景数组不会自动继承新增字段默认项；只补缺项，保留用户映射。
            var mappings = so.FindProperty("mappings");
            bool hasLoss = false;
            for (int i = 0; i < mappings.arraySize; i++)
                hasLoss |= mappings.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue == (int)AudioCue.AbilityLost;
            if (!hasLoss)
            {
                int index = mappings.arraySize++;
                var entry = mappings.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("cue").intValue = (int)AudioCue.AbilityLost;
                entry.FindPropertyRelative("eventName").stringValue = "Play_Player_AbilityLoss";
            }
            so.ApplyModifiedProperties();
            AddMapping(backend, AudioCue.UIConfirm, "Play_UI_Click");
            AddMapping(backend, AudioCue.UIHovered, "Play_UI_Hover");
            AddMapping(backend, AudioCue.SwitchActivated, "Play_SwitchActivate");
            var switches = UnityEngine.Object.FindObjectsByType<PrototypeToggleButton2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var worldSwitch in switches) ConfigureEmitter(worldSwitch.gameObject);
            foreach (var panel in UnityEngine.Object.FindObjectsByType<ChoicePanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var button = new SerializedObject(panel).FindProperty("cancelButton").objectReferenceValue as Button;
                if (button != null && button.GetComponent<ChoiceCancelHover>() == null)
                    Undo.AddComponent<ChoiceCancelHover>(button.gameObject);
            }
            ConfigureEmitter(backend.gameObject);
            var chests = UnityEngine.Object.FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var chest in chests) ConfigureEmitter(chest.gameObject);
            var player = new SerializedObject(owner).FindProperty("player").objectReferenceValue as Transform;
            if (player == null) throw new InvalidOperationException("缺少玩家引用。");
            ConfigureEmitter(player.gameObject);
            EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
            EditorSceneManager.SaveScene(owner.gameObject.scene);
            Debug.Log($"[RewardAudioSetup] Installed backend, player, {chests.Length} chests and {switches.Length} switches.");
        }
        private static void ConfigureEmitter(GameObject target)
        {
            var emitter = target.GetComponent<AkGameObj>();
            if (emitter == null) emitter = Undo.AddComponent<AkGameObj>(target);
            var so = new SerializedObject(emitter);
            so.FindProperty("isEnvironmentAware").boolValue = false;
            so.ApplyModifiedProperties();
        }
        private static void AddMapping(RewardAudioBackend backend, AudioCue cue, string eventName)
        {
            var so = new SerializedObject(backend);
            var list = so.FindProperty("mappings");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue == (int)cue) return;
            int index = list.arraySize++;
            var entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("cue").intValue = (int)cue;
            entry.FindPropertyRelative("eventName").stringValue = eventName;
            so.ApplyModifiedProperties();
        }
    }
}
