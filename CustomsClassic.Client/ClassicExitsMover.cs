using System;
using System.Linq;
using CustomsClassic.Shared;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>Moves each exit in custom_DesignMain to its source spot, set on the classic terrain by a raycast.</summary>
    internal static class ClassicExitsMover
    {
        const string DesignMain = "custom_DesignMain";

        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != DesignMain || !ClassicState.Loaded()) return;
            try
            {
                int moved = 0;
                var exits = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ExfiltrationPoint>(true)).ToArray();
                Plugin.Log.LogInfo($"[CC] DesignMain exits: {string.Join(", ", exits.Select(e => e.Settings?.Name))}");
                foreach (var exit in exits)
                {
                    // An exit with no classic name goes to the classic spot nearest its modern position.
                    var target = ClassicExits.Find(exit.Settings?.Name) ?? Nearest(exit.transform.position);
                    var at = Ground((float)target.X, (float)target.Z);
                    exit.transform.position = at;
                    moved++;
                    Plugin.Log.LogInfo($"[CC] exit '{exit.Settings?.Name}' moved to {target.Name}'s spot ({at.x:0.0}, {at.y:0.0}, {at.z:0.0})");
                }
                Plugin.Log.LogInfo($"[CC] {moved} exit(s) moved to the source's spots");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] moving the exits failed; they stay at modern positions: {e}");
            }
        }

        static ClassicExit Nearest(Vector3 at)
            => ClassicExits.All.OrderBy(e => (e.X - at.x) * (e.X - at.x) + (e.Z - at.z) * (e.Z - at.z)).First();

        static Vector3 Ground(float x, float z)
        {
            var from = new Vector3(x, 200f, z);
            // Terrain only: power lines and roofs stand over some spots, and exits are outdoors.
            return Physics.Raycast(from, Vector3.down, out var hit, 400f, 1 << 11, QueryTriggerInteraction.Ignore)
                ? hit.point
                : new Vector3(x, 1f, z);
        }
    }
}
