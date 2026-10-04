using System;
using System.Collections.Generic;
using System.Linq;

namespace VaporwaveToons;

internal sealed class Toon
{
    public Genus G;
    public Act Type;
    public ActDef Def => G[Type];

    public int X, Y, U, V;
    public int Dir;                 // 0 = left, 1 = right; for climbers, the side the wall is on
    public int Frame, Cycle;

    public bool Active;
    public bool Terminating;        // playing its exit; never comes back
    public int RespawnAt;

    public int PrefDir = -1;        // direction to resume walking in after a fall
    public bool PrefClimb;          // just climbed over a wall: climb the next one too
    public int FallFrames;
    public bool ChuteDecided;

    public IntPtr AssocHwnd;        // the window it is standing on or clinging to
    public List<IntPtr> Ghosts;     // windows that appeared on top of it: it walks on in front of them
    public Assoc AssocKind;
    public Box AssocBox;

    public ToonWindow Win;
}

internal enum Assoc { Down, WallLeft, WallRight }

internal sealed class Stats
{
    public readonly int[] Entered = new int[Theme.ActCount];
    public int Spawns, Squashes, Splats, Zaps, Rides, Chutes, Ghosts;
    public long WorldTicks, WorldMicros;

    public override string ToString()
    {
        var acts = string.Join(" ", Enumerable.Range(0, Theme.ActCount)
            .Select(i => $"{((Act)i).ToString().ToLowerInvariant()}={Entered[i]}"));
        double avg = WorldTicks == 0 ? 0 : WorldMicros / (double)WorldTicks;
        return $"spawns={Spawns} squashes={Squashes} splats={Splats} zaps={Zaps} rides={Rides} chutes={Chutes} ghosts={Ghosts} " +
               $"tick_us={avg:F0}\n{acts}";
    }
}

/// <summary>
/// The XPenguins behaviour loop (xpenguins_core.c), reimplemented for Windows.
/// All coordinates are physical pixels; theme speeds and sizes are multiplied by Scale.
/// </summary>
internal sealed class Engine : IDisposable
{
    private enum Status { Ok, Blocked, Finished }
    private enum Gravity { Still, Down, Here }

    private const int JumpUnits = 8;            // PENGUIN_JUMP: highest step a walker takes in its stride

    private readonly Theme _theme;
    private readonly Settings _settings;
    private readonly World _world = new();
    private readonly Random _rng = new();
    private readonly List<Toon> _toons = new();
    private readonly System.Diagnostics.Stopwatch _clock = new();
    private readonly VirtualDesktops _desktops = new();

    private int _frame;
    private int _jump;
    private int _skyH;

    public int Scale { get; private set; } = 1;
    public bool Paused, Hidden;
    public bool Exiting { get; private set; }
    public readonly Stats Stats = new();
    public event Action AllGone;

    public World World => _world;
    public int LiveCount => _toons.Count(t => t.Active && !t.Terminating);
    public int PopulationTarget { get; private set; }

    public Engine(Theme theme, Settings settings)
    {
        _theme = theme;
        _settings = settings;
    }

    // ----------------------------------------------------------------- setup

    public static int AutoScale(World w)
    {
        uint dpi = 96;
        try { dpi = Native.GetDpiForSystem(); } catch (EntryPointNotFoundException) { }
        var primary = w.Screens.FirstOrDefault(s => s.Primary) ?? w.Screens.First();
        int byDpi = (int)Math.Floor(dpi / 96.0 + 0.5);
        int byHeight = (int)Math.Floor(primary.Bounds.H / 1080.0 + 0.5);
        return Theme.Clamp(Math.Max(byDpi, byHeight), 1, 4);
    }

    /// <summary>(Re)build everything for the current scale and settings.</summary>
    public void Restart()
    {
        foreach (var t in _toons) t.Win.Dispose();
        _toons.Clear();

        _world.WalkOverMaximized = _settings.WalkOverMaximized;
        _world.HideInFullscreen = _settings.HideInFullscreen;
        _world.RefreshScreens(0);
        Scale = _settings.Scale > 0 ? _settings.Scale : AutoScale(_world);
        _jump = JumpUnits * Scale;
        _skyH = (_theme.MaxFrameH + 8) * Scale;
        _world.RefreshScreens(_skyH);
        Repopulate();
    }

