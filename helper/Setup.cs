using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameEQ
{
    /// <summary>
    /// Everything about getting the sound engine (Equalizer APO) installed and switched on
    /// for the right headset, so the user never has to go looking for it.
    /// </summary>
    public static class Setup
    {
        const string ApoDownload = "https://sourceforge.net/projects/equalizerapo/files/latest/download";

        public static string ApoInstallDir
        {
            get
            {
                try
                {
                    using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                              .OpenSubKey(@"SOFTWARE\EqualizerAPO"))
                    {
                        var p = k?.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
                    }
                }
                catch { }
                var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "EqualizerAPO");
                return Directory.Exists(fallback) ? fallback : null;
            }
        }

        /// <summary>Equalizer APO's own "which device?" window (Device Selector, called Configurator in older versions).</summary>
        static string DeviceSelectorExe
        {
            get
            {
                var dir = ApoInstallDir;
                if (dir == null) return null;
                foreach (var name in new[] { "DeviceSelector.exe", "Configurator.exe" })
                {
                    var p = Path.Combine(dir, name);
                    if (File.Exists(p)) return p;
                }
                return null;
            }
        }

        /// <summary>Step 1: Equalizer APO is missing. Download the official installer and run it.</summary>
        public static bool InstallApo()
        {
            var device = AudioDevice.Name(AudioDevice.DefaultDeviceGuid());
            var r = MessageBox.Show(
                "GameEQ uses Equalizer APO (free, open source) as its sound engine. It isn't installed yet.\n\n" +
                "GameEQ can download the official installer from SourceForge and start it for you:\n\n" +
                "  1. Click Yes when Windows asks for permission.\n" +
                "  2. Click through the installer (Next / Install).\n" +
                "  3. When the device list appears, tick ONLY your headset:\n" +
                "        " + device + "\n" +
                "  4. Click OK. Restart the PC if it asks.\n\n" +
                "Download and start the installer now?",
                "GameEQ — install the sound engine", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r != DialogResult.Yes) return false;

            string path;
            try { path = Download(); }
            catch (Exception ex)
            {
                var r2 = MessageBox.Show("The automatic download didn't work (" + ex.Message + ").\n\n" +
                    "Open the download page instead? Install it, tick your headset, then open GameEQ Helper again.",
                    "GameEQ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r2 == DialogResult.Yes) Program.OpenUrl("https://sourceforge.net/projects/equalizerapo/");
                return false;
            }

            try
            {
                using (var p = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, Verb = "runas" }))
                    p.WaitForExit();
            }
            catch (Win32Exception) { return false; } // user clicked "No" on the Windows prompt
            finally { try { File.Delete(path); } catch { } }

            if (!Apo.IsInstalled)
            {
                MessageBox.Show("The installer finished, but Equalizer APO wasn't found. If you cancelled it, open GameEQ Helper again to retry.",
                    "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        static string Download()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var path = Path.Combine(Path.GetTempPath(), "EqualizerAPO-setup.exe");

            using (var form = new Form { Text = "GameEQ", Width = 360, Height = 120, FormBorderStyle = FormBorderStyle.FixedDialog,
                                         StartPosition = FormStartPosition.CenterScreen, ControlBox = false, TopMost = true })
            using (var wc = new WebClient())
            {
                form.Controls.Add(new Label { Text = "Downloading Equalizer APO from SourceForge…", AutoSize = false,
                                              Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter });
                form.Show(); Application.DoEvents();
                wc.Headers[HttpRequestHeader.UserAgent] = "Wget/1.21 (GameEQ Helper)";
                try { wc.DownloadFile(ApoDownload, path); }
                finally { form.Close(); }
            }

            // Make sure we got a real Windows program, not a web page.
            var head = new byte[2];
            using (var fs = File.OpenRead(path)) fs.Read(head, 0, 2);
            if (new FileInfo(path).Length < 300_000 || head[0] != (byte)'M' || head[1] != (byte)'Z')
            {
                try { File.Delete(path); } catch { }
                throw new InvalidDataException("the download wasn't an installer");
            }
            return path;
        }

        /// <summary>Opens Equalizer APO's device list so the user can tick their headset. Needs admin.</summary>
        public static bool OpenDeviceSelector()
        {
            var exe = DeviceSelectorExe;
            if (exe == null)
            {
                MessageBox.Show("Couldn't find Equalizer APO's Device Selector. Reinstall Equalizer APO, or open it from the Start menu.",
                    "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" }))
                    p.WaitForExit();
                return true;
            }
            catch (Win32Exception) { return false; }
        }

        /// <summary>
        /// Checks the headset Windows is using right now. If Equalizer APO isn't on for it,
        /// explains and offers to open the Device Selector. quiet = don't say anything when all is fine.
        /// </summary>
        public static void CheckDevice(bool quiet)
        {
            var guid = AudioDevice.DefaultDeviceGuid();
            var name = AudioDevice.Name(guid);
            var active = AudioDevice.IsApoActive(guid);

            if (active == true)
            {
                if (quiet) return;
                var others = Apo.OtherActiveLines();
                var extra = others.Length == 0 ? "" :
                    "\n\nNote: Equalizer APO also has other settings switched on, which add on top of GameEQ " +
                    "(volume drops and EQ stack up):\n  " + string.Join("\n  ", others.Take(6)) +
                    "\nIf the sound is too quiet or strange, remove them in Equalizer APO's Editor or in Peace.";
                var mine = Apo.GameEqPreamp(); var otherDb = Apo.OtherPreamp();
                var vol = "\n\nVolume reduction right now:\n  GameEQ: " + (mine < 0 ? mine.ToString("0.#") + " dB" : "none (Louder mode)") +
                          "\n  Other Equalizer APO settings: " + (otherDb < 0 ? otherDb.ToString("0.#") + " dB  ⚠ this makes everything quieter" : "none");
                MessageBox.Show("GameEQ is active on:\n" + name + vol + extra +
                    "\n\nNo effect? On Windows 11 check Settings → System → Sound → your headset → Audio enhancements is ON." +
                    "\nAlso set your headset app's own EQ (e.g. NGENUITY) to flat, so two EQs don't stack.",
                    "GameEQ — check", MessageBoxButtons.OK, others.Length == 0 && otherDb >= 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                return;
            }
            if (active == null)
            {
                if (!quiet) MessageBox.Show("Windows didn't let GameEQ check your headset (" + name + ").\n\n" +
                    "If the EQ has no effect, choose \"Fix my headset…\" in the tray menu and tick your headset.",
                    "GameEQ — check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var r = MessageBox.Show(
                "GameEQ isn't active on the headset you're using now:\n\n    " + name + "\n\n" +
                "This happens after installing a new headset, changing USB ports, or some Windows updates.\n\n" +
                "Open Equalizer APO's device list now? Tick that device, click OK, and restart the PC if it asks.",
                "GameEQ — fix my headset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes) OpenDeviceSelector();
        }
    }
}
