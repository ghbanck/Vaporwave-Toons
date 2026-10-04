using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace VaporwaveToons;

/// <summary>User preferences, kept as key=value lines in %APPDATA%\VaporwaveToons\settings.ini.</summary>
internal sealed class Settings
{
    public int Count;                       // 0 = the theme's own "number" totals
    public int Scale;                       // 0 = automatic
    public int SpeedPercent = 100;
    public bool Squish;                     // click a toon to zap it
    public bool NoBlood;                    // every death is the tame explosion
    public bool Angels = true;
    public bool WalkOverMaximized = true;
    public bool HideInFullscreen = true;
    public bool Welcomed;
    public string Language = "";            // "en", "pt", or empty for the Windows display language
    public HashSet<string> Disabled = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Command-line overrides are in effect; don't write them back to disk.</summary>
    public bool Transient;

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VaporwaveToons");

    public static string FilePath => Path.Combine(Folder, "settings.ini");

    public bool IsEnabled(string genus) => !Disabled.Contains(genus);

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            if (!File.Exists(FilePath)) return s;
            foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string k = raw.Substring(0, eq).Trim().ToLowerInvariant();
                string v = raw.Substring(eq + 1).Trim();
                switch (k)
                {
                    case "count": s.Count = Int(v, 0, 0, 200); break;
                    case "scale": s.Scale = Int(v, 0, 0, 6); break;
                    case "speed": s.SpeedPercent = Int(v, 100, 25, 400); break;
                    case "squish": s.Squish = v == "1"; break;
                    case "noblood": s.NoBlood = v == "1"; break;
                    case "angels": s.Angels = v != "0"; break;
                    case "walkovermaximized": s.WalkOverMaximized = v != "0"; break;
                    case "hideinfullscreen": s.HideInFullscreen = v != "0"; break;
                    case "welcomed": s.Welcomed = v == "1"; break;
                    case "lang": s.Language = v is "en" or "pt" ? v : ""; break;
                    case "disabled":
                        foreach (var n in v.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0)) s.Disabled.Add(n);
                        break;
                }
            }
        }
        catch (Exception)
        {
            // A broken settings file just means defaults.
        }
        return s;
    }

    public void Save()
    {
        if (Transient) return;
        try
        {
            Directory.CreateDirectory(Folder);
            var lines = new List<string>
            {
                "# Vaporwave Toons settings. Delete this file to go back to the defaults.",
                $"count={Count}",
                $"scale={Scale}",
                $"speed={SpeedPercent}",
                $"squish={(Squish ? 1 : 0)}",
                $"noblood={(NoBlood ? 1 : 0)}",
                $"angels={(Angels ? 1 : 0)}",
                $"walkovermaximized={(WalkOverMaximized ? 1 : 0)}",
                $"hideinfullscreen={(HideInFullscreen ? 1 : 0)}",
                $"welcomed={(Welcomed ? 1 : 0)}",
                $"lang={Language}",
                $"disabled={string.Join(",", Disabled.OrderBy(x => x))}",
            };
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(false));
        }
        catch (Exception)
        {
            // Read-only profile or similar: preferences just won't stick.
        }
    }

    private static int Int(string v, int dflt, int lo, int hi) =>
        int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? Theme.Clamp(n, lo, hi) : dflt;
}
