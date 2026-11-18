using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.AI;

namespace CustomsClassic.Client
{
    /// <summary>Fits the kept custom_AI scene's cover and patrol points to the classic map's navmesh.</summary>
    internal static class ClassicAi
    {
        const float OnMesh = 1f;          // a point this close to the navmesh is kept and set onto it
        const float Taken = 1.5f;         // a source spot this close to a kept point adds nothing
        const float Cell = 10f;
        const int Solid = (1 << 0) | (1 << 11) | (1 << 12) | (1 << 18);

        // Runs from the navmesh swap. A sub-point list never goes empty (the game indexes it), nor does a zone's first way.
        internal static void TrimPatrols()
        {
            int moved = 0, dropped = 0;
            var closed = new List<string>();
            foreach (var zone in UnityEngine.Object.FindObjectsOfType<BotZone>())
            {
                var ways = (zone.PatrolWays ?? new PatrolWay[0]).Where(w => w != null && w.Points != null).ToList();
                foreach (var way in ways)
                    foreach (var point in way.Points.Where(p => p != null).ToArray())
                    {
                        var subs = (point.subPoints ?? new List<PatrolPoint>()).Where(s => s != null && s != point).ToList();
                        var subAt = subs.Select(s => s.transform.position).ToList();
                        var ok = Settle(point.transform, 3f, ref moved);
                        for (var i = 0; i < subs.Count; i++) subs[i].transform.position = subAt[i];   // children moved with it
                        if (!ok) { way.Points.Remove(point); dropped++; continue; }
                        var good = subs.Where(s => Settle(s.transform, 3f, ref moved)).ToList();
                        dropped += subs.Count - good.Count;
                        if (good.Count == 0 && subs.Count > 0) { subs[0].transform.position = point.transform.position; good.Add(subs[0]); }
                        if (subs.Count > 0) point.subPoints = good;
                    }
                var live = ways.Where(w => w.Points.Count > 0).ToArray();
                if (live.Length > 0) { zone.PatrolWays = live; continue; }
                zone.SpawnPointMarkers?.Clear();   // the zone keeps its name for the spawn tables, and takes no bots
                closed.Add(zone.name);
            }
            Plugin.Log.LogInfo($"[CC] patrols: {moved} point(s) set onto the source navmesh, {dropped} dropped, " +
                               (closed.Count == 0 ? "no zone closed" : "closed to spawns: " + string.Join(", ", closed)));
        }

        static bool Settle(Transform t, float reach, ref int moved)
        {
            if (!NavMesh.SamplePosition(t.position, out var hit, reach, NavMesh.AllAreas)) return false;
            if ((hit.position - t.position).sqrMagnitude > 0.02f) { t.position = hit.position; moved++; }
            return true;
        }

        static (int, int) Key(Vector3 p) => (Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));

        static GroupPoint Nearest(Dictionary<(int, int), List<GroupPoint>> grid, Vector3 at, int rings)
        {
            GroupPoint best = null;
            var bestSqr = float.MaxValue;
            var (cx, cz) = Key(at);
            for (var ring = 0; ring <= rings && (best == null || ring <= 1); ring++)
                for (var x = cx - ring; x <= cx + ring; x++)
                    for (var z = cz - ring; z <= cz + ring; z++)
                    {
                        if (Math.Max(Math.Abs(x - cx), Math.Abs(z - cz)) != ring || !grid.TryGetValue((x, z), out var cell)) continue;
                        foreach (var p in cell)
                        {
                            var sqr = (p.Position - at).sqrMagnitude;
                            if (sqr < bestSqr) { bestSqr = sqr; best = p; }
                        }
                    }
            return best;
        }

        // After the game has resolved its cover graph and before it builds the per-bot lists.
        internal static void RepairCovers(AICoversData covers)
        {
            var watch = Stopwatch.StartNew();
            var dead = new HashSet<GroupPoint>();
            var grid = new Dictionary<(int, int), List<GroupPoint>>();
            var set = 0;
            foreach (var p in covers.Points)
            {
                if (!NavMesh.SamplePosition(p.Position, out var hit, OnMesh, NavMesh.AllAreas)) { dead.Add(p); continue; }
                var shift = hit.position - p.Position;
                if (shift.sqrMagnitude > 0.02f) { p.SetPosition(hit.position); p.FirePosition += shift; set++; }
                if (!grid.TryGetValue(Key(p.Position), out var cell)) grid[Key(p.Position)] = cell = new List<GroupPoint>();
                cell.Add(p);
            }

            // The game's own RemovePoint walks every voxel per point; this is the same unlinking done once for the lot.
            var deadIds = new HashSet<int>(dead.Select(p => p.Id));
            var deadWays = new HashSet<GroupPointWay>(dead.SelectMany(p => p.NeighbourhoodsWays));
            covers.Points.RemoveAll(dead.Contains);
            foreach (var p in covers.Points)
                foreach (var way in p.NeighbourhoodsWays.Where(w => dead.Contains(w.Target)).ToArray()) { p.RemoveWay(way); deadWays.Add(way); }
            covers.Ways.RemoveAll(deadWays.Contains);
            var orphaned = 0;
            foreach (var voxel in covers.VoxelsList)
            {
                voxel.Points?.RemoveAll(dead.Contains);
                voxel.PointsIds?.RemoveAll(deadIds.Contains);
                if (voxel.PointStartSearch == null || !dead.Contains(voxel.PointStartSearch)) continue;
                var heir = Nearest(grid, voxel.PointStartSearch.Position, 6);
                if (heir != null) voxel.SetClosestsPoint(heir);
                else { voxel._closetsPointId = 0; voxel.PointStartSearch = null; orphaned++; }
            }

            var added = Add(covers, grid, out var offered, out var why);
            Plugin.Log.LogInfo($"[CC] cover points: {dead.Count} off the source navmesh removed, {set} set onto it, {orphaned} voxel(s) left " +
                               $"with no start point, {added} of {offered} source spot(s) added ({why}), {covers.Points.Count} in all, {watch.ElapsedMilliseconds} ms");
        }

