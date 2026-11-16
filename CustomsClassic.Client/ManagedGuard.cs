using System;
using System.Linq;
using CustomsClassic.Shared;
using Newtonsoft.Json.Linq;
using SPT.Common.Http;

namespace CustomsClassic.Client
{
    /// <summary>
    /// Whether MapVariants' server manages Customs, asked once per session. Without it a failed server half would
    /// load the classic scenes over the shipped location's spawn points. Unanswerable counts as not managed.
    /// </summary>
    internal static class ManagedGuard
    {
        static bool? _managed;

        internal static bool CustomsManaged()
        {
            if (_managed.HasValue) return _managed.Value;
            try
            {
                var ids = JObject.Parse(RequestHandler.GetJson("/mapvariants/managed"))["locationIds"] as JArray;
                _managed = ids != null && ids.Any(i => string.Equals(i?.ToString(), CustomsScenes.LocationId, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[CC] could not read /mapvariants/managed ({e.Message}); no classic raid this session");
                _managed = false;
            }
            if (!_managed.Value) Plugin.Log.LogError("[CC] MapVariants does not manage bigmap; the shipped Customs loads whatever is chosen");
            return _managed.Value;
        }
    }
}
