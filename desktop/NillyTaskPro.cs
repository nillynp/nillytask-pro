// ============================================================================
// NillyTask Pro — Ultra-Light Native Windows 10/11 Task Manager,
//                 Performance Monitor, Startup Manager & Old-Laptop Rescue
// created by Nilly graphics (Nillygraphics · Sotiris Christos) — https://nillygraphics.com/
// Strictly C# 5 compatible (.NET Framework 4.x csc.exe — 0 external DLLs)
// Features:
//  - Opens in <50ms even at 100% CPU (100% Native Win32/GDI — zero WinUI/XAML!)
//  - Real-Time Processes (CPU %, RAM MB, Threads, Path, Kill / Kill Tree / Trim RAM / Priority)
//  - Live 60s Monokai Performance Sparklines (CPU & RAM + Top 5 Consumers)
//  - Startup Apps Manager (HKCU/HKLM + native Windows StartupApproved toggle)
//  - Services Viewer + 1-Click Safe Old-Laptop Rescue Toggles (100% Reversible)
//  - 0 MB background RAM when closed · Always-On-Top (Ctrl+T) · Dark/Light Theme
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("NillyTask Pro")]
[assembly: AssemblyDescription("Ultra-Light Native Windows 10/11 Task Manager, Performance Monitor & Startup Rescue — created by Nilly graphics (Nillygraphics · Sotiris Christos)")]
[assembly: AssemblyCompany("Nilly graphics (Nillygraphics · Sotiris Christos) — https://nillygraphics.com/")]
[assembly: AssemblyProduct("NillyTask Pro")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Nilly graphics (Sotiris Christos) — https://nillygraphics.com/")]
[assembly: AssemblyTrademark("Nilly graphics / Nillygraphics — Sotiris Christos")]
[assembly: AssemblyVersion("1.3.4.0")]
[assembly: AssemblyFileVersion("1.3.4.0")]

namespace NillyTaskPro
{
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
            public ulong ToUInt64() { return ((ulong)dwHighDateTime << 32) | dwLowDateTime; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr minSize, IntPtr maxSize);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        public static void TrimMemory()
        {
            try
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                SetProcessWorkingSetSize(GetCurrentProcess(), (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }

        public static string GetBatterySummary()
        {
            try
            {
                SYSTEM_POWER_STATUS ps;
                if (!GetSystemPowerStatus(out ps)) return "";
                if (ps.BatteryFlag == 128 || ps.BatteryLifePercent == 255) return ""; // Desktop / No battery
                string state = (ps.ACLineStatus == 1) ? "AC/Φόρτιση" : "Μπαταρία";
                if (ps.ACLineStatus == 0 && ps.BatteryLifeTime > 60)
                {
                    int hrs = ps.BatteryLifeTime / 3600;
                    int mins = (ps.BatteryLifeTime % 3600) / 60;
                    state = string.Format("~{0}ω {1}λ", hrs, mins);
                }
                return string.Format(" · Μπατ.: {0}% ({1})", ps.BatteryLifePercent, state);
            }
            catch { return ""; }
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);
        [DllImport("kernel32.dll")] public static extern ulong GetTickCount64();
        [DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(IntPtr hProcess, out FILETIME creation, out FILETIME exit, out FILETIME kernel, out FILETIME user);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetPriorityClass(IntPtr hProcess);

        public static void QueryProcessFast(int pid, bool needPath, out long cpuTicks, out bool hasCpu, out string priority, out string path)
        {
            cpuTicks = 0; hasCpu = false; priority = "System"; path = "";
            if (pid <= 4) return;
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) return;
            try
            {
                FILETIME cr, ex, krn, usr;
                if (GetProcessTimes(h, out cr, out ex, out krn, out usr))
                {
                    cpuTicks = (long)(krn.ToUInt64() + usr.ToUInt64());
                    hasCpu = true;
                }
                uint pc = GetPriorityClass(h);
                if (pc == 0x20) priority = "Normal";
                else if (pc == 0x40) priority = "Idle";
                else if (pc == 0x80) priority = "High";
                else if (pc == 0x100) priority = "RealTime";
                else if (pc == 0x4000) priority = "BelowNormal";
                else if (pc == 0x8000) priority = "AboveNormal";

                if (needPath)
                {
                    StringBuilder sb = new StringBuilder(520);
                    int sz = sb.Capacity;
                    if (QueryFullProcessImageName(h, 0, sb, ref sz) && sz > 0)
                        path = sb.ToString(0, sz);
                }
            }
            finally { CloseHandle(h); }
        }

        public static void SetDarkTitlebar(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int v = dark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref v, 4) != 0)
                    DwmSetWindowAttribute(hwnd, 19, ref v, 4);
            }
            catch { }
        }

        public static MEMORYSTATUSEX GetMemory()
        {
            MEMORYSTATUSEX m = new MEMORYSTATUSEX();
            m.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            GlobalMemoryStatusEx(ref m);
            return m;
        }

        public static long TrimAllUserProcesses()
        {
            MEMORYSTATUSEX before = GetMemory();
            int curSession = Process.GetCurrentProcess().SessionId;
            Process[] all = Process.GetProcesses();
            for (int i = 0; i < all.Length; i++)
            {
                Process p = all[i];
                try
                {
                    if (p.SessionId == curSession && p.Id > 4)
                        EmptyWorkingSet(p.Handle);
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
            MEMORYSTATUSEX after = GetMemory();
            long freedBytes = (long)after.ullAvailPhys - (long)before.ullAvailPhys;
            return freedBytes > 0 ? freedBytes / (1024 * 1024) : 0;
        }
    }

    internal static class IconHelper
    {
        public static Bitmap LoadCrispIcon(string icoPath, int sz)
        {
            if (string.IsNullOrEmpty(icoPath) || !File.Exists(icoPath)) return null;
            try
            {
                byte[] data = File.ReadAllBytes(icoPath);
                if (data.Length < 22) return null;
                int count = BitConverter.ToUInt16(data, 4);
                int bestOff = -1, bestLen = 0, bestDim = -1;
                for (int i = 0; i < count; i++)
                {
                    int e = 6 + i * 16;
                    if (e + 16 > data.Length) break;
                    int w = data[e] == 0 ? 256 : data[e];
                    int len = BitConverter.ToInt32(data, e + 8);
                    int off = BitConverter.ToInt32(data, e + 12);
                    if (off < 0 || len <= 8 || off + len > data.Length) continue;
                    bool isPng = (data[off] == 0x89 && data[off + 1] == 0x50 && data[off + 2] == 0x4E && data[off + 3] == 0x47);
                    if (isPng && w > bestDim) { bestDim = w; bestOff = off; bestLen = len; }
                }
                if (bestOff >= 0)
                {
                    using (MemoryStream ms = new MemoryStream(data, bestOff, bestLen, false))
                    using (Image src = Image.FromStream(ms))
                    {
                        Bitmap dst = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
                        using (Graphics g = Graphics.FromImage(dst))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.DrawImage(src, 0, 0, sz, sz);
                        }
                        return dst;
                    }
                }
            }
            catch { }
            return null;
        }

        public static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            float d = r * 2f;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    internal sealed class ProcInfo
    {
        public int Pid;
        public string Name;
        public double CpuPct;
        public double RamMb;
        public int Threads;
        public string Priority;
        public string Path;
        public bool IsCritical;
        public bool IsNilly;
    }

    internal sealed class StartupEntry
    {
        public string Name;
        public string Command;
        public string Scope;      // "HKCU", "HKLM", "Folder"
        public string RegSubKey;
        public string FilePath;
        public bool Enabled;
    }

    internal sealed class ServiceRow
    {
        public string Name;
        public string DisplayName;
        public string Status;
        public bool Running;
    }

    internal sealed class DarkListView : ListView
    {
        public bool DarkMode = true;
        public int SortColumn = 3; // Default sort by RAM MB
        public bool SortDesc = true;

        public DarkListView()
        {
            DoubleBuffered = true;
            View = View.Details;
            FullRowSelect = true;
            MultiSelect = false;
            HideSelection = false;
            BorderStyle = BorderStyle.None;
            OwnerDraw = true;
            HeaderStyle = ColumnHeaderStyle.Clickable;
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            Color bg = DarkMode ? Color.FromArgb(30, 32, 39) : Color.FromArgb(234, 238, 244);
            Color fg = DarkMode ? Color.FromArgb(196, 204, 218) : Color.FromArgb(38, 46, 58);
            Color div = DarkMode ? Color.FromArgb(46, 50, 60) : Color.FromArgb(210, 216, 226);
            Color acc = Color.FromArgb(252, 150, 31);

            using (SolidBrush br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);
            using (Pen p = new Pen(div))
            {
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            }

            string txt = e.Header.Text;
            if (e.ColumnIndex == SortColumn) txt += SortDesc ? " ▼" : " ▲";
            TextFormatFlags tf = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (e.Header.TextAlign == HorizontalAlignment.Right) tf |= TextFormatFlags.Right;
            else tf |= TextFormatFlags.Left;

            Rectangle r = new Rectangle(e.Bounds.X + 7, e.Bounds.Y, Math.Max(10, e.Bounds.Width - 14), e.Bounds.Height);
            using (Font hf = new Font("Segoe UI", 8.4f, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, txt, hf, r, e.ColumnIndex == SortColumn ? acc : fg, tf);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            // Handled per-subitem for flicker-free rendering
        }

        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            bool sel = e.Item.Selected;
            Color bg = sel
                ? (DarkMode ? Color.FromArgb(54, 42, 26) : Color.FromArgb(255, 236, 210))
                : ((e.ItemIndex % 2 == 0)
                    ? (DarkMode ? Color.FromArgb(21, 22, 27) : Color.White)
                    : (DarkMode ? Color.FromArgb(25, 27, 33) : Color.FromArgb(247, 249, 252)));
            Color fg = sel
                ? (DarkMode ? Color.FromArgb(255, 214, 152) : Color.FromArgb(120, 52, 0))
                : e.SubItem.ForeColor;
            if (fg == Color.Empty || fg == SystemColors.WindowText)
                fg = DarkMode ? Color.FromArgb(232, 236, 244) : Color.FromArgb(24, 28, 36);

            using (SolidBrush br = new SolidBrush(bg)) e.Graphics.FillRectangle(br, e.Bounds);

            TextFormatFlags tf = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right) tf |= TextFormatFlags.Right;
            else tf |= TextFormatFlags.Left;

            Rectangle r = new Rectangle(e.Bounds.X + 7, e.Bounds.Y, Math.Max(10, e.Bounds.Width - 14), e.Bounds.Height);
            Font f = e.SubItem.Font ?? Font;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, f, r, fg, tf);
        }
    }

    // ========================================================================
    // OWNER-DRAWN LIVE 60s MONOKAI PERFORMANCE GRAPHS (CPU, RAM & TOP 5 BARS)
    // ========================================================================
    internal sealed class PerfViewPanel : Control
    {
        public bool DarkMode = true;
        private readonly float[] _cpuHist = new float[60];
        private readonly float[] _ramHist = new float[60];
        private int _samples = 0;
        private double _cpuNow, _ramPctNow, _ramUsedGb, _ramTotalGb;
        private int _procCount, _threadCount;
        private string _uptimeStr = "";
        private List<ProcInfo> _topRam = new List<ProcInfo>();
        private List<ProcInfo> _topCpu = new List<ProcInfo>();

        private static readonly Font FontHdr = new Font("Segoe UI", 9.6f, FontStyle.Bold);
        private static readonly Font FontBig = new Font("Segoe UI", 16f, FontStyle.Bold);
        private static readonly Font FontSub = new Font("Segoe UI", 8.3f);
        private static readonly Font FontBar = new Font("Segoe UI", 8.4f, FontStyle.Bold);

        public PerfViewPanel()
        {
            Dock = DockStyle.Fill;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void PushSample(double cpuPct, double ramPct, double ramUsedGb, double ramTotalGb,
                               int procCount, int threadCount, string uptime, List<ProcInfo> all)
        {
            for (int i = 0; i < 59; i++) { _cpuHist[i] = _cpuHist[i + 1]; _ramHist[i] = _ramHist[i + 1]; }
            _cpuHist[59] = (float)Math.Max(0, Math.Min(100, cpuPct));
            _ramHist[59] = (float)Math.Max(0, Math.Min(100, ramPct));
            if (_samples < 60) _samples++;
            _cpuNow = cpuPct; _ramPctNow = ramPct; _ramUsedGb = ramUsedGb; _ramTotalGb = ramTotalGb;
            _procCount = procCount; _threadCount = threadCount; _uptimeStr = uptime;

            List<ProcInfo> byRam = new List<ProcInfo>(all);
            byRam.Sort(delegate(ProcInfo a, ProcInfo b) { return b.RamMb.CompareTo(a.RamMb); });
            _topRam = byRam.GetRange(0, Math.Min(5, byRam.Count));

            List<ProcInfo> byCpu = new List<ProcInfo>(all);
            byCpu.Sort(delegate(ProcInfo a, ProcInfo b) { return b.CpuPct.CompareTo(a.CpuPct); });
            _topCpu = byCpu.GetRange(0, Math.Min(5, byCpu.Count));

            if (Visible) Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            Color bg = DarkMode ? Color.FromArgb(20, 21, 25) : Color.FromArgb(242, 244, 248);
            Color card = DarkMode ? Color.FromArgb(28, 30, 36) : Color.White;
            Color border = DarkMode ? Color.FromArgb(46, 50, 60) : Color.FromArgb(216, 222, 232);
            Color fg = DarkMode ? Color.FromArgb(240, 242, 248) : Color.FromArgb(22, 24, 30);
            Color muted = DarkMode ? Color.FromArgb(148, 156, 170) : Color.FromArgb(95, 105, 120);
            Color green = Color.FromArgb(166, 226, 46);
            Color cyan = Color.FromArgb(102, 217, 239);
            Color orange = Color.FromArgb(252, 150, 31);
            Color pink = Color.FromArgb(249, 38, 114);

            g.Clear(bg);
            int pad = 14, gap = 12;
            int colW = Math.Max(220, (Width - pad * 2 - gap) / 2);
            int chartH = Math.Max(155, (Height - pad * 2 - gap) / 2);
            int botH = Math.Max(140, Height - pad * 2 - gap - chartH);

            DrawSparkCard(g, new Rectangle(pad, pad, colW, chartH),
                "CPU Επεξεργαστής", string.Format("{0:0.0}%", _cpuNow),
                string.Format("{0} πυρήνες  ·  {1} διεργασίες  ·  {2} νήματα  ·  Uptime: {3}",
                    Environment.ProcessorCount, _procCount, _threadCount, _uptimeStr),
                _cpuHist, green, card, border, fg, muted);

            DrawSparkCard(g, new Rectangle(pad + colW + gap, pad, colW, chartH),
                "Μνήμη RAM", string.Format("{0:0.0}%", _ramPctNow),
                string.Format("{0:0.0} / {1:0.0} GB σε χρήση  ·  {2:0.0} GB ελεύθερα",
                    _ramUsedGb, _ramTotalGb, Math.Max(0, _ramTotalGb - _ramUsedGb)),
                _ramHist, cyan, card, border, fg, muted);

            DrawTopBarsCard(g, new Rectangle(pad, pad + chartH + gap, colW, botH),
                "Top 5 Κατανάλωση RAM (MB)", _topRam, true, orange, pink, card, border, fg, muted);

            DrawTopBarsCard(g, new Rectangle(pad + colW + gap, pad + chartH + gap, colW, botH),
                "Top 5 Χρήση CPU (%)", _topCpu, false, green, pink, card, border, fg, muted);
        }

        private void DrawSparkCard(Graphics g, Rectangle r, string title, string bigVal, string sub,
                                   float[] data, Color accent, Color card, Color border, Color fg, Color muted)
        {
            using (GraphicsPath gp = IconHelper.RoundRect(r.X, r.Y, r.Width, r.Height, 10f))
            using (SolidBrush br = new SolidBrush(card))
            using (Pen bp = new Pen(border, 1f))
            { g.FillPath(br, gp); g.DrawPath(bp, gp); }

            TextRenderer.DrawText(g, title, FontHdr, new Point(r.X + 14, r.Y + 12), fg, TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, bigVal, FontBig, new Rectangle(r.Right - 120, r.Y + 6, 106, 28), accent, TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, sub, FontSub, new Rectangle(r.X + 14, r.Y + 34, r.Width - 28, 18), muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            Rectangle gr = new Rectangle(r.X + 14, r.Y + 58, r.Width - 28, Math.Max(40, r.Height - 72));
            Color gridBg = DarkMode ? Color.FromArgb(20, 22, 27) : Color.FromArgb(246, 248, 252);
            Color gridLn = DarkMode ? Color.FromArgb(36, 40, 48) : Color.FromArgb(224, 230, 238);
            using (SolidBrush gb = new SolidBrush(gridBg)) g.FillRectangle(gb, gr);
            using (Pen gl = new Pen(gridLn, 1f))
            {
                for (int i = 1; i <= 3; i++)
                {
                    int gy = gr.Y + gr.Height * i / 4;
                    g.DrawLine(gl, gr.Left, gy, gr.Right, gy);
                }
                for (int i = 1; i <= 5; i++)
                {
                    int gx = gr.X + gr.Width * i / 6;
                    g.DrawLine(gl, gx, gr.Top, gx, gr.Bottom);
                }
                g.DrawRectangle(gl, gr);
            }

            PointF[] pts = new PointF[60];
            for (int i = 0; i < 60; i++)
            {
                float px = gr.X + (gr.Width * i) / 59f;
                float py = gr.Bottom - (gr.Height * data[i]) / 100f;
                pts[i] = new PointF(px, Math.Max(gr.Top + 1, Math.Min(gr.Bottom - 1, py)));
            }
            using (GraphicsPath area = new GraphicsPath())
            {
                area.AddLine(gr.Left, gr.Bottom, pts[0].X, pts[0].Y);
                area.AddLines(pts);
                area.AddLine(pts[59].X, pts[59].Y, gr.Right, gr.Bottom);
                area.CloseFigure();
                using (LinearGradientBrush fb = new LinearGradientBrush(gr, Color.FromArgb(72, accent), Color.FromArgb(6, accent), 90f))
                    g.FillPath(fb, area);
            }
            using (Pen lp = new Pen(accent, 2f))
            {
                lp.LineJoin = LineJoin.Round;
                g.DrawLines(lp, pts);
            }
        }

        private void DrawTopBarsCard(Graphics g, Rectangle r, string title, List<ProcInfo> items, bool isRam,
                                     Color accent, Color hot, Color card, Color border, Color fg, Color muted)
        {
            using (GraphicsPath gp = IconHelper.RoundRect(r.X, r.Y, r.Width, r.Height, 10f))
            using (SolidBrush br = new SolidBrush(card))
            using (Pen bp = new Pen(border, 1f))
            { g.FillPath(br, gp); g.DrawPath(bp, gp); }

            TextRenderer.DrawText(g, title, FontHdr, new Point(r.X + 14, r.Y + 12), fg, TextFormatFlags.NoPrefix);
            if (items.Count == 0) return;

            double maxVal = isRam ? Math.Max(100.0, items[0].RamMb) : Math.Max(10.0, items[0].CpuPct);
            int y = r.Y + 42;
            int rowH = Math.Max(22, (r.Height - 54) / 5);
            for (int i = 0; i < items.Count; i++)
            {
                ProcInfo p = items[i];
                double val = isRam ? p.RamMb : p.CpuPct;
                string label = p.Name;
                string valStr = isRam ? string.Format("{0:0.0} MB", val) : string.Format("{0:0.0}%", val);
                int barAreaX = r.X + 14;
                int barAreaW = Math.Max(80, r.Width - 28);

                TextRenderer.DrawText(g, label, FontBar, new Rectangle(barAreaX, y, barAreaW - 84, 17), fg, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                Color vc = (isRam && val > 500) || (!isRam && val > 35) ? hot : accent;
                TextRenderer.DrawText(g, valStr, FontBar, new Rectangle(barAreaX + barAreaW - 84, y, 84, 17), vc, TextFormatFlags.Right | TextFormatFlags.NoPrefix);

                int barY = y + 18;
                using (SolidBrush tr = new SolidBrush(DarkMode ? Color.FromArgb(40, 44, 54) : Color.FromArgb(228, 232, 240)))
                    g.FillRectangle(tr, barAreaX, barY, barAreaW, 5);
                int fw = Math.Max(3, (int)(barAreaW * Math.Min(1.0, val / maxVal)));
                using (SolidBrush fl = new SolidBrush(vc))
                    g.FillRectangle(fl, barAreaX, barY, fw, 5);

                y += rowH;
            }
        }
    }

    // ========================================================================
    // MAIN NILLYTASK PRO WINDOW
    // ========================================================================
    internal sealed class MainForm : Form
    {
        private static readonly HashSet<string> HardProtectedProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "System", "Idle", "Registry", "smss", "csrss", "wininit", "lsass", "services", "winlogon" };

        private static readonly HashSet<string> SoftWarnProcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "svchost", "dwm", "explorer", "sihost", "ctfmon", "fontdrvhost", "MsMpEng" };

        private readonly bool _smokeMode;
        private bool _dark = true;
        private bool _paused = false;
        private int _activeTab = 0; // 0=Processes, 1=Performance, 2=Startup, 3=Services & Rescue
        private Bitmap _logoBmp;

        private ulong _prevIdle, _prevKernel, _prevUser;
        private long _prevWallTicks;
        private readonly Dictionary<int, long> _prevProcTicks = new Dictionary<int, long>();
        private readonly Dictionary<int, string> _pidPathCache = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _pidNameCache = new Dictionary<int, string>();
        private bool _wasMinimized = false;
        private List<ProcInfo> _lastProcs = new List<ProcInfo>();
        private List<StartupEntry> _startupEntries = new List<StartupEntry>();
        private List<ServiceRow> _serviceRows = new List<ServiceRow>();
        private System.Windows.Forms.Timer _refreshTimer;

        private Panel _header, _hudBar, _tabBar, _actionBar, _footer, _bodyHost;
        private Panel _procPage, _startupPage, _svcPage, _rescueStrip;
        private PerfViewPanel _perfView;
        private DarkListView _lvProc, _lvStartup, _lvSvc;
        private TextBox _txtSearch;
        private AkButton _btnPause, _btnTop, _btnTheme, _btnRamRescue;
        private AkButton _btnKill, _btnKillTree, _btnTrimOne, _btnRestartExp, _btnOpenLoc;
        private AkButton _btnStartupToggle, _btnStartupRefresh, _btnSvcStart, _btnSvcStop, _btnSvcRefresh;
        private AkButton _btnResAnim, _btnResBg, _btnResWid, _btnResRestore;
        private readonly AkButton[] _tabBtns = new AkButton[4];
        private Label _lblTag, _lblHudCpu, _lblHudRam, _lblHudTop, _lblStatus, _lblCredit;
        private readonly ToolTip _tip = new ToolTip();

        public int LastProcessCount { get { return _lastProcs.Count; } }
        public int LastStartupCount { get { return _startupEntries.Count; } }
        public int LastServicesCount { get { return _serviceRows.Count; } }

        private readonly string _initialFilter;

        public MainForm(bool smokeMode) : this(smokeMode, null) { }

        public MainForm(bool smokeMode, string initialFilter)
        {
            _smokeMode = smokeMode;
            _initialFilter = initialFilter;
            _dark = ReadSuiteDark(true);
            Text = "NillyTask Pro — Nilly graphics";
            Size = new Size(1060, 680);
            MinimumSize = new Size(700, 460);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NillyTaskPro.ico");
            if (!File.Exists(icoPath))
            {
                DirectoryInfo par = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
                if (par != null) { string alt = Path.Combine(par.FullName, "NillyTaskPro.ico"); if (File.Exists(alt)) icoPath = alt; }
            }
            try
            {
                if (File.Exists(icoPath)) Icon = new Icon(icoPath);
                else Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }
            _logoBmp = IconHelper.LoadCrispIcon(icoPath, 28);

            PrimeCpuCounters();
            BuildUi();
            ApplyTheme();
            SwitchTab(0);

            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 1500;
            _refreshTimer.Tick += delegate { if (!_paused && WindowState != FormWindowState.Minimized) RefreshProcesses(); };

            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized) _wasMinimized = true;
                else if (_wasMinimized) { _wasMinimized = false; if (!_paused) RefreshProcesses(); }
            };

            Shown += delegate
            {
                if (!string.IsNullOrEmpty(_initialFilter)) _txtSearch.Text = _initialFilter;
                RefreshProcesses();
                LoadStartupItems();
                LoadServices();
                Native.TrimMemory();
                if (!_smokeMode) _refreshTimer.Start();
            };
        }

        private static bool ReadSuiteDark(bool fallback)
        {
            try
            {
                string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NillySuite\suite.theme");
                if (File.Exists(p))
                {
                    string s = File.ReadAllText(p, Encoding.ASCII).Trim();
                    if (s.Equals("light", StringComparison.OrdinalIgnoreCase)) return false;
                    if (s.Equals("dark", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch { }
            return fallback;
        }

        private void PrimeCpuCounters()
        {
            Native.FILETIME idl, krn, usr;
            if (Native.GetSystemTimes(out idl, out krn, out usr))
            {
                _prevIdle = idl.ToUInt64();
                _prevKernel = krn.ToUInt64();
                _prevUser = usr.ToUInt64();
            }
            _prevWallTicks = DateTime.UtcNow.Ticks;
        }

        private void BuildUi()
        {
            // 1. Responsive Ariakit Top Header Bar
            _header = new Panel { Dock = DockStyle.Top, Height = 50 };
            _header.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(_dark ? Color.FromArgb(54, 57, 61) : Color.FromArgb(222, 226, 232)))
                    pe.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };
            PictureBox pic = new PictureBox { Left = 14, Top = 11, Width = 28, Height = 28, Image = _logoBmp, SizeMode = PictureBoxSizeMode.Zoom };
            Label lblTitle = new Label { Left = 48, Top = 10, Width = 140, Height = 26, Font = new Font("Segoe UI", 11.2f, FontStyle.Bold), Text = "NillyTask Pro", UseMnemonic = false };
            _lblTag = new Label { Left = 188, Top = 15, Width = 220, Height = 18, Font = new Font("Segoe UI", 8.2f), Text = "Ultra-Light Native Task Manager", UseMnemonic = false, Tag = "muted" };

            _btnTheme = MkBtn("", 32, AkKind.Secondary, AkIcon.Sun, delegate { _dark = !_dark; ApplyTheme(); });
            _btnTop = MkBtn("", 32, AkKind.Secondary, AkIcon.Pin, delegate { TopMost = !TopMost; StyleTopButtons(); });
            _btnPause = MkBtn("", 32, AkKind.Secondary, AkIcon.Pause, delegate { TogglePause(); });
            _btnRamRescue = MkBtn("Ανάσα RAM", 112, AkKind.Primary, AkIcon.Bolt, delegate { RunRamRescue(); });
            _txtSearch = new TextBox { Width = 160, Height = 26, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f) };
            _txtSearch.TextChanged += delegate { FilterCurrentView(); };

            _tip.SetToolTip(_btnRamRescue, "Ελευθερώνει αδρανή RAM από ανοιχτές εφαρμογές (Win32 EmptyWorkingSet)");
            _tip.SetToolTip(_btnPause, "Παύση / Συνέχιση ανανέωσης (Space)");
            _tip.SetToolTip(_btnTop, "Πάντα στην κορυφή (Ctrl+T)");
            _tip.SetToolTip(_btnTheme, "Εναλλαγή Dark / Light");
            _tip.SetToolTip(_txtSearch, "Αναζήτηση διεργασίας / υπηρεσίας (Ctrl+F)");
            _header.Controls.AddRange(new Control[] { pic, lblTitle, _lblTag, _txtSearch, _btnRamRescue, _btnPause, _btnTop, _btnTheme });
            _header.Resize += delegate { LayoutResponsiveChrome(); _header.Invalidate(); };

            // 2. Live Telemetry HUD Bar (CPU · RAM · Top Consumer)
            _hudBar = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(14, 0, 14, 0) };
            _lblHudCpu = new Label { Dock = DockStyle.Left, Width = 195, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), UseMnemonic = false, Text = "CPU: ..." };
            _lblHudRam = new Label { Dock = DockStyle.Left, Width = 275, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.8f, FontStyle.Bold), UseMnemonic = false, Text = "RAM: ..." };
            _lblHudTop = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), UseMnemonic = false, AutoEllipsis = true, Text = "" };
            _hudBar.Controls.Add(_lblHudTop);
            _hudBar.Controls.Add(_lblHudRam);
            _hudBar.Controls.Add(_lblHudCpu);

            // 3. Clean Ariakit Bottom-Indicator 4-Segment Tab Bar
            _tabBar = new Panel { Dock = DockStyle.Top, Height = 38 };
            _tabBar.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(_dark ? Color.FromArgb(54, 57, 61) : Color.FromArgb(222, 226, 232)))
                    pe.Graphics.DrawLine(p, 0, _tabBar.Height - 1, _tabBar.Width, _tabBar.Height - 1);
            };
            string[] tabTitles = new string[] { "Διεργασίες", "Επιδόσεις (Live)", "Εκκίνηση (Startup)", "Υπηρεσίες & Ανάσα" };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                AkButton b = MkBtn(tabTitles[i], 148, AkKind.Tab, AkIcon.None, delegate { SwitchTab(idx); });
                b.Height = 28;
                _tabBtns[i] = b;
                _tabBar.Controls.Add(b);
            }
            _tabBar.Resize += delegate { LayoutResponsiveChrome(); _tabBar.Invalidate(); };

            // 4. Footer (zero overlap on narrow widths!)
            _footer = new Panel { Dock = DockStyle.Bottom, Height = 28, Padding = new Padding(14, 0, 14, 0) };
            _footer.Paint += delegate(object s, PaintEventArgs pe)
            {
                using (Pen p = new Pen(_dark ? Color.FromArgb(54, 57, 61) : Color.FromArgb(222, 226, 232)))
                    pe.Graphics.DrawLine(p, 0, 0, _footer.Width, 0);
            };
            _lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 8.2f), UseMnemonic = false, AutoEllipsis = true, Text = "Έτοιμο · 0 MB background RAM" };
            _lblCredit = new Label { Dock = DockStyle.Right, Width = 365, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 8.2f), Cursor = Cursors.Hand, UseMnemonic = false, Text = "created by Nilly graphics (Sotiris Christos) · nillygraphics.com" };
            _lblCredit.Click += delegate { try { Process.Start(new ProcessStartInfo("https://nillygraphics.com/") { UseShellExecute = true }); } catch { } };
            _footer.Controls.Add(_lblStatus);
            _footer.Controls.Add(_lblCredit);

            // 5. Body Host with 4 Pages
            _bodyHost = new Panel { Dock = DockStyle.Fill };

            // --- Page 0: Processes ---
            _procPage = new Panel { Dock = DockStyle.Fill };
            _actionBar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(14, 6, 14, 6) };
            FlowLayoutPanel actLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _btnKill = MkBtn("Τερματισμός", 118, AkKind.Danger, AkIcon.Close, delegate { KillSelectedProcess(false); });
            _btnKillTree = MkBtn("Δέντρο", 92, AkKind.Danger, AkIcon.Trash, delegate { KillSelectedProcess(true); });
            _btnTrimOne = MkBtn("Trim RAM", 96, AkKind.Secondary, AkIcon.Bolt, delegate { TrimSelectedProcess(); });
            _btnOpenLoc = MkBtn("Φάκελος", 92, AkKind.Secondary, AkIcon.Folder, delegate { OpenSelectedLocation(); });
            _btnRestartExp = MkBtn("Επανεκκίνηση Explorer", 174, AkKind.Secondary, AkIcon.Refresh, delegate { RestartExplorerSafe(); });
            actLeft.Controls.AddRange(new Control[] { _btnKill, _btnKillTree, _btnTrimOne, _btnOpenLoc, _btnRestartExp });
            _actionBar.Controls.Add(actLeft);

            _lvProc = new DarkListView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            _lvProc.Columns.Add("Διεργασία", 210);
            _lvProc.Columns.Add("PID", 66, HorizontalAlignment.Right);
            _lvProc.Columns.Add("CPU %", 74, HorizontalAlignment.Right);
            _lvProc.Columns.Add("RAM (MB)", 96, HorizontalAlignment.Right);
            _lvProc.Columns.Add("Νήματα", 70, HorizontalAlignment.Right);
            _lvProc.Columns.Add("Προτεραιότητα", 112);
            _lvProc.Columns.Add("Διαδρομή Αρχείου", 376);
            _lvProc.ColumnClick += delegate(object s, ColumnClickEventArgs ce)
            {
                if (_lvProc.SortColumn == ce.Column) _lvProc.SortDesc = !_lvProc.SortDesc;
                else { _lvProc.SortColumn = ce.Column; _lvProc.SortDesc = (ce.Column == 2 || ce.Column == 3 || ce.Column == 4); }
                RenderProcessList();
            };
            _lvProc.ContextMenuStrip = BuildProcContextMenu();
            _procPage.Controls.Add(_lvProc);
            _procPage.Controls.Add(_actionBar);

            // --- Page 1: Live Performance Charts ---
            _perfView = new PerfViewPanel();

            // --- Page 2: Startup Manager ---
            _startupPage = new Panel { Dock = DockStyle.Fill };
            Panel stBar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(14, 6, 14, 6), Tag = "bar" };
            FlowLayoutPanel stFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _btnStartupToggle = MkBtn("Ενεργοποίηση / Απενεργοποίηση", 224, AkKind.Secondary, AkIcon.Swap, delegate { ToggleSelectedStartup(); });
            _btnStartupRefresh = MkBtn("Ανανέωση", 102, AkKind.Secondary, AkIcon.Refresh, delegate { LoadStartupItems(); });
            stFlow.Controls.AddRange(new Control[] { _btnStartupToggle, _btnStartupRefresh });
            stBar.Controls.Add(stFlow);

            _lvStartup = new DarkListView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), SortColumn = 0, SortDesc = false };
            _lvStartup.Columns.Add("Εφαρμογή Εκκίνησης", 210);
            _lvStartup.Columns.Add("Κατάσταση", 112);
            _lvStartup.Columns.Add("Πηγή", 84);
            _lvStartup.Columns.Add("Εντολή / Διαδρομή", 600);
            _lvStartup.DoubleClick += delegate { ToggleSelectedStartup(); };
            _startupPage.Controls.Add(_lvStartup);
            _startupPage.Controls.Add(stBar);

            // --- Page 3: Services + Old-Laptop Rescue Strip ---
            _svcPage = new Panel { Dock = DockStyle.Fill };
            _rescueStrip = new Panel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(14, 6, 14, 6) };
            Label lRes = new Label { Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8.2f, FontStyle.Bold), UseMnemonic = false, Text = "Γρήγορη Ανάσα Παλιού Laptop (Zero Admin · HKCU · 100% Αναστρέψιμο):" };
            FlowLayoutPanel resFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 2, 0, 0) };
            _btnResAnim = MkBtn("Animations & Blur OFF", 160, AkKind.Secondary, AkIcon.None, delegate { ApplyLaptopTweak("anim"); });
            _btnResBg = MkBtn("Background Store Apps OFF", 180, AkKind.Secondary, AkIcon.None, delegate { ApplyLaptopTweak("bgapps"); });
            _btnResWid = MkBtn("Widgets & News OFF", 148, AkKind.Secondary, AkIcon.None, delegate { ApplyLaptopTweak("widgets"); });
            _btnResRestore = MkBtn("Επαναφορά Προεπιλογών", 178, AkKind.Secondary, AkIcon.Undo, delegate { ApplyLaptopTweak("restore"); });
            resFlow.Controls.AddRange(new Control[] { _btnResAnim, _btnResBg, _btnResWid, _btnResRestore });
            _rescueStrip.Controls.Add(resFlow);
            _rescueStrip.Controls.Add(lRes);

            Panel svcBar = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(14, 6, 14, 6), Tag = "bar" };
            FlowLayoutPanel svcFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            _btnSvcStart = MkBtn("Έναρξη", 92, AkKind.Secondary, AkIcon.Play, delegate { ControlSelectedService(true); });
            _btnSvcStop = MkBtn("Διακοπή", 96, AkKind.Danger, AkIcon.Stop, delegate { ControlSelectedService(false); });
            _btnSvcRefresh = MkBtn("Ανανέωση Υπηρεσιών", 164, AkKind.Secondary, AkIcon.Refresh, delegate { LoadServices(); });
            svcFlow.Controls.AddRange(new Control[] { _btnSvcStart, _btnSvcStop, _btnSvcRefresh });
            svcBar.Controls.Add(svcFlow);

            _lvSvc = new DarkListView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f), SortColumn = 2, SortDesc = true };
            _lvSvc.Columns.Add("Υπηρεσία", 210);
            _lvSvc.Columns.Add("Περιγραφή Windows", 490);
            _lvSvc.Columns.Add("Κατάσταση", 120);
            _svcPage.Controls.Add(_lvSvc);
            _svcPage.Controls.Add(_rescueStrip);
            _svcPage.Controls.Add(svcBar);

            _bodyHost.Controls.AddRange(new Control[] { _procPage, _perfView, _startupPage, _svcPage });

            // WinForms Docking Order (back-to-front)
            Controls.Add(_bodyHost);
            Controls.Add(_footer);
            Controls.Add(_tabBar);
            Controls.Add(_hudBar);
            Controls.Add(_header);

            SizeChanged += delegate { LayoutResponsiveChrome(); FitActiveColumns(); };
            LayoutResponsiveChrome();
        }

        private void LayoutResponsiveChrome()
        {
            if (_header == null || _tabBar == null) return;
            int w = Math.Max(660, _header.ClientSize.Width);
            int pad = 14, gap = 6;
            if (_lblTag != null) _lblTag.Visible = (w >= 900);
            if (_lblCredit != null) _lblCredit.Visible = (w >= 760);

            int rx = w - pad, y = 11;
            rx -= _btnTheme.Width; _btnTheme.Location = new Point(rx, y); rx -= gap;
            rx -= _btnTop.Width; _btnTop.Location = new Point(rx, y); rx -= gap;
            rx -= _btnPause.Width; _btnPause.Location = new Point(rx, y); rx -= gap;
            rx -= _btnRamRescue.Width; _btnRamRescue.Location = new Point(rx, y); rx -= gap;
            int brandRight = (_lblTag != null && _lblTag.Visible) ? (_lblTag.Right + 10) : 194;
            int sw = Math.Min(210, rx - brandRight);
            if (sw >= 120)
            {
                if (_header.Height != 50) _header.Height = 50;
                _txtSearch.SetBounds(rx - sw, y + 1, sw, 26);
            }
            else
            {
                if (_header.Height != 84) _header.Height = 84;
                _txtSearch.SetBounds(pad, 48, Math.Max(180, w - pad * 2), 26);
            }

            // Proportional Ariakit tabs
            int avail = w - pad * 2 - gap * 3;
            int tw = Math.Min(164, Math.Max(120, avail / 4));
            int tx = pad;
            for (int i = 0; i < 4; i++)
            {
                _tabBtns[i].SetBounds(tx, 5, tw, 28);
                tx += tw + gap;
            }
        }

        private static string FindSiblingExe(string folder, string exeName)
        {
            try
            {
                string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NillySuite\" + folder + @"\" + exeName);
                if (File.Exists(appData)) return appData;
                DirectoryInfo par = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
                if (par != null)
                {
                    string sib = Path.Combine(Path.Combine(par.FullName, folder), exeName);
                    if (File.Exists(sib)) return sib;
                }
            }
            catch { }
            return null;
        }

        private ContextMenuStrip BuildProcContextMenu()
        {
            ContextMenuStrip cm = new ContextMenuStrip();
            cm.Items.Add("✕ Τερματισμός Διεργασίας (Del)", null, delegate { KillSelectedProcess(false); });
            cm.Items.Add("✕✕ Τερματισμός Δέντρου (Kill Tree)", null, delegate { KillSelectedProcess(true); });
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("Ελευθέρωση RAM (Trim Working Set)", null, delegate { TrimSelectedProcess(); });
            ToolStripMenuItem pri = new ToolStripMenuItem("Προτεραιότητα CPU");
            pri.DropDownItems.Add("Χαμηλή (Idle)", null, delegate { SetSelectedPriority(ProcessPriorityClass.Idle); });
            pri.DropDownItems.Add("Κάτω από Κανονική", null, delegate { SetSelectedPriority(ProcessPriorityClass.BelowNormal); });
            pri.DropDownItems.Add("Κανονική (Normal)", null, delegate { SetSelectedPriority(ProcessPriorityClass.Normal); });
            pri.DropDownItems.Add("Πάνω από Κανονική", null, delegate { SetSelectedPriority(ProcessPriorityClass.AboveNormal); });
            pri.DropDownItems.Add("Υψηλή (High)", null, delegate { SetSelectedPriority(ProcessPriorityClass.High); });
            cm.Items.Add(pri);
            cm.Items.Add(new ToolStripSeparator());
            string filesExe = FindSiblingExe("NillyFilesPro", "NillyFilesPro.exe");
            if (filesExe != null)
            {
                cm.Items.Add("Άνοιγμα Φακέλου σε NillyFiles Pro", null, delegate
                {
                    ProcInfo p = GetSelectedProc();
                    if (p != null && !string.IsNullOrEmpty(p.Path) && File.Exists(p.Path))
                        try { Process.Start(new ProcessStartInfo(filesExe, "\"" + Path.GetDirectoryName(p.Path) + "\"") { WorkingDirectory = Path.GetDirectoryName(filesExe), UseShellExecute = true }); } catch { }
                });
            }
            string netExe = FindSiblingExe("NillyNetPro", "NillyNetPro.exe");
            if (netExe != null)
            {
                cm.Items.Add("Δες Συνδέσεις Δικτύου (NillyNet Pro)", null, delegate
                {
                    ProcInfo p = GetSelectedProc();
                    string arg = (p != null && !string.IsNullOrEmpty(p.Name)) ? ("--filter \"" + p.Name.Replace("\"", "") + "\"") : "";
                    try { Process.Start(new ProcessStartInfo(netExe, arg) { WorkingDirectory = Path.GetDirectoryName(netExe), UseShellExecute = true }); } catch { }
                });
            }
            cm.Items.Add("Άνοιγμα Φακέλου (Windows Explorer)", null, delegate { OpenSelectedLocation(); });
            cm.Items.Add("Αντιγραφή Διαδρομής", null, delegate
            {
                ProcInfo p = GetSelectedProc();
                if (p != null && !string.IsNullOrEmpty(p.Path)) { try { Clipboard.SetText(p.Path); _lblStatus.Text = "✓ Αντιγράφηκε: " + p.Path; } catch { } }
            });
            cm.Items.Add("Αναζήτηση στο Google (Τι είναι αυτό το .exe;)", null, delegate
            {
                ProcInfo p = GetSelectedProc();
                if (p != null) { try { Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(p.Name + ".exe process windows")) { UseShellExecute = true }); } catch { } }
            });
            return cm;
        }

        private static AkButton MkBtn(string txt, int w, AkKind kind, AkIcon icon, EventHandler onClick)
        {
            AkButton b = new AkButton
            {
                Kind = kind, Icon = icon,
                Text = txt, Width = w, Height = 28,
                Font = new Font("Segoe UI", 8.3f, FontStyle.Bold),
                Margin = new Padding(0, 1, 6, 0)
            };
            b.Click += onClick;
            return b;
        }

        public void SwitchTab(int idx)
        {
            _activeTab = idx;
            _procPage.Visible = (idx == 0);
            _perfView.Visible = (idx == 1);
            _startupPage.Visible = (idx == 2);
            _svcPage.Visible = (idx == 3);
            StyleTabs();
            FitActiveColumns();
            if (idx == 1) _perfView.Invalidate();
        }

        private void FitActiveColumns()
        {
            try
            {
                if (_activeTab == 0 && _lvProc.ClientSize.Width > 300)
                {
                    int used = 210 + 66 + 74 + 96 + 70 + 112 + 22;
                    _lvProc.Columns[6].Width = Math.Max(180, _lvProc.ClientSize.Width - used);
                }
                else if (_activeTab == 2 && _lvStartup.ClientSize.Width > 300)
                {
                    int used = 210 + 112 + 84 + 22;
                    _lvStartup.Columns[3].Width = Math.Max(220, _lvStartup.ClientSize.Width - used);
                }
                else if (_activeTab == 3 && _lvSvc.ClientSize.Width > 300)
                {
                    int used = 210 + 120 + 22;
                    _lvSvc.Columns[1].Width = Math.Max(240, _lvSvc.ClientSize.Width - used);
                }
            }
            catch { }
        }

        public void ApplyTheme()
        {
            Native.SetDarkTitlebar(Handle, _dark);
            Color bg = _dark ? Color.FromArgb(29, 31, 32) : Color.FromArgb(248, 249, 250);
            Color well = _dark ? Color.FromArgb(24, 26, 27) : Color.White;
            Color top = _dark ? Color.FromArgb(34, 36, 38) : Color.White;
            Color subBg = _dark ? Color.FromArgb(31, 33, 35) : Color.FromArgb(240, 242, 245);
            Color fg = _dark ? Color.FromArgb(242, 244, 247) : Color.FromArgb(26, 29, 33);
            Color muted = _dark ? Color.FromArgb(154, 160, 168) : Color.FromArgb(95, 104, 116);
            Color orange = Color.FromArgb(252, 150, 31);
            Color green = Color.FromArgb(166, 226, 46);
            Color cyan = Color.FromArgb(102, 217, 239);

            BackColor = bg; _bodyHost.BackColor = bg;
            _header.BackColor = top; _tabBar.BackColor = top; _footer.BackColor = top;
            _hudBar.BackColor = subBg; _actionBar.BackColor = top; _rescueStrip.BackColor = subBg;
            foreach (Control c in _startupPage.Controls) if (c.Tag as string == "bar") c.BackColor = top;
            foreach (Control c in _svcPage.Controls) if (c.Tag as string == "bar") c.BackColor = top;
            foreach (Control c in _header.Controls) if (c is Label) c.ForeColor = (c.Tag as string == "muted") ? muted : fg;
            foreach (Control c in _rescueStrip.Controls) if (c is Label) c.ForeColor = green;

            _txtSearch.BackColor = well;
            _txtSearch.ForeColor = fg;
            _lblHudCpu.ForeColor = green;
            _lblHudRam.ForeColor = cyan;
            _lblHudTop.ForeColor = orange;
            _lblStatus.ForeColor = muted;
            _lblCredit.ForeColor = orange;
            _btnTheme.Icon = _dark ? AkIcon.Sun : AkIcon.Moon;

            AkButton[] allBtns = new AkButton[]
            {
                _btnPause, _btnTop, _btnTheme, _btnRamRescue, _btnKill, _btnKillTree, _btnTrimOne, _btnOpenLoc, _btnRestartExp,
                _btnStartupToggle, _btnStartupRefresh, _btnSvcStart, _btnSvcStop, _btnSvcRefresh,
                _btnResAnim, _btnResBg, _btnResWid, _btnResRestore
            };
            for (int i = 0; i < allBtns.Length; i++) { allBtns[i].Dark = _dark; allBtns[i].Invalidate(); }

            StyleTopButtons();
            StyleTabs();
            _header.Invalidate(); _tabBar.Invalidate(); _footer.Invalidate();

            foreach (DarkListView lv in new DarkListView[] { _lvProc, _lvStartup, _lvSvc })
            { lv.DarkMode = _dark; lv.BackColor = well; lv.ForeColor = fg; lv.Invalidate(); }
            _perfView.DarkMode = _dark;
            _perfView.Invalidate();
        }

        private void StyleTopButtons()
        {
            _btnTop.Active = TopMost; _btnTop.Dark = _dark; _btnTop.Invalidate();
            _btnPause.Active = _paused; _btnPause.Icon = _paused ? AkIcon.Play : AkIcon.Pause; _btnPause.Dark = _dark; _btnPause.Invalidate();
        }

        private void StyleTabs()
        {
            for (int i = 0; i < 4; i++)
            {
                _tabBtns[i].Active = (i == _activeTab);
                _tabBtns[i].Dark = _dark;
                _tabBtns[i].Invalidate();
            }
        }

        // ====================================================================
        // LIVE PROCESS & SYSTEM SAMPLER (0% WMI, ~3ms NATIVE WIN32!)
        // ====================================================================
        public void RefreshProcesses()
        {
            double sysCpuPct = 0;
            Native.FILETIME idl, krn, usr;
            if (Native.GetSystemTimes(out idl, out krn, out usr))
            {
                ulong curIdle = idl.ToUInt64(), curKrn = krn.ToUInt64(), curUsr = usr.ToUInt64();
                ulong dIdle = curIdle - _prevIdle;
                ulong dTotal = (curKrn - _prevKernel) + (curUsr - _prevUser);
                if (dTotal > 0) sysCpuPct = Math.Max(0, Math.Min(100.0, (double)(dTotal - dIdle) * 100.0 / dTotal));
                _prevIdle = curIdle; _prevKernel = curKrn; _prevUser = curUsr;
            }

            long nowTicks = DateTime.UtcNow.Ticks;
            long wallDelta = Math.Max(1, nowTicks - _prevWallTicks);
            _prevWallTicks = nowTicks;
            int cores = Math.Max(1, Environment.ProcessorCount);

            Dictionary<int, long> nextProcTicks = new Dictionary<int, long>(256);
            Dictionary<int, string> nextPaths = new Dictionary<int, string>(256);
            Dictionary<int, string> nextNames = new Dictionary<int, string>(256);
            List<ProcInfo> list = new List<ProcInfo>(256);
            int totalThreads = 0;
            Process[] raw = Process.GetProcesses();

            for (int i = 0; i < raw.Length; i++)
            {
                Process p = raw[i];
                try
                {
                    if (p.Id == 0) continue;
                    ProcInfo pi = new ProcInfo();
                    pi.Pid = p.Id;
                    pi.Name = p.ProcessName;
                    pi.RamMb = p.WorkingSet64 / (1024.0 * 1024.0);
                    pi.Threads = p.Threads.Count;
                    totalThreads += pi.Threads;
                    pi.IsCritical = HardProtectedProcs.Contains(pi.Name);
                    pi.IsNilly = pi.Name.StartsWith("Nilly", StringComparison.OrdinalIgnoreCase);

                    string cachedName, cachedPath = "";
                    bool hasCachedPath = _pidNameCache.TryGetValue(pi.Pid, out cachedName)
                        && string.Equals(cachedName, pi.Name, StringComparison.OrdinalIgnoreCase)
                        && _pidPathCache.TryGetValue(pi.Pid, out cachedPath);

                    long pt; bool hasCpu; string prio, queriedPath;
                    Native.QueryProcessFast(pi.Pid, !hasCachedPath, out pt, out hasCpu, out prio, out queriedPath);
                    if (hasCpu)
                    {
                        nextProcTicks[pi.Pid] = pt;
                        long prevPt;
                        if (_prevProcTicks.TryGetValue(pi.Pid, out prevPt) && pt >= prevPt)
                            pi.CpuPct = Math.Min(100.0, ((pt - prevPt) * 100.0) / (wallDelta * cores));
                    }
                    pi.Priority = prio;
                    pi.Path = hasCachedPath ? cachedPath : queriedPath;
                    nextNames[pi.Pid] = pi.Name;
                    nextPaths[pi.Pid] = pi.Path;
                    list.Add(pi);
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }

            _prevProcTicks.Clear();
            foreach (KeyValuePair<int, long> kv in nextProcTicks) _prevProcTicks[kv.Key] = kv.Value;
            _pidNameCache.Clear();
            foreach (KeyValuePair<int, string> kv in nextNames) _pidNameCache[kv.Key] = kv.Value;
            _pidPathCache.Clear();
            foreach (KeyValuePair<int, string> kv in nextPaths) _pidPathCache[kv.Key] = kv.Value;
            _lastProcs = list;

            Native.MEMORYSTATUSEX mem = Native.GetMemory();
            double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
            double availGb = mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
            double usedGb = Math.Max(0, totalGb - availGb);
            double ramPct = mem.dwMemoryLoad;

            TimeSpan up = TimeSpan.FromMilliseconds(Native.GetTickCount64());
            string batStr = Native.GetBatterySummary();
            string upStr = string.Format("{0}η {1}ω {2}λ{3}", up.Days, up.Hours, up.Minutes, batStr);

            ProcInfo heaviest = null;
            for (int i = 0; i < list.Count; i++)
                if (heaviest == null || list[i].RamMb > heaviest.RamMb) heaviest = list[i];

            _lblHudCpu.Text = string.Format("CPU: {0:0.0}%  ·  {1} διεργασίες", sysCpuPct, list.Count);
            _lblHudRam.Text = string.Format("RAM: {0:0.0}%  ({1:0.0} / {2:0.0} GB){3}", ramPct, usedGb, totalGb, batStr);
            _lblHudTop.Text = heaviest != null
                ? string.Format("Πιο βαρύ τώρα: {0} ({1:0.0} MB)", heaviest.Name, heaviest.RamMb)
                : "";
            _tabBtns[0].Text = "Διεργασίες (" + list.Count + ")";

            _perfView.PushSample(sysCpuPct, ramPct, usedGb, totalGb, list.Count, totalThreads, upStr, list);
            if (_activeTab == 0) RenderProcessList();
        }

        private void RenderProcessList()
        {
            string q = (_txtSearch.Text ?? "").Trim();
            List<ProcInfo> view = new List<ProcInfo>(_lastProcs.Count);
            for (int i = 0; i < _lastProcs.Count; i++)
            {
                ProcInfo p = _lastProcs[i];
                if (q.Length > 0 &&
                    p.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    p.Pid.ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (p.Path == null || p.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                view.Add(p);
            }

            int col = _lvProc.SortColumn;
            int dir = _lvProc.SortDesc ? -1 : 1;
            view.Sort(delegate(ProcInfo a, ProcInfo b)
            {
                int cmp = 0;
                if (col == 0) cmp = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                else if (col == 1) cmp = a.Pid.CompareTo(b.Pid);
                else if (col == 2) cmp = a.CpuPct.CompareTo(b.CpuPct);
                else if (col == 3) cmp = a.RamMb.CompareTo(b.RamMb);
                else if (col == 4) cmp = a.Threads.CompareTo(b.Threads);
                else if (col == 5) cmp = string.Compare(a.Priority, b.Priority, StringComparison.OrdinalIgnoreCase);
                else cmp = string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
                return cmp * dir;
            });

            int selectedPid = -1;
            if (_lvProc.SelectedItems.Count > 0 && _lvProc.SelectedItems[0].Tag is ProcInfo)
                selectedPid = ((ProcInfo)_lvProc.SelectedItems[0].Tag).Pid;

            Color fg = _dark ? Color.FromArgb(232, 236, 244) : Color.FromArgb(24, 28, 36);
            Color muted = _dark ? Color.FromArgb(136, 144, 160) : Color.FromArgb(100, 110, 126);
            Color orange = Color.FromArgb(252, 150, 31);
            Color pink = Color.FromArgb(249, 38, 114);
            Color green = Color.FromArgb(166, 226, 46);

            _lvProc.BeginUpdate();
            _lvProc.Items.Clear();
            for (int i = 0; i < view.Count; i++)
            {
                ProcInfo p = view[i];
                ListViewItem it = new ListViewItem(p.Name);
                it.UseItemStyleForSubItems = false;
                it.ForeColor = p.IsNilly ? green : fg;
                it.SubItems.Add(p.Pid.ToString()).ForeColor = muted;
                it.SubItems.Add(string.Format("{0:0.0}%", p.CpuPct)).ForeColor = (p.CpuPct >= 35 ? pink : (p.CpuPct >= 10 ? orange : fg));
                it.SubItems.Add(string.Format("{0:0.0}", p.RamMb)).ForeColor = (p.RamMb >= 500 ? pink : (p.RamMb >= 150 ? orange : fg));
                it.SubItems.Add(p.Threads.ToString()).ForeColor = muted;
                it.SubItems.Add(p.Priority).ForeColor = muted;
                it.SubItems.Add(p.Path).ForeColor = muted;
                it.Tag = p;
                if (p.Pid == selectedPid) it.Selected = true;
                _lvProc.Items.Add(it);
            }
            _lvProc.EndUpdate();
        }

        private ProcInfo GetSelectedProc()
        {
            return (_lvProc.SelectedItems.Count > 0) ? (_lvProc.SelectedItems[0].Tag as ProcInfo) : null;
        }

        private void KillSelectedProcess(bool killTree)
        {
            ProcInfo p = GetSelectedProc();
            if (p == null) return;
            if (p.IsCritical)
            {
                MessageBox.Show(this, "Η διεργασία «" + p.Name + "» είναι κρίσιμη για τα Windows και προστατεύεται από τερματισμό.", "NillyTask Pro — Προστασία", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (SoftWarnProcs.Contains(p.Name))
            {
                if (MessageBox.Show(this, "Προσοχή: Το «" + p.Name + "» είναι βασική διεργασία Windows.\r\nΘέλεις σίγουρα να την τερματίσεις;", "Επιβεβαίωση — NillyTask Pro", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }
            try
            {
                if (killTree)
                {
                    ProcessStartInfo psi = new ProcessStartInfo("taskkill.exe", "/PID " + p.Pid + " /T /F");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    using (Process tk = Process.Start(psi)) { if (tk != null) tk.WaitForExit(4000); }
                }
                else
                {
                    using (Process target = Process.GetProcessById(p.Pid)) target.Kill();
                }
                _lblStatus.Text = "✓ Τερματίστηκε: " + p.Name + " (PID " + p.Pid + ")";
                RefreshProcesses();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Αδυναμία τερματισμού «" + p.Name + "»:\r\n" + ex.Message, "NillyTask Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TrimSelectedProcess()
        {
            ProcInfo p = GetSelectedProc();
            if (p == null) return;
            try
            {
                using (Process target = Process.GetProcessById(p.Pid))
                {
                    double before = target.WorkingSet64 / (1024.0 * 1024.0);
                    Native.EmptyWorkingSet(target.Handle);
                    target.Refresh();
                    double after = target.WorkingSet64 / (1024.0 * 1024.0);
                    _lblStatus.Text = string.Format("✓ Trim RAM ({0}): {1:0.0} MB → {2:0.0} MB", p.Name, before, after);
                }
                RefreshProcesses();
            }
            catch (Exception ex) { _lblStatus.Text = "Απαιτεί δικαιώματα για " + p.Name + ": " + ex.Message; }
        }

        private void SetSelectedPriority(ProcessPriorityClass pri)
        {
            ProcInfo p = GetSelectedProc();
            if (p == null) return;
            try
            {
                using (Process target = Process.GetProcessById(p.Pid)) target.PriorityClass = pri;
                _lblStatus.Text = "✓ Προτεραιότητα " + p.Name + " → " + pri;
                RefreshProcesses();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "NillyTask Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void OpenSelectedLocation()
        {
            ProcInfo p = GetSelectedProc();
            if (p == null || string.IsNullOrEmpty(p.Path) || !File.Exists(p.Path))
            { _lblStatus.Text = "Δεν υπάρχει διαθέσιμη διαδρομή για αυτή τη διεργασία."; return; }
            try { Process.Start("explorer.exe", "/select,\"" + p.Path + "\""); } catch { }
        }

        private void RestartExplorerSafe()
        {
            if (MessageBox.Show(this, "Καθαρή επανεκκίνηση του Windows Explorer (Taskbar / Επιφάνεια Εργασίας);\r\nΔεν χάνονται ανοιχτά προγράμματα.", "NillyTask Pro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            try
            {
                foreach (Process ex in Process.GetProcessesByName("explorer"))
                { try { ex.Kill(); ex.WaitForExit(2000); } catch { } finally { ex.Dispose(); } }
                Process.Start("explorer.exe");
                _lblStatus.Text = "✓ Ο Windows Explorer επανεκκίνησε καθαρά!";
            }
            catch { }
        }

        private void RunRamRescue()
        {
            long freedMb = Native.TrimAllUserProcesses();
            RefreshProcesses();
            _lblStatus.Text = freedMb > 0
                ? ("✓ Ανάσα RAM: Ελευθερώθηκαν ~" + freedMb + " MB αδρανούς μνήμης!")
                : "✓ Ολοκληρώθηκε ο καθαρισμός αδρανούς RAM (Working Set Trim).";
        }

        // ====================================================================
        // STARTUP APPS MANAGER (HKCU / HKLM / StartupApproved / Folder)
        // ====================================================================
        private void LoadStartupItems()
        {
            List<StartupEntry> list = new List<StartupEntry>();
            ReadRunKey(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run",
                       @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "HKCU", list);
            ReadRunKey(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run",
                       @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "HKLM", list);

            try
            {
                string stDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(stDir))
                {
                    foreach (string f in Directory.GetFiles(stDir))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        list.Add(new StartupEntry { Name = fn, Command = f, Scope = "Folder", FilePath = f, Enabled = true });
                    }
                }
            }
            catch { }

            _startupEntries = list;
            _tabBtns[2].Text = "Εκκίνηση (" + list.Count + ")";
            RenderStartupList();
        }

        private static void ReadRunKey(RegistryKey root, string runSub, string approvedSub, string scope, List<StartupEntry> target)
        {
            try
            {
                using (RegistryKey rk = root.OpenSubKey(runSub, false))
                {
                    if (rk == null) return;
                    RegistryKey ap = null;
                    try { ap = root.OpenSubKey(approvedSub, false); } catch { }
                    try
                    {
                        foreach (string name in rk.GetValueNames())
                        {
                            string cmd = Convert.ToString(rk.GetValue(name) ?? "");
                            bool enabled = true;
                            if (ap != null)
                            {
                                byte[] b = ap.GetValue(name) as byte[];
                                if (b != null && b.Length >= 1 && (b[0] == 3 || b[0] == 1)) enabled = false;
                            }
                            target.Add(new StartupEntry { Name = name, Command = cmd, Scope = scope, RegSubKey = runSub, Enabled = enabled });
                        }
                    }
                    finally { if (ap != null) ap.Dispose(); }
                }
            }
            catch { }
        }

        private void RenderStartupList()
        {
            string q = (_txtSearch.Text ?? "").Trim();
            Color fg = _dark ? Color.FromArgb(232, 236, 244) : Color.FromArgb(24, 28, 36);
            Color muted = _dark ? Color.FromArgb(136, 144, 160) : Color.FromArgb(100, 110, 126);
            Color green = Color.FromArgb(166, 226, 46);
            Color pink = Color.FromArgb(249, 38, 114);

            _lvStartup.BeginUpdate();
            _lvStartup.Items.Clear();
            for (int i = 0; i < _startupEntries.Count; i++)
            {
                StartupEntry se = _startupEntries[i];
                if (q.Length > 0 &&
                    se.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    se.Command.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                ListViewItem it = new ListViewItem(se.Name);
                it.UseItemStyleForSubItems = false;
                it.ForeColor = fg;
                it.SubItems.Add(se.Enabled ? "Ενεργό" : "Ανενεργό").ForeColor = se.Enabled ? green : pink;
                it.SubItems.Add(se.Scope).ForeColor = muted;
                it.SubItems.Add(se.Command).ForeColor = muted;
                it.Tag = se;
                _lvStartup.Items.Add(it);
            }
            _lvStartup.EndUpdate();
        }

        private void ToggleSelectedStartup()
        {
            if (_lvStartup.SelectedItems.Count == 0) return;
            StartupEntry se = _lvStartup.SelectedItems[0].Tag as StartupEntry;
            if (se == null) return;
            if (se.Scope != "HKCU")
            {
                MessageBox.Show(this, "Η εγγραφή «" + se.Name + "» ανήκει στο " + se.Scope + " (άλλαξέ την από το φάκελο εκκίνησης ή με δικαιώματα Administrator).", "NillyTask Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                using (RegistryKey ap = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"))
                {
                    if (ap != null)
                    {
                        bool next = !se.Enabled;
                        byte[] data = new byte[12];
                        data[0] = next ? (byte)2 : (byte)3; // Official Windows 10/11 StartupApproved flags
                        ap.SetValue(se.Name, data, RegistryValueKind.Binary);
                        se.Enabled = next;
                        _lblStatus.Text = "✓ " + se.Name + " → " + (next ? "Ενεργοποιήθηκε" : "Απενεργοποιήθηκε") + " στην εκκίνηση.";
                        RenderStartupList();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "NillyTask Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        // ====================================================================
        // WINDOWS SERVICES & SAFE OLD-LAPTOP HKCU RESCUE TOGGLES
        // ====================================================================
        private void LoadServices()
        {
            List<ServiceRow> list = new List<ServiceRow>();
            try
            {
                ServiceController[] all = ServiceController.GetServices();
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        ServiceController sc = all[i];
                        bool run = (sc.Status == ServiceControllerStatus.Running);
                        list.Add(new ServiceRow { Name = sc.ServiceName, DisplayName = sc.DisplayName, Status = run ? "Εκτελείται" : sc.Status.ToString(), Running = run });
                    }
                    catch { }
                    finally { try { all[i].Dispose(); } catch { } }
                }
            }
            catch { }
            list.Sort(delegate(ServiceRow a, ServiceRow b)
            {
                int c = b.Running.CompareTo(a.Running);
                return c != 0 ? c : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            _serviceRows = list;
            RenderServicesList();
        }

        private void RenderServicesList()
        {
            string q = (_txtSearch.Text ?? "").Trim();
            Color fg = _dark ? Color.FromArgb(232, 236, 244) : Color.FromArgb(24, 28, 36);
            Color muted = _dark ? Color.FromArgb(136, 144, 160) : Color.FromArgb(100, 110, 126);
            Color green = Color.FromArgb(166, 226, 46);

            _lvSvc.BeginUpdate();
            _lvSvc.Items.Clear();
            for (int i = 0; i < _serviceRows.Count; i++)
            {
                ServiceRow s = _serviceRows[i];
                if (q.Length > 0 &&
                    s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.DisplayName.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                ListViewItem it = new ListViewItem(s.Name);
                it.UseItemStyleForSubItems = false;
                it.ForeColor = fg;
                it.SubItems.Add(s.DisplayName).ForeColor = muted;
                it.SubItems.Add(s.Status).ForeColor = s.Running ? green : muted;
                it.Tag = s;
                _lvSvc.Items.Add(it);
            }
            _lvSvc.EndUpdate();
        }

        private void ControlSelectedService(bool start)
        {
            if (_lvSvc.SelectedItems.Count == 0) return;
            ServiceRow row = _lvSvc.SelectedItems[0].Tag as ServiceRow;
            if (row == null) return;
            try
            {
                using (ServiceController sc = new ServiceController(row.Name))
                {
                    if (start && sc.Status != ServiceControllerStatus.Running) { sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(6)); }
                    else if (!start && sc.Status == ServiceControllerStatus.Running) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(6)); }
                }
                _lblStatus.Text = "✓ Υπηρεσία «" + row.Name + "» → " + (start ? "Ξεκίνησε" : "Σταμάτησε");
                LoadServices();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Αδυναμία αλλαγής της υπηρεσίας «" + row.Name + "» (οι υπηρεσίες συστήματος απαιτούν Administrator):\r\n" + ex.Message,
                    "NillyTask Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ApplyLaptopTweak(string kind)
        {
            try
            {
                if (kind == "anim")
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                        if (k != null) k.SetValue("EnableTransparency", 0, RegistryValueKind.DWord);
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                        if (k != null) k.SetValue("TaskbarAnimations", 0, RegistryValueKind.DWord);
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                        if (k != null) k.SetValue("MinAnimate", "0", RegistryValueKind.String);
                    _lblStatus.Text = "✓ Animations & Blur απενεργοποιήθηκαν (άμεση επιτάχυνση σε παλιό iGPU)!";
                }
                else if (kind == "bgapps")
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"))
                        if (k != null) k.SetValue("GlobalUserDisabled", 1, RegistryValueKind.DWord);
                    _lblStatus.Text = "✓ Background Store Apps απενεργοποιήθηκαν (HKCU · 100% αναστρέψιμο)!";
                }
                else if (kind == "widgets")
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                        if (k != null) k.SetValue("TaskbarDa", 0, RegistryValueKind.DWord);
                    _lblStatus.Text = "✓ Widgets & News απενεργοποιήθηκαν από το Taskbar!";
                }
                else if (kind == "restore")
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                        if (k != null) k.SetValue("EnableTransparency", 1, RegistryValueKind.DWord);
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                        if (k != null) { k.SetValue("TaskbarAnimations", 1, RegistryValueKind.DWord); k.SetValue("TaskbarDa", 1, RegistryValueKind.DWord); }
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics"))
                        if (k != null) k.SetValue("MinAnimate", "1", RegistryValueKind.String);
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"))
                        if (k != null) k.SetValue("GlobalUserDisabled", 0, RegistryValueKind.DWord);
                    _lblStatus.Text = "✓ Επαναφέρθηκαν όλες οι προεπιλογές Windows (Animations, Background Apps, Widgets).";
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "NillyTask Pro", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void FilterCurrentView()
        {
            if (_activeTab == 0) RenderProcessList();
            else if (_activeTab == 2) RenderStartupList();
            else if (_activeTab == 3) RenderServicesList();
        }

        private void TogglePause()
        {
            _paused = !_paused;
            StyleTopButtons();
            _lblStatus.Text = _paused ? "Παύση ανανέωσης (πάτα Space για συνέχεια)" : "Ζωντανή παρακολούθηση (1.5s)";
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { _txtSearch.Focus(); _txtSearch.SelectAll(); return true; }
            if (keyData == (Keys.Control | Keys.T)) { TopMost = !TopMost; StyleTopButtons(); return true; }
            if (keyData == Keys.F5) { RefreshProcesses(); return true; }
            if (keyData == Keys.Delete && _activeTab == 0 && !_txtSearch.Focused) { KillSelectedProcess(false); return true; }
            if (keyData == Keys.Space && !_txtSearch.Focused) { TogglePause(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_refreshTimer != null) _refreshTimer.Dispose();
                if (_logoBmp != null) { _logoBmp.Dispose(); _logoBmp = null; }
                _tip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0 && args[0].Equals("--test-smoke", StringComparison.OrdinalIgnoreCase))
                return RunSmokeTest(args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null);

            string initFilter = null;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if ((args[i].Equals("--filter", StringComparison.OrdinalIgnoreCase) || args[i].Equals("--pid", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                    { initFilter = args[i + 1]; break; }
                }
            }

            bool createdNew;
            using (Mutex m = new Mutex(true, "Local\\NillyTaskPro_SingleInstance_v1", out createdNew))
            {
                Application.Run(SuiteUpdater.Attach(new MainForm(false, initFilter), "nillytask-pro", "NillyTask-Pro", "Install_NillyTaskPro.ps1"));
            }
            return 0;
        }

        private static int RunSmokeTest(string logPath, string previewPngPath)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                using (MainForm f = new MainForm(true))
                {
                    f.Show();
                    Application.DoEvents();
                    Thread.Sleep(200);
                    f.RefreshProcesses();
                    Application.DoEvents();

                    int procs = f.LastProcessCount;
                    int startups = f.LastStartupCount;
                    int svcs = f.LastServicesCount;
                    Native.MEMORYSTATUSEX mem = Native.GetMemory();
                    double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);

                    if (!string.IsNullOrEmpty(previewPngPath))
                    {
                        using (Bitmap shot = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(shot, new Rectangle(0, 0, f.Width, f.Height));
                            shot.Save(previewPngPath, ImageFormat.Png);
                        }
                    }

                    // Also exercise Performance tab paint cleanly
                    f.SwitchTab(1);
                    Application.DoEvents();
                    if (!string.IsNullOrEmpty(previewPngPath))
                    {
                        string perfPng = Path.Combine(Path.GetDirectoryName(previewPngPath), Path.GetFileNameWithoutExtension(previewPngPath) + "_perf.png");
                        using (Bitmap shot2 = new Bitmap(f.Width, f.Height))
                        {
                            f.DrawToBitmap(shot2, new Rectangle(0, 0, f.Width, f.Height));
                            shot2.Save(perfPng, ImageFormat.Png);
                        }
                    }

                    long ramMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
                    bool pass = (procs >= 15 && totalGb > 0.5 && svcs >= 15 && f.IsHandleCreated);
                    sb.AppendLine("PROCESS_COUNT=" + procs);
                    sb.AppendLine("STARTUP_COUNT=" + startups);
                    sb.AppendLine("SERVICES_COUNT=" + svcs);
                    sb.AppendLine("RAM_TOTAL_GB=" + totalGb.ToString("0.0"));
                    sb.AppendLine("WINDOW_OPENED_OK=" + f.IsHandleCreated);
                    sb.AppendLine("WORKING_SET_MB=" + ramMb);
                    sb.AppendLine("SMOKE_RESULT=" + (pass ? "PASS" : "FAIL"));
                    f.Close();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("SMOKE_RESULT=FAIL");
                sb.AppendLine(ex.ToString());
                if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
                return 1;
            }
            if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, sb.ToString(), Encoding.UTF8);
            return 0;
        }
    }

    // ========================================================================
    // BUILT-IN ULTRA-LIGHT AUTO-UPDATER (1x/24h shared CDN check · SHA-256)
    // ========================================================================
    internal static class SuiteUpdater
    {
        private const string CATALOG_URL = "https://raw.githubusercontent.com/nillynp/nillyhub/main/catalog.json";

        public static T Attach<T>(T form, string repoName, string pkgPrefix, string installerScript) where T : System.Windows.Forms.Form
        {
            if (form == null) return null;
            form.Shown += delegate
            {
                System.Threading.Thread th = new System.Threading.Thread(delegate()
                {
                    try
                    {
                        System.Threading.Thread.Sleep(2200);
                        if (form.IsDisposed) return;
                        string curTag, newTag;
                        if (!CheckForUpdate(repoName, out curTag, out newTag)) return;
                        if (form.IsDisposed || !form.IsHandleCreated) return;
                        form.BeginInvoke((System.Action)delegate
                        {
                            if (form.IsDisposed || form.FormBorderStyle == System.Windows.Forms.FormBorderStyle.None) return;
                            ShowUpdateBanner(form, repoName, pkgPrefix, installerScript, curTag, newTag);
                        });
                    }
                    catch { }
                });
                th.IsBackground = true;
                th.Start();
            };
            return form;
        }

        private static void ShowUpdateBanner(System.Windows.Forms.Form form, string repoName, string pkgPrefix, string installerScript, string curTag, string newTag)
        {
            System.Drawing.Font fTxt = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Regular);
            System.Drawing.Font fBtn = new System.Drawing.Font("Segoe UI", 8.2f, System.Drawing.FontStyle.Bold);

            System.Windows.Forms.Panel bar = new System.Windows.Forms.Panel();
            bar.Dock = System.Windows.Forms.DockStyle.Top;
            bar.Height = 28;
            bar.BackColor = System.Drawing.Color.FromArgb(40, 29, 18);
            bar.Padding = new System.Windows.Forms.Padding(10, 0, 8, 1);
            bar.Paint += delegate(object s, System.Windows.Forms.PaintEventArgs e)
            {
                using (System.Drawing.Pen p = new System.Drawing.Pen(System.Drawing.Color.FromArgb(252, 150, 31)))
                    e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            System.Windows.Forms.Button btnClose = new System.Windows.Forms.Button();
            btnClose.Text = "×";
            btnClose.Dock = System.Windows.Forms.DockStyle.Right;
            btnClose.Width = 26;
            btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.BackColor = System.Drawing.Color.Transparent;
            btnClose.ForeColor = System.Drawing.Color.FromArgb(200, 170, 130);
            btnClose.Font = fBtn;
            btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            btnClose.TabStop = false;
            btnClose.Click += delegate { bar.Dispose(); };

            System.Windows.Forms.Button btnUpd = new System.Windows.Forms.Button();
            btnUpd.Text = "Ενημέρωση";
            btnUpd.Dock = System.Windows.Forms.DockStyle.Right;
            btnUpd.Width = 88;
            btnUpd.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnUpd.FlatAppearance.BorderSize = 0;
            btnUpd.BackColor = System.Drawing.Color.FromArgb(252, 150, 31);
            btnUpd.ForeColor = System.Drawing.Color.FromArgb(20, 22, 26);
            btnUpd.Font = fBtn;
            btnUpd.Cursor = System.Windows.Forms.Cursors.Hand;
            btnUpd.TabStop = false;

            System.Windows.Forms.Label lbl = new System.Windows.Forms.Label();
            lbl.Dock = System.Windows.Forms.DockStyle.Fill;
            lbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lbl.Font = fTxt;
            lbl.ForeColor = System.Drawing.Color.FromArgb(255, 185, 95);
            lbl.UseMnemonic = false;
            lbl.Text = "Νέα έκδοση " + newTag + " διαθέσιμη (έχεις " + curTag + ")";

            btnUpd.Click += delegate
            {
                btnUpd.Enabled = false;
                btnUpd.Text = "Λήψη...";
                System.Threading.Thread uth = new System.Threading.Thread(delegate()
                {
                    try
                    {
                        ApplySelfUpdate(repoName, pkgPrefix, installerScript,
                            delegate(string st)
                            {
                                try { if (!form.IsDisposed) form.BeginInvoke((System.Action)delegate { lbl.Text = st; }); } catch { }
                            },
                            delegate
                            {
                                try { if (!form.IsDisposed) form.BeginInvoke((System.Action)delegate { System.Windows.Forms.Application.Exit(); }); } catch { }
                            });
                    }
                    catch (System.Exception ex)
                    {
                        try
                        {
                            if (!form.IsDisposed) form.BeginInvoke((System.Action)delegate
                            {
                                lbl.Text = "Σφάλμα ενημέρωσης: " + ex.Message;
                                btnUpd.Text = "Ενημέρωση";
                                btnUpd.Enabled = true;
                            });
                        }
                        catch { }
                    }
                });
                uth.IsBackground = true;
                uth.Start();
            };

            bar.Disposed += delegate { fTxt.Dispose(); fBtn.Dispose(); };
            bar.Controls.Add(lbl);
            bar.Controls.Add(btnUpd);
            bar.Controls.Add(btnClose);
            form.Controls.Add(bar);
        }

        private static bool TryParseVer(string raw, out System.Version ver)
        {
            ver = null;
            if (string.IsNullOrEmpty(raw)) return false;
            string s = raw.Trim();
            if (s.StartsWith("v", System.StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            string[] p = s.Split('.');
            if (p.Length < 2 || p.Length > 4) return false;
            int maj, min, pat = 0;
            if (!int.TryParse(p[0], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out maj)) return false;
            if (!int.TryParse(p[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out min)) return false;
            if (p.Length >= 3 && !int.TryParse(p[2], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out pat)) return false;
            ver = new System.Version(maj, min, pat, 0);
            return true;
        }

        private static string FmtVer(System.Version v)
        {
            return v.Build > 0
                ? string.Format(System.Globalization.CultureInfo.InvariantCulture, "v{0}.{1}.{2}", v.Major, v.Minor, v.Build)
                : string.Format(System.Globalization.CultureInfo.InvariantCulture, "v{0}.{1}", v.Major, v.Minor);
        }

        public static bool CheckForUpdate(string repoName, out string curTag, out string newTag)
        {
            curTag = null; newTag = null;
            System.Version rawCur = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            System.Version curVer = new System.Version(rawCur.Major, rawCur.Minor, rawCur.Build >= 0 ? rawCur.Build : 0, 0);
            curTag = FmtVer(curVer);

            string suiteDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "NillySuite");
            string cachePath = System.IO.Path.Combine(suiteDir, ".nillyhub_catalog.json");
            string json = null;
            try
            {
                if (System.IO.File.Exists(cachePath))
                {
                    System.DateTime wt = System.IO.File.GetLastWriteTimeUtc(cachePath);
                    if ((System.DateTime.UtcNow - wt).TotalHours < 24.0)
                    {
                        string cached = System.IO.File.ReadAllText(cachePath, System.Text.Encoding.UTF8);
                        if (cached.IndexOf("\"latestVersion\"", System.StringComparison.Ordinal) >= 0) json = cached;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(json))
            {
                json = HttpGetText(CATALOG_URL, 5000);
                if (string.IsNullOrEmpty(json) || json.Length > 256 * 1024) return false;
                try
                {
                    System.IO.Directory.CreateDirectory(suiteDir);
                    System.IO.File.WriteAllText(cachePath, json, new System.Text.UTF8Encoding(false));
                }
                catch { }
            }

            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                json,
                "\"repo\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(repoName) + "\"[^\\}]{0,500}?\"latestVersion\"\\s*:\\s*\"(v[0-9]+\\.[0-9]+(?:\\.[0-9]+)?)\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return false;
            System.Version remVer;
            if (!TryParseVer(m.Groups[1].Value, out remVer) || remVer <= curVer) return false;
            newTag = FmtVer(remVer);
            return true;
        }

        private static string HttpGetText(string url, int timeoutMs)
        {
            try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072 | System.Net.SecurityProtocolType.Tls12; } catch { }
            System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
            req.UserAgent = "NillySuite-Updater/1.0 (https://nillygraphics.com/)";
            req.Accept = "application/vnd.github+json, application/json";
            req.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            using (System.Net.HttpWebResponse resp = (System.Net.HttpWebResponse)req.GetResponse())
            using (System.IO.Stream st = resp.GetResponseStream())
            using (System.IO.StreamReader sr = new System.IO.StreamReader(st, System.Text.Encoding.UTF8))
                return sr.ReadToEnd();
        }

        private static string ExtractAssetBlock(string json, string assetName)
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                json, "\"name\"\\s*:\\s*\"" + System.Text.RegularExpressions.Regex.Escape(assetName) + "\"");
            if (!m.Success) return null;
            int start = m.Index;
            int depth = 0;
            while (start > 0)
            {
                char c = json[start];
                if (c == '}') depth++;
                else if (c == '{') { if (depth == 0) break; depth--; }
                start--;
            }
            int end = start;
            depth = 0;
            bool inStr = false;
            for (int i = start; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\')) inStr = !inStr;
                if (inStr) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            return end > start ? json.Substring(start, end - start + 1) : null;
        }

        public static void ApplySelfUpdate(string repoName, string pkgPrefix, string installerScript, System.Action<string> onStatus, System.Action onExit)
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string appDir = System.IO.Path.GetDirectoryName(exePath);
            if (System.IO.Directory.Exists(System.IO.Path.Combine(appDir, ".git")))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/nillynp/" + repoName + "/releases/latest") { UseShellExecute = true });
                if (onStatus != null) onStatus("Άνοιξε το GitHub Release (φάκελος Git).");
                return;
            }

            if (onStatus != null) onStatus("Έλεγχος SHA-256 στο GitHub...");
            string relJson = HttpGetText("https://api.github.com/repos/nillynp/" + repoName + "/releases/latest", 12000);
            if (string.IsNullOrEmpty(relJson)) throw new System.IO.InvalidDataException("Κενή απόκριση GitHub.");
            if (System.Text.RegularExpressions.Regex.IsMatch(relJson, "\"draft\"\\s*:\\s*true") ||
                System.Text.RegularExpressions.Regex.IsMatch(relJson, "\"prerelease\"\\s*:\\s*true"))
                throw new System.IO.InvalidDataException("Μη σταθερή έκδοση.");

            System.Text.RegularExpressions.Match tm = System.Text.RegularExpressions.Regex.Match(relJson, "\"tag_name\"\\s*:\\s*\"(v[0-9]+\\.[0-9]+(?:\\.[0-9]+)?)\"");
            if (!tm.Success) throw new System.IO.InvalidDataException("Μη έγκυρο tag έκδοσης.");
            string tag = tm.Groups[1].Value;

            System.Version curRaw = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            System.Version curVer = new System.Version(curRaw.Major, curRaw.Minor, curRaw.Build >= 0 ? curRaw.Build : 0, 0);
            System.Version remVer;
            if (!TryParseVer(tag, out remVer) || remVer <= curVer)
                throw new System.InvalidOperationException("Έχεις ήδη την τελευταία έκδοση.");

            string zipName = pkgPrefix + "-" + tag + "-Portable.zip";
            string expectedUrl = "https://github.com/nillynp/" + repoName + "/releases/download/" + tag + "/" + zipName;
            string blk = ExtractAssetBlock(relJson, zipName);
            if (string.IsNullOrEmpty(blk)) throw new System.IO.FileNotFoundException("Δεν βρέθηκε το Portable ZIP.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(blk, "\"state\"\\s*:\\s*\"uploaded\""))
                throw new System.IO.InvalidDataException("Το αρχείο δεν είναι έτοιμο.");
            System.Text.RegularExpressions.Match um = System.Text.RegularExpressions.Regex.Match(blk, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"");
            if (!um.Success || !string.Equals(um.Groups[1].Value, expectedUrl, System.StringComparison.Ordinal))
                throw new System.IO.InvalidDataException("Μη έγκυρο URL λήψης.");
            System.Text.RegularExpressions.Match dm = System.Text.RegularExpressions.Regex.Match(blk, "\"digest\"\\s*:\\s*\"sha256:([0-9a-f]{64})\"");
            if (!dm.Success) throw new System.IO.InvalidDataException("Απουσιάζει SHA-256.");
            string expectedSha = dm.Groups[1].Value;

            string tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NillyUpd-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tmpDir);
            string zipFile = System.IO.Path.Combine(tmpDir, "pkg.zip");

            if (onStatus != null) onStatus("Λήψη " + tag + "...");
            try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072 | System.Net.SecurityProtocolType.Tls12; } catch { }
            System.Net.HttpWebRequest dreq = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(expectedUrl);
            dreq.UserAgent = "NillySuite-Updater/1.0 (https://nillygraphics.com/)";
            dreq.Timeout = 20000;
            dreq.ReadWriteTimeout = 30000;
            using (System.Net.HttpWebResponse dresp = (System.Net.HttpWebResponse)dreq.GetResponse())
            using (System.IO.Stream src = dresp.GetResponseStream())
            using (System.IO.FileStream dst = new System.IO.FileStream(zipFile, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
            {
                byte[] buf = new byte[16384];
                int n; long total = 0;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    total += n;
                    if (total > 32L * 1024L * 1024L) throw new System.IO.InvalidDataException("Υπέρβαση μεγέθους ZIP.");
                    dst.Write(buf, 0, n);
                }
            }

            if (onStatus != null) onStatus("Επαλήθευση SHA-256...");
            string actualSha;
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
            using (System.IO.FileStream fs = System.IO.File.OpenRead(zipFile))
            {
                byte[] hash = sha.ComputeHash(fs);
                System.Text.StringBuilder sb = new System.Text.StringBuilder(64);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                actualSha = sb.ToString();
            }
            if (!string.Equals(actualSha, expectedSha, System.StringComparison.OrdinalIgnoreCase))
            {
                try { System.IO.Directory.Delete(tmpDir, true); } catch { }
                throw new System.IO.InvalidDataException("Αποτυχία ελέγχου SHA-256.");
            }

            if (onStatus != null) onStatus("Εγκατάσταση " + tag + " & επανεκκίνηση...");
            string ps1 = System.IO.Path.Combine(tmpDir, "apply.ps1");
            string script =
                "param($myPid,$zip,$appDir,$instName,$exePath,$tag)\r\n" +
                "for($i=0;$i -lt 60;$i++){ if(-not (Get-Process -Id $myPid -ErrorAction SilentlyContinue)){break}; Start-Sleep -Milliseconds 150 }\r\n" +
                "Add-Type -AssemblyName System.IO.Compression.FileSystem\r\n" +
                "$za = [System.IO.Compression.ZipFile]::OpenRead($zip)\r\n" +
                "try {\r\n" +
                "  $pref = $null; $shared = $true\r\n" +
                "  foreach($e in $za.Entries){ $fn=$e.FullName -replace '\\\\','/'; $sl=$fn.IndexOf('/'); if($sl -le 0){$shared=$false;break}; $p=$fn.Substring(0,$sl+1); if(-not $pref){$pref=$p}elseif(-not $pref.Equals($p,[StringComparison]::OrdinalIgnoreCase)){$shared=$false;break} }\r\n" +
                "  $rootFull = [System.IO.Path]::GetFullPath($appDir).TrimEnd('\\') + '\\'\r\n" +
                "  foreach($e in $za.Entries){\r\n" +
                "    $rel = $e.FullName -replace '\\\\','/'\r\n" +
                "    if($shared -and $pref -and $rel.StartsWith($pref,[StringComparison]::OrdinalIgnoreCase)){ $rel=$rel.Substring($pref.Length) }\r\n" +
                "    if([string]::IsNullOrEmpty($rel) -or $rel.EndsWith('/') -or $rel.Contains(':')){ continue }\r\n" +
                "    $dst = [System.IO.Path]::GetFullPath((Join-Path $appDir ($rel -replace '/','\\')))\r\n" +
                "    if(-not $dst.StartsWith($rootFull,[StringComparison]::OrdinalIgnoreCase)){ continue }\r\n" +
                "    $par = Split-Path -Parent $dst; if($par){ New-Item -ItemType Directory -Force -Path $par | Out-Null }\r\n" +
                "    for($k=0;$k -lt 5;$k++){ try { [System.IO.Compression.ZipFileExtensions]::ExtractToFile($e,$dst,$true); break } catch { Start-Sleep -Milliseconds 250 } }\r\n" +
                "  }\r\n" +
                "} finally { $za.Dispose() }\r\n" +
                "$bak = $exePath + '.bak'\r\n" +
                "if(Test-Path $exePath){ Copy-Item -LiteralPath $exePath -Destination $bak -Force -ErrorAction SilentlyContinue; for($k=0;$k -lt 5;$k++){ try { Remove-Item -LiteralPath $exePath -Force -ErrorAction Stop; break } catch { Start-Sleep -Milliseconds 250 } } }\r\n" +
                "Get-ChildItem (Join-Path $appDir 'desktop') -Filter '*.cs' -ErrorAction SilentlyContinue | ForEach-Object { $_.LastWriteTimeUtc = [DateTime]::UtcNow }\r\n" +
                "if($instName -and (Test-Path (Join-Path $appDir $instName))){ & (Join-Path $appDir $instName) -Silent 2>$null | Out-Null }\r\n" +
                "if(-not (Test-Path $exePath) -and (Test-Path $bak)){ Move-Item -LiteralPath $bak -Destination $exePath -Force -ErrorAction SilentlyContinue } else { Remove-Item -LiteralPath $bak -Force -ErrorAction SilentlyContinue }\r\n" +
                "if($tag){ Set-Content -LiteralPath (Join-Path $appDir '.nilly_version') -Value $tag -Encoding UTF8 -ErrorAction SilentlyContinue }\r\n" +
                "if(Test-Path $exePath){ Start-Process -FilePath $exePath -WorkingDirectory $appDir }\r\n" +
                "Start-Sleep -Milliseconds 400\r\n" +
                "Remove-Item -LiteralPath (Split-Path $zip) -Recurse -Force -ErrorAction SilentlyContinue\r\n";
            System.IO.File.WriteAllText(ps1, script, new System.Text.UTF8Encoding(false));

            int myPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(
                "powershell.exe",
                "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + ps1 + "\" " +
                myPid + " \"" + zipFile + "\" \"" + appDir + "\" \"" + installerScript + "\" \"" + exePath + "\" \"" + tag + "\"");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            System.Diagnostics.Process.Start(psi);
            if (onExit != null) onExit();
        }
    }

    // ========================================================================
    // ARIAKIT-INSPIRED MINIMAL VECTOR UI KIT (Zero DLLs · 1.5px Stroke Icons)
    // ========================================================================
    internal enum AkKind { Primary, Secondary, Ghost, Tab, AccentOutline, Danger }

    internal enum AkIcon
    {
        None, Refresh, Sun, Moon, Search, Pin, Info, Play, Pause, Stop,
        Plus, Minus, Close, Up, Down, Folder, Copy, Trash, Check, Swap,
        Download, Link, Split, Pipette, Bolt, List, Filter, Shield, Lock, Unlock,
        Edit, Save, External, Bold, Italic, Undo, ChevLeft, ChevRight, Star
    }

    internal static class AkDraw
    {
        public static System.Drawing.Drawing2D.GraphicsPath RoundRect(float x, float y, float w, float h, float r)
        {
            System.Drawing.Drawing2D.GraphicsPath p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = System.Math.Min(r * 2f, System.Math.Min(w, h));
            if (d <= 0.5f) { p.AddRectangle(new System.Drawing.RectangleF(x, y, w, h)); return p; }
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void DrawIcon(System.Drawing.Graphics g, AkIcon icon, float cx, float cy, float sz, System.Drawing.Color color)
        {
            if (icon == AkIcon.None) return;
            System.Drawing.Drawing2D.SmoothingMode oldSm = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float r = sz * 0.5f;
            using (System.Drawing.Pen pen = new System.Drawing.Pen(color, 1.5f))
            using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(color))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                switch (icon)
                {
                    case AkIcon.Refresh:
                        g.DrawArc(pen, cx - r * 0.82f, cy - r * 0.82f, r * 1.64f, r * 1.64f, -55, 295);
                        g.DrawLine(pen, cx + r * 0.28f, cy - r * 0.85f, cx + r * 0.62f, cy - r * 0.52f);
                        g.DrawLine(pen, cx + r * 0.62f, cy - r * 0.52f, cx + r * 0.20f, cy - r * 0.42f);
                        break;
                    case AkIcon.Sun:
                        g.DrawEllipse(pen, cx - r * 0.42f, cy - r * 0.42f, r * 0.84f, r * 0.84f);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * System.Math.PI / 4.0;
                            float x1 = cx + (float)(System.Math.Cos(a) * r * 0.68f);
                            float y1 = cy + (float)(System.Math.Sin(a) * r * 0.68f);
                            float x2 = cx + (float)(System.Math.Cos(a) * r * 0.94f);
                            float y2 = cy + (float)(System.Math.Sin(a) * r * 0.94f);
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                        break;
                    case AkIcon.Moon:
                        g.DrawArc(pen, cx - r * 0.78f, cy - r * 0.78f, r * 1.56f, r * 1.56f, 40, 235);
                        g.DrawArc(pen, cx - r * 0.30f, cy - r * 0.92f, r * 1.32f, r * 1.32f, 115, 130);
                        break;
                    case AkIcon.Search:
                        g.DrawEllipse(pen, cx - r * 0.82f, cy - r * 0.82f, r * 1.24f, r * 1.24f);
                        g.DrawLine(pen, cx + r * 0.25f, cy + r * 0.25f, cx + r * 0.82f, cy + r * 0.82f);
                        break;
                    case AkIcon.Pin:
                        g.DrawLine(pen, cx - r * 0.48f, cy - r * 0.72f, cx + r * 0.48f, cy - r * 0.72f);
                        g.DrawLine(pen, cx - r * 0.34f, cy - r * 0.72f, cx - r * 0.56f, cy + r * 0.14f);
                        g.DrawLine(pen, cx + r * 0.34f, cy - r * 0.72f, cx + r * 0.56f, cy + r * 0.14f);
                        g.DrawLine(pen, cx - r * 0.66f, cy + r * 0.14f, cx + r * 0.66f, cy + r * 0.14f);
                        g.DrawLine(pen, cx, cy + r * 0.14f, cx, cy + r * 0.86f);
                        break;
                    case AkIcon.Info:
                        g.DrawEllipse(pen, cx - r * 0.85f, cy - r * 0.85f, r * 1.70f, r * 1.70f);
                        g.DrawLine(pen, cx, cy - r * 0.08f, cx, cy + r * 0.48f);
                        g.FillEllipse(br, cx - 1.1f, cy - r * 0.52f, 2.2f, 2.2f);
                        break;
                    case AkIcon.Play:
                        g.FillPolygon(br, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx - r * 0.45f, cy - r * 0.68f),
                            new System.Drawing.PointF(cx + r * 0.68f, cy),
                            new System.Drawing.PointF(cx - r * 0.45f, cy + r * 0.68f)
                        });
                        break;
                    case AkIcon.Pause:
                        g.DrawLine(pen, cx - r * 0.36f, cy - r * 0.62f, cx - r * 0.36f, cy + r * 0.62f);
                        g.DrawLine(pen, cx + r * 0.36f, cy - r * 0.62f, cx + r * 0.36f, cy + r * 0.62f);
                        break;
                    case AkIcon.Stop:
                        g.FillRectangle(br, cx - r * 0.56f, cy - r * 0.56f, r * 1.12f, r * 1.12f);
                        break;
                    case AkIcon.Plus:
                        g.DrawLine(pen, cx - r * 0.68f, cy, cx + r * 0.68f, cy);
                        g.DrawLine(pen, cx, cy - r * 0.68f, cx, cy + r * 0.68f);
                        break;
                    case AkIcon.Minus:
                        g.DrawLine(pen, cx - r * 0.68f, cy, cx + r * 0.68f, cy);
                        break;
                    case AkIcon.Close:
                        g.DrawLine(pen, cx - r * 0.58f, cy - r * 0.58f, cx + r * 0.58f, cy + r * 0.58f);
                        g.DrawLine(pen, cx + r * 0.58f, cy - r * 0.58f, cx - r * 0.58f, cy + r * 0.58f);
                        break;
                    case AkIcon.Up:
                        g.DrawLine(pen, cx, cy + r * 0.68f, cx, cy - r * 0.64f);
                        g.DrawLine(pen, cx - r * 0.52f, cy - r * 0.14f, cx, cy - r * 0.66f);
                        g.DrawLine(pen, cx + r * 0.52f, cy - r * 0.14f, cx, cy - r * 0.66f);
                        break;
                    case AkIcon.Down:
                        g.DrawLine(pen, cx, cy - r * 0.68f, cx, cy + r * 0.64f);
                        g.DrawLine(pen, cx - r * 0.52f, cy + r * 0.14f, cx, cy + r * 0.66f);
                        g.DrawLine(pen, cx + r * 0.52f, cy + r * 0.14f, cx, cy + r * 0.66f);
                        break;
                    case AkIcon.Folder:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx - r * 0.85f, cy - r * 0.56f),
                            new System.Drawing.PointF(cx - r * 0.22f, cy - r * 0.56f),
                            new System.Drawing.PointF(cx - r * 0.04f, cy - r * 0.34f),
                            new System.Drawing.PointF(cx + r * 0.85f, cy - r * 0.34f),
                            new System.Drawing.PointF(cx + r * 0.85f, cy + r * 0.64f),
                            new System.Drawing.PointF(cx - r * 0.85f, cy + r * 0.64f)
                        });
                        break;
                    case AkIcon.Copy:
                        g.DrawRectangle(pen, cx - r * 0.26f, cy - r * 0.26f, r * 1.02f, r * 1.02f);
                        g.DrawLine(pen, cx - r * 0.68f, cy + r * 0.30f, cx - r * 0.68f, cy - r * 0.68f);
                        g.DrawLine(pen, cx - r * 0.68f, cy - r * 0.68f, cx + r * 0.30f, cy - r * 0.68f);
                        break;
                    case AkIcon.Trash:
                        g.DrawLine(pen, cx - r * 0.76f, cy - r * 0.44f, cx + r * 0.76f, cy - r * 0.44f);
                        g.DrawLine(pen, cx - r * 0.28f, cy - r * 0.74f, cx + r * 0.28f, cy - r * 0.74f);
                        g.DrawRectangle(pen, cx - r * 0.54f, cy - r * 0.42f, r * 1.08f, r * 1.16f);
                        break;
                    case AkIcon.Check:
                        g.DrawLine(pen, cx - r * 0.68f, cy + r * 0.04f, cx - r * 0.14f, cy + r * 0.58f);
                        g.DrawLine(pen, cx - r * 0.14f, cy + r * 0.58f, cx + r * 0.74f, cy - r * 0.52f);
                        break;
                    case AkIcon.Swap:
                        g.DrawLine(pen, cx - r * 0.70f, cy - r * 0.32f, cx + r * 0.70f, cy - r * 0.32f);
                        g.DrawLine(pen, cx + r * 0.32f, cy - r * 0.68f, cx + r * 0.72f, cy - r * 0.32f);
                        g.DrawLine(pen, cx + r * 0.70f, cy + r * 0.34f, cx - r * 0.70f, cy + r * 0.34f);
                        g.DrawLine(pen, cx - r * 0.32f, cy + r * 0.70f, cx - r * 0.72f, cy + r * 0.34f);
                        break;
                    case AkIcon.Download:
                        g.DrawLine(pen, cx, cy - r * 0.72f, cx, cy + r * 0.28f);
                        g.DrawLine(pen, cx - r * 0.44f, cy - r * 0.12f, cx, cy + r * 0.32f);
                        g.DrawLine(pen, cx + r * 0.44f, cy - r * 0.12f, cx, cy + r * 0.32f);
                        g.DrawLine(pen, cx - r * 0.74f, cy + r * 0.72f, cx + r * 0.74f, cy + r * 0.72f);
                        break;
                    case AkIcon.Link:
                    case AkIcon.External:
                        g.DrawLine(pen, cx - r * 0.58f, cy + r * 0.58f, cx + r * 0.62f, cy - r * 0.62f);
                        g.DrawLine(pen, cx - r * 0.06f, cy - r * 0.64f, cx + r * 0.64f, cy - r * 0.64f);
                        g.DrawLine(pen, cx + r * 0.64f, cy - r * 0.64f, cx + r * 0.64f, cy + r * 0.06f);
                        break;
                    case AkIcon.Split:
                        g.DrawRectangle(pen, cx - r * 0.82f, cy - r * 0.64f, r * 1.64f, r * 1.28f);
                        g.DrawLine(pen, cx, cy - r * 0.64f, cx, cy + r * 0.64f);
                        break;
                    case AkIcon.Pipette:
                        g.DrawLine(pen, cx - r * 0.66f, cy + r * 0.66f, cx + r * 0.40f, cy - r * 0.40f);
                        g.DrawLine(pen, cx + r * 0.12f, cy - r * 0.68f, cx + r * 0.68f, cy - r * 0.12f);
                        break;
                    case AkIcon.Bolt:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx + r * 0.14f, cy - r * 0.82f),
                            new System.Drawing.PointF(cx - r * 0.62f, cy + r * 0.08f),
                            new System.Drawing.PointF(cx - r * 0.04f, cy + r * 0.08f),
                            new System.Drawing.PointF(cx - r * 0.18f, cy + r * 0.82f),
                            new System.Drawing.PointF(cx + r * 0.62f, cy - r * 0.10f),
                            new System.Drawing.PointF(cx + r * 0.04f, cy - r * 0.10f)
                        });
                        break;
                    case AkIcon.List:
                        g.DrawLine(pen, cx - r * 0.72f, cy - r * 0.46f, cx + r * 0.72f, cy - r * 0.46f);
                        g.DrawLine(pen, cx - r * 0.72f, cy, cx + r * 0.72f, cy);
                        g.DrawLine(pen, cx - r * 0.72f, cy + r * 0.46f, cx + r * 0.72f, cy + r * 0.46f);
                        break;
                    case AkIcon.Filter:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx - r * 0.78f, cy - r * 0.60f),
                            new System.Drawing.PointF(cx + r * 0.78f, cy - r * 0.60f),
                            new System.Drawing.PointF(cx + r * 0.16f, cy + r * 0.08f),
                            new System.Drawing.PointF(cx + r * 0.16f, cy + r * 0.68f),
                            new System.Drawing.PointF(cx - r * 0.16f, cy + r * 0.52f),
                            new System.Drawing.PointF(cx - r * 0.16f, cy + r * 0.08f)
                        });
                        break;
                    case AkIcon.Shield:
                        g.DrawPolygon(pen, new System.Drawing.PointF[] {
                            new System.Drawing.PointF(cx, cy - r * 0.82f),
                            new System.Drawing.PointF(cx + r * 0.72f, cy - r * 0.44f),
                            new System.Drawing.PointF(cx + r * 0.58f, cy + r * 0.34f),
                            new System.Drawing.PointF(cx, cy + r * 0.82f),
                            new System.Drawing.PointF(cx - r * 0.58f, cy + r * 0.34f),
                            new System.Drawing.PointF(cx - r * 0.72f, cy - r * 0.44f)
                        });
                        break;
                    case AkIcon.Lock:
                        g.DrawRectangle(pen, cx - r * 0.58f, cy - r * 0.08f, r * 1.16f, r * 0.84f);
                        g.DrawArc(pen, cx - r * 0.38f, cy - r * 0.76f, r * 0.76f, r * 0.84f, 180, 180);
                        break;
                    case AkIcon.Unlock:
                        g.DrawRectangle(pen, cx - r * 0.58f, cy - r * 0.08f, r * 1.16f, r * 0.84f);
                        g.DrawArc(pen, cx - r * 0.38f, cy - r * 0.76f, r * 0.76f, r * 0.84f, 180, 130);
                        break;
                    case AkIcon.Save:
                        g.DrawRectangle(pen, cx - r * 0.72f, cy - r * 0.72f, r * 1.44f, r * 1.44f);
                        g.DrawRectangle(pen, cx - r * 0.42f, cy - r * 0.72f, r * 0.72f, r * 0.48f);
                        g.DrawRectangle(pen, cx - r * 0.44f, cy + r * 0.14f, r * 0.88f, r * 0.58f);
                        break;
                    case AkIcon.Edit:
                        g.DrawLine(pen, cx - r * 0.62f, cy + r * 0.62f, cx + r * 0.56f, cy - r * 0.56f);
                        g.DrawLine(pen, cx + r * 0.24f, cy - r * 0.74f, cx + r * 0.74f, cy - r * 0.24f);
                        g.DrawLine(pen, cx - r * 0.72f, cy + r * 0.72f, cx - r * 0.34f, cy + r * 0.62f);
                        break;
                    case AkIcon.Undo:
                        g.DrawArc(pen, cx - r * 0.52f, cy - r * 0.34f, r * 1.24f, r * 1.04f, -135, 225);
                        g.DrawLine(pen, cx - r * 0.72f, cy - r * 0.22f, cx - r * 0.28f, cy - r * 0.62f);
                        g.DrawLine(pen, cx - r * 0.72f, cy - r * 0.22f, cx - r * 0.24f, cy + r * 0.10f);
                        break;
                    case AkIcon.ChevLeft:
                        g.DrawLine(pen, cx + r * 0.28f, cy - r * 0.58f, cx - r * 0.34f, cy);
                        g.DrawLine(pen, cx - r * 0.34f, cy, cx + r * 0.28f, cy + r * 0.58f);
                        break;
                    case AkIcon.ChevRight:
                        g.DrawLine(pen, cx - r * 0.28f, cy - r * 0.58f, cx + r * 0.34f, cy);
                        g.DrawLine(pen, cx + r * 0.34f, cy, cx - r * 0.28f, cy + r * 0.58f);
                        break;
                    case AkIcon.Star:
                        System.Drawing.PointF[] sp = new System.Drawing.PointF[10];
                        for (int k = 0; k < 10; k++)
                        {
                            double ang = -System.Math.PI / 2.0 + k * System.Math.PI / 5.0;
                            float rad = (k % 2 == 0) ? r * 0.85f : r * 0.38f;
                            sp[k] = new System.Drawing.PointF(cx + (float)(System.Math.Cos(ang) * rad), cy + (float)(System.Math.Sin(ang) * rad));
                        }
                        g.DrawPolygon(pen, sp);
                        break;
                }
            }
            g.SmoothingMode = oldSm;
        }
    }

    internal sealed class AkButton : System.Windows.Forms.Button
    {
        public AkKind Kind = AkKind.Secondary;
        public AkIcon Icon = AkIcon.None;
        public bool Active = false;
        public bool Dark = true;
        public int Radius = 6;
        private bool _hov = false;
        private bool _down = false;

        public AkButton()
        {
            SetStyle(System.Windows.Forms.ControlStyles.AllPaintingInWmPaint |
                     System.Windows.Forms.ControlStyles.OptimizedDoubleBuffer |
                     System.Windows.Forms.ControlStyles.UserPaint |
                     System.Windows.Forms.ControlStyles.ResizeRedraw, true);
            FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = System.Windows.Forms.Cursors.Hand;
            TabStop = false;
            UseMnemonic = false;
        }

        protected override void OnMouseEnter(System.EventArgs e) { _hov = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(System.EventArgs e) { _hov = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e) { if (e.Button == System.Windows.Forms.MouseButtons.Left) { _down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(System.Windows.Forms.PaintEventArgs e)
        {
            System.Drawing.Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            System.Drawing.Color parentBg = Parent != null ? Parent.BackColor : (Dark ? System.Drawing.Color.FromArgb(29, 31, 32) : System.Drawing.Color.FromArgb(248, 249, 250));
            g.Clear(parentBg);

            System.Drawing.Color accent = Dark ? System.Drawing.Color.FromArgb(252, 150, 31) : System.Drawing.Color.FromArgb(224, 118, 12);
            System.Drawing.Color accentHov = Dark ? System.Drawing.Color.FromArgb(255, 169, 64) : System.Drawing.Color.FromArgb(204, 102, 8);
            System.Drawing.Color surf = Dark ? System.Drawing.Color.FromArgb(39, 41, 43) : System.Drawing.Color.White;
            System.Drawing.Color surfHov = Dark ? System.Drawing.Color.FromArgb(50, 53, 56) : System.Drawing.Color.FromArgb(236, 239, 243);
            System.Drawing.Color border = Dark ? System.Drawing.Color.FromArgb(58, 62, 66) : System.Drawing.Color.FromArgb(210, 216, 224);
            System.Drawing.Color borderHov = Dark ? System.Drawing.Color.FromArgb(84, 89, 95) : System.Drawing.Color.FromArgb(176, 184, 196);
            System.Drawing.Color fgMain = Dark ? System.Drawing.Color.FromArgb(242, 244, 247) : System.Drawing.Color.FromArgb(26, 29, 33);
            System.Drawing.Color fgMuted = Dark ? System.Drawing.Color.FromArgb(154, 160, 168) : System.Drawing.Color.FromArgb(95, 104, 116);
            System.Drawing.Color fgDis = Dark ? System.Drawing.Color.FromArgb(92, 97, 104) : System.Drawing.Color.FromArgb(170, 178, 188);

            bool en = Enabled;
            System.Drawing.Color bg = parentBg;
            System.Drawing.Color bd = System.Drawing.Color.Transparent;
            System.Drawing.Color fg = en ? fgMain : fgDis;
            bool drawFill = false;

            if (Kind == AkKind.Primary)
            {
                drawFill = true;
                bg = en ? (_hov ? accentHov : accent) : (Dark ? System.Drawing.Color.FromArgb(56, 48, 38) : System.Drawing.Color.FromArgb(228, 224, 218));
                fg = en ? (Dark ? System.Drawing.Color.FromArgb(20, 21, 23) : System.Drawing.Color.White) : fgDis;
            }
            else if (Kind == AkKind.AccentOutline)
            {
                drawFill = true;
                bg = en ? (_hov ? (Dark ? System.Drawing.Color.FromArgb(52, 42, 30) : System.Drawing.Color.FromArgb(255, 243, 228)) : surf) : (Dark ? System.Drawing.Color.FromArgb(32, 34, 36) : System.Drawing.Color.FromArgb(244, 246, 248));
                bd = en ? accent : border;
                fg = en ? accent : fgDis;
            }
            else if (Kind == AkKind.Danger)
            {
                drawFill = true;
                System.Drawing.Color red = Dark ? System.Drawing.Color.FromArgb(240, 104, 118) : System.Drawing.Color.FromArgb(196, 40, 56);
                bg = en ? (_hov ? (Dark ? System.Drawing.Color.FromArgb(62, 30, 36) : System.Drawing.Color.FromArgb(254, 228, 231)) : (Dark ? System.Drawing.Color.FromArgb(46, 28, 32) : System.Drawing.Color.FromArgb(255, 240, 242))) : surf;
                bd = en ? (Dark ? System.Drawing.Color.FromArgb(116, 48, 58) : System.Drawing.Color.FromArgb(236, 154, 164)) : border;
                fg = en ? red : fgDis;
            }
            else if (Kind == AkKind.Tab)
            {
                drawFill = _hov && en;
                bg = surfHov;
                fg = en ? (Active ? fgMain : (_hov ? fgMain : fgMuted)) : fgDis;
            }
            else if (Kind == AkKind.Ghost)
            {
                drawFill = (Active || _hov) && en;
                bg = Active ? (Dark ? System.Drawing.Color.FromArgb(52, 42, 30) : System.Drawing.Color.FromArgb(255, 243, 228)) : surfHov;
                bd = Active ? accent : System.Drawing.Color.Transparent;
                fg = en ? (Active ? accent : (_hov ? fgMain : fgMuted)) : fgDis;
            }
            else // Secondary (Ariakit tactile elevated pill)
            {
                drawFill = true;
                bg = en ? (Active ? (Dark ? System.Drawing.Color.FromArgb(52, 42, 30) : System.Drawing.Color.FromArgb(255, 243, 228)) : (_hov ? surfHov : surf)) : (Dark ? System.Drawing.Color.FromArgb(32, 34, 36) : System.Drawing.Color.FromArgb(245, 247, 249));
                bd = en ? (Active ? accent : (_hov ? borderHov : border)) : (Dark ? System.Drawing.Color.FromArgb(44, 47, 50) : System.Drawing.Color.FromArgb(224, 228, 234));
                fg = en ? (Active ? accent : fgMain) : fgDis;
            }

            float ox = 0.5f, oy = (_down && en) ? 1.5f : 0.5f;
            float rw = Width - 1.5f, rh = Height - 1.5f - ((_down && en) ? 1f : 0f);
            if (drawFill || bd.A > 0)
            {
                using (System.Drawing.Drawing2D.GraphicsPath gp = AkDraw.RoundRect(ox, oy, rw, rh, Radius))
                {
                    if (drawFill) using (System.Drawing.SolidBrush br = new System.Drawing.SolidBrush(bg)) g.FillPath(br, gp);
                    if (bd.A > 0) using (System.Drawing.Pen p = new System.Drawing.Pen(bd, 1f)) g.DrawPath(p, gp);
                }
            }
            if (Kind == AkKind.Tab && Active)
            {
                using (System.Drawing.SolidBrush ab = new System.Drawing.SolidBrush(accent))
                    g.FillRectangle(ab, 6, Height - 3, System.Math.Max(8, Width - 12), 2.5f);
            }

            string txt = Text ?? "";
            float yShift = (_down && en) ? 1f : 0f;
            if (Icon != AkIcon.None && txt.Length == 0)
            {
                AkDraw.DrawIcon(g, Icon, Width * 0.5f, Height * 0.5f + yShift, 13.5f, fg);
            }
            else if (Icon != AkIcon.None)
            {
                System.Drawing.Size ts = System.Windows.Forms.TextRenderer.MeasureText(txt, Font, new System.Drawing.Size(Width, Height), System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.NoPadding);
                float iconW = 13f, gap = 6f;
                float totalW = iconW + gap + ts.Width;
                float startX = System.Math.Max(8f, (Width - totalW) * 0.5f);
                AkDraw.DrawIcon(g, Icon, startX + iconW * 0.5f, Height * 0.5f + yShift, 12.5f, fg);
                System.Drawing.Rectangle tr = new System.Drawing.Rectangle((int)(startX + iconW + gap), (int)yShift, System.Math.Max(10, Width - (int)(startX + iconW + gap) - 4), Height);
                System.Windows.Forms.TextRenderer.DrawText(g, txt, Font, tr, fg, System.Windows.Forms.TextFormatFlags.Left | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.EndEllipsis);
            }
            else
            {
                System.Drawing.Rectangle tr = new System.Drawing.Rectangle(4, (int)yShift, System.Math.Max(8, Width - 8), Height);
                System.Windows.Forms.TextRenderer.DrawText(g, txt, Font, tr, fg, System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPrefix | System.Windows.Forms.TextFormatFlags.EndEllipsis);
            }
        }
    }

}
