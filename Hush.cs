using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using SystemInformation = System.Windows.Forms.SystemInformation;

namespace HushApp
{
    public class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // The WebView2 libraries are packed inside the exe: one file to download, like Sentinel, Vigil and Hoard.
            AppDomain.CurrentDomain.AssemblyResolve += Libs.Resolve;
            return Run(args);
        }

        // Kept apart from Main: the JIT must not meet a library type before the resolver is in place.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Run(string[] args)
        {
            if (Array.IndexOf(args, "--selftest") >= 0) return SelfTest.Run();
            bool fromStartup = Array.IndexOf(args, "--startup") >= 0;
            Mutex single = null;
            bool owned = false;
            try
            {
                single = new Mutex(false, "Local\\HushApp");
                owned = single.WaitOne(0);
            }
            catch (AbandonedMutexException) { owned = true; }
            catch (UnauthorizedAccessException) { }
            if (!owned)
            {
                if (!fromStartup) MessageBox.Show(L.T("Hush è già acceso: lo trovi nell'area di notifica, vicino all'orologio.",
                                                      "Hush is already running: look for it in the notification area, next to the clock."),
                                                  "Hush", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new HushContext(fromStartup));
            }
            finally { single.ReleaseMutex(); }
            return 0;
        }
    }

    public static class Libs
    {
        static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>();

        static byte[] Resource(string name)
        {
            using (Stream s = Open(name))
            {
                if (s == null) return null;
                var m = new MemoryStream();
                s.CopyTo(m);
                return m.ToArray();
            }
        }

        public static Stream Open(string name) { return Assembly.GetExecutingAssembly().GetManifestResourceStream(name); }

        public static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            string file = new AssemblyName(e.Name).Name + ".dll";
            lock (Loaded)
            {
                Assembly a;
                if (Loaded.TryGetValue(file, out a)) return a;
                byte[] bytes = Resource(file);
                if (bytes == null) return null;
                a = Assembly.Load(bytes);
                Loaded[file] = a;
                return a;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string path);

        // A native DLL can't be loaded from memory: it goes to disk once, then into the process by full path.
        public static void LoadNative(string name, string folder)
        {
            byte[] bytes = Resource(name);
            string path = Path.Combine(folder, name);
            Directory.CreateDirectory(folder);
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length) File.WriteAllBytes(path, bytes);
            if (LoadLibrary(path) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public static Icon Icon(string name, Size size)
        {
            using (var s = Open(name))
                return s == null ? SystemIcons.Application : new Icon(s, size);
        }
    }

    // Italian if Windows speaks Italian, English otherwise. Same as Sentinel.
    public static class L
    {
        public static readonly bool It = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it";
        public static string T(string it, string en) { return It ? it : en; }
        public static Icon AppIcon(Size size) { return Libs.Icon("Hush.ico", size); }
    }

    public static class Json
    {
        static readonly JavaScriptSerializer S = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static string Write(object o) { return S.Serialize(o); }
        public static T Read<T>(string s) { return S.Deserialize<T>(s); }
    }

    public class Mic
    {
        public string Id, Name;
        public bool Muted, Default, Comms;
    }

    // The microphones, through Core Audio: the same switch as Settings › Sound › Input. Muting there silences every app at once.
    public static class Audio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }

        // Only the first methods of each interface: the order is the vtable, the rest is never called
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetAt(int index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid ctx);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid ctx);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Fmt; public int Pid; }
        // 24 bytes on x64, the size Windows writes: a smaller struct would get the stack overwritten
        [StructLayout(LayoutKind.Explicit, Size = 24)] struct PropVariant { [FieldOffset(0)] public ushort Vt; [FieldOffset(8)] public IntPtr Ptr; }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant pv);

        const int Capture = 1, Active = 1, Console = 0, Communications = 2, ClsctxAll = 23, VtLpwstr = 31;
        static readonly PropertyKey FriendlyName = new PropertyKey { Fmt = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Pid = 14 };

        static IAudioEndpointVolume Volume(IMMDevice d)
        {
            object o;
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            return d.Activate(ref iid, ClsctxAll, IntPtr.Zero, out o) == 0 ? (IAudioEndpointVolume)o : null;
        }

        static string Name(IMMDevice d)
        {
            IPropertyStore ps;
            if (d.OpenPropertyStore(0, out ps) != 0) return null;
            try
            {
                var key = FriendlyName;
                PropVariant pv;
                if (ps.GetValue(ref key, out pv) != 0) return null;
                try { return pv.Vt == VtLpwstr ? Marshal.PtrToStringUni(pv.Ptr) : null; }
                finally { PropVariantClear(ref pv); }
            }
            finally { Marshal.ReleaseComObject(ps); }
        }

        static string DefaultId(IMMDeviceEnumerator en, int role)
        {
            IMMDevice d;
            if (en.GetDefaultAudioEndpoint(Capture, role, out d) != 0) return null;   // no microphone at all
            try { string id; d.GetId(out id); return id; }
            finally { Marshal.ReleaseComObject(d); }
        }

        public static List<Mic> List()
        {
            var res = new List<Mic>();
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            try
            {
                string def = DefaultId(en, Console), comms = DefaultId(en, Communications);
                IMMDeviceCollection all;
                if (en.EnumAudioEndpoints(Capture, Active, out all) != 0) return res;
                try
                {
                    int n;
                    all.GetCount(out n);
                    for (int i = 0; i < n; i++)
                    {
                        IMMDevice d;
                        if (all.Item(i, out d) != 0) continue;
                        try
                        {
                            var m = new Mic();
                            d.GetId(out m.Id);
                            m.Name = Name(d) ?? L.T("Microfono", "Microphone");
                            var v = Volume(d);
                            if (v != null)
                            {
                                bool muted;
                                v.GetMute(out muted);
                                m.Muted = muted;
                                Marshal.ReleaseComObject(v);
                            }
                            m.Default = m.Id == def;
                            m.Comms = m.Id == comms;
                            res.Add(m);
                        }
                        finally { Marshal.ReleaseComObject(d); }
                    }
                }
                finally { Marshal.ReleaseComObject(all); }
            }
            finally { Marshal.ReleaseComObject(en); }
            return res.OrderByDescending(m => m.Default).ThenByDescending(m => m.Comms).ThenBy(m => m.Name).ToList();
        }

        public static bool SetMute(string id, bool mute)
        {
            var en = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            try
            {
                IMMDevice d;
                if (en.GetDevice(id, out d) != 0) return false;
                try
                {
                    var v = Volume(d);
                    if (v == null) return false;
                    Guid ctx = Guid.Empty;
                    int hr = v.SetMute(mute, ref ctx);
                    Marshal.ReleaseComObject(v);
                    return hr == 0;
                }
                finally { Marshal.ReleaseComObject(d); }
            }
            finally { Marshal.ReleaseComObject(en); }
        }
    }

    // What Hush decides, in one place, checked by --selftest.
    public static class Rules
    {
        // "Off" means every microphone off. No microphone: nothing to switch off.
        public static bool AllMuted(List<Mic> mics) { return mics.Count > 0 && mics.All(m => m.Muted); }

        // One key: anything live goes off; all off comes back on.
        public static bool ToggleTarget(List<Mic> mics) { return !AllMuted(mics); }

        // A microphone plugged in while Hush has everything off gets switched off too
        public static List<string> Newcomers(List<Mic> mics, HashSet<string> known, bool hushed)
        {
            if (!hushed) return new List<string>();
            return mics.Where(m => !known.Contains(m.Id) && !m.Muted).Select(m => m.Id).ToList();
        }
    }

    public class Use
    {
        public string Key, Cap;
        public bool Packaged;
        public long Start, Stop;   // FILETIME
        public bool Now { get { return Access.InUse(Start, Stop); } }
    }

    // Who used the microphone and the camera, and who is using them now. Windows writes it in the registry
    // for its own privacy icon in the tray; any user can read their own.
    public static class Access
    {
        const string Root = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\";

        public static bool InUse(long start, long stop) { return start > 0 && (stop == 0 || stop < start); }

        public static string ExePath(string key) { return key.Replace('#', '\\'); }

        // "Microsoft.WindowsCamera_8wekyb3d8bbwe" -> "Windows Camera", "MSTeams_8wekyb3d8bbwe" -> "MS Teams"
        // ponytail: a name from the package id, not the localized display name (that needs the package API)
        public static string PackageName(string family)
        {
            string s = family;
            int u = s.IndexOf('_');
            if (u > 0) s = s.Substring(0, u);
            int dot = s.LastIndexOf('.');
            if (dot >= 0 && dot < s.Length - 1) s = s.Substring(dot + 1);
            return Regex.Replace(s, "(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        }

        static long Qword(object v) { return v is long ? (long)v : 0; }

        public static List<Use> Read(string cap)
        {
            var byKey = new Dictionary<string, Use>(StringComparer.OrdinalIgnoreCase);
            foreach (RegistryKey hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using (RegistryKey root = hive.OpenSubKey(Root + cap))
                    {
                        if (root == null) continue;
                        foreach (string name in root.GetSubKeyNames())
                        {
                            if (name != "NonPackaged") { Add(byKey, root, name, true, cap); continue; }
                            using (RegistryKey np = root.OpenSubKey(name))
                                if (np != null)
                                    foreach (string exe in np.GetSubKeyNames()) Add(byKey, np, exe, false, cap);
                        }
                    }
                }
                catch { }   // a key we may not read: the others still count
            }
            return byKey.Values.OrderByDescending(u => u.Now).ThenByDescending(u => u.Start).ToList();
        }

        static void Add(Dictionary<string, Use> to, RegistryKey parent, string sub, bool packaged, string cap)
        {
            using (RegistryKey k = parent.OpenSubKey(sub))
            {
                if (k == null) return;
                long start = Qword(k.GetValue("LastUsedTimeStart")), stop = Qword(k.GetValue("LastUsedTimeStop"));
                if (start <= 0) return;   // allowed, never used
                Use old;
                if (to.TryGetValue(sub, out old) && old.Start >= start) return;
                to[sub] = new Use { Key = sub, Cap = cap, Packaged = packaged, Start = start, Stop = stop };
            }
        }
    }

    // A key that works in every app, through RegisterHotKey: no keyboard hook, nothing read but this one combination.
    public static class Hotkey
    {
        public const int Alt = 1, Ctrl = 2, Shift = 4, Win = 8;
        // Win+Shift+Q, Q for quiet. Win+Shift+A (PowerToys' old one), Win+Alt+K (Windows' call mute) and Win+Shift+M were taken on a Windows 11 PC.
        public const int DefaultMods = Win | Shift, DefaultKey = 0x51;
        // tried in order when the default is taken and you never picked a key yourself
        public static readonly int[][] Fallbacks = { new[] { Ctrl | Alt, 0x4D }, new[] { Ctrl | Shift, 0x7B } };

        static bool Letter(int k) { return (k >= 0x41 && k <= 0x5A) || (k >= 0x30 && k <= 0x39); }
        static bool Function(int k) { return k >= 0x70 && k <= 0x87; }   // F1-F24
        static bool Lonely(int k) { return (k >= 0x7C && k <= 0x87) || k == 0x13; }   // F13-F24 and Pause: nobody types them, they may go alone

        public static bool Valid(int mods, int key)
        {
            if (mods < 0 || mods > 15 || !(Letter(key) || Function(key) || key == 0x13)) return false;
            return mods != 0 || Lonely(key);
        }

        public static string KeyName(int key)
        {
            if (Letter(key)) return ((char)key).ToString();
            if (Function(key)) return "F" + (key - 0x6F);
            return "Pause";
        }

        public static string Text(int mods, int key)
        {
            var parts = new List<string>();
            if ((mods & Ctrl) != 0) parts.Add("Ctrl");
            if ((mods & Alt) != 0) parts.Add("Alt");
            if ((mods & Shift) != 0) parts.Add("Shift");
            if ((mods & Win) != 0) parts.Add("Win");
            parts.Add(KeyName(key));
            return string.Join("+", parts);
        }
    }

    public class HotkeyWindow : NativeWindow
    {
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        const int NoRepeat = 0x4000, WmHotkey = 0x0312;

        public event Action Pressed;

        public HotkeyWindow() { CreateHandle(new CreateParams()); }

        // false: another app already owns the combination
        public bool Set(int mods, int key)
        {
            UnregisterHotKey(Handle, 1);
            return RegisterHotKey(Handle, 1, mods | NoRepeat, key);
        }

        public void Release() { UnregisterHotKey(Handle, 1); DestroyHandle(); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && Pressed != null) Pressed();
            base.WndProc(ref m);
        }
    }

    // The little card that says "Microphone off" in the middle of whatever you're doing. Never takes the focus, clicks go through it.
    public class Osd : Form
    {
        readonly System.Windows.Forms.Timer hide = new System.Windows.Forms.Timer { Interval = 1400 };
        Icon icon;
        string text = "";
        float dpi = 1;

        public Osd()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(0x0b, 0x0f, 0x0d);
            DoubleBuffered = true;
            Opacity = 0.96;   // layered: what makes the clicks go through
            hide.Tick += (s, e) => { hide.Stop(); Hide(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x80 | 0x20 | 0x8;   // no activate, tool window (not in Alt+Tab), click-through, topmost
                return cp;
            }
        }

        [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

        public void Flash(bool muted)
        {
            using (var g = CreateGraphics()) dpi = g.DpiX / 96f;
            text = muted ? L.T("Microfono spento", "Microphone off") : L.T("Microfono acceso", "Microphone on");
            if (icon != null) icon.Dispose();
            icon = Libs.Icon(muted ? "TrayOff.ico" : "TrayLive.ico", new Size((int)(40 * dpi), (int)(40 * dpi)));
            Size = new Size((int)(250 * dpi), (int)(64 * dpi));
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, (int)(28 * dpi), (int)(28 * dpi)));
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;   // the screen you're working on
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Bottom - Height - (int)(area.Height * 0.12));
            Show();
            Invalidate();
            hide.Stop();
            hide.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int pad = (int)(12 * dpi), size = Height - 2 * pad;
            if (icon != null) g.DrawIcon(icon, new Rectangle(pad, pad, size, size));
            using (var f = new Font("Segoe UI Semibold", 15f * dpi, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { LineAlignment = StringAlignment.Center })
                g.DrawString(text, f, Brushes.White, new RectangleF(pad * 2 + size, 0, Width - size - pad * 3, Height), sf);
        }
    }

    public static class SelfTest
    {
        static void Check(bool ok, string what) { if (!ok) throw new Exception("self-test failed: " + what); }

        static Mic M(string id, bool muted) { return new Mic { Id = id, Name = id, Muted = muted }; }

        public static int Run()
        {
            try
            {
                var on = new List<Mic> { M("a", false), M("b", true) };
                var off = new List<Mic> { M("a", true), M("b", true) };
                Check(!Rules.AllMuted(on) && Rules.AllMuted(off) && !Rules.AllMuted(new List<Mic>()), "off means every microphone off");
                Check(Rules.ToggleTarget(on) && !Rules.ToggleTarget(off), "one key: anything live goes off, all off comes back on");
                var plugged = new List<Mic> { M("a", true), M("usb", false) };
                var known = new HashSet<string> { "a" };
                Check(Rules.Newcomers(plugged, known, true).SequenceEqual(new[] { "usb" }), "a microphone plugged in while off is switched off");
                Check(Rules.Newcomers(plugged, known, false).Count == 0, "while on, a new microphone is left alone");

                Check(Access.InUse(100, 0) && Access.InUse(200, 100) && !Access.InUse(100, 200) && !Access.InUse(0, 0), "in use: started and not stopped");
                Check(Access.PackageName("Microsoft.WindowsCamera_8wekyb3d8bbwe") == "Windows Camera", "package name, dotted");
                Check(Access.PackageName("MSTeams_8wekyb3d8bbwe") == "MS Teams", "package name, capitals");
                Check(Access.ExePath(@"C:#Program Files#Zoom#bin#Zoom.exe") == @"C:\Program Files\Zoom\bin\Zoom.exe", "exe path from the registry key");

                Check(Hotkey.Valid(Hotkey.DefaultMods, Hotkey.DefaultKey) && Hotkey.Text(Hotkey.DefaultMods, Hotkey.DefaultKey) == "Shift+Win+Q", "the default key");
                Check(!Hotkey.Valid(0, 0x41) && Hotkey.Valid(0, 0x7C) && Hotkey.Valid(0, 0x13), "a letter alone no, F13 or Pause alone yes");
                Check(!Hotkey.Valid(16, 0x41) && !Hotkey.Valid(Hotkey.Ctrl, 0x20) && Hotkey.Text(Hotkey.Ctrl | Hotkey.Alt, 0x7B) == "Ctrl+Alt+F12", "bad modifiers and keys refused, names in order");

                Audio.List();                 // Core Audio answers (a PC with no microphone is fine)
                Access.Read("microphone");    // and the registry too
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }

    public class HushContext : ApplicationContext
    {
        const string RegistryPath = "SOFTWARE\\HushApp";
        const string RunPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        const int RecentDays = 30, MaxUses = 30;

        // Portable version: a Hush.ini next to the exe. Settings live there, the registry is never touched.
        static readonly string IniPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "Hush.ini");
        public static readonly bool Portable = File.Exists(IniPath);
        const string IniHeader = "; Hush settings. While this file sits next to Hush.exe, nothing goes to the registry.";

        readonly NotifyIcon tray;
        readonly MainForm form;
        readonly HotkeyWindow hotkey = new HotkeyWindow();
        readonly Osd osd = new Osd();
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly Icon appIcon, offIcon, liveIcon;
        readonly ToolStripMenuItem toggleItem;
        readonly HashSet<string> known = new HashSet<string>();
        readonly Dictionary<string, object[]> apps = new Dictionary<string, object[]>(StringComparer.OrdinalIgnoreCase);   // exe: name, icon
        List<Mic> mics = new List<Mic>();
        List<Use> micUses = new List<Use>(), camUses = new List<Use>();
        bool hushed, startup, showOsd = true, hotkeyOk, keyChosen;
        int mods = Hotkey.DefaultMods, key = Hotkey.DefaultKey;
        string trayState = "";

        public HushContext(bool fromStartup)
        {
            LoadSettings();
            Size small = SystemInformation.SmallIconSize;
            appIcon = L.AppIcon(small);
            offIcon = Libs.Icon("TrayOff.ico", small);
            liveIcon = Libs.Icon("TrayLive.ico", small);

            tray = new NotifyIcon();
            tray.Icon = appIcon;
            tray.Text = "Hush";
            tray.Visible = true;
            tray.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) Toggle(false); };   // one click, like the volume

            var menu = new ContextMenuStrip();
            var header = new ToolStripMenuItem("Hush");
            header.Enabled = false;
            header.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());
            toggleItem = new ToolStripMenuItem("", null, (s, e) => Toggle(false));
            menu.Items.Add(toggleItem);
            menu.Items.Add(L.T("Apri Hush", "Open Hush"), null, (s, e) => ShowForm());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(L.T("Esci", "Exit"), null, (s, e) => Exit());
            tray.ContextMenuStrip = menu;

            startup = !Portable && RunKeySet();
            if (startup) SetRunKey(true);   // the exe may have moved: the entry follows it

            hotkey.Pressed += () => Toggle(true);
            hotkeyOk = hotkey.Set(mods, key);
            if (!hotkeyOk && !keyChosen)
                foreach (int[] f in Hotkey.Fallbacks)
                    if (hotkey.Set(f[0], f[1])) { mods = f[0]; key = f[1]; hotkeyOk = true; break; }

            form = new MainForm(DataFolder());
            form.Message += OnMessage;
            var handle = form.Handle;

            mics = Audio.List();
            hushed = Rules.AllMuted(mics);   // off since last time (Windows keeps it): Hush carries on from there
            foreach (var m in mics) known.Add(m.Id);
            poll.Tick += (s, e) => Poll();
            poll.Start();
            Poll();

            if (!hotkeyOk) tray.ShowBalloonTip(10000, "Hush", string.Format(L.T("{0} è già usato da un'altra app: scegli un altro tasto nelle impostazioni.",
                                                                                 "{0} is taken by another app: pick another key in the settings."), Hotkey.Text(mods, key)), ToolTipIcon.Warning);
            if (!fromStartup) ShowForm();
        }

        static string DataFolder()
        {
            string root = Portable ? Path.GetTempPath() : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Hush", "web");
        }

        // Once a second: the microphones, who uses them, the tray, and the window if it's open
        void Poll()
        {
            try { mics = Audio.List(); } catch { mics = new List<Mic>(); }
            foreach (string id in Rules.Newcomers(mics, known, hushed))
            {
                Audio.SetMute(id, true);
                mics.First(m => m.Id == id).Muted = true;
            }
            foreach (var m in mics) known.Add(m.Id);
            if (hushed && !Rules.AllMuted(mics)) hushed = false;   // switched back on from Windows' settings: that's fine too

            micUses = Access.Read("microphone");
            camUses = Access.Read("webcam");
            UpdateTray();
            if (form.PageReady) form.Post(State());
        }

        void Toggle(bool fromKey)
        {
            var now = Audio.List();
            if (now.Count == 0) { if (fromKey) tray.ShowBalloonTip(5000, "Hush", L.T("Nessun microfono collegato.", "No microphone connected."), ToolTipIcon.Info); return; }
            bool mute = Rules.ToggleTarget(now);
            foreach (var m in now) Audio.SetMute(m.Id, mute);
            hushed = mute;
            Poll();
            if (fromKey && showOsd) osd.Flash(mute);
        }

        void UpdateTray()
        {
            bool off = Rules.AllMuted(mics);
            bool listened = micUses.Any(u => u.Now);
            string state = off ? "off" : listened ? "live" : "on";
            string who = string.Join(", ", micUses.Where(u => u.Now).Select(u => AppName(u)).Distinct());
            string tip = mics.Count == 0 ? L.T("Hush · nessun microfono", "Hush · no microphone")
                       : off ? L.T("Hush · microfono spento", "Hush · microphone off")
                       : listened ? L.T("Hush · ti sente: ", "Hush · listening: ") + who
                       : L.T("Hush · microfono acceso", "Hush · microphone on");
            tray.Text = tip.Length > 63 ? tip.Substring(0, 62) + "…" : tip;
            toggleItem.Text = off ? L.T("Accendi il microfono", "Turn the microphone on") : L.T("Spegni il microfono", "Turn the microphone off");
            toggleItem.Enabled = mics.Count > 0;
            if (state == trayState) return;
            trayState = state;
            tray.Icon = off ? offIcon : listened ? liveIcon : appIcon;
        }

        // The program's own name and icon, read once per exe
        object[] AppInfo(Use u)
        {
            if (u.Packaged) return new object[] { Access.PackageName(u.Key), null };
            string path = Access.ExePath(u.Key);
            object[] info;
            if (apps.TryGetValue(path, out info)) return info;
            string name = Path.GetFileNameWithoutExtension(path), icon = null;
            try
            {
                if (File.Exists(path))
                {
                    string desc = FileVersionInfo.GetVersionInfo(path).FileDescription;
                    if (!string.IsNullOrWhiteSpace(desc)) name = desc.Trim();
                    using (var ic = Icon.ExtractAssociatedIcon(path))
                    using (var bmp = new Bitmap(ic.ToBitmap(), 32, 32))
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        icon = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch { }   // gone, or no icon: the name is enough
            return apps[path] = new object[] { name, icon };
        }

        string AppName(Use u) { return (string)AppInfo(u)[0]; }

        static long Ms(long filetime)
        {
            if (filetime <= 0) return 0;
            return (long)(DateTime.FromFileTimeUtc(filetime) - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        object Uses(List<Use> list)
        {
            long since = DateTime.UtcNow.AddDays(-RecentDays).ToFileTimeUtc();
            return list.Where(u => u.Now || u.Start >= since).Take(MaxUses).Select(u =>
            {
                var info = AppInfo(u);
                return new Dictionary<string, object> {
                    { "n", info[0] }, { "icon", info[1] }, { "k", u.Key }, { "pk", u.Packaged },
                    { "s", Ms(u.Start) }, { "e", u.Now ? 0 : Ms(u.Stop) }, { "now", u.Now } };
            }).ToList();
        }

        object State()
        {
            return new Dictionary<string, object> {
                { "t", "state" }, { "muted", Rules.AllMuted(mics) },
                { "mics", mics.Select(m => new Dictionary<string, object> { { "id", m.Id }, { "n", m.Name }, { "muted", m.Muted }, { "def", m.Default }, { "comms", m.Comms } }).ToList() },
                { "mic", Uses(micUses) }, { "cam", Uses(camUses) } };
        }

        object Settings()
        {
            return new Dictionary<string, object> {
                { "t", "settings" }, { "startup", startup }, { "osd", showOsd },
                { "mods", mods }, { "key", key }, { "keyText", Hotkey.Text(mods, key) }, { "keyOk", hotkeyOk } };
        }

        // Everything the window asks for. The page is ours, but what it sends is still checked here.
        void OnMessage(Dictionary<string, object> m)
        {
            object v;
            switch (Convert.ToString(m.TryGetValue("t", out v) ? v : ""))
            {
                case "ready":
                    form.Post(new Dictionary<string, object> {
                        { "t", "init" }, { "lang", L.It ? "it" : "en" }, { "portable", Portable },
                        { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString(3) } });
                    form.Post(Settings());
                    form.Post(State());
                    break;
                case "toggle": Toggle(false); break;
                case "mute":
                    string id = m.TryGetValue("id", out v) ? Convert.ToString(v) : null;
                    if (id != null && mics.Any(x => x.Id == id) && m.TryGetValue("on", out v))
                    {
                        bool mute = Convert.ToBoolean(v);
                        Audio.SetMute(id, mute);
                        if (!mute) hushed = false;
                    }
                    Poll();
                    break;
                case "open":
                    string url = m.TryGetValue("url", out v) ? Convert.ToString(v) : "";
                    if (url == "https://github.com/davide-mariotti/Hush" || url == "https://ko-fi.com/Z0X32ATNHL" || url == "https://davide-mariotti.github.io/apps/hush")
                        try { Process.Start("explorer.exe", "\"" + url + "\""); } catch { }
                    break;
                case "set":
                    if (m.TryGetValue("startup", out v) && !Portable)
                    {
                        try { SetRunKey(Convert.ToBoolean(v)); } catch { }
                        startup = RunKeySet();
                    }
                    if (m.TryGetValue("osd", out v)) showOsd = Convert.ToBoolean(v);
                    object km;
                    if (m.TryGetValue("mods", out v) && m.TryGetValue("key", out km))
                    {
                        int nm = Convert.ToInt32(v), nk = Convert.ToInt32(km);
                        if (Hotkey.Valid(nm, nk))
                        {
                            if (hotkey.Set(nm, nk)) { mods = nm; key = nk; hotkeyOk = true; keyChosen = true; }
                            else
                            {
                                hotkeyOk = hotkey.Set(mods, key);   // taken: the old one stays
                                form.Post(new Dictionary<string, object> { { "t", "taken" }, { "keyText", Hotkey.Text(nm, nk) } });
                            }
                        }
                    }
                    SaveSettings();
                    form.Post(Settings());
                    break;
            }
        }

        void ShowForm()
        {
            if (form.Visible) { if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal; form.Activate(); }
            else form.Show();
        }

        // Windows keeps a microphone off after Hush closes: ask, so nobody finds it silent in tomorrow's call
        void Exit()
        {
            if (Rules.AllMuted(mics))
            {
                var answer = MessageBox.Show(L.T("Il microfono è spento, e resta spento anche senza Hush. Lo riaccendo prima di uscire?",
                                                 "The microphone is off, and stays off without Hush too. Turn it back on before closing?"),
                                             "Hush", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) return;
                if (answer == DialogResult.Yes) foreach (var m in mics) Audio.SetMute(m.Id, false);
            }
            poll.Stop();
            hotkey.Release();
            tray.Visible = false;
            osd.Dispose();
            form.Dispose();
            Application.Exit();
        }

        // ── Start with Windows: the Run key, like Sentinel ──────────────────────

        bool RunKeySet()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunPath, false)) return k != null && k.GetValue("Hush") != null; }
            catch { return false; }
        }

        void SetRunKey(bool on)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunPath, true))
            {
                if (on) k.SetValue("Hush", "\"" + Application.ExecutablePath + "\" --startup");
                else k.DeleteValue("Hush", false);
            }
        }

        // ── Settings: registry, or Hush.ini in the portable version ──────────────

        void LoadSettings()
        {
            try
            {
                var v = ReadValues();
                string s, k;
                if (v.TryGetValue("Osd", out s)) showOsd = Convert.ToBoolean(s);
                if (v.TryGetValue("HotkeyMods", out s) && v.TryGetValue("HotkeyKey", out k) && Hotkey.Valid(Convert.ToInt32(s), Convert.ToInt32(k)))
                {
                    mods = Convert.ToInt32(s);
                    key = Convert.ToInt32(k);
                    keyChosen = true;
                }
            }
            catch { }   // broken values: defaults win
        }

        Dictionary<string, string> ReadValues()
        {
            var d = new Dictionary<string, string>();
            if (Portable)
            {
                foreach (string line in File.ReadAllLines(IniPath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0 && !line.StartsWith(";")) d[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                return d;
            }
            using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(RegistryPath))
                if (rk != null)
                    foreach (string name in rk.GetValueNames())
                        d[name] = Convert.ToString(rk.GetValue(name), CultureInfo.InvariantCulture);
            return d;
        }

        void SaveSettings()
        {
            var values = new Dictionary<string, object> { { "Osd", showOsd }, { "HotkeyMods", mods }, { "HotkeyKey", key } };
            try
            {
                if (Portable)
                {
                    var lines = new List<string> { IniHeader };
                    foreach (var kv in values) lines.Add(kv.Key + "=" + Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
                    File.WriteAllLines(IniPath, lines.ToArray());
                    return;
                }
                using (RegistryKey rk = Registry.CurrentUser.CreateSubKey(RegistryPath))
                    foreach (var kv in values) rk.SetValue(kv.Key, kv.Value);
            }
            catch { }   // read-only stick or locked key: the change doesn't stick, Hush keeps working
        }
    }
}
