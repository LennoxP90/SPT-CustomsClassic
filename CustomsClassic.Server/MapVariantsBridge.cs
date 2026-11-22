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
    private MethodInfo? _register, _installedVariant, _registerSource, _shipped;
    private bool _bound;

    /// <summary>MapVariants 1.2.0 and up: a map may hold several mods' variants, so Classic joins under its own id.</summary>
    public bool HasSources
    {
        get { Bind(); return _registerSource is not null && _shipped is not null; }
    }

    private bool Bind()
    {
        if (_bound) return _providers is not null;
        _bound = true;
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(ProvidersTypeName, false)).FirstOrDefault(t => t is not null);
        if (type is null) { logger.Error($"[CC] {ProvidersTypeName} is not loaded; Customs has no variant switching"); return false; }
        _providers = provider.GetService(type);
        _register = type.GetMethod("Register", [typeof(string), typeof(Location), typeof(Location)]);
        _installedVariant = type.GetMethod("InstalledVariant", [typeof(string)]);
        // Absent before MapVariants 1.2.0, which is not an error: Register is used instead.
        _registerSource = type.GetMethod("RegisterSource", [typeof(string), typeof(string), typeof(Location)]);
        _shipped = type.GetMethod("Shipped", [typeof(string)]);
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

    /// <summary>
    /// Hands Classic over as one source among several (MapVariants 1.2.0 and up). The stock side is the shipped map
    /// MapVariants captured itself.
    /// </summary>
    public bool RegisterSource(string locationId, string variantId, Location variant)
    {
        if (!HasSources) return false;
        try { return _registerSource!.Invoke(_providers, [locationId, variantId, variant]) is true; }
        catch (Exception e) { logger.Error($"[CC] handing '{locationId}' to MapVariants as source '{variantId}' threw: {e}"); return false; }
    }

    /// <summary>
    /// The shipped location as MapVariants captured it before any mod's Preload, or null before 1.2.0. By PostLoad the
    /// live location may already be another mod's Customs.
    /// </summary>
    public Location? Shipped(string locationId)
    {
        if (!HasSources) return null;
        try { return _shipped!.Invoke(_providers, [locationId]) as Location; }
        catch (Exception e) { logger.Warning($"[CC] reading the shipped '{locationId}' from MapVariants threw, the live one is used: {e.Message}"); return null; }
    }

    /// <summary>MapVariants' wire value; translate with VariantVocabulary before comparing to this mod's.</summary>
    public string? InstalledVariant(string locationId)
    {
        if (!Bind()) return null;
        try { return _installedVariant!.Invoke(_providers, [locationId]) as string; }
        catch { return null; }
    }
}
