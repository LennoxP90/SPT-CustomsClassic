using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace CustomsClassic.Server;

/// <summary>One source loot container (db/classic/bigmap/containers.json): its scene id and container template.</summary>
public sealed class SourceContainer
{
    public string Id { get; set; } = "";
    public string Template { get; set; } = "";
    public string Scene { get; set; } = "";
}

/// <summary>The classic container table: one copy of a shipped entry per source container, keyed by its scene id.</summary>
public static class ClassicContainers
{
    public static StaticContainerDetails Build(StaticContainerDetails shipped, IEnumerable<SourceContainer> source, JsonUtil jsonUtil,
                                               out string summary)
    {
        var byTemplate = shipped.StaticContainers
            .Where(c => c.Template?.Items?.FirstOrDefault() is not null)
            .GroupBy(c => c.Template!.Items!.First().Template.ToString())
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<StaticContainerData>();
        var seen = new HashSet<string>();
        int noTemplate = 0, duplicates = 0;
        foreach (var a in source)
        {
            if (!seen.Add(a.Id)) { duplicates++; continue; }
            if (!byTemplate.TryGetValue(a.Template, out var model)) { noTemplate++; continue; }
            var copy = jsonUtil.Deserialize<StaticContainerData>(jsonUtil.Serialize(model)!)!;
            var root = new MongoId();
            var items = copy.Template!.Items!.ToList();
            items[0].Id = root;
            copy.Template.Items = items;
            copy.Template.Root = root.ToString();
            copy.Template.Id = a.Id;
            result.Add(copy);
        }

        summary = $"{result.Count} source container(s) served loot; {noTemplate} without a shipped template, {duplicates} duplicate id(s) skipped";
        return new StaticContainerDetails
        {
            StaticWeapons = [],
            StaticContainers = result,
            StaticForced = [],
        };
    }
}