    /// <summary>Match the live population to the settings, adding toons or retiring extras.</summary>
    public void Repopulate()
    {
        var enabled = _theme.Genera.Where(g => _settings.IsEnabled(g.Name)).ToList();
        int total = _settings.Count > 0 ? _settings.Count : enabled.Sum(g => g.Number);
        PopulationTarget = total;
        var quota = Distribute(total, enabled);
        int stagger = 0;
        foreach (var g in _theme.Genera)
        {
            quota.TryGetValue(g, out int want);
            var live = _toons.Where(t => t.G == g && !t.Terminating).ToList();
            for (int i = live.Count; i < want; i++)
            {
                var t = new Toon { G = g, Type = Act.Faller };
                t.Win = NewWindow(t);
                t.RespawnAt = _frame + 2 + stagger * 4 + _rng.Next(12);
                stagger++;
                _toons.Add(t);
            }
            for (int i = want; i < live.Count; i++) Terminate(live[i]);
        }
    }

    private ToonWindow NewWindow(Toon t)
    {
        var w = new ToonWindow(_theme.MaxFrameW * Scale, _theme.MaxFrameH * Scale, _settings.Squish);
        w.Clicked += () => Zap(t);
        return w;
    }

    /// <summary>After a switch to another virtual desktop, rebuild the windows so the toons come along.</summary>
    private void FollowVirtualDesktop()
    {
        var probe = _toons.FirstOrDefault(t => t.Win.Visible);
        if (probe == null || _desktops.IsOnCurrentDesktop(probe.Win.Handle)) return;
        foreach (var t in _toons)
        {
            t.Win.Dispose();
            t.Win = NewWindow(t);
        }
        Log.Write("virtual desktop changed: toon windows recreated");
    }

    private static Dictionary<Genus, int> Distribute(int total, List<Genus> genera)
    {
        var res = new Dictionary<Genus, int>();
        if (genera.Count == 0 || total <= 0) return res;
        double sum = genera.Sum(g => Math.Max(1, g.Number));
        var fracs = new List<(Genus g, double f)>();
        int given = 0;
        foreach (var g in genera)
        {
            double exact = total * Math.Max(1, g.Number) / sum;
            int whole = (int)exact;
            res[g] = whole;
            given += whole;
            fracs.Add((g, exact - whole));
        }
        foreach (var (g, _) in fracs.OrderByDescending(x => x.f))
        {
            if (given >= total) break;
            res[g]++;
            given++;
        }
        return res;
    }

    public void SetClickable(bool on)
    {
        foreach (var t in _toons) t.Win.SetClickable(on);
    }

    public void BeginExit()
    {
        Exiting = true;
        foreach (var t in _toons) Terminate(t);
        if (Hidden || Paused) foreach (var t in _toons) t.Active = false;
    }

    private void Terminate(Toon t)
    {
        t.Terminating = true;
        if (!t.Active || t.Type == Act.Exit) return;
        if (IsFrozen(t)) { t.Active = false; return; }
        if (t.G.Has(Act.Exit)) SetType(t, Act.Exit, t.Dir, Gravity.Down);
        else if (t.G.Has(Act.Explosion)) SetType(t, Act.Explosion, t.Dir, Gravity.Here);
        else { t.Active = false; return; }
        t.U = t.V = 0;
    }

    // ----------------------------------------------------------------- frame

    public void Tick()
    {
        _frame++;
        if (_frame % 32 == 0 && _world.RefreshScreens(_skyH))
        {
            // Monitors were added, removed or resized: start the population over.
            if (Exiting) foreach (var t in _toons) t.Active = false;
            else
            {
                Restart();
                return;
            }
        }

        if ((!Paused && !Hidden) || Exiting)
        {
            _clock.Restart();
            _world.RefreshWindows();
            Stats.WorldMicros += _clock.ElapsedTicks * 1_000_000 / System.Diagnostics.Stopwatch.Frequency;
            Stats.WorldTicks++;

            Relocate();
            foreach (var t in _toons)
            {
                if (!t.Active)
                {
                    if (!t.Terminating && !Exiting && !Hidden && _frame >= t.RespawnAt && !Spawn(t))
                        t.RespawnAt = _frame + 8;
                    continue;
                }
                if (IsFrozen(t)) continue;
                Step(t);
            }
            foreach (var t in _toons) Associate(t);
        }
        if (_frame % 8 == 0) FollowVirtualDesktop();
        Render();

        for (int i = _toons.Count - 1; i >= 0; i--)
        {
            var t = _toons[i];
            if (t.Terminating && !t.Active && !Exiting)
            {
                t.Win.Dispose();
                _toons.RemoveAt(i);
            }
        }
        if (Exiting && _toons.All(t => !t.Active)) AllGone?.Invoke();
    }

