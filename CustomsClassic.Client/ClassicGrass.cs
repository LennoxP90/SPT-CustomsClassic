using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EFT.CameraControl;
using GPUInstancer;
using UnityEngine;

namespace CustomsClassic.Client
{
    /// <summary>
    /// Draws the source terrain's grass layers through GPUInstancer, as EFT draws its grass: Unity's stock detail
    /// renderer is outside EFT's lighting. The manager is assembled by hand, since the editor setup is absent in game.
    /// </summary>
    internal static class ClassicGrass
    {
        internal static bool Plant(Terrain terrain)
        {
            try
            {
                if (terrain.terrainData.detailPrototypes.Length == 0) return false;
                var holder = new GameObject($"CustomsClassic_Grass_{terrain.name}");
                holder.SetActive(false);
                holder.transform.SetParent(terrain.transform, false);
                var manager = holder.AddComponent<GPUInstancerDetailManager>();
                manager._terrain = terrain;
                var settings = ScriptableObject.CreateInstance<GPUInstancerTerrainSettings>();
                settings.maxDetailDistance = Mathf.Max(terrain.detailObjectDistance, 60f);
                settings.maxDetailDistanceLegacy = terrain.detailObjectDistance;
                // Blade size and colour are read from the noise: with none bound the blades collapse and draw dark.
                settings.healthyDryNoiseTexture = Resources.Load<Texture2D>(GPUInstancerConstants.NOISE_TEXTURES_PATH + GPUInstancerConstants.DEFAULT_HEALTHY_DRY_NOISE) ?? Noise(false);
                settings.windWaveNormalTexture = Resources.Load<Texture2D>(GPUInstancerConstants.NOISE_TEXTURES_PATH + GPUInstancerConstants.DEFAULT_WIND_WAVE_NOISE) ?? Noise(true);
                settings.wavingGrassTint = terrain.terrainData.wavingGrassTint;
                manager.terrainSettings = settings;
                manager.prototypeList = Prototypes(terrain, settings, manager.IsOptic);
                Plugin.Instance.StartCoroutine(StartWithCamera(manager));
                Plugin.Log.LogInfo($"[CC] grass: {terrain.name} on GPUInstancer, {manager.prototypeList.Count} prototype(s), {settings.maxDetailDistance:0} m");
                return manager.prototypeList.Count > 0;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"[CC] grass: {terrain.name} not taken by GPUInstancer, stock grass hidden: {e}");
                return false;
            }
        }

        // AddDetailInstancePrototypeFromTerrainPrototype without its editor parts, which are empty or throw in the game build.
        static List<GPUInstancerPrototype> Prototypes(Terrain terrain, GPUInstancerTerrainSettings settings, bool optic)
        {
            var list = new List<GPUInstancerPrototype>();
            // A manager loads these in its Awake, which has not run on the still-inactive holder.
            if (GPUInstancerConstants.gpuiSettings == null)
                GPUInstancerConstants.gpuiSettings = GPUInstancerSettings.GetDefaultGPUInstancerSettings();
            GPUInstancerConstants.gpuiSettings.SetDefultBindings();
            var gpui = GPUInstancerConstants.gpuiSettings;
            var source = terrain.terrainData.detailPrototypes;
            for (var i = 0; i < source.Length; i++)
            {
                var d = source[i];
                if (d.usePrototypeMesh || !d.prototypeTexture) continue;   // the source's are all texture grass
                var p = ScriptableObject.CreateInstance<GPUInstancerDetailPrototype>();
                p.name = $"Detail_{i}_{d.prototypeTexture.name}";
                p.prototypeIndex = i;
                p.detailRenderMode = d.renderMode;
                p.usePrototypeMesh = false;
                p.prototypeTexture = d.prototypeTexture;
                p.useCrossQuads = true;
                p.quadCount = 2;
                p.useVertexFit = true;
                p.detailHealthyColor = d.healthyColor;
                p.detailDryColor = d.dryColor;
                p.noiseSpread = d.noiseSpread;
                p.detailScale = new Vector4(d.minWidth, d.maxWidth, d.minHeight, d.maxHeight);
                p.windWaveTintColor = settings.wavingGrassTint;
                p.isBillboardDisabled = true;
                p.isShadowCasting = gpui.DEFAULT_GRASS_SHADOW_DISTANCE > 0f && !optic;
                p.useCustomShadowDistance = p.isShadowCasting;
                p.shadowDistance = gpui.DEFAULT_GRASS_SHADOW_DISTANCE;
                p.cullShadows = true;
                list.Add(p);
            }
            return list;
        }

