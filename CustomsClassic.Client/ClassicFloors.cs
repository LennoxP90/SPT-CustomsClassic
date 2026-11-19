using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>Lets bodies and dropped items rest on LowPolyCollider, the only collider many source floors carry.</summary>
    internal static class ClassicFloors
    {
        static bool _resting;

        internal static void Install()
        {
            SceneManager.sceneLoaded += (scene, mode) => Apply();
            SceneManager.sceneUnloaded += scene => Apply();
        }

        static void Apply()
        {
            var classic = ClassicState.Loaded();
            if (classic == _resting) return;
            _resting = classic;
            var floor = LayerMask.NameToLayer("LowPolyCollider");
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Deadbody"), floor, !classic);
            Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Loot"), floor, !classic);
            Plugin.Log.LogInfo($"[CC] bodies and dropped items {(classic ? "rest on" : "no longer rest on")} walking colliders");
        }
    }
}