    private void Render()
    {
        foreach (var t in _toons)
        {
            if (!t.Active || Hidden || IsFrozen(t))
            {
                t.Win.Hide();
                continue;
            }
            var sheet = t.Def.Sheet;
            int row = sheet.Rows > 1 ? t.Dir : 0;
            t.Win.Draw(sheet, t.Frame % sheet.Frames, row, t.X, t.Y, Scale);
        }
    }

    // ----------------------------------------------------------------- helpers

    private int TW(Toon t) => t.Def.Width * Scale;
    private int TH(Toon t) => t.Def.Height * Scale;

    private bool BlockedOffset(Toon t, int dx, int dy) => _world.Blocked(t.X + dx, t.Y + dy, TW(t), TH(t), t.Ghosts);
    private bool BlockedDown(Toon t) => BlockedOffset(t, 0, 1);
    private bool BlockedSide(Toon t, int dir) => BlockedOffset(t, dir == 1 ? 1 : -1, 0);

    private bool IsFrozen(Toon t)
    {
        var s = _world.ScreenAt(t.X + TW(t) / 2, t.Y + TH(t) / 2);
        return s != null && s.Covered;
    }

    private static bool IsAction(Act a) => a >= Act.Action0 && a <= Act.Action5;
    private static bool IsDeath(Act a) => a is Act.Explosion or Act.Squashed or Act.Zapped or Act.Splatted;
    private static bool IsImmune(Act a) => IsDeath(a) || a is Act.Angel or Act.Exit;

    private void SetType(Toon t, Act type, int dir, Gravity g)
    {
        var od = t.Def;
        var nd = t.G[type];
        int ow = od.Width * Scale, oh = od.Height * Scale, nw = nd.Width * Scale, nh = nd.Height * Scale;
        switch (g)
        {
            case Gravity.Down:
                t.X += (ow - nw) / 2;
                t.Y += oh - nh;
                break;
            case Gravity.Here:
                t.X += (ow - nw) / 2;
                t.Y += (oh - nh) / 2;
                break;
        }
        t.Type = type;
        t.Dir = dir;
        t.Frame = 0;
        t.Cycle = 0;
        Stats.Entered[(int)type]++;
    }

    /// <summary>ToonAdvance: move one pixel at a time until something is in the way, then animate.</summary>
    private Status Advance(Toon t, bool animate)
    {
        var st = Status.Ok;
        if (t.U != 0 || t.V != 0)
        {
            if (t.Type == Act.Angel)
            {
                // Angels pass through windows; they only bounce off the sides of the desktop.
                t.X += t.U;
                t.Y += t.V;
                if (t.X < _world.Extent.L) { t.X = _world.Extent.L; st = Status.Blocked; }
                else if (t.X + TW(t) > _world.Extent.R) { t.X = _world.Extent.R - TW(t); st = Status.Blocked; }
            }
            else
            {
                int w = TW(t), h = TH(t), u = t.U, v = t.V;
                int n = Math.Max(Math.Abs(u), Math.Abs(v));
                int x0 = t.X, y0 = t.Y;
                for (int i = 1; i <= n; i++)
                {
                    int nx = x0 + u * i / n, ny = y0 + v * i / n;
                    if (_world.Blocked(nx, ny, w, h, t.Ghosts)) { st = Status.Blocked; break; }
                    t.X = nx;
                    t.Y = ny;
                }
            }
        }
        if (animate && ++t.Frame >= t.Def.Frames)
        {
            t.Frame = 0;
            t.Cycle++;
            if (st == Status.Ok && LoopDone(t)) st = Status.Finished;
        }
        return st;
    }

    private bool LoopDone(Toon t)
    {
        int loop = t.Def.Loop;
        if (loop > 0) return t.Cycle >= loop;
        if (loop < 0) return _rng.Next(-loop) == 0;
        return IsAction(t.Type) || IsDeath(t.Type) || t.Type == Act.Exit;   // loop 0: play once
    }

