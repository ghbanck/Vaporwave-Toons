using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace VaporwaveToons;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        EnableDpiAwareness();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var settings = Settings.Load();
        Strings.Use(settings.Language);   // --lang, if given, overrides this for one run

        Options opts;
        try
        {
            opts = Options.Parse(args);
        }
        catch (Exception ex)
        {
            Tell(ex.Message + "\n\n" + Strings.Usage, MessageBoxIcon.Warning);
            return 2;
        }
        if (opts.Help)
        {
            Tell(Strings.Usage, MessageBoxIcon.Information);
            return 0;
        }
        Log.File = opts.LogFile;

        Theme theme;
        try
        {
            theme = opts.ThemeDir != null
                ? Theme.Load(new FolderSource(opts.ThemeDir), Path.GetFileName(opts.ThemeDir.TrimEnd('\\', '/')).Replace('_', ' '))
                : Theme.Load(new EmbeddedSource(), "Vaporwave");
        }
        catch (Exception ex)
        {
            Tell(Strings.ThemeLoadFailed(ex.Message), MessageBoxIcon.Error);
            return 1;
        }

        if (opts.SelfTest) return SelfTest(theme, opts);
        if (opts.ListWindows) return ListWindows(theme);

        using var single = new Mutex(true, @"Local\VaporwaveToons.Singleton", out bool first);
        if (!first)
        {
            Tell(Strings.AlreadyRunning, MessageBoxIcon.Information);
            return 0;
        }

        foreach (var w in theme.Warnings) Log.Write("theme: " + w);
        opts.ApplyTo(settings);
        using var app = new TrayApp(theme, settings, opts);
        Application.Run(app);
        return 0;
    }

    private static void EnableDpiAwareness()
    {
        // The manifest asks for per-monitor v2 already; this covers running under a host that ignores it.
        try
        {
            if (!Native.SetProcessDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                Native.SetProcessDPIAware();
        }
        catch (EntryPointNotFoundException)
        {
            Native.SetProcessDPIAware();
        }
    }

    private static void Tell(string text, MessageBoxIcon icon)
    {
        Out(text);
        if (Log.File == null) MessageBox.Show(text, Strings.AppName, MessageBoxButtons.OK, icon);
    }

    private static void Out(string text)
    {
        try { Console.WriteLine(text); } catch (IOException) { }
        Log.Write(text);
    }

    /// <summary>--selftest: load the theme with the real loader and check every sheet against the config.</summary>
    private static int SelfTest(Theme theme, Options opts)
    {
        var problems = new List<string>(theme.Warnings);
        Out($"theme \"{theme.Name}\": delay {theme.Delay} ms, {theme.Genera.Count} toons, {theme.PixmapCount} pixmaps");
        int defs = 0;
        foreach (var g in theme.Genera)
        {
            var have = Enumerable.Range(0, Theme.ActCount).Where(i => g.Acts[i] != null).Select(i => (Act)i).ToList();
            defs += have.Count;
            var missing = Enumerable.Range(0, Theme.ActCount).Select(i => (Act)i).Except(have).ToList();
            Out($"  {g.Name,-10} number={g.Number} activities={have.Count} actions={g.ActionCount}" +
                (missing.Count > 0 ? " missing: " + string.Join(",", missing) : ""));
            foreach (var a in have)
            {
                var d = g[a];
                var s = d.Sheet;
                if (s.FrameW != d.Width || s.FrameH != d.Height || s.Frames != d.Frames)
                    problems.Add($"{g.Name}/{a}: sheet {s.FrameW}x{s.FrameH}x{s.Frames} != config");
                bool twoRows = a is Act.Walker or Act.Runner or Act.Climber or Act.Zapped;
                if (twoRows && d.Directions != 2) problems.Add($"{g.Name}/{a}: expected 2 directions");
                if (s.Px.All(p => p == 0)) problems.Add($"{g.Name}/{a}: empty sheet");
            }
        }
        Out($"{defs} activity definitions");

        // Facing check: the walker's row 0 must be the mirror image of row 1,
        // and every synthesised row 0 must mirror its row 1 too.
        foreach (var g in theme.Genera)
            foreach (var a in new[] { Act.Walker, Act.Runner, Act.Faller, Act.Action0 })
            {
                if (!g.Has(a)) continue;
                var s = g[a].Sheet;
                if (s.Rows < 2) continue;
                bool mirrored = true;
                for (int f = 0; f < s.Frames && mirrored; f++)
                    for (int y = 0; y < s.FrameH && mirrored; y++)
                        for (int x = 0; x < s.FrameW; x++)
                            if (s.Px[y * s.Stride + f * s.FrameW + x] !=
                                s.Px[(s.FrameH + y) * s.Stride + f * s.FrameW + s.FrameW - 1 - x])
                            {
                                mirrored = false;
                                break;
                            }
                if (!mirrored) problems.Add($"{g.Name}/{a}: row 0 is not the mirror image of row 1");
            }

        foreach (var p in problems) Out("PROBLEM: " + p);
        Out(problems.Count == 0 ? "all good" : $"{problems.Count} problem(s)");
        return problems.Count == 0 ? 0 : 1;
    }

    /// <summary>--list-windows: which windows the toons would treat as solid, and why the rest are ignored.</summary>
    private static int ListWindows(Theme theme)
    {
        var world = new World { Trace = new List<string>() };
        world.RefreshScreens(0);
        int scale = Engine.AutoScale(world);
        world.RefreshScreens((theme.MaxFrameH + 8) * scale);
        world.RefreshWindows();
        Out($"automatic scale: {scale}x (system DPI {Native.GetDpiForSystem()})");
        foreach (var l in world.DescribeLayout()) Out(l);
        Out($"{world.Obstacles.Count} obstacle(s):");
        foreach (var l in world.Trace) Out(l);
        return 0;
    }
}
