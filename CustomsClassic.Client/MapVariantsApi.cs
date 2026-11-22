using System;
using System.Reflection;
using HarmonyLib;

namespace CustomsClassic.Client
{
    /// <summary>
    /// MapVariants' public API by reflection: a compile reference would load a second copy of its shared assembly and
    /// turn a version mismatch into a hard load failure. The BepInDependency guarantees the plugin is present.
    /// </summary>
    internal static class MapVariantsApi
    {
        const string TypeName = "MapVariants.Client.Api.Maps";
        static Type _type;
        static MethodInfo _register, _registerSource;
        static PropertyInfo _currentLocationId;

        static bool Look()
        {
            if (_type != null) return true;
            _type = AccessTools.TypeByName(TypeName);
            if (_type == null) { Plugin.Log.LogError($"[CC] {TypeName} not found"); return false; }
            _register = AccessTools.Method(_type, "Register");
            // MapVariants 1.2.0 and up. Absent before that, and then Register is used.
            _registerSource = AccessTools.Method(_type, "RegisterSource");
            _currentLocationId = AccessTools.Property(_type, "CurrentLocationId");
            return true;
        }

        internal static bool Register(string[] locationIds, string displayName, string backportName, string originalName,
                                      string backportTile, string originalTile, Action<string> onVariant, Func<string[]> warnings)
        {
            if (!Look() || _register == null) return false;
            var result = _register.Invoke(null, new object[]
                { locationIds, displayName, backportName, originalName, backportTile, originalTile, onVariant, warnings });
            return result is bool ok && ok;
        }

        /// <summary>True when MapVariants holds several mods' variants per map, so Classic registers as a source.</summary>
        internal static bool HasSources => Look() && _registerSource != null;

        /// <summary>
        /// Classic as one source among several: another mod's Customs is offered beside it. onVariant still hears
        /// "backport" for Classic and "original" for anything else.
        /// </summary>
        internal static bool RegisterSource(string[] locationIds, string displayName, string variantId, string variantName,
                                            string originalName, string variantTile, string originalTile,
                                            Action<string> onVariant, Func<string[]> warnings)
        {
            if (!HasSources) return false;
            var result = _registerSource.Invoke(null, new object[]
                { locationIds, displayName, variantId, variantName, originalName, variantTile, originalTile, onVariant, warnings });
            return result is bool ok && ok;
        }

        internal static string CurrentLocationId => Look() ? _currentLocationId?.GetValue(null) as string : null;
    }
}
