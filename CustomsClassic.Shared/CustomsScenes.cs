#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CustomsClassic.Shared
{
    /// <summary>Customs' scene preset and what the Classic variant loads instead: geometry and culling scenes are dropped.</summary>
    public static class CustomsScenes
    {
        public const string LocationId = "bigmap";
        public const string ScriptsScene = "Assets/Content/Locations/Custom/custom_Scripts.unity";
        public const string ClassicScenePrefix = "customsclassic_";

        static readonly HashSet<string> Kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "custom_Scripts", "custom_DesignMain", "custom_AI", "Custom_Sound" };

        public static bool IsCustomsPreset(IEnumerable<string?> paths) => paths.Any(p => p == ScriptsScene);

        public static bool IsKept(string? path) => path != null && Kept.Contains(Path.GetFileNameWithoutExtension(path));

        public static bool IsClassicScene(string? sceneName)
            => sceneName != null && sceneName.StartsWith(ClassicScenePrefix, StringComparison.Ordinal);

        public static IReadOnlyList<string> ClassicPaths(IReadOnlyList<string?> shipped, IReadOnlyList<string> bundleScenes)
        {
            var result = new List<string>();
            foreach (var path in shipped)
            {
                if (!IsKept(path)) continue;
                result.Add(path!);
                if (path == ScriptsScene) result.AddRange(bundleScenes);
            }
            if (!result.Contains(ScriptsScene)) result.InsertRange(0, bundleScenes);
            return result;
        }
    }
}