    // ----------------------------------------------------------------- state changes

    private bool Spawn(Toon t)
    {
        if (_world.Skies.Count == 0) return false;
        bool chute = t.G.Has(Act.Floater) && _rng.Next(5) == 0;
        var type = chute ? Act.Floater : Act.Faller;
        var d = t.G[type];
        int w = d.Width * Scale, h = d.Height * Scale;
        long total = _world.Skies.Sum(s => (long)Math.Max(0, s.W - w));
        if (total <= 0) return false;

        for (int attempt = 0; attempt < 6; attempt++)
        {
            long pick = (long)(_rng.NextDouble() * total);
            foreach (var sky in _world.Skies)
            {
                int span = Math.Max(0, sky.W - w);
                if (pick >= span) { pick -= span; continue; }
                int x = sky.L + (int)pick, y = sky.B - h + Scale;
                var below = _world.ScreenAt(x + w / 2, sky.B);
                if (below == null || below.Covered || _world.Blocked(x, y, w, h)) break;

                t.Type = type;
                t.Frame = t.Cycle = 0;
                t.X = x;
                t.Y = y;
                t.Dir = _rng.Next(2);
                t.U = (t.Dir * 2 - 1) * Scale;
                t.V = chute ? FloatSpeed(t) : d.Speed * Scale;
                t.Active = true;
                t.PrefDir = -1;
                t.PrefClimb = false;
                t.FallFrames = 0;
                t.ChuteDecided = chute;
                t.AssocHwnd = IntPtr.Zero;
                t.Ghosts = null;
                Stats.Spawns++;
                Stats.Entered[(int)type]++;
                return true;
            }
        }
        return false;
    }

    private void Deactivate(Toon t)
    {
        t.Active = false;
        t.AssocHwnd = IntPtr.Zero;
        t.Ghosts = null;
        t.RespawnAt = _frame + 10 + _rng.Next(40);
    }

    private void MakeWalker(Toon t, bool keepPosition)
    {
        var type = t.G.Has(Act.Runner) && _rng.Next(4) == 0 ? Act.Runner : Act.Walker;
        SetType(t, type, t.Dir, keepPosition ? Gravity.Still : Gravity.Down);
        t.U = t.G[type].Speed * Scale * (t.Dir * 2 - 1);
        t.V = 0;
        t.FallFrames = 0;
    }

    private void SwitchGait(Toon t, Act type)
    {
        SetType(t, type, t.Dir, Gravity.Down);
        t.U = t.G[type].Speed * Scale * (t.Dir * 2 - 1);
        t.V = 0;
    }

    private void MakeClimber(Toon t)
    {
        SetType(t, Act.Climber, t.Dir, Gravity.Still);
        t.U = 0;
        t.V = -t.G[Act.Climber].Speed * Scale;
    }

    private void MakeFaller(Toon t)
    {
        SetType(t, Act.Faller, t.Dir, Gravity.Still);
        t.U = (t.Dir * 2 - 1) * Scale;
        t.V = t.G[Act.Faller].Speed * Scale;
        t.FallFrames = 0;
        t.ChuteDecided = false;
    }

    private void MakeTumbler(Toon t)
    {
        SetType(t, Act.Tumbler, t.Dir, Gravity.Down);
        t.U = 0;
        t.V = t.G[Act.Tumbler].Speed * Scale;
        t.FallFrames = 0;
        t.ChuteDecided = false;
    }

    /// <summary>The floppy parachute drifts down at the floater's configured speed.</summary>
    private int FloatSpeed(Toon t) => Math.Max(1, t.G[Act.Floater].Speed) * Scale;

    private bool TryMakeFloater(Toon t)
    {
        if (!t.G.Has(Act.Floater)) return false;
        var fd = t.G[Act.Floater];
        int nw = fd.Width * Scale, nh = fd.Height * Scale;
        int nx = t.X + (TW(t) - nw) / 2, ny = t.Y + TH(t) - nh;
        if (_world.Blocked(nx, ny, nw, nh, t.Ghosts)) return false;
        SetType(t, Act.Floater, t.Dir, Gravity.Down);
        t.U = (t.Dir * 2 - 1) * Scale;
        t.V = FloatSpeed(t);
        t.ChuteDecided = true;
        Stats.Chutes++;
        return true;
    }

