using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>Switches off the modern props custom_DesignMain brings to a classic raid. Seasonal event objects stay.</summary>
    internal static class ClassicLeftovers
    {
        const string DesignMain = "custom_DesignMain";
        // Whole groups with nothing a classic raid uses: a mounted gun must not stay usable while unseen, and a lamp
        // controller would switch its light back on at dusk.
        static readonly string[] Removed = { "Stationary_Weapons", "LIGHT_TOGGLEABLE" };

        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != DesignMain || !ClassicState.Loaded()) return;
            try
            {
                int groups = 0, drawn = 0, lights = 0, solid = 0, sounds = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name.StartsWith("Event", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Removed.Contains(root.name)) { root.SetActive(false); groups++; continue; }
                    // Exits and quest zones live among the rest, so only what shows, lights, blocks or sounds goes.
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true)) { r.enabled = false; drawn++; }
                    foreach (var l in root.GetComponentsInChildren<Light>(true)) { l.enabled = false; lights++; }
                    foreach (var c in root.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger)) { c.enabled = false; solid++; }
                    foreach (var a in root.GetComponentsInChildren<AudioSource>(true)) { a.enabled = false; sounds++; }
                }
                Plugin.Log.LogInfo($"[CC] DesignMain leftovers: {groups} group(s) off, {drawn} renderer(s), {lights} light(s), {solid} solid collider(s), {sounds} sound(s) disabled");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] hiding the modern props failed; some stay visible: {e}");
            }
        }
    }
}
