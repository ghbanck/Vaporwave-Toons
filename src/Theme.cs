using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace VaporwaveToons;

/// <summary>The 18 activities XPenguins knows about, in its own order.</summary>
internal enum Act
{
    Walker, Faller, Tumbler, Floater, Climber, Runner,
    Action0, Action1, Action2, Action3, Action4, Action5,
    Explosion, Squashed, Zapped, Splatted, Angel, Exit,
}

/// <summary>One <c>define</c> block of a theme config.</summary>
internal sealed class ActDef
{
    public string Pixmap;
    public int Width = 30, Height = 30, Frames = 1, Directions = 1, Speed = 4;
    public int Acceleration, TerminalVelocity, Loop;
    public Sheet Sheet;

    public ActDef Clone() => (ActDef)MemberwiseClone();
}

/// <summary>One <c>toon</c> block: a kind of creature with its own set of activities.</summary>
internal sealed class Genus
{
    public string Name;
    public int Number = 1;
    public readonly ActDef[] Acts = new ActDef[Theme.ActCount];

    public bool Has(Act a) => Acts[(int)a] != null;
    public ActDef this[Act a] => Acts[(int)a];

    /// <summary>XPenguins only uses action N if actions 0..N-1 all exist.</summary>
    public int ActionCount
    {
        get
        {
            int n = 0;
            while (n < 6 && Has(Act.Action0 + n)) n++;
            return n;
        }
    }
}

/// <summary>
/// A decoded sprite sheet: frames left to right, direction rows top to bottom.
/// When a sheet has a single row drawn facing right, a mirrored row is synthesised
/// so row 0 always faces left and row 1 faces right, the same as walker sheets.
/// </summary>
internal sealed class Sheet
{
    public readonly int FrameW, FrameH, Frames, Rows;
    public readonly int[] Px;
    public int Stride => FrameW * Frames;

    public Sheet(PixelImage img, int frameW, int frameH, int frames, int rows, bool synthMirror)
    {
        FrameW = frameW;
        FrameH = frameH;
        Frames = frames;
        Rows = synthMirror ? 2 : rows;
        Px = new int[Stride * FrameH * Rows];
        for (int r = 0; r < rows; r++)
        {
            int dstRow = synthMirror ? 1 : r;
            for (int y = 0; y < frameH; y++)
            {
                Array.Copy(img.Pixels, (r * frameH + y) * img.Width, Px, (dstRow * frameH + y) * Stride,
                    Math.Min(Stride, img.Width));
            }
        }
        if (synthMirror)
        {
            for (int f = 0; f < frames; f++)
                for (int y = 0; y < frameH; y++)
                {
                    int src = (frameH + y) * Stride + f * frameW;
                    int dst = y * Stride + f * frameW;
                    for (int x = 0; x < frameW; x++) Px[dst + x] = Px[src + frameW - 1 - x];
                }
        }
    }
}

/// <summary>Where theme files come from: a folder on disk or the zip embedded in the exe.</summary>
internal interface IThemeSource
{
    string Describe { get; }
    bool Exists(string name);
    byte[] Read(string name);
}

internal sealed class FolderSource : IThemeSource
{
    private readonly string _dir;
    public FolderSource(string dir) { _dir = dir; }
    public string Describe => _dir;
    public bool Exists(string name) => File.Exists(Path.Combine(_dir, name));
    public byte[] Read(string name) => File.ReadAllBytes(Path.Combine(_dir, name));
}

internal sealed class EmbeddedSource : IThemeSource
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public EmbeddedSource()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("theme.zip")
            ?? throw new InvalidOperationException("embedded theme missing");
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var e in zip.Entries)
        {
            if (e.Length == 0 && e.FullName.EndsWith("/")) continue;
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            _files[e.Name] = ms.ToArray();
        }
    }

    public string Describe => "(embedded in the executable)";
    public bool Exists(string name) => _files.ContainsKey(Path.GetFileName(name));
    public byte[] Read(string name) => _files[Path.GetFileName(name)];
}

internal sealed class Theme
{
    public const int ActCount = 18;