        // Each source spot is [x, y, z, wallX, wallZ]. The wall's side and height are read off the geometry, not the
        // vector's sign. Core point and group come from the nearest kept point.
        static int Add(AICoversData covers, Dictionary<(int, int), List<GroupPoint>> grid, out int offered, out string why)
        {
            offered = 0;
            why = "";
            int offMesh = 0, alone = 0, taken = 0, noWall = 0, noPath = 0;
            var file = Path.Combine(Plugin.PluginDir, "plugin-data", "ai", "source_cover.json");
            if (!File.Exists(file)) { Plugin.Log.LogWarning($"[CC] {file} is missing; no source cover added"); return 0; }
            var nextId = covers.Points.Select(p => p.Id).DefaultIfEmpty(0).Max() + 1;
            var added = 0;
            foreach (var spot in JArray.Parse(File.ReadAllText(file)).Select(s => s.Select(v => (float)v).ToArray()))
            {
                offered++;
                if (!NavMesh.SamplePosition(new Vector3(spot[0], spot[1], spot[2]), out var hit, OnMesh, NavMesh.AllAreas)) { offMesh++; continue; }
                var at = hit.position;
                var near = Nearest(grid, at, 3);
                if (near == null) { alone++; continue; }
                if ((near.Position - at).sqrMagnitude < Taken * Taken) { taken++; continue; }
                var core = covers.AICorePointsHolder.GetCorePoint(near.CorePointId);
                var wall = new Vector3(spot[3], 0f, spot[4]).normalized;
                if (core == null || wall == Vector3.zero) { alone++; continue; }
                if (!Wall(at, wall, 0.45f)) { wall = -wall; if (!Wall(at, wall, 0.45f)) { noWall++; continue; } }
                var tall = Wall(at, wall, 1.5f);
                var level = tall ? CoverLevel.Stay : Wall(at, wall, 1f) ? CoverLevel.Sit : CoverLevel.Lay;
                // Behind a wall taller than a head there is nothing to shoot over: a place to hide.
                var point = new GroupPoint(nextId, null, at, null, core, level, false, wall, at, tall ? PointWithNeighborType.ambush : PointWithNeighborType.cover);
                covers.Points.Add(point);
                if (!AIManualPointsHolder.TryConnectPoint(point, covers)) { covers.Points.Remove(point); noPath++; continue; }
                covers.GetVoxelSafe(at)?.AddCover(point);
                point.InitForGame(covers.AICorePointsHolder);
                if (!grid.TryGetValue(Key(at), out var cell)) grid[Key(at)] = cell = new List<GroupPoint>();
                cell.Add(point);
                nextId++;
                added++;
            }
            why = $"{taken} beside a kept point, {noWall} with no wall at hand, {offMesh} off the navmesh, {alone} with no kept point near, {noPath} unreachable";
            return added;
        }

        static bool Wall(Vector3 feet, Vector3 toward, float height) =>
            Physics.Raycast(feet + Vector3.up * height, toward, 1.2f, Solid, QueryTriggerInteraction.Ignore);
    }

    internal class ClassicCoversPatch : ModulePatch
    {
        static int _done;

        protected override MethodBase GetTargetMethod() => typeof(AICoversData).GetMethod(nameof(AICoversData.RestoreData));

        [PatchPostfix]
        static void Postfix(AICoversData __instance, bool __result)
        {
            if (!__result || !ClassicState.Loaded() || _done == __instance.GetInstanceID()) return;
            _done = __instance.GetInstanceID();
            try { ClassicAi.RepairCovers(__instance); }
            catch (Exception e) { Plugin.Log.LogError($"[CC] cover repair failed part way; bots may lack cover: {e}"); }
        }
    }
}
