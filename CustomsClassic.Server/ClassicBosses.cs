using System.Text.Json.Serialization;
using CustomsClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace CustomsClassic.Server;

/// <summary>One boss's rule in config/bosses.json: chance in percent, zones by name or "*" for every open one.</summary>
public sealed class BossRule
{
    [JsonPropertyName("chance")] public double? Chance { get; set; }
    [JsonPropertyName("zones")] public List<string>? Zones { get; set; }
}

/// <summary>
/// Applies config/bosses.json to the classic boss list at every raid start, not once at load: a spawn mod loading
/// later rewrites the list in place, and again after every raid. The file is read each time.
/// </summary>
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class ClassicBossList(JsonUtil jsonUtil, ICloner cloner, ISptLogger<ClassicBossList> logger)
{
    private readonly Lock _gate = new();
    private LocationBase? _classic;
    private List<BossLocationSpawn> _shipped = [];
    private HashSet<string> _open = [];
    private string _rulesPath = "";
    private Dictionary<string, BossRule> _rules = new();

    /// <summary>`classic` still carries the shipped boss list here; it is kept as the template for named bosses.</summary>
    public void Bind(LocationBase classic, HashSet<string> open, string rulesPath)
    {
        lock (_gate)
        {
            _classic = classic;
            _shipped = cloner.Clone(classic.BossLocationSpawn ?? [])!;
            _open = open;
            _rulesPath = rulesPath;
        }
    }

    public string Apply()
    {
        lock (_gate)
        {
            if (_classic is null) return "classic Customs is not built";
            ReadRules();

            var list = new List<BossLocationSpawn>();
            var notes = new List<string>();
            foreach (var boss in _classic.BossLocationSpawn ?? [])
            {
                if (_rules.ContainsKey(boss.BossName ?? "")) continue;
                var zones = Zones(boss);
                var left = ClassicBossZones.Resolve(null, zones, _open);
                if (zones.Length > 0 && left.Length == 0) { notes.Add($"{boss.BossName} removed (its zones are closed)"); continue; }
                boss.BossZone = string.Join(",", left);
                list.Add(boss);
            }
            var others = list.Count;

            foreach (var (name, rule) in _rules)
            {
                var templates = _shipped.Where(b => b.BossName == name).ToList();
                if (templates.Count == 0) { notes.Add($"{name} is not a boss of this map, rule ignored"); continue; }
                foreach (var template in templates)
                {
                    var zones = ClassicBossZones.Resolve(rule.Zones, Zones(template), _open);
                    var chance = rule.Chance ?? template.BossChance ?? 0;
                    if (chance <= 0 || zones.Length == 0) { notes.Add($"{name} off"); continue; }
                    var boss = cloner.Clone(template)!;
                    boss.BossChance = chance;
                    boss.BossZone = string.Join(",", zones);
                    list.Add(boss);
                    notes.Add($"{name} {chance}% in {(rule.Zones?.Contains(ClassicBossZones.Everywhere) == true ? $"any of {zones.Length} zone(s)" : boss.BossZone)}");
                }
            }

            _classic.BossLocationSpawn = list;
            return $"{string.Join("; ", notes)}; {others} other spawn(s) kept";
        }
    }

    private static string[] Zones(BossLocationSpawn boss) => (boss.BossZone ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    // A file that does not parse keeps the last rules that did, so a typo cannot hand the bosses back to another mod.
    private void ReadRules()
    {
        try
        {
            if (File.Exists(_rulesPath))
                _rules = jsonUtil.DeserializeFromFile<Dictionary<string, BossRule>>(_rulesPath) ?? _rules;
        }
        catch (Exception e)
        {
            logger.Error($"[CC] {_rulesPath} could not be read; the last good boss rules stay in force: {e.Message}");
        }
    }
}

public sealed class ClassicRaidBoundary : IRequestData
{
    [JsonPropertyName("location")] public string Location { get; set; } = "";
}

// After any spawn mod's own load step (the bot placement mod's is PostLoad + 69420).
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 100000), UsedImplicitly]
public class ClassicBossLoad(ClassicBossList bosses, ISptLogger<ClassicBossLoad> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        logger.Info($"[CC] classic bosses after every mod loaded: {bosses.Apply()}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Reapplies the rules at raid configuration, raid start and raid end. Runs one step ahead of SPT's own routers,
/// whose match router copies the location into the response.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Routers - 1), UsedImplicitly]
public class ClassicBossRaidHook(JsonUtil jsonUtil, ClassicBossList bosses, ISptLogger<ClassicBossRaidHook> logger)
    : StaticRouter(jsonUtil,
    [
        new RouteAction<ClassicRaidBoundary>("/client/raid/configuration",
            (url, info, sessionId, output, cancellationToken) => Reapply(info?.Location, "raid configuration", output, bosses, logger)),
        new RouteAction<ClassicRaidBoundary>("/client/match/local/start",
            (url, info, sessionId, output, cancellationToken) => Reapply(info?.Location, "raid start", output, bosses, logger)),
        new RouteAction<ClassicRaidBoundary>("/client/match/local/end",
            (url, info, sessionId, output, cancellationToken) => Reapply(CustomsScenes.LocationId, "raid end", output, bosses, logger)),
    ])
{
    private static ValueTask<string> Reapply(string? location, string when, string? output, ClassicBossList bosses, ISptLogger<ClassicBossRaidHook> logger)
    {
        if (string.Equals(location, CustomsScenes.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            try { logger.Info($"[CC] classic bosses at {when}: {bosses.Apply()}"); }
            catch (Exception e) { logger.Error($"[CC] boss rules could not be applied at {when}; the list is served as it stands: {e}"); }
        }
        return ValueTask.FromResult(output ?? "");
    }
}
