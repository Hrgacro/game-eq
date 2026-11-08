using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameEQ
{
    static class Program
    {
        public const string WebsiteUrl = "https://hrgacro.github.io/game-eq/";
        public const string ApoDownloadUrl = "https://sourceforge.net/projects/equalizerapo/";
        const string SettingsKey = @"Software\GameEQ";

        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var arg = args.FirstOrDefault() ?? "";
            try
            {
                // Elevated helper modes (started by ourselves with an admin prompt)
                if (arg == "--setup-admin") { Apo.AdminSetup(); return 0; }
                if (arg == "--uninstall-admin") { Apo.AdminUninstall(); return 0; }
            }
            catch (Exception ex)
            {
                MessageBox.Show("GameEQ setup failed:\n" + ex.Message, "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            // Opened from the website: gameeq://apply?...
            if (arg.StartsWith("gameeq:", StringComparison.OrdinalIgnoreCase))
            {
                ApplyFromLink(arg);
                return 0;
            }

            // Normal start: setup check, then live in the tray
            using (var mutex = new Mutex(true, "GameEQHelperTray", out bool first))
            {
                if (!first)
                {
                    MessageBox.Show("GameEQ is already running. Look for the green bars icon next to the clock.",
                        "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                if (!EnsureReady()) return 1;
                Application.Run(new TrayApp());
            }
            return 0;
        }

        /// <summary>Makes sure Equalizer APO exists and GameEQ is set up. Asks for admin once.</summary>
        public static bool EnsureReady()
        {
            RegisterLinkHandler();

            if (!Apo.IsInstalled && !Setup.InstallApo()) return false;

            if (!Apo.IsSetUp)
            {
                var r = MessageBox.Show(
                    "GameEQ setup.\n\n" +
                    "Windows will ask for permission once. GameEQ will only:\n" +
                    "  • create its own preset file in the Equalizer APO folder\n" +
                    "  • add one line (Include: gameeq.txt) to Equalizer APO's config\n" +
                    "  • switch off Equalizer APO's demo settings, which lower your volume\n" +
                    "    by 6 dB and add a test EQ on top of GameEQ\n\n" +
                    "You can undo everything from the tray menu (Restore original sound).",
                    "GameEQ — setup", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (r != DialogResult.OK) return false;

                if (!RunElevated("--setup-admin") || !Apo.IsSetUp)
                {
                    MessageBox.Show("Setup was cancelled or didn't finish. Open GameEQ Helper again to retry.",
                        "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }
            Setup.CheckDevice(quiet: true);   // warns only if the EQ isn't active on the headset in use
            return true;
        }

        static void ApplyFromLink(string url)
        {
            var preset = Preset.FromUrl(url);
            if (preset == null)
            {
                MessageBox.Show("That GameEQ link isn't valid. Nothing was changed.", "GameEQ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!EnsureReady()) return;

            if (!TryApply(preset, out var error))
            {
                MessageBox.Show("Couldn't apply the preset:\n" + error, "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var note = preset.Name;
            if (preset.Game == "flat") GameWatcher.SaveDesktop(preset);
            else if (preset.Game != null)
            {
                GameWatcher.SaveForGame(preset.Game, preset);
                var g = GameWatcher.Games.FirstOrDefault(x => x.Key == preset.Game);
                if (g != null) note += "\nSaved for " + g.Name + ": it switches on by itself when " + g.Name + " starts.";
            }
            ShowToastAndExit("EQ applied", note);
        }

        /// <summary>On by default: keep the normal volume and make footsteps louder instead of everything else quieter.</summary>
        public static bool LouderMode
        {
            get => GetSetting("Louder") != "off";
            set => SetSetting("Louder", value ? "on" : "off");
        }

        public static bool TryApply(Preset preset, out string error)
        {
            error = null;
            try
            {
                Apo.Write(preset);
                SetSetting("Current", preset?.Name ?? "Off");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Permission got lost (e.g. Equalizer APO was reinstalled). Run setup again and retry once.
                if (RunElevated("--setup-admin"))
                {
                    try { Apo.Write(preset); SetSetting("Current", preset?.Name ?? "Off"); return true; }
                    catch (Exception ex) { error = ex.Message; return false; }
                }
                error = "Permission was not granted.";
                return false;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>Lets the website's "Apply to my headset" button open this app. Per-user, no admin needed.</summary>
        static void RegisterLinkHandler()
        {
            try
            {
                var exe = Application.ExecutablePath;
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\gameeq"))
                {
                    key.SetValue("", "URL:GameEQ Protocol");
                    key.SetValue("URL Protocol", "");
                    using (var cmd = key.CreateSubKey(@"shell\open\command"))
                        cmd.SetValue("", "\"" + exe + "\" \"%1\"");
                }
            }
            catch { /* not fatal: tray presets still work */ }
        }

        public static void RemoveUserSettings()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\gameeq", false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(SettingsKey, false); } catch { }
            SetStartWithWindows(false);
        }

        public static bool RunElevated(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath, arguments) { UseShellExecute = true, Verb = "runas" };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (Win32Exception) { return false; } // user clicked "No"
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        public static string GetSetting(string name)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(SettingsKey)) return k?.GetValue(name) as string;
        }
        public static void SetSetting(string name, string value)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(SettingsKey)) k.SetValue(name, value); } catch { }
        }

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public static bool StartsWithWindows
        {
            get { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k?.GetValue("GameEQ") != null; }
        }
        public static void SetStartWithWindows(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue("GameEQ", "\"" + Application.ExecutablePath + "\"");
                    else if (k.GetValue("GameEQ") != null) k.DeleteValue("GameEQ");
                }
            }
            catch { }
        }

        static void ShowToastAndExit(string title, string text)
        {
            var icon = new NotifyIcon { Icon = TrayApp.MakeIcon(), Visible = true, Text = "GameEQ" };
            icon.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
            var timer = new System.Windows.Forms.Timer { Interval = 3500 };
            timer.Tick += (s, e) => { timer.Stop(); icon.Visible = false; icon.Dispose(); Application.Exit(); };
            timer.Start();
            Application.Run();
        }
    }
}
