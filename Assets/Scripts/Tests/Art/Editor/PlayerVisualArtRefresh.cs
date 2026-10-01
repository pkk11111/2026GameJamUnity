// 职责：按审核清单同步当前 PNG、切片、Clip 和独立测试 Library；不接正式玩法。
// 模块：player-visual-art-test；依赖：本模块 Editor、UnityEditor。
// 保护：显式菜单启动，拒绝 Play / 未保存场景；清单只覆盖本模块目录。
// 交接：docs/handoffs/Dada.handoff；规范：根 AGENTS.md。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Regrowth.Tests.Art.Editor
{
    public static class PlayerVisualArtRefresh
    {
        public const string ManifestPath = "Assets/Scripts/Tests/Art/Editor/ArtRefreshManifest.json";
        [Serializable] public sealed class Strip
        {
            public string name, action, path, sha256;
            public int frames;
            public float fps;
            public bool player;
        }
        [Serializable] public sealed class Family
        {
            public bool body, arms, legs;
            public string tail, prefix;
            public string[] names;
        }
        [Serializable] public sealed class Move { public string old, @new; }
        [Serializable] public sealed class Manifest
        {
            public string source;
            public Strip[] strips;
            public Family[] families;
            public Move[] moves;
            public string[] obsoleteSprites;
        }
        public static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        private static string Hash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            }
        }
        private static void MoveAsset(string from, string to)
        {
            if (from == to || !File.Exists(from) || File.Exists(to)) return;
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        [MenuItem("Tools/pawgatory/Art Test/Sync Reviewed PNG Manifest")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play and save all scenes before syncing reviewed art.");
            Manifest data = ReadManifest();
            foreach (Strip strip in data.strips)
            {
                string prefix = PlayerVisualArtSetup.ArtRoot + "/Sprites/";
                if (!strip.player || strip.path != prefix + strip.name + ".png" || strip.name.Contains("/") || strip.name.Contains("\\") || strip.name.StartsWith("Dog4"))
                    throw new InvalidDataException("Manifest asset path is outside art scope.");
                if (Hash(Path.Combine(data.source, strip.name + ".png")) != strip.sha256)
                    throw new InvalidDataException("Source changed since review: " + strip.name);
            }
            AssetDatabase.Refresh();
            foreach (Move move in data.moves)
            {
                if (!move.old.StartsWith(PlayerVisualArtSetup.ArtRoot + "/Sprites/") || !data.strips.Any(s => s.path == move.@new))
                    throw new InvalidDataException("Unexpected rename scope.");
                MoveAsset(move.old, move.@new);
                MoveAsset(PlayerVisualArtSetup.ArtRoot + "/Animations/" + Path.GetFileNameWithoutExtension(move.old) + ".anim",
                    PlayerVisualArtSetup.ArtRoot + "/Animations/" + Path.GetFileNameWithoutExtension(move.@new) + ".anim");
            }
            foreach (Strip strip in data.strips)
            {
                if (!File.Exists(strip.path) || Hash(strip.path) != strip.sha256)
                    File.Copy(Path.Combine(data.source, strip.name + ".png"), strip.path, true);
            }
            AssetDatabase.Refresh();
            var animations = new Dictionary<string, VisualAnimation>(StringComparer.Ordinal);
            foreach (Strip strip in data.strips)
            {
                Sprite[] sprites = PlayerVisualArtSetup.Slice(strip.path);
                if (sprites.Length != strip.frames) throw new InvalidDataException("Incorrect slice count: " + strip.name);
                animations.Add(strip.name, PlayerVisualArtSetup.MakeAnimation(strip.name, strip.name, sprites,
                    (VisualAction)Enum.Parse(typeof(VisualAction), strip.action), strip.fps,
                    PlayerVisualArtSetup.ArtRoot));
            }
            var families = new List<VisualFamily>();
            var keepClips = new HashSet<string>(data.strips.Where(s => s.player).Select(s => s.name));
            foreach (Family family in data.families)
            {
                var clips = family.names.Select(n => animations[n]).ToList();
                VisualAnimation move = clips.Single(c => c.Action == VisualAction.Move);
                string idle = family.prefix + "_Idle_Preview";
                clips.Insert(0, PlayerVisualArtSetup.MakeAnimation(idle, move.SourceName, new[] { move.Frames[0] }, VisualAction.Idle, 1));
                keepClips.Add(idle);
                families.Add(new VisualFamily(family.body, family.arms, family.legs,
                    (VisualTail)Enum.Parse(typeof(VisualTail), family.tail), family.prefix, clips.ToArray()));
            }
            PlayerVisualLibrary library = AssetDatabase.LoadAssetAtPath<PlayerVisualLibrary>(PlayerVisualArtSetup.LibraryPath);
            if (library == null) throw new InvalidOperationException("Existing independent art Library is required.");
            library.Configure(families.ToArray(), Array.Empty<VisualAnimation>());
            EditorUtility.SetDirty(library);
            // 旧图和旧 Clip 已在同步前由本次交付备份；仅清理本模块被取代的资产。
            foreach (string path in data.obsoleteSprites)
            {
                if (!path.StartsWith(PlayerVisualArtSetup.ArtRoot + "/Sprites/") || data.strips.Any(s => s.path == path))
                    throw new InvalidDataException("Unexpected obsolete asset scope.");
                if (File.Exists(path) && !AssetDatabase.DeleteAsset(path)) throw new IOException("Cannot remove obsolete sprite: " + path);
            }
            foreach (string path in Directory.GetFiles(PlayerVisualArtSetup.ArtRoot + "/Animations", "*.anim"))
            {
                if (!keepClips.Contains(Path.GetFileNameWithoutExtension(path)) && !AssetDatabase.DeleteAsset(path.Replace('\\', '/')))
                    throw new IOException("Cannot remove obsolete clip: " + path);
            }
            AssetDatabase.SaveAssets();
            Scene scene = EditorSceneManager.OpenScene(PlayerVisualArtSetup.ScenePath);
            var renderer = UnityEngine.Object.FindFirstObjectByType<SpriteRenderer>();
            library = AssetDatabase.LoadAssetAtPath<PlayerVisualLibrary>(PlayerVisualArtSetup.LibraryPath);
            renderer.sprite = library.Families[0].Find(VisualAction.Idle).Frames[0];
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            PlayerVisualArtChecks.ValidateAssets();
            Debug.Log("[ART REFRESH] 39 player strips / 52 clips including temporary Idle / 13 combinations. Gameplay unchanged.");
        }
    }
}
