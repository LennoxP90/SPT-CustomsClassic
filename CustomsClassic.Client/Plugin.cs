using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using CustomsClassic.Shared;

namespace CustomsClassic.Client
{
    [BepInPlugin(BuildInfo.Guid, "CustomsClassic", BuildInfo.Version)]
    // Hard: guarantees MapVariants' Awake ran before this one, which makes registering from Awake legal.
    [BepInDependency("com.lennoxp90.mapvariants", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Plugin Instance;
        internal static string PluginDir;

        void Awake()
        {
            Log = Logger;
            Instance = this;
            PluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Safe(nameof(PresetSwapPatch), () => new PresetSwapPatch().Enable());
            Safe(nameof(ClassicWater), ClassicWater.Install);
            Safe(nameof(ClassicRebind), ClassicRebind.Install);
            Safe(nameof(ClassicSounds), ClassicSounds.Install);
            Safe(nameof(ClassicRegistry), ClassicRegistry.Install);
            Safe(nameof(ClassicExitsMover), ClassicExitsMover.Install);
            Safe(nameof(ClassicLeftovers), ClassicLeftovers.Install);
            Safe(nameof(ClassicNavMeshPatch), () => new ClassicNavMeshPatch().Enable());
            Safe(nameof(ClassicCoversPatch), () => new ClassicCoversPatch().Enable());
            Safe(nameof(WaypointsStandDownPatch), () => { if (WaypointsStandDownPatch.Injector() != null) new WaypointsStandDownPatch().Enable(); });
            Safe("MapVariants", RegisterWithMapVariants);

            Log.LogInfo($"[CC] CustomsClassic {BuildInfo.Version} loaded; MapVariants owns the choice");
        }

        // Short names alone: MapVariants composes "Customs - Classic" itself.
        static void RegisterWithMapVariants()
        {
            var ok = MapVariantsApi.Register(new[] { CustomsScenes.LocationId }, "Customs", "Classic", "Vanilla",
                TilePath("customs_classic.png"), TilePath("customs_vanilla.png"), PresetSwap.OnMapVariantsAnswer, Warnings);
            if (ok) Log.LogInfo("[CC] registered Customs with MapVariants");
            else Log.LogError("[CC] MapVariants refused the registration; the shipped Customs loads and nothing is offered");
        }

        static string[] Warnings() => new[] { "Customs quest objectives are placed for the modern map; many cannot be completed on Classic." };

        // Absolute: MapVariants resolves a tile against its own folder otherwise, and the miss is silent.
        static string TilePath(string file) => Path.Combine(PluginDir, "plugin-data", "ui", file);

        static void Safe(string what, Action install)
        {
            try { install(); }
            catch (Exception e) { Log.LogError($"[CC] {what} failed to install: {e}"); }
        }
    }
}
