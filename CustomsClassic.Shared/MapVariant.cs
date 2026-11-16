#nullable enable
using System;

namespace CustomsClassic.Shared
{
    /// <summary>The two variants of Customs. "original" is what SPT ships; "classic" is the one this mod builds.</summary>
    public static class MapVariant
    {
        public const string Original = "original";
        public const string Classic = "classic";

        public static bool IsClassic(string? variant)
            => string.Equals((variant ?? "").Trim(), Classic, StringComparison.OrdinalIgnoreCase);

        public static string Normalise(string? variant) => IsClassic(variant) ? Classic : Original;
    }

    /// <summary>
    /// MapVariants' words against this mod's. Every crossing goes through here: passing a value straight across
    /// yields the shipped map, silently, in either direction.
    /// </summary>
    public static class VariantVocabulary
    {
        public const string MapVariantsOriginal = "original";
        public const string MapVariantsBackport = "backport";
        public const string MapVariantsUnmanaged = "unmanaged";

        public static bool IsUnmanaged(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsUnmanaged, StringComparison.OrdinalIgnoreCase);

        public static string FromMapVariants(string? variant)
            => string.Equals((variant ?? "").Trim(), MapVariantsBackport, StringComparison.OrdinalIgnoreCase)
                ? MapVariant.Classic
                : MapVariant.Original;

        public static string ToMapVariants(string? variant)
            => MapVariant.IsClassic(variant) ? MapVariantsBackport : MapVariantsOriginal;
    }
}
