using System.Linq;
using CustomsClassic.Shared;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomsClassic.Client
{
    /// <summary>Points the rebuilt scene's silent barbed wire at the game's loaded BarbedWire sound bank, by name.</summary>
    internal static class ClassicSounds
    {
        const string Bank = "BarbedWire";

        internal static void Install() => SceneManager.sceneLoaded += OnSceneLoaded;

        static bool HasClip(SoundBank bank) => bank && bank.PickSingleClip(0);

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!CustomsScenes.IsClassicScene(scene.name)) return;
            var field = AccessTools.Field(typeof(BarbedWire), "_soundBank");
            var wires = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BarbedWire>(true)).ToArray();
            if (field == null || wires.Length == 0)
            {
                Plugin.Log.LogWarning($"[CC] barbed wire: {wires.Length} wire(s), sound field {(field == null ? "not found" : "found")}; nothing bound");
                return;
            }
            var banks = Resources.FindObjectsOfTypeAll<SoundBank>();
            var bank = banks.FirstOrDefault(b => b && b.name == Bank && HasClip(b)) ?? banks.FirstOrDefault(b => b && b.name == Bank);
            var first = field.GetValue(wires[0]) as SoundBank;
            var silent = wires.Where(w => !HasClip(field.GetValue(w) as SoundBank)).ToArray();
            if (bank) foreach (var w in silent) field.SetValue(w, bank);
            // A bank borrowed by name has no owner to load its clips.
            foreach (var clip in bank?.Environments?.Where(e => e?.Clips != null).SelectMany(e => e.Clips).Where(v => v?.Clips != null)
                         .SelectMany(v => v.Clips).Where(c => c && c.loadState != AudioDataLoadState.Loaded) ?? Enumerable.Empty<AudioClip>())
                clip.LoadAudioData();
            Plugin.Log.LogInfo($"[CC] barbed wire: {wires.Length} wire(s), {silent.Length} silent (first held {(first ? first.name : "nothing")}), " +
                               (bank ? $"bound to '{bank.name}' ({(HasClip(bank) ? "has clips" : "NO clips")})" : $"no loaded bank named '{Bank}' among {banks.Length}"));
        }
    }
}
