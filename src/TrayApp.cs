using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VaporwaveToons;

/// <summary>The notification-area icon, its menu, and the frame timer.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "VaporwaveToons";

    private static readonly int[] Counts = { 4, 8, 12, 18, 24, 32, 48 };
    private static (int pct, string name)[] Speeds => new[]
    {
        (50, Strings.Slow), (100, Strings.Normal), (150, Strings.Fast), (200, Strings.Turbo),
    };

    private readonly Theme _theme;
    private readonly Settings _settings;
    private readonly Options _opts;
    private readonly Engine _engine;
    private readonly NotifyIcon _icon;
    private readonly Timer _timer;
    private readonly Timer _failsafe = new() { Interval = 5000 };
    private readonly DateTime _started = DateTime.Now;
    private bool _quitting, _done, _lockedPause;

    private MenuItem _pause, _hide, _squish, _noBlood, _angels, _walkMax, _fullscreen, _startup;
    private readonly List<(MenuItem item, int value)> _countItems = new(), _scaleItems = new(), _speedItems = new();
    private readonly List<(MenuItem item, Genus genus)> _toonItems = new();
    private readonly List<(MenuItem item, string code)> _langItems = new();

    public TrayApp(Theme theme, Settings settings, Options opts)
    {
        _theme = theme;
        _settings = settings;
        _opts = opts;

        _engine = new Engine(theme, settings);
        _engine.AllGone += Finish;
        _engine.Restart();

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = Strings.AppName,
            ContextMenu = BuildMenu(),
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => TogglePause();

        _timer = new Timer { Interval = FrameInterval() };
        _timer.Tick += OnFrame;
        _timer.Start();

        _failsafe.Tick += (_, _) => Finish();

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.SessionEnding += OnSessionEnding;
        Application.ApplicationExit += (_, _) => Cleanup();

        if (!_settings.Welcomed && !_settings.Transient)
        {
            _icon.ShowBalloonTip(8000, Strings.AppName, Strings.Welcome, ToolTipIcon.None);
            _settings.Welcomed = true;
            _settings.Save();
        }

        Log.Write($"start: theme={theme.Name} genera={theme.Genera.Count} scale={_engine.Scale} " +
                  $"interval={_timer.Interval}ms target={_engine.PopulationTarget}");
        foreach (var line in _engine.World.DescribeLayout()) Log.Write(line);
    }

    // ----------------------------------------------------------------- frame loop

    private void OnFrame(object sender, EventArgs e)
    {
        try
        {
            _engine.Tick();
        }
        catch (Exception ex)
        {
            Log.Write("tick failed: " + ex);
            _timer.Stop();
            MessageBox.Show(Strings.Crashed(ex.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Finish();
            return;
        }

        if (_opts.Seconds > 0 && !_quitting && (DateTime.Now - _started).TotalSeconds >= _opts.Seconds)
            RequestQuit();

        if (_opts.LogFile != null && Environment.TickCount % 5000 < _timer.Interval)
        {
            Log.Write($"live={_engine.LiveCount} {_engine.Stats}");
            foreach (var line in _engine.DescribeToons()) Log.Write("  " + line);
        }
    }

    private int FrameInterval() => Math.Max(10, _theme.Delay * 100 / Math.Max(25, _settings.SpeedPercent));

    // ----------------------------------------------------------------- menu

    private ContextMenu BuildMenu()
    {
        _countItems.Clear();
        _scaleItems.Clear();
        _speedItems.Clear();
        _toonItems.Clear();
        _langItems.Clear();

        var menu = new ContextMenu();
        menu.Popup += (_, _) => SyncChecks();

        menu.MenuItems.Add(new MenuItem(Strings.AppName) { Enabled = false, DefaultItem = true });
        menu.MenuItems.Add("-");

        _pause = new MenuItem(Strings.Pause, (_, _) => TogglePause());
        _hide = new MenuItem(Strings.Hide, (_, _) => ToggleHide());
        menu.MenuItems.Add(_pause);
        menu.MenuItems.Add(_hide);
        menu.MenuItems.Add("-");

        var count = new MenuItem(Strings.Count);
        var themeTotal = _theme.Genera.Sum(g => g.Number);
        var countItem = new MenuItem(Strings.ThemeDefault(themeTotal), (_, _) => SetCount(0)) { RadioCheck = true };
        count.MenuItems.Add(countItem);
        _countItems.Add((countItem, 0));
        count.MenuItems.Add("-");
        foreach (var n in Counts)
        {
            var it = new MenuItem(n.ToString(), (_, _) => SetCount(n)) { RadioCheck = true };
            count.MenuItems.Add(it);
            _countItems.Add((it, n));
        }
        menu.MenuItems.Add(count);

        var toons = new MenuItem(Strings.Toons);
        foreach (var g in _theme.Genera)
        {
            var genus = g;
            var it = new MenuItem(DisplayName(g), (_, _) => ToggleGenus(genus));
            toons.MenuItems.Add(it);
            _toonItems.Add((it, g));
        }
        toons.MenuItems.Add("-");
        toons.MenuItems.Add(new MenuItem(Strings.All, (_, _) => SetAllGenera(true)));
        menu.MenuItems.Add(toons);

        var size = new MenuItem(Strings.Size);
        var auto = new MenuItem(Strings.Automatic(Engine.AutoScale(_engine.World)), (_, _) => SetScale(0)) { RadioCheck = true };
        size.MenuItems.Add(auto);
        _scaleItems.Add((auto, 0));
        size.MenuItems.Add("-");
        for (int s = 1; s <= 4; s++)
        {
            int v = s;
            var it = new MenuItem($"{s}×", (_, _) => SetScale(v)) { RadioCheck = true };
            size.MenuItems.Add(it);
            _scaleItems.Add((it, v));
        }
        menu.MenuItems.Add(size);

        var speed = new MenuItem(Strings.Speed);
        foreach (var (pct, name) in Speeds)
        {
            int v = pct;
            var it = new MenuItem(name, (_, _) => SetSpeed(v)) { RadioCheck = true };
            speed.MenuItems.Add(it);
            _speedItems.Add((it, v));
        }
        menu.MenuItems.Add(speed);

        var language = new MenuItem(Strings.Language);
        foreach (var (code, name) in new[] { ("", Strings.LanguageAuto), ("en", "English"), ("pt", "Português") })
        {
            string c = code;
            var it = new MenuItem(name, (_, _) => SetLanguage(c)) { RadioCheck = true };
            language.MenuItems.Add(it);
            _langItems.Add((it, c));
        }
        menu.MenuItems.Add(language);
        menu.MenuItems.Add("-");

        _squish = new MenuItem(Strings.ClickToZap, (_, _) =>
        {
            _settings.Squish = !_settings.Squish;
            _engine.SetClickable(_settings.Squish);
            _settings.Save();
        });
        _noBlood = new MenuItem(Strings.GentleDeaths, (_, _) => { _settings.NoBlood = !_settings.NoBlood; _settings.Save(); });
        _angels = new MenuItem(Strings.Angels, (_, _) => { _settings.Angels = !_settings.Angels; _settings.Save(); });
        _walkMax = new MenuItem(Strings.WalkOverMaximized, (_, _) =>
        {
            _settings.WalkOverMaximized = !_settings.WalkOverMaximized;
            _engine.World.WalkOverMaximized = _settings.WalkOverMaximized;
            _settings.Save();
        });
        _fullscreen = new MenuItem(Strings.HideInFullscreen, (_, _) =>
        {
            _settings.HideInFullscreen = !_settings.HideInFullscreen;
            _engine.World.HideInFullscreen = _settings.HideInFullscreen;
            _settings.Save();
        });
        _startup = new MenuItem(Strings.StartWithWindows, (_, _) => ToggleStartup());
        menu.MenuItems.AddRange(new[] { _squish, _noBlood, _angels, _walkMax, _fullscreen, _startup });
        menu.MenuItems.Add("-");

        menu.MenuItems.Add(new MenuItem(Strings.AboutMenu, (_, _) => ShowAbout()));
        menu.MenuItems.Add(new MenuItem(Strings.Exit, (_, _) => RequestQuit()));
        return menu;
    }

    private void SyncChecks()
    {
        _pause.Checked = _engine.Paused;
        _hide.Checked = _engine.Hidden;
        _squish.Checked = _settings.Squish;
        _noBlood.Checked = _settings.NoBlood;
        _angels.Checked = _settings.Angels;
        _walkMax.Checked = _settings.WalkOverMaximized;
        _fullscreen.Checked = _settings.HideInFullscreen;
        _startup.Checked = StartupEnabled();
        foreach (var (item, v) in _countItems) item.Checked = _settings.Count == v;
        foreach (var (item, v) in _scaleItems) item.Checked = _settings.Scale == v;
        foreach (var (item, v) in _speedItems) item.Checked = _settings.SpeedPercent == v;
        foreach (var (item, g) in _toonItems) item.Checked = _settings.IsEnabled(g.Name);
        foreach (var (item, c) in _langItems) item.Checked = _settings.Language == c;
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        _icon.Text = Strings.Tooltip(_engine.PopulationTarget, _engine.Paused, _engine.Hidden);
    }

    private static string DisplayName(Genus g) =>
        Strings.ToonName(g.Name) ??
        (g.Name.Length > 0 ? char.ToUpperInvariant(g.Name[0]) + g.Name.Substring(1) : "?");

    private void TogglePause()
    {
        _engine.Paused = !_engine.Paused;
        _lockedPause = false;
        UpdateTooltip();
    }

    private void ToggleHide()
    {
        _engine.Hidden = !_engine.Hidden;
        UpdateTooltip();
    }

    private void SetCount(int n)
    {
        _settings.Count = n;
        _settings.Save();
        _engine.Repopulate();
        UpdateTooltip();
    }

    private void ToggleGenus(Genus g)
    {
        if (_settings.IsEnabled(g.Name))
        {
            if (_theme.Genera.Count(x => _settings.IsEnabled(x.Name)) == 1) return;   // keep at least one
            _settings.Disabled.Add(g.Name);
        }
        else _settings.Disabled.Remove(g.Name);
        _settings.Save();
        _engine.Repopulate();
        UpdateTooltip();
    }

    private void SetAllGenera(bool on)
    {
        if (on) _settings.Disabled.Clear();
        _settings.Save();
        _engine.Repopulate();
        UpdateTooltip();
    }

    private void SetScale(int s)
    {
        _settings.Scale = s;
        _settings.Save();
        _engine.Restart();
    }

    private void SetSpeed(int pct)
    {
        _settings.SpeedPercent = pct;
        _settings.Save();
        _timer.Interval = FrameInterval();
    }

    private void SetLanguage(string code)
    {
        _settings.Language = code;
        _settings.Save();
        Strings.Use(code);

        // The menu has closed by the time a click is handled; swap in one built in the new language.
        var old = _icon.ContextMenu;
        _icon.ContextMenu = BuildMenu();
        System.Threading.SynchronizationContext.Current?.Post(_ => old.Dispose(), null);
        UpdateTooltip();
    }

    // ----------------------------------------------------------------- startup with Windows

    private static string ExePath => Assembly.GetExecutingAssembly().Location;

    private static bool StartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(RunValue) is string v &&
                   v.Trim('"').Equals(ExePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception) { return false; }
    }

    private void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (StartupEnabled()) key.DeleteValue(RunValue, false);
            else key.SetValue(RunValue, $"\"{ExePath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(Strings.StartupFailed(ex.Message), Strings.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ----------------------------------------------------------------- about

    private void ShowAbout()
    {
        string F(string k) => _theme.AboutField(k) ?? "—";
        string text = Strings.About(
            Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
            string.Join(", ", _theme.Genera.Select(DisplayName)),
            F("artist"), F("maintainer"), F("license"),
            _engine.LiveCount, _engine.PopulationTarget, _engine.Scale, _theme.PixmapCount,
            Settings.FilePath);
        MessageBox.Show(text, Strings.AboutTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ----------------------------------------------------------------- shutdown

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock && !_engine.Paused)
        {
            _engine.Paused = true;
            _lockedPause = true;
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock && _lockedPause)
        {
            _engine.Paused = false;
            _lockedPause = false;
        }
    }

    private void OnSessionEnding(object sender, SessionEndingEventArgs e) => Finish();

    /// <summary>Everyone plays their power-off animation, then the app exits.</summary>
    private void RequestQuit()
    {
        if (_quitting) return;
        _quitting = true;
        Log.Write("quit requested");
        _engine.BeginExit();
        _failsafe.Start();
    }

    private void Finish()
    {
        if (_done) return;
        _done = true;
        Log.Write($"finish: {_engine.Stats}");
        Cleanup();
        ExitThread();
    }

    private bool _cleaned;

    private void Cleanup()
    {
        if (_cleaned) return;
        _cleaned = true;
        _timer.Stop();
        _failsafe.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.SessionEnding -= OnSessionEnding;
        _icon.Visible = false;
        _engine.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Dispose();
            _timer.Dispose();
            _failsafe.Dispose();
        }
        base.Dispose(disposing);
    }

    private static Icon LoadIcon()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("vaporwave.ico");
        int size = SystemInformation.SmallIconSize.Width;
        try
        {
            uint dpi = Native.GetDpiForSystem();
            size = (int)Math.Round(16 * dpi / 96.0);
        }
        catch (EntryPointNotFoundException) { }
        return s != null ? new Icon(s, size, size) : SystemIcons.Application;
    }
}
