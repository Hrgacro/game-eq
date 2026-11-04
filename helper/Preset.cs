using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameEQ
{
    public class Preset
    {
        public string Name;
        public double Preamp;
        public double[] Values; // 10 bands, see Apo.Bands

        public Preset(string name, double preamp, double[] values)
        {
            Name = Clean(name);
            Preamp = Math.Max(-24, Math.Min(0, preamp));        // preamp can only lower volume
            Values = values.Select(v => Math.Max(-12, Math.Min(12, v))).ToArray(); // limit every band to ±12 dB
        }

        static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Custom";
            var chars = s.Where(ch => !char.IsControl(ch)).ToArray();
            var clean = new string(chars).Trim();
            return clean.Length > 80 ? clean.Substring(0, 80) : clean;
        }

        /// <summary>
        /// Parses links from the website, e.g.
        /// gameeq://apply?v=-5,-3,0,-1,0,2,5,5,-2,-2&amp;p=-6&amp;n=CS2%20Footsteps
        /// Returns null if anything is wrong, so a bad link can never apply garbage.
        /// </summary>
        public static Preset FromUrl(string url)
        {
            try
            {
                if (url == null || !url.StartsWith("gameeq:", StringComparison.OrdinalIgnoreCase)) return null;
                int q = url.IndexOf('?');
                if (q < 0) return null;

                var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var part in url.Substring(q + 1).Split('&'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    query[part.Substring(0, eq)] = Uri.UnescapeDataString(part.Substring(eq + 1).Replace('+', ' ')).TrimEnd('/');
                }

                if (!query.TryGetValue("v", out var v)) return null;
                var values = v.Split(',').Select(x => double.Parse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                if (values.Length != Apo.Bands.Length || values.Any(x => double.IsNaN(x) || double.IsInfinity(x))) return null;

                double preamp;
                if (!query.TryGetValue("p", out var p) || !double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out preamp))
                    preamp = -(Math.Ceiling(Math.Max(0, values.Max())) + 1);

                query.TryGetValue("n", out var name);
                return new Preset(name, preamp, values);
            }
            catch { return null; }
        }

        /// <summary>Quick presets in the tray menu (same numbers the website gives with a "Balanced" headset at 100%).</summary>
        public static readonly Preset[] BuiltIn =
        {
            new Preset("CS2 — Footsteps",               -6, new double[]{ -5,-3, 0,-1, 0, 2, 5, 5,-2,-2 }),
            new Preset("CS2 — Footsteps + Less harsh",  -6, new double[]{ -5,-3, 0,-1, 0, 2, 5, 4,-5,-4 }),
            new Preset("CS2 — Footsteps + Gunfight",    -7, new double[]{ -6,-5,-2,-2, 0, 2, 6, 6,-4,-2 }),
            new Preset("Valorant — Footsteps",          -6, new double[]{ -5,-3,-1,-1, 0, 2, 4, 5, 1,-1 }),
            new Preset("Valorant — Footsteps + Gunfight", -7, new double[]{ -6,-5,-3,-2, 0, 2, 5, 6,-1,-1 }),
            new Preset("Voice clarity (Discord)",       -3, new double[]{ -1,-1,-1, 0, 1, 2, 2, 1, 0, 0 }),
            new Preset("Music / Movies — Rich + Bass",  -7, new double[]{  6, 5, 3, 0, 0, 0, 0, 0, 1, 1 }),
            new Preset("Night mode",                    -2, new double[]{ -4,-3,-1, 0, 0, 1, 1, 0,-1,-1 }),
        };
    }
}