    private void Kill(Toon t, Act how)
    {
        var a = _settings.NoBlood || !t.G.Has(how) ? Act.Explosion : how;
        t.U = t.V = 0;
        if (!t.G.Has(a)) { AfterDeath(t); return; }
        SetType(t, a, t.Dir, a == Act.Explosion ? Gravity.Here : Gravity.Down);
    }

    private void AfterDeath(Toon t)
    {
        if (!t.Terminating && _settings.Angels && t.G.Has(Act.Angel))
        {
            SetType(t, Act.Angel, t.Dir, Gravity.Here);
            t.U = (_rng.Next(5) - 2) * Scale;
            t.V = -t.G[Act.Angel].Speed * Scale;
            t.AssocHwnd = IntPtr.Zero;
        }
        else Deactivate(t);
    }

    private void Zap(Toon t)
    {
        if (!_settings.Squish || !t.Active || IsImmune(t.Type) || Paused || Hidden) return;
        Stats.Zaps++;
        Kill(t, Act.Zapped);
    }

    // ----------------------------------------------------------------- behaviour

    private void Step(Toon t)
    {
        if (Squashed(t))
        {
            Stats.Squashes++;
            Kill(t, Act.Squashed);
            return;
        }

        var st = Advance(t, true);
        switch (t.Type)
        {
            case Act.Faller: StepFaller(t, st); break;
            case Act.Tumbler: StepTumbler(t, st); break;
            case Act.Walker:
            case Act.Runner: StepWalker(t, st); break;
            case Act.Climber: StepClimber(t, st); break;
            case Act.Floater: StepFloater(t, st); break;
            case Act.Angel: StepAngel(t, st); break;
            case Act.Exit:
                if (st == Status.Finished) t.Active = false;
                break;
            case Act.Explosion:
            case Act.Squashed:
            case Act.Zapped:
            case Act.Splatted:
                if (st == Status.Finished) AfterDeath(t);
                break;
            default:
                // Idle actions: finish, or give up if the ground went away.
                if (st == Status.Finished || !BlockedDown(t)) MakeWalker(t, false);
                break;
        }
    }

    private readonly List<IntPtr> _hits = new();

    /// <summary>
    /// XPenguins squashes a toon whenever it finds itself inside a window. Here that only happens
    /// when the window was already solid last frame, i.e. it was dragged or resized onto the toon.
    /// A window that just appeared (opened, restored, un-snapped) lets the toon carry on in front
    /// of it until it walks out the other side.
    /// </summary>
    private bool Squashed(Toon t)
    {
        int w = TW(t), h = TH(t);
        if (t.Ghosts != null)
        {
            t.Ghosts.RemoveAll(hw => !_world.WindowBoxes.TryGetValue(hw, out var b) || !b.Hits(t.X, t.Y, w, h));
            if (t.Ghosts.Count == 0) t.Ghosts = null;
        }
        if (IsImmune(t.Type)) return false;

        _world.Overlapping(t.X, t.Y, w, h, t.Ghosts, _hits);
        if (_hits.Count == 0) return _world.OffDesktop(t.X, t.Y, w, h);
        foreach (var hw in _hits)
        {
            if (!_world.PrevWindowBoxes.ContainsKey(hw)) continue;
            if (Log.File != null)
                Log.Write($"squash {t.G.Name} {t.Type} at ({t.X},{t.Y}) by 0x{hw.ToInt64():X} {_world.WindowBoxes[hw]}" +
                          $" (was {_world.PrevWindowBoxes[hw]})");
            return true;
        }
        (t.Ghosts ??= new List<IntPtr>()).AddRange(_hits);
        Stats.Ghosts++;
        return false;
    }

