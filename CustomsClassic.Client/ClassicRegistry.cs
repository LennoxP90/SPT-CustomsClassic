using System;
using System.Collections.Generic;
using System.Linq;
using CustomsClassic.Shared;
using EFT.Interactive;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>
    /// The classic scene has no LocationScene, through which the game finds containers and interactive objects, so it
    /// gets one filled before it wakes. Containers keep their source ids; a duplicate id is switched off.
    /// </summary>
    internal static class ClassicRegistry
    {
        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CustomsScenes.IsClassicScene(scene.name)) return;
            try
            {
                var roots = scene.GetRootGameObjects();
                var host = new GameObject("CustomsClassic_LocationScene");
                host.SetActive(false);
                SceneManager.MoveGameObjectToScene(host, scene);
                var ls = host.AddComponent<LocationScene>();
                foreach (var f in typeof(LocationScene).GetFields().Where(f => f.FieldType.IsArray && !f.IsStatic && f.GetValue(ls) == null))
                    f.SetValue(ls, Array.CreateInstance(f.FieldType.GetElementType(), 0));

                var seen = new HashSet<string>();
                var containers = new List<LootableContainer>();
                foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<LootableContainer>(true)))
                {
                    if (string.IsNullOrEmpty(c.Id) || !seen.Add(c.Id)) { c.gameObject.SetActive(false); continue; }
                    containers.Add(c);
                }
                ls.LootableContainers = containers.ToArray();
                ls.WorldInteractiveObjects = roots.SelectMany(r => r.GetComponentsInChildren<WorldInteractiveObject>(true))
                    .Where(o => o.gameObject.activeInHierarchy || o is LootableContainer && containers.Contains((LootableContainer)o))
                    .ToArray();

                host.SetActive(true);
                Plugin.Log.LogInfo($"[CC] {scene.name}: registered {ls.LootableContainers.Length} loot container(s), " +
                                   $"{ls.WorldInteractiveObjects.Length} interactive object(s)");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] registry failed on {scene.name}; its containers and doors stay inert: {e}");
            }
        }
    }
}
