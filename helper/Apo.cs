using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameEQ
{
    /// <summary>
    /// Everything that touches Equalizer APO lives here.
    /// GameEQ only ever changes two things:
    ///   1. its own file:  ...\EqualizerAPO\config\gameeq.txt
    ///   2. one line in   ...\EqualizerAPO\config\config.txt  ("Include: gameeq.txt")
    /// </summary>
    public static class Apo
    {
        public const string PresetFileName = "gameeq.txt";
        const string IncludeLine = "Include: " + PresetFileName;
        static readonly Regex IncludeRegex = new Regex(@"^\s*Include:\s*gameeq\.txt\s*$", RegexOptions.IgnoreCase);

        // Equalizer APO ships with demo settings in config.txt: "Preamp: -6 dB" + "Include: example.txt"
        // (lower volume + a small test EQ). They add on top of GameEQ, so GameEQ switches them off
        // by commenting them out with this tag, and puts them back on uninstall.
        const string DisabledTag = "# (turned off by GameEQ) ";
        static readonly Regex DemoPreamp = new Regex(@"^\s*Preamp:\s*-6(\.0+)?\s*dB\s*$", RegexOptions.IgnoreCase);
        static readonly Regex DemoInclude = new Regex(@"^\s*Include:\s*example\.txt\s*$", RegexOptions.IgnoreCase);
        static bool IsDemo(string line) => DemoPreamp.IsMatch(line) || DemoInclude.IsMatch(line);

        static readonly Regex PreampRx = new Regex(@"^\s*Preamp:\s*(-?\d+(?:[.,]\d+)?)\s*dB\s*$", RegexOptions.IgnoreCase);
        static readonly Regex AnyIncludeRx = new Regex(@"^\s*Include:\s*(.+?)\s*$", RegexOptions.IgnoreCase);
        static double ParseDb(string s) => double.Parse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

        public static readonly int[] Bands = { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

        public static string ConfigDir
        {
            get
            {
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var key = hklm.OpenSubKey(@"SOFTWARE\EqualizerAPO"))
                    {
                        var path = key?.GetValue("ConfigPath") as string;
                        if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                    }
                }
                catch { /* fall through to default */ }

                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO", "config");
                return Directory.Exists(fallback) ? fallback : null;
            }
        }

        public static bool IsInstalled => ConfigDir != null;

        static string ConfigTxt => Path.Combine(ConfigDir, "config.txt");
        static string PresetPath => Path.Combine(ConfigDir, PresetFileName);

        /// <summary>True when config.txt includes our file and we can write our file without admin.</summary>
        public static bool IsSetUp
        {
            get
            {
                try
                {
                    if (!IsInstalled || !File.Exists(ConfigTxt) || !File.Exists(PresetPath)) return false;
                    var lines = File.ReadAllLines(ConfigTxt);
                    if (!lines.Any(l => IncludeRegex.IsMatch(l))) return false;
                    if (lines.Any(IsDemo)) return false;   // demo settings still on: run setup again to switch them off
                    using (new FileStream(PresetPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { }
                    return true;
                }
                catch { return false; }
            }
        }

        /// <summary>Runs elevated (admin). Adds the Include line, creates our file, lets normal users write it.</summary>
        public static void AdminSetup()
        {
            if (!IsInstalled) throw new InvalidOperationException("Equalizer APO is not installed.");

            if (!File.Exists(ConfigTxt)) File.WriteAllText(ConfigTxt, "");
            var original = File.ReadAllLines(ConfigTxt);
            var lines = original.Select(l => IsDemo(l) ? DisabledTag + l : l).ToList();
            if (!lines.Any(l => IncludeRegex.IsMatch(l))) lines.Add(IncludeLine);
            if (!lines.SequenceEqual(original))
            {
                var backup = ConfigTxt + ".before-gameeq";
                if (!File.Exists(backup)) File.Copy(ConfigTxt, backup);
                File.WriteAllLines(ConfigTxt, lines, new UTF8Encoding(false));
            }

            if (!File.Exists(PresetPath)) File.WriteAllText(PresetPath, BuildOff(), new UTF8Encoding(false));

            var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var security = File.GetAccessControl(PresetPath);
            security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Modify, AccessControlType.Allow));
            File.SetAccessControl(PresetPath, security);
        }

        /// <summary>Runs elevated (admin). Removes our Include line and our file, restoring the original sound.</summary>
        public static void AdminUninstall()
        {
            if (!IsInstalled) return;
            if (File.Exists(ConfigTxt))
            {
                var lines = File.ReadAllLines(ConfigTxt)
                    .Where(l => !IncludeRegex.IsMatch(l))
                    .Select(l => l.StartsWith(DisabledTag) ? l.Substring(DisabledTag.Length) : l)   // demo settings back on
                    .ToList();
                File.WriteAllLines(ConfigTxt, lines, new UTF8Encoding(false));
            }
            if (File.Exists(PresetPath)) File.Delete(PresetPath);
        }

        /// <summary>
        /// Other active lines in config.txt (not GameEQ's, not comments). These also change the sound
        /// (e.g. Peace or AutoEq settings) and stack with GameEQ: volume drops add up.
        /// </summary>
        public static string[] OtherActiveLines()
        {
            try
            {
                if (!IsInstalled || !File.Exists(ConfigTxt)) return new string[0];
                return File.ReadAllLines(ConfigTxt)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && !l.StartsWith("#") && !IncludeRegex.IsMatch(l))
                    .ToArray();
            }
            catch { return new string[0]; }
        }

        /// <summary>Writes the preset. Equalizer APO notices the change and applies it within about a second.</summary>
        public static void Write(Preset preset)
        {
            File.WriteAllText(PresetPath, preset == null ? BuildOff() : Build(preset), new UTF8Encoding(false));
        }

        static string BuildOff() => "# GameEQ: off (flat)\r\n";

        /// <summary>
        /// Reads back the preset that is in gameeq.txt now (so it can be re-written, e.g. when Louder mode is switched).
        /// The safe preamp is always "minus the biggest boost", so it is recomputed rather than read.
        /// </summary>
        public static Preset ReadCurrent()
        {
            try
            {
                if (!IsInstalled || !File.Exists(PresetPath)) return null;
                var c = CultureInfo.InvariantCulture;
                string name = "Custom"; double[] vals = null;
                foreach (var raw in File.ReadAllLines(PresetPath))
                {
                    var l = raw.Trim();
                    if (l.StartsWith("# GameEQ preset:")) name = l.Substring("# GameEQ preset:".Length).Trim();
                    else if (l.StartsWith("GraphicEQ:", StringComparison.OrdinalIgnoreCase))
                        vals = l.Substring("GraphicEQ:".Length).Split(';').Select(x => x.Trim()).Where(x => x.Length > 0)
                                .Select(x => double.Parse(x.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1], NumberStyles.Float, c))
                                .ToArray();
                }
                if (vals == null || vals.Length != Bands.Length) return null;
                return new Preset(name, -Math.Max(0, vals.Max()), vals);
            }
            catch { return null; }
        }

        /// <summary>How much GameEQ's own file lowers the volume right now (dB, 0 = not at all).</summary>
        public static double GameEqPreamp()
        {
            try
            {
                if (!IsInstalled || !File.Exists(PresetPath)) return 0;
                return File.ReadAllLines(PresetPath).Select(l => PreampRx.Match(l)).Where(m => m.Success).Sum(m => ParseDb(m.Groups[1].Value));
            }
            catch { return 0; }
        }

        /// <summary>
        /// How much everything else in Equalizer APO (config.txt and files it includes, except GameEQ's) lowers the volume.
        /// Approximate: it doesn't follow channel/device sections. Used only to warn the user.
        /// </summary>
        public static double OtherPreamp()
        {
            double sum = 0;
            try
            {
                if (!IsInstalled || !File.Exists(ConfigTxt)) return 0;
                foreach (var raw in File.ReadAllLines(ConfigTxt))
                {
                    var l = raw.Trim();
                    if (l.StartsWith("#") || IncludeRegex.IsMatch(l)) continue;
                    var m = PreampRx.Match(l);
                    if (m.Success) { sum += ParseDb(m.Groups[1].Value); continue; }
                    var inc = AnyIncludeRx.Match(l);
                    if (!inc.Success) continue;
                    var path = Path.Combine(ConfigDir, inc.Groups[1].Value);
                    if (!File.Exists(path)) continue;
                    foreach (var r2 in File.ReadAllLines(path))
                    {
                        if (r2.TrimStart().StartsWith("#")) continue;
                        var m2 = PreampRx.Match(r2);
                        if (m2.Success) sum += ParseDb(m2.Groups[1].Value);
                    }
                }
            }
            catch { }
            return sum;
        }

        static string Build(Preset p)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("# GameEQ preset: ").Append(p.Name).Append("\r\n");
            sb.Append("# Written by GameEQ Helper. Use the tray icon to change or remove.\r\n");
            // Louder mode: no volume reduction, so footsteps really get louder (tiny risk of crackle in the loudest moments).
            // Safe mode: volume lowered by exactly the biggest boost, so nothing can ever distort.
            var preamp = Program.LouderMode ? 0 : p.Preamp;
            sb.Append("# Volume: ").Append(Program.LouderMode ? "Louder mode (no reduction)" : "Safe mode").Append("\r\n");
            sb.Append("Preamp: ").Append(preamp.ToString("0.#", c)).Append(" dB\r\n");
            sb.Append("GraphicEQ: ");
            sb.Append(string.Join("; ", Bands.Select((f, i) => f.ToString(c) + " " + p.Values[i].ToString("0.#", c))));
            sb.Append("\r\n");
            return sb.ToString();
        }
    }
}
