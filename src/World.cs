using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static VaporwaveToons.Native;

namespace VaporwaveToons;

/// <summary>Half-open rectangle [L,R) x [T,B) in physical screen pixels.</summary>
internal readonly struct Box : IEquatable<Box>
{
    public readonly int L, T, R, B;
    public Box(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; }
    public Box(RECT rc) : this(rc.Left, rc.Top, rc.Right, rc.Bottom) { }

    public int W => R - L;
    public int H => B - T;
    public bool Empty => R <= L || B <= T;

    public bool Hits(int x, int y, int w, int h) => x < R && x + w > L && y < B && y + h > T;
    public bool Hits(Box o) => o.L < R && o.R > L && o.T < B && o.B > T;
    public bool Contains(Box o) => o.L >= L && o.R <= R && o.T >= T && o.B <= B;
    public bool Contains(int x, int y) => x >= L && x < R && y >= T && y < B;

    public bool Equals(Box o) => L == o.L && T == o.T && R == o.R && B == o.B;
    public override bool Equals(object obj) => obj is Box b && Equals(b);
    public override int GetHashCode() => L ^ (T << 8) ^ (R << 16) ^ (B << 24);
    public override string ToString() => $"({L},{T})-({R},{B}) {W}x{H}";

    /// <summary>this minus o, as up to four pieces.</summary>
    public void Subtract(Box o, List<Box> into)
    {
        if (!Hits(o)) { into.Add(this); return; }
        if (o.T > T) into.Add(new Box(L, T, R, o.T));
        if (o.B < B) into.Add(new Box(L, o.B, R, B));
        int t = Math.Max(T, o.T), b = Math.Min(B, o.B);
        if (o.L > L) into.Add(new Box(L, t, o.L, b));
        if (o.R < R) into.Add(new Box(o.R, t, R, b));
    }
}

internal sealed class Screen
{
    public Box Bounds, Work;
    public bool Primary;
    /// <summary>A full-screen window (game, video) is on this monitor right now.</summary>
    public bool Covered;
}

internal readonly struct Obstacle
{
    public readonly IntPtr Hwnd;
    public readonly Box Box;
    public Obstacle(IntPtr hwnd, Box box) { Hwnd = hwnd; Box = box; }
}

/// <summary>
/// The toons' idea of the desktop: free space is the union of every monitor's work
/// area plus a strip of "sky" above the top monitors, minus every real window.
/// Everything else is solid, exactly like XPenguins treats the X root window.
/// </summary>
internal sealed class World
{
    public readonly List<Screen> Screens = new();
    public readonly List<Box> Skies = new();
    public readonly List<Obstacle> Obstacles = new();
    /// <summary>Full rectangle of every window that is currently an obstacle, for dragging toons along.</summary>
    public readonly Dictionary<IntPtr, Box> WindowBoxes = new();
    /// <summary>The same, one frame ago: tells a window that moved onto a toon from one that just appeared.</summary>
    public readonly Dictionary<IntPtr, Box> PrevWindowBoxes = new();
    public Box Extent;

    private readonly List<Box> _outside = new();
    private readonly List<Box> _planes = new();
    private readonly List<Box> _scratchA = new(), _scratchB = new();
    private readonly Dictionary<IntPtr, WinInfo> _info = new();
    private readonly uint _myPid = (uint)Process.GetCurrentProcess().Id;
    private readonly EnumWindowsProc _enumCb;
    private readonly StringBuilder _sb = new(256);
    private int _generation;
    private string _monitorSignature = "";

    public bool WalkOverMaximized = true;
    public bool HideInFullscreen = true;
    public List<string> Trace;   // set to collect a --list-windows report

    public World() { _enumCb = OnWindow; }

    private sealed class WinInfo
    {
        public bool Ignore;
        public string Class;
        public int Seen;
    }

