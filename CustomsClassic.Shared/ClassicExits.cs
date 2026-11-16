#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomsClassic.Shared
{
    /// <summary>A 4.1 exit, by its scene name, and the source spot (x, z) it moves to on the classic map.</summary>
    public sealed class ClassicExit
    {
        public ClassicExit(string name, double x, double z, bool scav)
        {
            Name = name;
            X = x;
            Z = z;
            Scav = scav;
        }

        public string Name { get; }
        public double X { get; }
        public double Z { get; }
        public bool Scav { get; }
    }

    /// <summary>
    /// The source map's four exit spots, under the names of 4.1 PMC exits whose objects live in custom_DesignMain, and
    /// again under four 4.1 scav exit names. Height comes from the terrain at run time.
    /// </summary>
    public static class ClassicExits
    {
        public static readonly IReadOnlyList<ClassicExit> All = new[]
        {
            new ClassicExit("Old Gas Station", 309.5, -174.3, false),
            new ClassicExit("ZB-1011", 459.4, -111.0, false),
            new ClassicExit("Crossroads", 621.4, -127.7, false),
            new ClassicExit("Trailer Park", -234.0, -219.0, false),
            new ClassicExit("Old Road Gate", 309.5, -174.3, true),
            new ClassicExit("Beyond Fuel Tank", 459.4, -111.0, true),
            new ClassicExit("Railroad To Port", 621.4, -127.7, true),
            new ClassicExit("Shack", -234.0, -219.0, true),
        };

        public static ClassicExit? Find(string? name)
            => All.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

        public static bool IsClassic(string? name) => Find(name) != null;

        /// <summary>Both 4.1 PMC spawn sides: on the small classic map every PMC exit serves either.</summary>
        public const string BothSides = "Customs,Boiler Tanks";
    }
}
