using System;
using System.IO;
using System.Linq;
using System.Reflection;
using EFT;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>
    /// Replaces 4.1's navmesh with the bake of the source map (plugin-data/map/customsclassic_navmesh.bundle) when
    /// bots initialise. Remove first, then add: the reverse drops the new mesh with the old.
    /// </summary>
    internal class ClassicNavMeshPatch : ModulePatch
    {
        const string Name = "customsclassic_navmesh";
        static NavMeshData _data;
        static bool _tried;
        static NavMeshDataInstance _instance;

        protected override MethodBase GetTargetMethod() => typeof(BotsController).GetMethod(nameof(BotsController.Init));

        [PatchPrefix]
        static void Prefix()
        {
            if (!ClassicState.Loaded()) return;
            try
            {
                var data = Data();
                if (data == null) return;
                if (_instance.valid) _instance.Remove();
                NavMesh.RemoveAllNavMeshData();
                _instance = NavMesh.AddNavMeshData(data);
                var tri = NavMesh.CalculateTriangulation();
                Plugin.Log.LogInfo($"[CC] navmesh replaced with the source bake: {tri.vertices.Length} vertices, {tri.indices.Length / 3} triangles");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] navmesh swap failed; bots walk 4.1's mesh: {e}");
                return;
            }
            try { ClassicAi.TrimPatrols(); }
            catch (Exception e) { Plugin.Log.LogError($"[CC] patrol trim failed part way: {e}"); }
        }

        static NavMeshData Data()
        {
            if (_tried) return _data;
            _tried = true;
            var dir = Path.Combine(Plugin.PluginDir, "plugin-data", "map");
            var bundlePath = Path.Combine(dir, Name + ".bundle");
            var stampPath = Path.Combine(dir, Name + "-stamp.json");
            if (!File.Exists(bundlePath) || !File.Exists(stampPath)) { Plugin.Log.LogError($"[CC] {Name} bundle or stamp missing in {dir}"); return null; }
            if ((long?)JObject.Parse(File.ReadAllText(stampPath))["BundleBytes"] != new FileInfo(bundlePath).Length)
            {
                Plugin.Log.LogError($"[CC] {Name} bundle size does not match its stamp");
                return null;
            }
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            _data = bundle ? bundle.LoadAllAssets<NavMeshData>().FirstOrDefault() : null;
            if (_data) UnityEngine.Object.DontDestroyOnLoad(_data);
            else Plugin.Log.LogError($"[CC] {Name} bundle holds no NavMeshData");
            return _data;
        }
    }

    /// <summary>Declines Waypoints' modern Customs navmesh on classic only. Enabled only when Waypoints is installed.</summary>
    internal class WaypointsStandDownPatch : ModulePatch
    {
        internal static MethodBase Injector()
        {
            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name.IndexOf("Waypoints", StringComparison.OrdinalIgnoreCase) >= 0);
                return assembly?.GetTypes().Select(t => t.GetMethod("InjectNavmesh", AccessTools.all))
                    .FirstOrDefault(m => m != null && m.IsStatic && m.GetParameters().Length == 1);
            }
            catch (Exception) { return null; }
        }

        protected override MethodBase GetTargetMethod() => Injector();

        [PatchPrefix]
        static bool Prefix()
        {
            if (!ClassicState.Loaded()) return true;
            Plugin.Log.LogInfo("[CC] declined Waypoints' Customs navmesh on the classic map");
            return false;
        }
    }
}