    // Shell furniture, popups and overlays that must never count as solid.
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
        "#32768", "tooltips_class32", "SysShadow", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "Xaml_WindowedPopupClass", "ForegroundStaging",
        "MultitaskingViewFrame", "TaskListThumbnailWnd", "TopLevelWindowForOverflowXamlIsland",
        "Shell_InputSwitchTopLevelWindow", "EdgeUiInputTopWndClass", "EdgeUiInputWndClass",
        "ApplicationManager_DesktopShellWindow", "Windows.Internal.Shell.TabProxyWindow",
        "ThumbnailDeviceHelperWnd", "IME", "MSCTFIME UI", "Shell_LightDismissOverlay",
        "PopupHost", "Microsoft.UI.Content.PopupWindowSiteBridge", "CEF-OSC-WIDGET",
        "SnipOverlayRootWindow", "Windows.UI.Input.InputSite.WindowClass",
    };

    // ----------------------------------------------------------------- monitors

    /// <summary>Re-read the monitor layout. Returns true when it changed.</summary>
    public bool RefreshScreens(int skyHeight)
    {
        var list = new List<Screen>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr mon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(mon, ref mi))
                list.Add(new Screen
                {
                    Bounds = new Box(mi.rcMonitor), Work = new Box(mi.rcWork),
                    Primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                });
            return true;
        }, IntPtr.Zero);
        if (list.Count == 0)
            list.Add(new Screen { Bounds = new Box(0, 0, 1920, 1080), Work = new Box(0, 0, 1920, 1040), Primary = true });

        string sig = skyHeight + ";" + string.Join(";", list.Select(s => s.Bounds + "|" + s.Work));
        if (sig == _monitorSignature) return false;
        _monitorSignature = sig;

        Screens.Clear();
        Screens.AddRange(list.OrderByDescending(s => s.Primary));

        // Sky: a strip above each work area that no other monitor occupies. Toons are
        // born there and fall in, the way XPenguins starts them above the screen.
        Skies.Clear();
        foreach (var s in Screens)
        {
            var pieces = new List<Box> { new(s.Work.L, s.Work.T - skyHeight, s.Work.R, s.Work.T) };
            foreach (var o in Screens)
            {
                if (o == s) continue;
                var next = new List<Box>();
                foreach (var p in pieces) p.Subtract(o.Bounds, next);
                pieces = next;
            }
            Skies.AddRange(pieces.Where(p => !p.Empty));
        }

        var free = Screens.Select(s => s.Work).Concat(Skies).ToList();
        Extent = new Box(free.Min(f => f.L), free.Min(f => f.T), free.Max(f => f.R), free.Max(f => f.B));
        _outside.Clear();
        _outside.AddRange(Complement(free, Extent));
        return true;
    }

    /// <summary>Solid cells of the extent not covered by any free rectangle.</summary>
    private static List<Box> Complement(List<Box> free, Box ext)
    {
        var xs = new SortedSet<int> { ext.L, ext.R };
        var ys = new SortedSet<int> { ext.T, ext.B };
        foreach (var f in free) { xs.Add(f.L); xs.Add(f.R); ys.Add(f.T); ys.Add(f.B); }
        var xa = xs.ToArray();
        var ya = ys.ToArray();
        var solid = new List<Box>();
        for (int j = 0; j + 1 < ya.Length; j++)
        {
            int runStart = int.MinValue;
            for (int i = 0; i + 1 < xa.Length; i++)
            {
                var cell = new Box(xa[i], ya[j], xa[i + 1], ya[j + 1]);
                bool isFree = free.Any(f => f.Contains(cell));
                if (!isFree && runStart == int.MinValue) runStart = xa[i];
                if (isFree && runStart != int.MinValue)
                {
                    solid.Add(new Box(runStart, ya[j], xa[i], ya[j + 1]));
                    runStart = int.MinValue;
                }
            }
            if (runStart != int.MinValue) solid.Add(new Box(runStart, ya[j], xa[xa.Length - 1], ya[j + 1]));
        }
        return solid;
    }

    // ----------------------------------------------------------------- windows

    public void RefreshWindows()
    {
        _generation++;
        Obstacles.Clear();
        PrevWindowBoxes.Clear();
        foreach (var kv in WindowBoxes) PrevWindowBoxes[kv.Key] = kv.Value;
        WindowBoxes.Clear();
        _planes.Clear();
        foreach (var s in Screens) s.Covered = false;
        EnumWindows(_enumCb, IntPtr.Zero);

        if (_generation % 120 == 0)
        {
            foreach (var dead in _info.Where(kv => _generation - kv.Value.Seen > 120).Select(kv => kv.Key).ToList())
                _info.Remove(dead);
        }
    }

    private bool OnWindow(IntPtr hwnd, IntPtr _)
    {
        if (!IsWindowVisible(hwnd) || IsIconic(hwnd)) return true;

        if (!_info.TryGetValue(hwnd, out var info))
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            _sb.Clear();
            GetClassName(hwnd, _sb, _sb.Capacity);
            string cls = _sb.ToString();
            info = new WinInfo { Class = cls, Ignore = pid == _myPid || IgnoredClasses.Contains(cls) };
            _info[hwnd] = info;
        }
        info.Seen = _generation;
        if (info.Ignore) return true;

        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        int style = GetWindowLong(hwnd, GWL_STYLE);
        string why = null;
        bool caption = (style & WS_CAPTION) == WS_CAPTION;
        if ((ex & WS_EX_TRANSPARENT) != 0) why = "click-through";
        else if ((style & WS_CHILD) != 0) why = "child";
        else if ((ex & WS_EX_TOOLWINDOW) != 0 && !caption) why = "toolwindow";
        else if ((style & WS_POPUP) != 0 && !caption && GetWindowTextLength(hwnd) == 0) why = "popup";
        else if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, 4) == 0 && cloaked != 0) why = "cloaked";
        if (why != null)
        {
            Trace?.Add($"  skip {why,-12} {Describe(hwnd, info)}");
            return true;
        }

        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT rc, 16) != 0)
            GetWindowRect(hwnd, out rc);
        var box = new Box(rc);
        if (box.W < 16 || box.H < 16 || !Screens.Any(s => s.Bounds.Hits(box)))
        {
            Trace?.Add($"  skip {"tiny/offscr",-12} {Describe(hwnd, info)} {box}");
            return true;
        }

        // Full screen covers a whole monitor. "Max" is anything with no room to stand on top of it:
        // maximised, or snapped to a side so it runs from the top of the work area to the bottom.
        bool full = Screens.Any(s => box.Contains(s.Bounds));
        bool max = !full && (IsZoomed(hwnd) ||
                             Screens.Any(s => box.T <= s.Work.T && box.B >= s.Work.B && box.L < s.Work.R && box.R > s.Work.L));

        // Windows below a maximised or full-screen window are hidden behind it.
        _scratchA.Clear();
        _scratchA.Add(box);
        foreach (var p in _planes)
        {
            _scratchB.Clear();
            foreach (var piece in _scratchA) piece.Subtract(p, _scratchB);
            _scratchA.Clear();
            _scratchA.AddRange(_scratchB);
            if (_scratchA.Count == 0) break;
        }
        if (_scratchA.Count == 0)
        {
            Trace?.Add($"  skip {"behind max",-12} {Describe(hwnd, info)} {box}");
            return true;
        }

        if (full && HideInFullscreen)
        {
            foreach (var s in Screens) if (box.Contains(s.Bounds)) s.Covered = true;
            AddObstacle(hwnd, box);
            _planes.Add(box);
            Trace?.Add($"  FULL             {Describe(hwnd, info)} {box}");
        }
        else if ((full || max) && WalkOverMaximized)
        {
            _planes.Add(box);
            Trace?.Add($"  plane (max)      {Describe(hwnd, info)} {box}");
        }
        else
        {
            foreach (var piece in _scratchA) Obstacles.Add(new Obstacle(hwnd, piece));
            WindowBoxes[hwnd] = box;
            Trace?.Add($"  SOLID            {Describe(hwnd, info)} {box}{(_scratchA.Count > 1 || !_scratchA[0].Equals(box) ? $" ({_scratchA.Count} parts)" : "")}");
        }
        return true;
    }

    private void AddObstacle(IntPtr hwnd, Box box)
    {
        Obstacles.Add(new Obstacle(hwnd, box));
        WindowBoxes[hwnd] = box;
    }

    private string Describe(IntPtr hwnd, WinInfo info)
    {
        _sb.Clear();
        GetWindowText(hwnd, _sb, _sb.Capacity);
        string title = _sb.ToString();
        if (title.Length > 40) title = title.Substring(0, 40) + "…";
        return $"0x{hwnd.ToInt64():X8} [{info.Class}] \"{title}\"";
    }

    // ----------------------------------------------------------------- queries

    /// <summary>Would a toon occupying this rectangle overlap anything solid?</summary>
    /// <param name="ignore">Windows this particular toon is passing in front of (see Engine ghosts).</param>
    public bool Blocked(int x, int y, int w, int h, List<IntPtr> ignore = null)
    {
        if (OffDesktop(x, y, w, h)) return true;
        foreach (var o in Obstacles)
            if (o.Box.Hits(x, y, w, h) && (ignore == null || !ignore.Contains(o.Hwnd))) return true;
        return false;
    }

    /// <summary>Outside every work area and sky: off the edge of the desktop, or on a taskbar.</summary>
    public bool OffDesktop(int x, int y, int w, int h)
    {
        if (x < Extent.L || y < Extent.T || x + w > Extent.R || y + h > Extent.B) return true;
        foreach (var o in _outside) if (o.Hits(x, y, w, h)) return true;
        return false;
    }

    /// <summary>Every window overlapping the rectangle, skipping the ignored ones.</summary>
    public void Overlapping(int x, int y, int w, int h, List<IntPtr> ignore, List<IntPtr> into)
    {
        into.Clear();
        foreach (var o in Obstacles)
            if (o.Box.Hits(x, y, w, h) && (ignore == null || !ignore.Contains(o.Hwnd)) && !into.Contains(o.Hwnd))
                into.Add(o.Hwnd);
    }

    public bool InSky(int x, int y, int w, int h)
    {
        foreach (var s in Skies) if (s.Hits(x, y, w, h)) return true;
        return false;
    }

    public bool OnAnyScreen(int x, int y, int w, int h)
    {
        foreach (var s in Screens) if (s.Bounds.Hits(x, y, w, h)) return true;
        return false;
    }

    public Screen ScreenAt(int x, int y)
    {
        foreach (var s in Screens) if (s.Bounds.Contains(x, y)) return s;
        return null;
    }

    /// <summary>The obstacle whose top edge the toon is standing on, if any.</summary>
    public IntPtr WindowBelow(int x, int y, int w, int h)
    {
        foreach (var o in Obstacles)
            if (o.Box.T == y + h && o.Box.L < x + w && o.Box.R > x) return o.Hwnd;
        return IntPtr.Zero;
    }

    /// <summary>The obstacle a climber is holding on to. side 0 = wall on its left.</summary>
    public IntPtr WindowBeside(int x, int y, int w, int h, int side)
    {
        foreach (var o in Obstacles)
        {
            bool touching = side == 0 ? o.Box.R == x : o.Box.L == x + w;
            if (touching && o.Box.T < y + h && o.Box.B > y) return o.Hwnd;
        }
        return IntPtr.Zero;
    }

    public List<string> DescribeLayout()
    {
        var lines = new List<string>();
        foreach (var s in Screens)
            lines.Add($"monitor {(s.Primary ? "(primary) " : "")}bounds={s.Bounds} work={s.Work}{(s.Covered ? " FULLSCREEN" : "")}");
        foreach (var s in Skies) lines.Add($"sky {s}");
        foreach (var o in _outside) lines.Add($"outside {o}");
        lines.Add($"extent {Extent}");
        return lines;
    }
}
