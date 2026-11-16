using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CustomsClassic.Shared;
using EFT;
using SPT.Reflection.Patching;

namespace CustomsClassic.Client
{
    /// <summary>Points Customs' scene preset at the classic map's scenes, and back for a vanilla raid.</summary>
    internal static class PresetSwap
    {
        // Rests on Classic as MapVariants' server does: the scenes must agree with the location table.
        static string _variant = MapVariant.Classic;

        sealed class Snapshot
        {
            internal List<SceneResourceKey> Entries;
            internal List<string> Paths, Rcids;
        }

        // Presets are cached for the process: remember the shipped list once so a vanilla raid can put it back.
        static readonly Dictionary<ScenesPreset, Snapshot> Snapshots = new Dictionary<ScenesPreset, Snapshot>();

        /// <summary>Called by MapVariants with its own wire value, never this mod's.</summary>
        internal static void OnMapVariantsAnswer(string theirs)
        {
            var ours = VariantVocabulary.FromMapVariants(theirs);
            if (MapVariant.IsClassic(ours) && !ManagedGuard.CustomsManaged()) ours = MapVariant.Original;
            _variant = ours;
            Plugin.Log.LogInfo($"[CC] MapVariants says {(MapVariant.IsClassic(ours) ? "Classic" : "Vanilla")}");
        }

        internal static void BeforePresetLoad(ScenesPreset preset)
        {
            if (!preset || preset._scenesResourceKeys == null) return;
            var keys = preset._scenesResourceKeys;
            var snapshot = Remember(preset, keys);
            if (!CustomsScenes.IsCustomsPreset(snapshot.Paths)) return;

            // Always restore first: the preset is cached and Apply shortens it.
            Revert(keys, snapshot);
            if (!MapVariant.IsClassic(_variant)) return;
            if (!ManagedGuard.CustomsManaged() || !ClassicBundle.Ensure())
            {
                Plugin.Log.LogError("[CC] classic chosen but unavailable; the shipped Customs loads");
                return;
            }
            Apply(keys, CustomsScenes.ClassicPaths(snapshot.Paths, ClassicBundle.ScenePaths));
        }

        static Snapshot Remember(ScenesPreset preset, List<SceneResourceKey> keys)
        {
            if (Snapshots.TryGetValue(preset, out var existing)) return existing;
            var s = new Snapshot
            {
                Entries = keys.ToList(),
                Paths = keys.Select(k => k?.path).ToList(),
                Rcids = keys.Select(k => k?.rcid).ToList(),
            };
            Snapshots[preset] = s;
            return s;
        }

        static void Apply(List<SceneResourceKey> keys, IReadOnlyList<string> want)
        {
            for (var i = 0; i < want.Count && i < keys.Count; i++) { keys[i].path = want[i]; keys[i].rcid = want[i]; }
            for (var i = keys.Count - 1; i >= want.Count; i--) keys.RemoveAt(i);
            Plugin.Log.LogInfo($"[CC] Customs preset now loads {keys.Count} scene(s): {string.Join(", ", keys.Select(k => k.FileName))}");
        }

        static void Revert(List<SceneResourceKey> keys, Snapshot s)
        {
            keys.Clear();
            for (var i = 0; i < s.Entries.Count; i++)
            {
                s.Entries[i].path = s.Paths[i];
                s.Entries[i].rcid = s.Rcids[i];
                keys.Add(s.Entries[i]);
            }
        }
    }

    internal class PresetSwapPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
            => typeof(LoadScenesFromPresetOperation).GetMethod(nameof(LoadScenesFromPresetOperation.LoadPresetAsync));

        [PatchPrefix]
        static void Prefix(ScenesPreset preset)
        {
            try { PresetSwap.BeforePresetLoad(preset); }
            catch (Exception e) { Plugin.Log.LogError($"[CC] preset swap failed, the shipped Customs loads: {e}"); }
        }
    }
}
