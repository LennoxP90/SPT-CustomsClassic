using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using Location = SPTarkov.Server.Core.Models.Eft.Common.Location;

namespace CustomsClassic.Server;

/// <summary>
/// MapVariants' server half by reflection: a compile reference would fail this mod's load when their assembly is a
/// different version. ModDependencies already refuses the mod when MapVariants is missing.
/// </summary>
[Injectable(InjectionType.Singleton), UsedImplicitly]
public class MapVariantsBridge(IServiceProvider provider, ISptLogger<MapVariantsBridge> logger)
{
    private const string ProvidersTypeName = "MapVariants.Server.Variants.Providers";
    private object? _providers;
    private MethodInfo? _register, _installedVariant;
    private bool _bound;

    private bool Bind()
    {
        if (_bound) return _providers is not null;
        _bound = true;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(ProvidersTypeName, false)).FirstOrDefault(t => t is not null);
        if (type is null) { logger.Error($"[CC] {ProvidersTypeName} is not loaded; Customs has no variant switching"); return false; }
        _providers = provider.GetService(type);
        _register = type.GetMethod("Register", [typeof(string), typeof(Location), typeof(Location)]);
        _installedVariant = type.GetMethod("InstalledVariant", [typeof(string)]);
        if (_providers is null || _register is null || _installedVariant is null)
        {
            logger.Error("[CC] MapVariants' provider surface is not the expected layout; no Customs variant is offered");
            _providers = null;
            return false;
        }
        return true;
    }

    public bool Register(string locationId, Location original, Location backport)
    {
        if (!Bind()) return false;
        try { _register!.Invoke(_providers, [locationId, original, backport]); return true; }
        catch (Exception e) { logger.Error($"[CC] handing '{locationId}' to MapVariants threw: {e}"); return false; }
    }

    /// <summary>MapVariants' wire value; translate with VariantVocabulary before comparing to this mod's.</summary>
    public string? InstalledVariant(string locationId)
    {
        if (!Bind()) return null;
        try { return _installedVariant!.Invoke(_providers, [locationId]) as string; }
        catch { return null; }
    }
}