    private void StepFaller(Toon t, Status st)
    {
        t.FallFrames++;
        if (st != Status.Ok)
        {
            if (BlockedDown(t))
            {
                t.Dir = t.PrefDir >= 0 ? t.PrefDir : _rng.Next(2);
                MakeWalker(t, false);
                t.PrefDir = -1;
            }
            else if (!t.G.Has(Act.Climber) || _rng.Next(2) == 0)
            {
                t.U = -t.U;
                t.Dir = t.U > 0 ? 1 : 0;
            }
            else
            {
                t.Dir = t.U > 0 ? 1 : 0;
                MakeClimber(t);
            }
            return;
        }
        // Gravity. XPenguins keeps a faller at its configured speed unless the theme gives it an
        // acceleration; at 3 px a frame that is a twenty-second drop down a 4K screen, so a faller
        // with none borrows the tumbler's pull and terminal velocity.
        var d = t.G[Act.Faller];
        var pull = d.Acceleration > 0 || !t.G.Has(Act.Tumbler) ? d : t.G[Act.Tumbler];
        int accel = Math.Max(1, pull.Acceleration);
        int terminal = pull.TerminalVelocity > 0 ? pull.TerminalVelocity : 8;
        if (t.V < terminal * Scale) t.V = Math.Min(terminal * Scale, t.V + accel * Scale);
        MaybeOpenChute(t);
    }

    private void StepTumbler(Toon t, Status st)
    {
        t.FallFrames++;
        var d = t.G[Act.Tumbler];
        if (st != Status.Ok)
        {
            if (t.V >= d.TerminalVelocity * Scale && _rng.Next(3) == 0 &&
                (t.G.Has(Act.Splatted) || t.G.Has(Act.Explosion)))
            {
                Stats.Splats++;
                Kill(t, Act.Splatted);
            }
            else
            {
                t.Dir = t.PrefDir >= 0 ? t.PrefDir : _rng.Next(2);
                MakeWalker(t, false);
                t.PrefDir = -1;
            }
            return;
        }
        if (t.V < d.TerminalVelocity * Scale) t.V += d.Acceleration * Scale;
        MaybeOpenChute(t);
    }

    /// <summary>Once per long fall, a toon may pop its floppy disk open.</summary>
    private void MaybeOpenChute(Toon t)
    {
        if (t.ChuteDecided || t.FallFrames < 10) return;
        t.ChuteDecided = true;
        if (_rng.Next(5) != 0) return;
        if (_world.Blocked(t.X, t.Y, TW(t), TH(t) * 7, t.Ghosts)) return;   // not worth it this close to the ground
        TryMakeFloater(t);
    }

    private void StepWalker(Toon t, Status st)
    {
        if (st == Status.Blocked)
        {
            int u = t.U;
            if (!BlockedOffset(t, u, -_jump))
            {
                // A low step: hop up onto it.
                t.X += u;
                t.Y -= _jump;
                t.U = 0;
                t.V = _jump - 1;
                Advance(t, false);
                t.U = u;
                t.V = 0;
            }
            else if (t.G.Has(Act.Climber) && (t.PrefClimb || _rng.Next(4) == 0))
            {
                MakeClimber(t);
            }
            else
            {
                t.Dir = 1 - t.Dir;
                MakeWalker(t, false);
            }
        }
        else if (!BlockedDown(t))
        {
            // Try a small step down; if there's nothing within reach, it's a ledge.
            int u = t.U;
            t.U = 0;
            t.V = _jump;
            if (Advance(t, false) == Status.Ok)
            {
                t.PrefDir = t.Dir;
                t.PrefClimb = false;
                if (t.G.Has(Act.Tumbler)) MakeTumbler(t);
                else
                {
                    MakeFaller(t);
                    t.U = 0;
                }
            }
            else
            {
                t.U = u;
                t.V = 0;
            }
        }
        else if (t.Type == Act.Walker)
        {
            int actions = t.G.ActionCount;
            if (actions > 0 && _rng.Next(100) == 0)
            {
                SetType(t, Act.Action0 + _rng.Next(actions), t.Dir, Gravity.Down);
                t.U = t.V = 0;
            }
            else if (t.G.Has(Act.Runner) && _rng.Next(250) == 0) SwitchGait(t, Act.Runner);
        }
        else if (_rng.Next(60) == 0) SwitchGait(t, Act.Walker);
    }

