#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomsClassic.Shared
{
    /// <summary>One source player spawn point (db/classic/bigmap/player-spawns.json).</summary>
    public sealed class SourceSpawn
    {
        public string Name { get; set; } = "";
        public double[] Pos { get; set; } = new double[3];
        public double Yaw { get; set; }

        /// <summary>The source's EPlayerSide flags: 1 Usec, 2 Bear, 4 Savage.</summary>
        public int Side { get; set; }
    }

    /// <summary>Which 4.1 spawn groups a source point feeds on the classic map.</summary>
    public readonly struct ClassicSpawnRole
    {
        public ClassicSpawnRole(bool pmc, bool savage, string infiltration)
        {
            Pmc = pmc;
            Savage = savage;
            Infiltration = infiltration;
        }

        public bool Pmc { get; }
        public bool Savage { get; }

        /// <summary>4.1's PMC side: "Customs" spawns west and centre, "Boiler Tanks" east. Empty for scavs only.</summary>
        public string Infiltration { get; }
    }

    public static class ClassicSpawns
    {
        public const string West = "Customs";
        public const string East = "Boiler Tanks";

        // East of this x a point belongs to 4.1's "Boiler Tanks" side.
        public const double EastFrom = 250;

        /// <summary>
        /// The source map has no eastern PMC spawn, so every scav point east of <see cref="EastFrom"/> also serves as one.
        /// Its two PMC points stay PMC (west); every scav point stays a scav spawn.
        /// </summary>
        public static ClassicSpawnRole RoleOf(SourceSpawn s)
        {
            var east = s.Pos[0] >= EastFrom;
            var sourcePmc = (s.Side & 3) != 0;
            var savage = (s.Side & 4) != 0;
            var pmc = sourcePmc || (savage && east);
            return new ClassicSpawnRole(pmc, savage, pmc ? (east ? East : West) : "");
        }

        public static string[]? WithoutPlayer(IEnumerable<string>? categories)
        {
            var left = (categories ?? Array.Empty<string>()).Where(c => !string.Equals(c, "Player", StringComparison.Ordinal)).ToArray();
            return left.Length == 0 ? null : left;
        }

        /// <summary>A stable id per source point, so a raid log names the same point every time.</summary>
        public static string IdFor(SourceSpawn s, bool pmc)
            => $"customsclassic-{(pmc ? "pmc" : "scav")}-{s.Name.Replace(" ", "").Replace("(", "").Replace(")", "").ToLowerInvariant()}";
    }
}
