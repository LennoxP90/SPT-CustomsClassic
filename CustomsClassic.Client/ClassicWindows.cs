using System;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>
    /// The rebuilt windows hold 0 for the glass values, and the game divides by one: a broken piece's trigger lands at
    /// infinity and stalls physics queries. Each classic window gets the game's defaults as its scene loads.
    /// </summary>
    internal static class ClassicWindows
    {
        const float ExplosionCoef = 15f, ShotDirectionCoef = 1.5f;
        static readonly Vector2 Torque = new Vector2(2f, 6f);

        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!ClassicState.Loaded()) return;
            try
            {
                int windows = 0, mended = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var w in root.GetComponentsInChildren<WindowBreaker>(true))
                    {
                        windows++;
                        if (Mend(w)) mended++;
                    }
                if (mended > 0) Plugin.Log.LogInfo($"[CC] windows: {mended} of {windows} in {scene.name} given the game's glass values");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] mending the windows of {scene.name} failed; broken glass can stall the raid: {e}");
            }
        }

        static bool Mend(WindowBreaker w)
        {
            var mended = false;
            if (w.InstantExplosionCoef == 0f) { w.InstantExplosionCoef = ExplosionCoef; mended = true; }
            if (w.ShotDirectionCoef == 0f) { w.ShotDirectionCoef = ShotDirectionCoef; mended = true; }
            if (w.TorqueRandomMinMaxCoefs == Vector2.zero) { w.TorqueRandomMinMaxCoefs = Torque; mended = true; }
            return mended;
        }
    }
}