    private void StepClimber(Toon t, Status st)
    {
        int d = t.Dir, ds = d * 2 - 1;
        if (_world.InSky(t.X, t.Y, TW(t), TH(t)))
        {
            // Reached the top of the screen: let go, and maybe open the floppy.
            t.Dir = 1 - d;
            t.PrefClimb = false;
            if (_rng.Next(2) != 0 || !TryMakeFloater(t)) MakeFaller(t);
        }
        else if (st == Status.Blocked)
        {
            int v = t.V;
            if (!BlockedOffset(t, ds * _jump, v))
            {
                t.X += ds * _jump;
                t.Y += v;
                t.U = -ds * (_jump - 1);
                t.V = 0;
                Advance(t, false);
                t.U = 0;
                t.V = v;
            }
            else
            {
                t.Dir = 1 - d;
                t.PrefClimb = false;
                MakeFaller(t);
            }
        }
        else if (!BlockedSide(t, d))
        {
            if (BlockedOffset(t, ds * _jump, 0))
            {
                // The wall steps out a little: shuffle across to it and keep climbing.
                t.U = ds * (_jump - 1);
                t.V = 0;
                Advance(t, false);
                t.U = 0;
                t.V = -t.G[Act.Climber].Speed * Scale;
            }
            else
            {
                // Over the top: walk onto it.
                MakeWalker(t, true);
                if (!BlockedOffset(t, ds * Scale, 0)) t.X += ds * Scale;
                t.PrefDir = d;
                t.PrefClimb = true;
            }
        }
    }

    private void StepFloater(Toon t, Status st)
    {
        if (st != Status.Blocked) return;
        if (BlockedDown(t))
        {
            if (t.U != 0) t.Dir = t.U > 0 ? 1 : 0;
            MakeWalker(t, false);
        }
        else
        {
            t.U = -t.U;
            t.Dir = 1 - t.Dir;
        }
    }

    private void StepAngel(Toon t, Status st)
    {
        if (st == Status.Blocked) t.U = -t.U;
        if (t.Y + TH(t) < _world.Extent.T || !_world.OnAnyScreen(t.X, t.Y, TW(t), TH(t))) Deactivate(t);
    }

    // ----------------------------------------------------------------- windows moving

    /// <summary>Toons ride along with the window they are standing on or climbing.</summary>
    private void Relocate()
    {
        foreach (var t in _toons)
        {
            if (!t.Active || t.AssocHwnd == IntPtr.Zero) continue;
            if (!_world.WindowBoxes.TryGetValue(t.AssocHwnd, out var nb))
            {
                t.AssocHwnd = IntPtr.Zero;
                continue;
            }
            var ob = t.AssocBox;
            if (nb.Equals(ob)) continue;
            bool moved = nb.W == ob.W && nb.H == ob.H;
            int dx, dy;
            switch (t.AssocKind)
            {
                case Assoc.WallLeft:
                    dx = nb.R - ob.R;
                    dy = moved ? nb.T - ob.T : 0;
                    break;
                case Assoc.WallRight:
                    dx = nb.L - ob.L;
                    dy = moved ? nb.T - ob.T : 0;
                    break;
                default:
                    dx = moved ? nb.L - ob.L : 0;
                    dy = nb.T - ob.T;
                    break;
            }
            t.X += dx;
            t.Y += dy;
            t.AssocBox = nb;
            Stats.Rides++;
        }
    }

    private void Associate(Toon t)
    {
        t.AssocHwnd = IntPtr.Zero;
        if (!t.Active) return;
        int w = TW(t), h = TH(t);
        switch (t.Type)
        {
            case Act.Walker:
            case Act.Runner:
            case Act.Splatted:
            case Act.Action0:
            case Act.Action1:
            case Act.Action2:
            case Act.Action3:
            case Act.Action4:
            case Act.Action5:
                t.AssocHwnd = _world.WindowBelow(t.X, t.Y, w, h);
                t.AssocKind = Assoc.Down;
                break;
            case Act.Climber:
                t.AssocHwnd = _world.WindowBeside(t.X, t.Y, w, h, t.Dir);
                t.AssocKind = t.Dir == 0 ? Assoc.WallLeft : Assoc.WallRight;
                break;
        }
        if (t.AssocHwnd != IntPtr.Zero) t.AssocBox = _world.WindowBoxes[t.AssocHwnd];
    }

    public IEnumerable<string> DescribeToons() =>
        _toons.Select(t => $"{t.G.Name,-10} {(t.Active ? t.Type.ToString() : "-"),-9} dir={t.Dir} pos=({t.X},{t.Y}) v=({t.U},{t.V})" +
                           (t.AssocHwnd != IntPtr.Zero ? $" on=0x{t.AssocHwnd.ToInt64():X}" : ""));

    public void Dispose()
    {
        foreach (var t in _toons) t.Win.Dispose();
        _toons.Clear();
    }
}