        // Starts the manager once a camera exists, then binds a normal map built from the heightmap: the classic terrains
        // draw without instancing, so Unity never builds Terrain.normalmapTexture and the grass would sample black.
        static IEnumerator StartWithCamera(GPUInstancerDetailManager manager)
        {
            // Started before the player's camera exists, the manager builds no instance cells and draws nothing.
            var deadline = Time.realtimeSinceStartup + 300f;
            while (manager && !(CameraManager.Exist && CameraManager.Instance.Camera) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!manager) yield break;
            manager.gameObject.SetActive(true);
            Plugin.Log.LogInfo($"[CC] grass: {manager.terrain.name} drawing for {(manager.Camera ? manager.Camera.name : "NO camera")}");
            for (var i = 0; i < 8; i++) yield return null;
            if (!manager || manager.runtimeDataList == null) yield break;
            var texture = NormalMap(manager.terrain.terrainData);
            var bound = 0;
            foreach (var data in manager.runtimeDataList)
                foreach (var lod in data?.instanceLODs ?? new System.Collections.Generic.List<GPUInstancerPrototypeLOD>())
                    foreach (var renderer in lod?.renderers ?? new System.Collections.Generic.List<GPUInstancerRenderer>())
                        if (renderer?.mpb != null) { renderer.mpb.SetTexture(GPUInstancerUtility._terrainNormalMap, texture); bound++; }
            Plugin.Log.LogInfo($"[CC] grass: {manager.terrain.name} normal map {texture.width}px bound to {bound} renderer(s)");
        }

        // Stand-ins when the game does not ship GPUInstancer's default noise: tiling Perlin greys, or a flat normal.
        static Texture2D Noise(bool normal)
        {
            const int n = 256;
            var px = new Color32[n * n];
            for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var v = (byte)(Mathf.Clamp01(Mathf.PerlinNoise(x / 24f, y / 24f) * .6f + Mathf.PerlinNoise(x / 7f, y / 7f) * .4f) * 255);
                    px[y * n + x] = normal ? new Color32(128, 128, 255, 255) : new Color32(v, v, v, 255);
                }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = normal ? "classic_wind_flat" : "classic_grass_noise", wrapMode = TextureWrapMode.Repeat };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }

        static Texture2D NormalMap(TerrainData data)
        {
            var res = data.heightmapResolution;
            var h = data.GetHeights(0, 0, res, res);
            var size = data.size;
            float cx = size.x / (res - 1), cz = size.z / (res - 1);
            var px = new Color32[res * res];
            for (var z = 0; z < res; z++)
            {
                int z0 = Mathf.Max(z - 1, 0), z1 = Mathf.Min(z + 1, res - 1);
                for (var x = 0; x < res; x++)
                {
                    int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
                    var n = new Vector3(-(h[z, x1] - h[z, x0]) * size.y / ((x1 - x0) * cx), 1f,
                                        -(h[z1, x] - h[z0, x]) * size.y / ((z1 - z0) * cz)).normalized;
                    px[z * res + x] = new Color32((byte)((n.x * .5f + .5f) * 255), (byte)((n.y * .5f + .5f) * 255),
                                                  (byte)((n.z * .5f + .5f) * 255), 255);
                }
            }
            var t = new Texture2D(res, res, TextureFormat.RGBA32, true, true) { name = data.name + "_classic_normals", wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }
}
