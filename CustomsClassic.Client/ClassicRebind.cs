using System.Collections.Generic;
using System.Linq;
using CustomsClassic.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CustomsClassic.Client
{
    /// <summary>Points classic materials at the game's shaders by name: a bundle's own shader copies draw wrong.</summary>
    internal static class ClassicRebind
    {
        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            ["Nature/SpeedTree"] = "Nature/SpeedTreeEFT",
            ["Nature/SpeedTree Billboard"] = "Nature/SpeedTree Billboard Replaced",
            ["Particles/Additive"] = "Legacy Shaders/Particles/Additive",
            ["Particles/Alpha Blended"] = "Legacy Shaders/Particles/Alpha Blended",
            ["Particles/Alpha Blended Premultiply"] = "Legacy Shaders/Particles/Alpha Blended",
            ["Particles/VertexLit Blended"] = "Legacy Shaders/Particles/Alpha Blended",
            ["Nature/Terrain/Standard"] = "Nature/Terrain/Diffuse",
            // 4.1 ships no FX/Water4; SimpleWater4 takes the same properties.
            ["FX/Water4"] = "FX/SimpleWater4",
        };
        const string TerrainShader = "Nature/Terrain/Diffuse";

        const int SourceRoadLayer = 21;

        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        // Shader.Find can hand back the bundle's own copy of a name, so the bundle's shaders are tracked and skipped.
        static readonly HashSet<Shader> Own = new HashSet<Shader>();
        static Dictionary<string, Shader> _game;

        static Shader Game(string name)
        {
            if (_game.TryGetValue(name, out var loaded)) return loaded;
            var found = Shader.Find(name);
            return found && !Own.Contains(found) ? found : null;
        }

        static Shader GameShader(string name)
        {
            var s = Game(name);
            return s ? s : Alias.TryGetValue(name, out var alt) ? Game(alt) : null;
        }

        static int Rebind(Material m, HashSet<string> missing)
        {
            if (!m || !m.shader) return 0;
            var game = GameShader(m.shader.name);
            if (!game) { missing.Add(m.shader.name); return 0; }
            if (game == m.shader) return 0;
            // Assigning a shader resets the render queue, which is the road pieces' draw order.
            var queue = m.renderQueue;
            m.shader = game;
            m.renderQueue = queue;
            return 1;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CustomsScenes.IsClassicScene(scene.name)) return;
            var missing = new HashSet<string>();
            int rebound = 0;
            var roots = scene.GetRootGameObjects();
            var renderers = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            var terrains = roots.SelectMany(g => g.GetComponentsInChildren<Terrain>(true)).ToArray();
            // Never cleared: a copy from an earlier classic raid in this process must not pass for the game's.
            foreach (var m in renderers.SelectMany(r => r.sharedMaterials)
                         .Concat(terrains.SelectMany(t => t.terrainData.treePrototypes).Where(p => p.prefab)
                             .SelectMany(p => p.prefab.GetComponentsInChildren<Renderer>(true)).SelectMany(r => r.sharedMaterials)))
                if (m && m.shader) Own.Add(m.shader);
            _game = new Dictionary<string, Shader>();
            foreach (var shader in Resources.FindObjectsOfTypeAll<Shader>())
                if (shader && !Own.Contains(shader) && !_game.ContainsKey(shader.name)) _game[shader.name] = shader;
            // Layer 21 was the source's road layer and is CullingMask in 4.1, which the sun skips: roads go to Default.
            int relayered = 0;
            foreach (var r in renderers.Where(r => r.gameObject.layer == SourceRoadLayer)) { r.gameObject.layer = 0; relayered++; }
            Plugin.Log.LogInfo("[CC] " + scene.name + ": " + relayered + " road renderer(s) moved off the CullingMask layer");
            foreach (var r in renderers)
                foreach (var m in r.sharedMaterials) rebound += Rebind(m, missing);
            Plugin.Log.LogInfo($"[CC] {scene.name}: AmbientLight {(Object.FindObjectOfType<AmbientLight>() ? "present" : "MISSING")}, " +
                               $"{roots.Sum(g => g.GetComponentsInChildren<AnalyticSource>(true).Length)} room volume(s), " +
                               $"{roots.Sum(g => g.GetComponentsInChildren<StencilShadow>(true).Length)} stencil volume(s)");

            var terrainShader = Shader.Find(TerrainShader);
            foreach (var t in terrains)
            {
                // Stock terrain grass draws black under EFT's lighting, so when GPUInstancer cannot take it the grass is hidden.
                if (!ClassicGrass.Plant(t)) t.detailObjectDistance = 0f;
                // The built-in terrain material's 4.1 shader is stripped yet still found by name, so it is replaced outright.
                if (terrainShader && (t.materialTemplate == null || t.materialTemplate.shader != terrainShader))
                    t.materialTemplate = new Material(terrainShader) { name = t.name + "_classic_terrain" };
                foreach (var proto in t.terrainData.treePrototypes.Where(p => p.prefab))
                    foreach (var m in proto.prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials))
                        rebound += Rebind(m, missing);
            }
            Plugin.Log.LogInfo($"[CC] {scene.name}: {rebound} material(s) rebound, terrains on {string.Join(", ", terrains.Select(t => $"{t.name}={(t.materialTemplate ? t.materialTemplate.shader.name : "built-in")}"))}");
            if (missing.Count > 0) Plugin.Log.LogWarning($"[CC] {scene.name}: no game shader for {string.Join(", ", missing)}");
        }
    }
}
