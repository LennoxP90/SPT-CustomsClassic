#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace CustomsClassic.Shared
{
    /// <summary>Which zones a boss may use on classic Customs, where some modern zones have no bot point left.</summary>
    public static class ClassicBossZones
    {
        public const string Everywhere = "*";

        /// <summary>
        /// `rule` is the boss's zones from config/bosses.json (null: none given), `shipped` its zones in the location
        /// table. "*" in the rule means every open zone but the sniper ones. A zone that is not open is never returned.
        /// </summary>
        public static string[] Resolve(IReadOnlyCollection<string>? rule, IReadOnlyCollection<string> shipped, IReadOnlyCollection<string> open)
        {
            var wanted = rule is null ? shipped
                : rule.Contains(Everywhere) ? open.Where(z => z.IndexOf("Snipe", StringComparison.OrdinalIgnoreCase) < 0).OrderBy(z => z, StringComparer.Ordinal).ToArray()
                : rule;
            return wanted.Where(open.Contains).ToArray();
        }

        /// <summary>The drawn zone if free, else another free zone of the boss picked by `roll`, or null: the boss stays out.</summary>
        public static string? AwayFromPlayers(string born, string zones, Func<string, bool> taken, int roll)
        {
            if (!taken(born)) return born;
            var free = zones.Split(',').Where(z => z.Length > 1 && !taken(z)).ToArray();
            return free.Length == 0 ? null : free[Math.Abs(roll) % free.Length];
        }

        /// <summary>Cultists come out between 22:00 and 06:00, as on the live game.</summary>
        public static bool IsNight(int hour) => hour >= 22 || hour < 6;
    }
}
