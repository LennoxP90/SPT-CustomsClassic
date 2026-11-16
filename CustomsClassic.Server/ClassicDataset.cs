using CustomsClassic.Shared;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace CustomsClassic.Server;

/// <summary>
/// Builds the classic Location as a new object, so a player who never picks Classic is untouched. The base is a JSON
/// round-trip copy of the shipped one with the source's player spawns; the tables not rebuilt are shared read-only.
/// </summary>
public static class ClassicDataset
{
    public static Location Build(Location vanilla, IReadOnlyList<SourceSpawn> source, IReadOnlyList<SourceContainer> containers,
                                 LooseKept looseKept, IReadOnlyCollection<string> offGround, IReadOnlyDictionary<string, float> lifted,
                                 JsonUtil jsonUtil,
                                 out HashSet<string> openZones, out string summary)
    {
        var copy = jsonUtil.Deserialize<LocationBase>(jsonUtil.Serialize(vanilla.Base)!)!;
        var shipped = copy.SpawnPointParams?.ToList() ?? new List<SpawnPointParam>();
        var template = shipped.FirstOrDefault(s => s.Categories?.Contains("Player") == true);

        // Shipped bot points with no walkable classic ground under them (db/classic/bigmap/bot-spawns-off.json).
        var off = new HashSet<string>(offGround);
        var kept = new List<SpawnPointParam>();
        var grounded = 0;
        var raised = 0;
        foreach (var s in shipped)
        {
            var left = ClassicSpawns.WithoutPlayer(s.Categories);
            if (left is null) continue;
            if (off.Contains(s.Id.ToString())) { grounded++; continue; }
            s.Categories = left;
            // The shipped height can be under classic's floor, which starts the bot inside the collider.
            if (lifted.TryGetValue(s.Id.ToString(), out var y) && s.Position is { } at) { s.Position = new Vector3(at.X, y, at.Z); raised++; }
            kept.Add(s);
        }

        var added = new List<SpawnPointParam>();
        foreach (var a in source)
        {
            var role = ClassicSpawns.RoleOf(a);
            if (role.Pmc) added.Add(Point(a, template, true, role.Infiltration));
            if (role.Savage) added.Add(Point(a, template, false, ""));
        }

        copy.SpawnPointParams = kept.Concat(added).ToList();

        // A zone with no bot point left takes no boss; ClassicBossList strikes it from the boss list at raid start.
        var open = kept.Select(s => s.BotZoneName).Where(z => !string.IsNullOrEmpty(z)).Select(z => z!).ToHashSet();
        var closed = shipped.Select(s => s.BotZoneName).Where(z => !string.IsNullOrEmpty(z) && !open.Contains(z!)).Distinct().OrderBy(z => z).ToList();
        openZones = open;
        var shippedExits = copy.Exits?.Count() ?? 0;
        copy.Exits = copy.Exits?.Where(e => ClassicExits.IsClassic(e.Name)).ToList();
        foreach (var e in copy.Exits ?? [])
        {
            if (ClassicExits.Find(e.Name)!.Scav) continue;
            e.EntryPoints = ClassicExits.BothSides;
            e.Chance = 100;
        }
        summary = $"{added.Count(p => p.Sides!.Contains("Pmc"))} PMC and {added.Count(p => p.Sides!.Contains("Savage"))} scav player spawn(s) " +
                  $"from {source.Count} source point(s); {kept.Count} shipped point(s) kept for bots, {grounded} off playable ground dropped, {raised} lifted onto classic's floor, {shipped.Count - kept.Count - grounded} player-only dropped; " +
                  $"zone(s) with no bot point left: {(closed.Count == 0 ? "none" : string.Join(", ", closed))}; " +
                  $"{copy.Exits?.Count() ?? 0} of {shippedExits} exits kept (moved to the source's spots by the client)";

        var staticContainers = ClassicContainers.Build(vanilla.StaticContainers!.Value!, containers, jsonUtil, out var containerSummary);
        summary += "; " + containerSummary;
        var looseLoot = ClassicLooseLoot.Build(vanilla.LooseLoot!.Value!, looseKept, out var looseSummary);
        summary += "; " + looseSummary;

        return new Location
        {
            Base = copy,
            LooseLoot = new SPTarkov.Server.Core.Utils.Json.LazyLoad<LooseLoot>(() => looseLoot),
            StaticContainers = new SPTarkov.Server.Core.Utils.Json.LazyLoad<StaticContainerDetails>(() => staticContainers),
            StaticLoot = vanilla.StaticLoot,
            StaticAmmo = vanilla.StaticAmmo,
            Statics = vanilla.Statics,
            AllExtracts = vanilla.AllExtracts,
        };
    }

    private static SpawnPointParam Point(SourceSpawn a, SpawnPointParam? template, bool pmc, string infiltration) => new()
    {
        Id = ClassicSpawns.IdFor(a, pmc),
        Position = new Vector3((float)a.Pos[0], (float)a.Pos[1], (float)a.Pos[2]),
        Rotation = a.Yaw,
        Sides = [pmc ? "Pmc" : "Savage"],
        Categories = ["Player"],
        Infiltration = infiltration,
        DelayToCanSpawnSec = template?.DelayToCanSpawnSec ?? 4,
        ColliderParams = template?.ColliderParams,
        CorePointId = template?.CorePointId,
        BotZoneName = template?.BotZoneName,
    };
}
