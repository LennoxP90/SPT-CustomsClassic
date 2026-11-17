using System;
using System.Linq;
using CustomsClassic.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterSSR;

namespace CustomsClassic.Client
{
    /// <summary>
    /// 4.1 draws water through a WaterForSSRv3 listing the meshes, not through a renderer's material. The source
    /// river's pieces are handed to one carrying the modern Customs river's settings and the source's own textures.
    /// </summary>
    internal static class ClassicWater
    {
        const string WaterMaterial = "Water4Advanced";

        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CustomsScenes.IsClassicScene(scene.name)) return;
            try
            {
                // Two roots are called river: the water, and the props along its banks. The water is found by material name,
                // since the bundle's copy of the water shader does not keep its name.
                var pieces = scene.GetRootGameObjects().Where(g => g.name == "river")
                    .SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(false))
                    .Where(r => r.GetComponent<MeshFilter>() && r.sharedMaterial && r.sharedMaterial.name.StartsWith(WaterMaterial)).ToArray();
                if (pieces.Length == 0) { Plugin.Log.LogInfo("[CC] water: no river water in this build; the bed stays dry"); return; }
                var river = pieces[0].transform.root.gameObject;
                var own = pieces[0].sharedMaterial;
                var waves = own.GetTexture("_BumpMap");
                var shore = own.GetTexture("_ShoreTex");

                var holder = new GameObject("CustomsClassic_Water");
                holder.SetActive(false);
                holder.transform.SetParent(river.transform, false);
                var water = holder.AddComponent<WaterForSSRv3>();
                water._settings = Settings(waves, shore);
                water._targets = pieces.Select(r => new WaterObject(r.GetComponent<MeshFilter>(), r, r.gameObject)).ToArray();
                foreach (var r in pieces) r.sharedMaterials = Array.Empty<Material>();
                holder.SetActive(true);
                Plugin.Log.LogInfo($"[CC] water: {pieces.Length} river piece(s) given to the game's water renderer (waves {(waves ? waves.name : "none")}, shore {(shore ? shore.name : "none")})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] water: the river could not be set up and may draw wrong: {e}");
            }
        }

        // The modern Customs river's values. Its textures are not loaded on a classic raid, so the source's stand in.
        static WaterSettings Settings(Texture waves, Texture shore)
        {
            var s = new WaterSettings
            {
                // Every texture must be set: the renderer's property block throws on a null one and nothing draws.
                _rippleTexture = Flat(new Color32(128, 128, 255, 255)),
                _normals = waves ? waves : Flat(new Color32(128, 128, 255, 255)),
                _normalsDetails = waves ? waves : Flat(new Color32(128, 128, 255, 255)),
                _foam = shore ? shore : Flat(new Color32(0, 0, 0, 255)),
                _normalsDetailsMipMapBias = -2f,
                _normalsA = Blend(0.071f, 7.9f, -5.3f, 0.367f), _normalsB = Blend(0.0973f, -6.7f, 4.7f, 0.361f),
                _normalsDetails0 = Blend(0.5f, -2.3f, 1.7f, 0.272f),
                _foamA = Blend(0.3f, -2f, 2f, 0.511f), _foamB = Blend(1f, 2f, -2f, 0.489f),
                _borderFade = 2f, _borderFadeDistStart = 5f, _borderFadeDistRange = 66f,
                _depthFade = 2f, _depthColorFade = 0.09f, _depthRefractions = 0.1f,
                _depthColorDeep = new Color(0f, 0.136f, 0.142f, 1f), _depthColorShallow = new Color(0.251f, 0.311f, 0.175f, 1f),
                _bumpiness = 0.5f, _rippleScale = 11f, _rippleBumpness = 0.25f,
                _foamSize = 0.12f, _foamIntensity = 7f, _foamColor = new Color(1f, 1f, 1f, 0f),
                _fresnelIntensity = 0.44f, _fresnelPower = 5f,
                _additionalCubemapReflectionMinWetting = 0.05f, _additionalCubemapReflectionMaxWetting = 0.35f,
                _reflectionColor = Color.white, _diffuseColor = Color.black,
            };
            s.IsDirty = true;
            return s;
        }

        static Texture2D Flat(Color32 colour)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "classic_water_flat", wrapMode = TextureWrapMode.Repeat };
            t.SetPixels32(Enumerable.Repeat(colour, 16).ToArray());
            t.Apply(false, true);
            return t;
        }

        static WaterSettings.TextureBlendSetting Blend(float scale, float x, float y, float blend)
            => new WaterSettings.TextureBlendSetting { Scale = scale, MovementDirection = new Vector2(x, y), Blend = blend };
    }
}
