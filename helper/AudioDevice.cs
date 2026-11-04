using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameEQ
{
    /// <summary>
    /// Finds the headset Windows is playing sound on right now, and checks whether
    /// Equalizer APO is actually switched on for it. Read-only: this class never changes anything.
    /// </summary>
    public static class AudioDevice
    {
        // ---- Windows Core Audio (only the two calls we need) ----
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr iface);
            [PreserveSig] int OpenPropertyStore(int access, out IntPtr store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        }

        const string RenderKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\";
        static readonly Regex GuidRx = new Regex(@"^\{[0-9a-fA-F\-]{36}\}$");

        /// <summary>Endpoint GUID of the default playback device, e.g. "{e3a1...}", or null.</summary>
        public static string DefaultDeviceGuid()
        {
            IMMDeviceEnumerator en = null; IMMDevice dev = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                if (en.GetDefaultAudioEndpoint(0 /*render*/, 0 /*console*/, out dev) != 0 || dev == null) return null;
                if (dev.GetId(out var id) != 0 || string.IsNullOrEmpty(id)) return null;
                int i = id.LastIndexOf('{');
                return i >= 0 ? id.Substring(i) : null;
            }
            catch { return null; }
            finally
            {
                if (dev != null) Marshal.ReleaseComObject(dev);
                if (en != null) Marshal.ReleaseComObject(en);
            }
        }

        static RegistryKey OpenHklm(string path)
        {
            try { return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(path); }
            catch { return null; }
        }

        /// <summary>Readable name such as "Speakers (HyperX Cloud Alpha 2 Wireless)".</summary>
        public static string Name(string guid)
        {
            if (guid == null) return "your headset";
            using (var k = OpenHklm(RenderKey + guid + @"\Properties"))
            {
                var desc = k?.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string;
                var adapter = k?.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string;
                if (!string.IsNullOrEmpty(desc) && !string.IsNullOrEmpty(adapter)) return desc + " (" + adapter + ")";
                return desc ?? adapter ?? "your headset";
            }
        }

        /// <summary>
        /// true = Equalizer APO is active on this device, false = it isn't,
        /// null = Windows didn't let us check (we then don't nag the user).
        /// </summary>
        public static bool? IsApoActive(string guid)
        {
            if (guid == null) return null;
            try
            {
                using (var fx = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                                           .OpenSubKey(RenderKey + guid + @"\FxProperties"))
                {
                    if (fx == null) return false; // device has no audio effects at all, so no Equalizer APO
                    foreach (var name in fx.GetValueNames())
                    {
                        var value = fx.GetValue(name);
                        var strings = value is string s ? new[] { s } : value as string[];
                        if (strings == null) continue;
                        foreach (var clsid in strings.Where(x => x != null && GuidRx.IsMatch(x.Trim())))
                            if (IsEqualizerApoClsid(clsid.Trim())) return true;
                    }
                    return false;
                }
            }
            catch { return null; }
        }

        static bool IsEqualizerApoClsid(string clsid)
        {
            using (var k = OpenHklm(@"SOFTWARE\Classes\CLSID\" + clsid + @"\InprocServer32"))
            {
                var dll = k?.GetValue("") as string;
                return dll != null && dll.IndexOf("EqualizerAPO", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }
    }
}
