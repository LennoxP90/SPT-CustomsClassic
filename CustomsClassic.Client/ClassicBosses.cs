using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using CustomsClassic.Shared;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CustomsClassic.Client
{
    /// <summary>Keeps a boss out of a zone a player stands in or another boss took, and cultists out of daytime.</summary>
    internal class ClassicBossZonePatch : ModulePatch
    {
        const float Near = 75f;   // a player this close to one of a zone's spawn points is in the zone

        // Zone name to the boss that took it, for the raid `ClaimsWorld` belongs to.
        static readonly Dictionary<string, BossLocationSpawn> Claims = new Dictionary<string, BossLocationSpawn>();
        static GameWorld ClaimsWorld;

        protected override MethodBase GetTargetMethod() =>
            typeof(BossLocationSpawn).GetMethod(nameof(BossLocationSpawn.GetPossibleZones), new[] { typeof(BotZone[]), typeof(List<BotZone>) });

        [PatchPrefix]
        static bool Prefix(BossLocationSpawn __instance, BotZone[] allZones, ref List<BotZone> __result) => Decide(__instance, allZones, ref __result);

        internal static bool Decide(BossLocationSpawn __instance, BotZone[] allZones, ref List<BotZone> __result)
        {
            if (!ClassicState.Loaded() || __instance._possibleShuffledZones.Count > 0) return true;
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            if (world == null) return true;

            if (__instance.BossType.IsSectant())
            {
                var hour = world.GameDateTime.Calculate().Hour;
                if (!ClassicBossZones.IsNight(hour))
                {
                    Plugin.Log.LogInfo($"[CC] {__instance.BossName} stays out: it is {hour}:00 and cultists come at night");
                    __result = new List<BotZone>();
                    return false;
                }
            }

            var born = __instance.BornZone;
            if (string.IsNullOrEmpty(born) || born.Length <= 1) return true;
            if (ClaimsWorld != world) { Claims.Clear(); ClaimsWorld = world; }
            var players = world.RegisteredPlayers.OfType<Player>().Concat(new[] { world.MainPlayer }).Where(p => p != null && !p.IsAI)
                .Select(p => p.Position).Distinct().ToList();
            Plugin.Log.LogInfo($"[CC] boss {__instance.BossName} drew {born} of ({__instance.BossZone}); {players.Count} player(s) to keep clear of");
            string taker = null;
            bool Taken(string name)
            {
                if (Claims.TryGetValue(name, out var other) && other != __instance) { if (name == born) taker = other.BossName; return true; }
                var zone = allZones.FirstOrDefault(z => z != null && z.NameZone == name);
                return zone?.SpawnPointMarkers != null && zone.SpawnPointMarkers.Any(m => m != null && players.Any(p => (m.transform.position - p).sqrMagnitude < Near * Near));
            }
            var zoneName = ClassicBossZones.AwayFromPlayers(born, __instance.BossZone ?? "", Taken, Random.Range(0, 1000));
            if (zoneName == null)
            {
                Plugin.Log.LogInfo($"[CC] {__instance.BossName} stays out: a player or another boss is in every zone it has ({__instance.BossZone})");
                __result = new List<BotZone>();
                return false;
            }
            Claims[zoneName] = __instance;
            if (zoneName == born) return true;
            Plugin.Log.LogInfo($"[CC] {__instance.BossName} moved from {born} to {zoneName}: {(taker ?? "a player")} is in {born}");
            __instance.BornZone = zoneName;
            return true;
        }
    }

    /// <summary>The same rule for a boss whose wave names an exact position.</summary>
    internal class ClassicBossZoneAtPositionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() =>
            typeof(BossLocationSpawn).GetMethod(nameof(BossLocationSpawn.GetPossibleZones), new[] { typeof(BotZone[]), typeof(List<BotZone>), typeof(Vector3) });

        [PatchPrefix]
        static bool Prefix(BossLocationSpawn __instance, BotZone[] allZones, ref List<BotZone> __result) => ClassicBossZonePatch.Decide(__instance, allZones, ref __result);
    }
}
