using CustomsClassic.Shared;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace CustomsClassic.Server;

/// <summary>
/// Builds classic Customs beside the shipped one and hands it to MapVariants. Runs at PostLoad: after other mods'
/// edits to the shipped location, before MapVariants' discovery pass. With MapVariants 1.2.0 and up Classic is one
/// source among several, so another mod's Customs (a backport such as FumsCustoms) is offered beside it; before
/// that the pair is registered and Classic owns the map alone.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public class VariantRegistry(
    LocationTable locationTable,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    MapVariantsBridge mapVariants,
    ClassicBossList bosses,
    ISptLogger<VariantRegistry> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var id = CustomsScenes.LocationId;
        var live = locationTable.GetLocation(id);
        if (live is null) { logger.Error($"[CC] no location '{id}'; Classic Customs is not available"); return Task.CompletedTask; }

        // Built from the shipped map, never from whatever another mod has already put on the live location.
        var sources = mapVariants.HasSources;
        var vanilla = (sources ? mapVariants.Shipped(id) : null) ?? live;

        try
        {
            var root = modHelper.GetAbsolutePathToModFolder(typeof(VariantRegistry).Assembly);
            var spawnsPath = Path.Combine(root, "db", "classic", id, "player-spawns.json");
            var source = jsonUtil.DeserializeFromFile<List<SourceSpawn>>(spawnsPath) ?? new List<SourceSpawn>();
            var containers = jsonUtil.DeserializeFromFile<List<SourceContainer>>(Path.Combine(root, "db", "classic", id, "containers.json"))
                             ?? new List<SourceContainer>();
            var looseKept = jsonUtil.DeserializeFromFile<LooseKept>(Path.Combine(root, "db", "classic", id, "loose-kept.json")) ?? new LooseKept();
            var offGround = jsonUtil.DeserializeFromFile<List<string>>(Path.Combine(root, "db", "classic", id, "bot-spawns-off.json")) ?? new List<string>();
            var liftedPath = Path.Combine(root, "db", "classic", id, "bot-spawns-y.json");
            var lifted = (File.Exists(liftedPath) ? jsonUtil.DeserializeFromFile<Dictionary<string, float>>(liftedPath) : null) ?? new Dictionary<string, float>();
            var classic = ClassicDataset.Build(vanilla, source, containers, looseKept, offGround, lifted, jsonUtil, out var openZones, out var summary);
            bosses.Bind(classic.Base, openZones, Path.Combine(root, "config", "bosses.json"));
            summary += "; bosses: " + bosses.Apply();
            logger.Info($"[CC] {id} classic dataset built: {summary}");

            if (sources)
            {
                if (mapVariants.RegisterSource(id, VariantVocabulary.MapVariantsClassic, classic))
                    logger.Info($"[CC] {id} handed to MapVariants as source '{VariantVocabulary.MapVariantsClassic}'; it owns the choice, the routes and the swap from here");
                else
                    logger.Error($"[CC] {id} could NOT be handed to MapVariants; it stays vanilla only");
            }
            // The LIVE location goes over as original: MapVariants captures both field sets before installing anything.
            else if (mapVariants.Register(id, vanilla, classic))
                logger.Info($"[CC] {id} handed to MapVariants; it owns the choice, the routes and the swap from here");
            else
                logger.Error($"[CC] {id} could NOT be handed to MapVariants; it stays vanilla only");
        }
        catch (Exception e)
        {
            logger.Error($"[CC] could not build classic Customs; it stays vanilla only: {e}");
        }
        return Task.CompletedTask;
    }
}
