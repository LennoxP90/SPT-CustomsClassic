using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CustomsClassic.Client
{
    /// <summary>The classic scene bundle, loaded once per process. A missing or mis-sized one latches off.</summary>
    internal static class ClassicBundle
    {
        const string Name = "customsclassic_map";
        static AssetBundle _bundle;
        static bool _tried;
        internal static string[] ScenePaths { get; private set; } = new string[0];

        static string Dir => Path.Combine(Plugin.PluginDir, "plugin-data", "map");

        internal static bool Ensure()
        {
            if (_bundle) return true;
            if (_tried) return false;
            _tried = true;
            var bundlePath = Path.Combine(Dir, Name + ".bundle");
            var stampPath = Path.Combine(Dir, Name + "-stamp.json");
            try
            {
                if (!File.Exists(bundlePath) || !File.Exists(stampPath))
                {
                    Plugin.Log.LogError($"[CC] map bundle or stamp missing in {Dir}; classic raids load the shipped Customs");
                    return false;
                }
                var expected = (long?)JObject.Parse(File.ReadAllText(stampPath))["BundleBytes"];
                var actual = new FileInfo(bundlePath).Length;
                if (expected != actual)
                {
                    Plugin.Log.LogError($"[CC] map bundle is {actual} bytes, its stamp says {expected}; classic disabled");
                    return false;
                }
                _bundle = AssetBundle.LoadFromFile(bundlePath);
                if (!_bundle) { Plugin.Log.LogError($"[CC] AssetBundle.LoadFromFile failed for {bundlePath}"); return false; }
                ScenePaths = _bundle.GetAllScenePaths();
                Plugin.Log.LogInfo($"[CC] map bundle loaded: {string.Join(", ", ScenePaths)}");
                return ScenePaths.Length > 0;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] loading the map bundle failed, classic disabled: {e}");
                return false;
            }
        }
    }
}
