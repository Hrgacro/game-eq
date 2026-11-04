using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GameEQ
{
    /// <summary>The icon next to the clock, with quick presets.</summary>
    public class TrayApp : ApplicationContext
    {
        readonly NotifyIcon icon;
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly GameWatcher games = new GameWatcher();
        readonly Timer timer = new Timer { Interval = 3000 };
        string lastDevice;
        int ticks;
        bool balloonIsFix;

        public TrayApp()
        {
            icon = new NotifyIcon { Icon = MakeIcon(), Visible = true, Text = "GameEQ", ContextMenuStrip = menu };
            icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Program.OpenUrl(Program.WebsiteUrl); };
            menu.Opening += (s, e) => BuildMenu();
            BuildMenu();
            icon.BalloonTipClicked += (s, e) => { if (balloonIsFix) Setup.CheckDevice(quiet: false); };
            icon.ShowBalloonTip(3000, "GameEQ is ready",
                "Right-click the icon for presets. Starting CS2 or Valorant switches the EQ on by itself.", ToolTipIcon.Info);

            games.GameStarted += (g, p) =>
            {
                if (p == null) { Balloon("GameEQ", g.Name + " started. No preset saved for it yet: pick " + g.Name + " on the website and click Apply."); return; }
                if (Program.TryApply(p, out _)) Balloon(g.Name + " started", "EQ: " + p.Name);
            };
            games.GameClosed += (g, p) =>
            {
                if (Program.TryApply(p, out _)) Balloon(g.Name + " closed", "Back to: " + (p?.Name ?? "EQ off"));
            };
            lastDevice = AudioDevice.DefaultDeviceGuid();
            timer.Tick += (s, e) => OnTimer();
            timer.Start();
        }

        void Balloon(string title, string text, bool isFix = false)
        {
            balloonIsFix = isFix;
            icon.ShowBalloonTip(3000, title, text, isFix ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }

        void OnTimer()
        {
            games.Tick();
            // every ~6 s: did Windows switch to another audio device (new headset, other USB port, Windows update)?
            if (++ticks % 2 != 0) return;
            var dev = AudioDevice.DefaultDeviceGuid();
            if (dev == null || dev == lastDevice) return;
            lastDevice = dev;
            if (AudioDevice.IsApoActive(dev) == false)
                Balloon("GameEQ isn't active on this device", AudioDevice.Name(dev) + "\nClick here to fix it.", isFix: true);
        }

        void BuildMenu()
        {
            menu.Items.Clear();
            var current = Program.GetSetting("Current") ?? "Off";

            menu.Items.Add(new ToolStripMenuItem("Now: " + current) { Enabled = false });
            var dev = AudioDevice.DefaultDeviceGuid();
            var active = AudioDevice.IsApoActive(dev);
            menu.Items.Add(new ToolStripMenuItem((active == false ? "⚠ Not active on: " : "Headset: ") + AudioDevice.Name(dev)) { Enabled = false });
            if (games.InGame) menu.Items.Add(new ToolStripMenuItem("Game: " + games.Current.Name + " (auto)") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());

            foreach (var preset in Preset.BuiltIn)
            {
                var p = preset;
                var item = new ToolStripMenuItem(p.Name) { Checked = current == p.Name };
                item.Click += (s, e) => Apply(p);
                menu.Items.Add(item);
            }

            var off = new ToolStripMenuItem("EQ off (flat)") { Checked = current == "Off" };
            off.Click += (s, e) => Apply(null);
            menu.Items.Add(off);

            menu.Items.Add(new ToolStripSeparator());
            var web = new ToolStripMenuItem("Make a custom preset (website)…");
            web.Click += (s, e) => Program.OpenUrl(Program.WebsiteUrl);
            menu.Items.Add(web);

            var auto = new ToolStripMenuItem("Auto-switch when a game starts") { Checked = GameWatcher.Enabled };
            auto.Click += (s, e) => GameWatcher.Enabled = !GameWatcher.Enabled;
            menu.Items.Add(auto);

            var fix = new ToolStripMenuItem("Check / fix my headset…");
            fix.Click += (s, e) => Setup.CheckDevice(quiet: false);
            menu.Items.Add(fix);

            var startup = new ToolStripMenuItem("Start with Windows") { Checked = Program.StartsWithWindows };
            startup.Click += (s, e) => Program.SetStartWithWindows(!Program.StartsWithWindows);
            menu.Items.Add(startup);

            var restore = new ToolStripMenuItem("Restore original sound && uninstall…");
            restore.Click += (s, e) => Uninstall();
            menu.Items.Add(restore);

            var exit = new ToolStripMenuItem("Exit (keeps current EQ)");
            exit.Click += (s, e) => Quit();
            menu.Items.Add(exit);
        }

        void Apply(Preset p)
        {
            if (Program.TryApply(p, out var error))
            {
                if (games.InGame) GameWatcher.SaveForGame(games.Current.Key, p);  // picked during a game = that game's preset
                else GameWatcher.SaveDesktop(p);                                   // picked on the desktop = your normal preset
                Balloon("EQ applied", (p?.Name ?? "Off (flat)") + (games.InGame ? "\nSaved for " + games.Current.Name + "." : ""));
            }
            else
                MessageBox.Show("Couldn't apply the preset:\n" + error, "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void Uninstall()
        {
            var r = MessageBox.Show(
                "This removes GameEQ's preset and its line from Equalizer APO, so your sound goes back to how it was.\n\n" +
                "Windows will ask for permission once. Continue?",
                "GameEQ — restore original sound", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            if (!Program.RunElevated("--uninstall-admin"))
            {
                MessageBox.Show("Permission wasn't granted, so nothing was changed.", "GameEQ");
                return;
            }
            Program.RemoveUserSettings();
            MessageBox.Show(
                "Original sound restored.\n\nYou can now delete GameEQHelper.exe.\n" +
                "To remove Equalizer APO as well, uninstall it from Windows Settings → Apps.",
                "GameEQ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Quit();
        }

        void Quit()
        {
            timer.Stop();
            icon.Visible = false;
            icon.Dispose();
            ExitThread();
        }

        /// <summary>Draws the green EQ-bars icon at runtime, so no .ico file is needed.</summary>
        public static Icon MakeIcon()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var bg = new SolidBrush(Color.FromArgb(13, 15, 20))) g.FillEllipse(bg, 0, 0, 31, 31);
                using (var bar = new SolidBrush(Color.FromArgb(56, 225, 164)))
                {
                    int[] heights = { 8, 16, 22, 12 };
                    for (int i = 0; i < heights.Length; i++)
                        g.FillRectangle(bar, 7 + i * 5, 24 - heights[i], 3, heights[i]);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}