    public static readonly Dictionary<string, Act> ActNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["walker"] = Act.Walker, ["faller"] = Act.Faller, ["tumbler"] = Act.Tumbler,
        ["floater"] = Act.Floater, ["climber"] = Act.Climber, ["runner"] = Act.Runner,
        ["action0"] = Act.Action0, ["action1"] = Act.Action1, ["action2"] = Act.Action2,
        ["action3"] = Act.Action3, ["action4"] = Act.Action4, ["action5"] = Act.Action5,
        ["explosion"] = Act.Explosion, ["squashed"] = Act.Squashed, ["zapped"] = Act.Zapped,
        ["splatted"] = Act.Splatted, ["angel"] = Act.Angel, ["exit"] = Act.Exit,
    };

    public string Name;
    public int Delay = 60;
    public readonly List<Genus> Genera = new();
    public readonly List<string> Warnings = new();
    public string About = "";
    public int PixmapCount;

    /// <summary>Largest frame in the theme, in theme pixels.</summary>
    public int MaxFrameW, MaxFrameH;

    public static Theme Load(IThemeSource src, string name)
    {
        if (!src.Exists("config")) throw new FileNotFoundException($"no config file in {src.Describe}");
        var t = new Theme { Name = name };
        t.ParseConfig(Encoding.UTF8.GetString(src.Read("config")));
        if (src.Exists("about")) t.About = Encoding.UTF8.GetString(src.Read("about"));
        t.LoadSheets(src);
        return t;
    }

    private void ParseConfig(string text)
    {
        var tokens = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            string line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line.Substring(0, hash);
            tokens.AddRange(line.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        }

        var defaults = new ActDef();
        Genus genus = null;
        ActDef current = null;

        Genus EnsureGenus()
        {
            if (genus == null)
            {
                genus = new Genus { Name = Name };
                Genera.Add(genus);
            }
            return genus;
        }

        int i = 0;
        string Next() => i < tokens.Count ? tokens[i++] : "";
        int NextInt()
        {
            string v = Next();
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            Warnings.Add($"invalid number \"{v}\"");
            return 0;
        }

        while (i < tokens.Count)
        {
            string key = tokens[i++].ToLowerInvariant();
            var target = current ?? defaults;
            switch (key)
            {
                case "delay": Delay = Math.Max(10, NextInt()); break;
                case "toon":
                    genus = new Genus { Name = Next() };
                    Genera.Add(genus);
                    current = null;
                    break;
                case "number": EnsureGenus().Number = Math.Max(0, NextInt()); break;
                case "define":
                {
                    string what = Next();
                    if (what.Equals("default", StringComparison.OrdinalIgnoreCase)) current = defaults;
                    else if (ActNames.TryGetValue(what, out var act))
                    {
                        current = defaults.Clone();
                        current.Pixmap = null;
                        EnsureGenus().Acts[(int)act] = current;
                    }
                    else
                    {
                        Warnings.Add($"unknown type \"{what}\": ignoring");
                        current = new ActDef();   // swallow its properties
                    }
                    break;
                }
                case "pixmap": target.Pixmap = Next(); break;
                case "width": target.Width = NextInt(); break;
                case "height": target.Height = NextInt(); break;
                case "frames": target.Frames = NextInt(); break;
                case "directions": target.Directions = NextInt(); break;
                case "speed": target.Speed = NextInt(); break;
                case "acceleration": target.Acceleration = NextInt(); break;
                case "terminal_velocity": target.TerminalVelocity = NextInt(); break;
                case "loop": target.Loop = NextInt(); break;
                default:
                    Warnings.Add($"unknown keyword \"{key}\"");
                    Next();
                    break;
            }
        }
    }

    private void LoadSheets(IThemeSource src)
    {
        var images = new Dictionary<string, PixelImage>(StringComparer.OrdinalIgnoreCase);
        var sheets = new Dictionary<string, Sheet>();

        foreach (var g in Genera)
        {
            for (int a = 0; a < ActCount; a++)
            {
                var d = g.Acts[a];
                if (d == null) continue;
                if (string.IsNullOrEmpty(d.Pixmap) || !src.Exists(d.Pixmap))
                {
                    Warnings.Add($"{g.Name}/{(Act)a}: cannot read {d.Pixmap}");
                    g.Acts[a] = null;
                    continue;
                }
                if (!images.TryGetValue(d.Pixmap, out var img))
                {
                    try
                    {
                        img = Xpm.Parse(Encoding.GetEncoding("ISO-8859-1").GetString(src.Read(d.Pixmap)), d.Pixmap);
                    }
                    catch (Exception ex)
                    {
                        Warnings.Add(ex.Message);
                        g.Acts[a] = null;
                        continue;
                    }
                    images[d.Pixmap] = img;
                }

                // Trust the image over the config when they disagree, like XPenguins does.
                d.Width = Clamp(d.Width, 1, img.Width);
                d.Height = Clamp(d.Height, 1, img.Height);
                int frames = Clamp(d.Frames, 1, img.Width / d.Width);
                int rows = Clamp(d.Directions, 1, Math.Min(2, img.Height / d.Height));
                if (frames != d.Frames || rows != d.Directions)
                    Warnings.Add($"{g.Name}/{(Act)a}: {d.Pixmap} has {frames} frame(s) x {rows} direction(s)");
                else if (img.Width != d.Width * frames || img.Height != d.Height * rows)
                    Warnings.Add($"{g.Name}/{(Act)a}: {d.Pixmap} is {img.Width}x{img.Height}, " +
                                 $"expected {d.Width * frames}x{d.Height * rows}");
                d.Frames = frames;
                d.Directions = rows;

                bool mirror = rows == 1 && (Act)a != Act.Explosion;
                string key = $"{d.Pixmap}|{d.Width}|{d.Height}|{frames}|{rows}|{mirror}";
                if (!sheets.TryGetValue(key, out var sheet))
                {
                    sheet = new Sheet(img, d.Width, d.Height, frames, rows, mirror);
                    sheets[key] = sheet;
                }
                d.Sheet = sheet;
                MaxFrameW = Math.Max(MaxFrameW, d.Width);
                MaxFrameH = Math.Max(MaxFrameH, d.Height);
            }
        }

        foreach (var g in Genera.ToList())
        {
            if (!g.Has(Act.Walker) || !g.Has(Act.Faller))
            {
                Warnings.Add($"toon \"{g.Name}\" needs a walker and a faller: ignored");
                Genera.Remove(g);
            }
        }
        PixmapCount = images.Count;
        if (Genera.Count == 0) throw new InvalidDataException("the theme has no usable toons");
    }

    public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

    /// <summary>A "key value" line from the about file, or null.</summary>
    public string AboutField(string field)
    {
        foreach (var raw in About.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(field + " ", StringComparison.OrdinalIgnoreCase))
                return line.Substring(field.Length).Trim();
        }
        return null;
    }
}
