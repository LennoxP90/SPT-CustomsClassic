using SPTarkov.Server.Core.Models.Eft.Common;

namespace CustomsClassic.Server;

/// <summary>The 4.1 loose loot points that rest on a source surface (db/classic/bigmap/loose-kept.json).</summary>
public sealed class LooseKept
{
    public List<string> Ids { get; set; } = [];
    public int Of { get; set; }
}

/// <summary>4.1's loose loot on the kept points, with the per-raid count scaled by the share kept.</summary>
public static class ClassicLooseLoot
{
    public static LooseLoot Build(LooseLoot shipped, LooseKept kept, out string summary)
    {
        var ids = new HashSet<string>(kept.Ids, StringComparer.Ordinal);
        var points = (shipped.Spawnpoints ?? []).Where(p => p.Template?.Id is { } id && ids.Contains(id)).ToList();
        var total = shipped.Spawnpoints?.Count() ?? 0;
        var share = total == 0 ? 0 : (double)points.Count / total;
        var mean = (shipped.SpawnpointCount?.Mean ?? 0) * share;
        var std = (shipped.SpawnpointCount?.Std ?? 0) * share;
        summary = $"{points.Count} of {total} loose loot point(s) kept on source surfaces, mean {mean:0} per raid";
        return new LooseLoot
        {
            SpawnpointCount = new SpawnpointCount { Mean = mean, Std = std },
            SpawnpointsForced = [],
            Spawnpoints = points,
        };
    }
}
