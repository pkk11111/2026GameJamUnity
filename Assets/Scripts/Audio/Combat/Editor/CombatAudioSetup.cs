// 职责：主图音频显式接线与事件补缺；只在Editor运行。
// 依赖音频/玩法/UnityEditor；维护audio-full-events；交接docs/handoffs/audio-full-events.handoff；规范AGENTS.md。
using System;
using System.Linq;
using Regrowth.Gameplay;
using Regrowth.Gameplay.Challenge;
using Regrowth.Gameplay.WhiteBox;
using Regrowth.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Regrowth.Audio.Editor
{
    public static class CombatAudioSetup
    {
        [MenuItem("Tools/Audio/Install Remaining Event Audio")]
        public static void Install()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Gameplay/MainLevel.unity") throw new InvalidOperationException("Open main scene outside Play.");
            var player = UnityEngine.Object.FindObjectsByType<WhiteboxPlayer2D>(FindObjectsSortMode.None).Single();
            var backend = UnityEngine.Object.FindObjectsByType<RewardAudioBackend>(FindObjectsSortMode.None).Single();
            var music = UnityEngine.Object.FindObjectsByType<ExplorationMusicZones>(FindObjectsSortMode.None).Single();
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyBasic>(FindObjectsSortMode.None);
            var elite = enemies.Single(e => e.name == "Elite_Exit");
            if (enemies.Any(e => e.transform.position.y > elite.transform.position.y)) throw new InvalidOperationException("Elite_Exit is not the top enemy; review classification.");
            foreach (var enemy in enemies)
            {
                bool added = enemy.GetComponent<EnemyAudio>() == null;
                var audio = enemy.GetComponent<EnemyAudio>() ?? Undo.AddComponent<EnemyAudio>(enemy.gameObject);
                var data = new SerializedObject(audio);
                data.FindProperty("elite").boolValue = enemy == elite;
                data.FindProperty("contact").objectReferenceValue = enemy.GetComponentInChildren<EnemyContactAttack>(true);
                if (added) data.FindProperty("hurtInterval").floatValue = enemy == elite ? 2.9f : 1.05f;
                data.ApplyModifiedProperties();
            }
            var component = player.GetComponent<PlayerCombatAudio>() ?? Undo.AddComponent<PlayerCombatAudio>(player.gameObject);
            var bindings = new SerializedObject(component);
            Bind(bindings, "state", player.GetComponent<PlayerState>()); Bind(bindings, "motor", player);
            Bind(bindings, "bite", player.GetComponentInChildren<PlayerBiteAttack>(true));
            Bind(bindings, "sword", player.GetComponentInChildren<PlayerSwordAttack>(true));
            Bind(bindings, "fire", player.GetComponentInChildren<PlayerFireAttack>(true));
            Bind(bindings, "elite", elite); Bind(bindings, "eliteAI", elite.GetComponent<EnemyBasicAI>()); Bind(bindings, "music", music);
            bindings.ApplyModifiedProperties();
            foreach (var door in UnityEngine.Object.FindObjectsByType<WorldDoor>(FindObjectsSortMode.None)) Emitter(door.gameObject);
            foreach (var door in UnityEngine.Object.FindObjectsByType<InspectionDoor2D>(FindObjectsSortMode.None)) Emitter(door.gameObject);
            foreach (var portal in UnityEngine.Object.FindObjectsByType<PrototypePortal2D>(FindObjectsSortMode.None))
            {
                Emitter(portal.gameObject);
                var destination = (Transform)new SerializedObject(portal).FindProperty("destination").objectReferenceValue;
                Emitter(destination.gameObject);
            }
            Add(backend, AudioCue.PlayerBite, "Play_Player_Bite"); Add(backend, AudioCue.PlayerWeaponAttack, "Play_Player_SwordSwing");
            Add(backend, AudioCue.PlayerHurt, "Play_Player_Hurt"); Add(backend, AudioCue.PlayerDied, "Play_Player_Death");
            Add(backend, AudioCue.PlayerLand, "Play_Land"); Add(backend, AudioCue.PlayerFire, "Play_Player_Fire");
            Add(backend, AudioCue.FireHit, "Play_Fire_Hit"); Add(backend, AudioCue.BiteHitNPC, "Play_PlayerBiteHit_NPC");
            Add(backend, AudioCue.BiteHitElite, "Play_PlayerBiteHit_Elite"); Add(backend, AudioCue.SwordHitNPC, "Play_PlayerSwordHit_NPC");
            Add(backend, AudioCue.SwordHitElite, "Play_PlayerSwordHit_Elite"); Add(backend, AudioCue.NPCHurt, "Play_NPC_Hurt");
            Add(backend, AudioCue.NPCDeath, "Play_NPC_Death"); Add(backend, AudioCue.EliteHurt, "Play_Elite_Hurt");
            Add(backend, AudioCue.EliteDeath, "Play_Elite_Death"); Add(backend, AudioCue.EliteAttack, "Play_Elite_Attack");
            Add(backend, AudioCue.DoorOpened, "Play_Door_Open"); Add(backend, AudioCue.PortalIn, "Play_Portal_In");
            Add(backend, AudioCue.PortalOut, "Play_Portal_Out");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[CombatAudio] Saved: " + enemies.Length + " enemies, sole elite=" + elite.name + " at " + elite.transform.position);
        }
        private static void Emitter(GameObject target) { if (target.GetComponent<AkGameObj>() == null) Undo.AddComponent<AkGameObj>(target); }
        private static void Bind(SerializedObject data, string key, UnityEngine.Object value)
        { if (value == null) throw new InvalidOperationException("Missing " + key); data.FindProperty(key).objectReferenceValue = value; }
        private static void Add(RewardAudioBackend backend, AudioCue cue, string name)
        {
            var data = new SerializedObject(backend); var list = data.FindProperty("mappings");
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).FindPropertyRelative("cue").intValue == (int)cue) return;
            var row = list.GetArrayElementAtIndex(list.arraySize++);
            row.FindPropertyRelative("cue").intValue = (int)cue; row.FindPropertyRelative("eventName").stringValue = name;
            data.ApplyModifiedProperties();
        }
    }
}
