using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;

namespace GameEQ
{
    /// <summary>
    /// Auto-switch: notices when a supported game is running and applies that game's preset,
    /// then puts your normal ("desktop") preset back when the game closes.
    /// Presets per game are remembered when you click "Apply to my headset" on the website.
    /// </summary>
    public class GameWatcher
    {
        public class GameInfo { public string Key, Name; public string[] Processes; public string DefaultPreset; }

        // Website game key -> how Windows names the running game.
        public static readonly GameInfo[] Games =
        {
            new GameInfo { Key = "cs2",      Name = "CS2",              Processes = new[]{ "cs2" }, DefaultPreset = "CS2 — Footsteps + Gunfight" },
            new GameInfo { Key = "valorant", Name = "Valorant",         Processes = new[]{ "VALORANT-Win64-Shipping" }, DefaultPreset = "Valorant — Footsteps + Gunfight" },
            new GameInfo { Key = "pubg",     Name = "PUBG",             Processes = new[]{ "TslGame" } },
            new GameInfo { Key = "fortnite", Name = "Fortnite",         Processes = new[]{ "FortniteClient-Win64-Shipping" } },
            new GameInfo { Key = "apex",     Name = "Apex Legends",     Processes = new[]{ "r5apex", "r5apex_dx12" } },
            new GameInfo { Key = "lol",      Name = "League of Legends",Processes = new[]{ "League of Legends" } },
            new GameInfo { Key = "rl",       Name = "Rocket League",    Processes = new[]{ "RocketLeague" } },
        };

        public static bool IsKnownGame(string key) => key != null && (Games.Any(g => g.Key == key) || key == "cod" || key == "fc" || key == "flat");

        const string StoreKey = @"Software\GameEQ\Games";
        const string DesktopValue = "Desktop";

        // ---- remembered presets ----
        public static void SaveForGame(string key, Preset p) => Write(key, p);
        public static Preset LoadForGame(string key) => Read(key);
        public static void SaveDesktop(Preset p) => Write(DesktopValue, p);   // null = EQ off
        public static Preset LoadDesktop() => Read(DesktopValue);

        static void Write(string name, Preset p)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(StoreKey)) k.SetValue(name, p == null ? "off" : p.Serialize()); } catch { }
        }
        static Preset Read(string name)
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(StoreKey)) { var s = k?.GetValue(name) as string; return s == null || s == "off" ? null : Preset.Deserialize(s); } }
            catch { return null; }
        }

        public static bool Enabled
        {
            get => Program.GetSetting("AutoSwitch") != "off";
            set => Program.SetSetting("AutoSwitch", value ? "on" : "off");
        }

        /// <summary>Which supported game is running right now (null = none).</summary>
        public static GameInfo RunningGame()
        {
            foreach (var g in Games)
                foreach (var proc in g.Processes)
                {
                    Process[] found = null;
                    try { found = Process.GetProcessesByName(proc); }
                    catch { }
                    if (found != null && found.Length > 0)
                    {
                        foreach (var f in found) f.Dispose();
                        return g;
                    }
                }
            return null;
        }

        public static Preset PresetFor(GameInfo g)
        {
            return LoadForGame(g.Key) ?? Preset.BuiltIn.FirstOrDefault(p => p.Name == g.DefaultPreset);
        }

        // ---- the watcher itself (runs in the tray app) ----
        GameInfo current;
        public event Action<GameInfo, Preset> GameStarted;   // preset may be null = nothing saved for this game
        public event Action<GameInfo, Preset> GameClosed;    // preset = desktop preset (null = EQ off)

        public bool InGame => current != null;
        public GameInfo Current => current;

        public void Tick()
        {
            if (!Enabled) { current = null; return; }
            var now = RunningGame();
            if (now?.Key == current?.Key) return;

            var previous = current;
            current = now;
            if (now != null) GameStarted?.Invoke(now, PresetFor(now));
            else if (previous != null) GameClosed?.Invoke(previous, LoadDesktop());
        }
    }
}
