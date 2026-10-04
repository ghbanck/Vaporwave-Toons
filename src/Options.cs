using System;
using System.Globalization;
using System.IO;

namespace VaporwaveToons;

/// <summary>Command-line switches, in the spirit of xpenguins' own.</summary>
internal sealed class Options
{
    public int? Toons, Scale, Speed;
    public bool? Squish, NoBlood, Angels;
    public string ThemeDir;
    public int Seconds;                 // quit (with the power-off animation) after this long
    public string LogFile;
    public bool ListWindows, SelfTest, Help;

    public bool OverridesSettings => Toons.HasValue || Scale.HasValue || Speed.HasValue ||
                                     Squish.HasValue || NoBlood.HasValue || Angels.HasValue;

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException(Strings.MissingValue(a));
            int NextInt() => int.Parse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture);
            switch (a)
            {
                case "-n": case "--toons": case "--penguins": o.Toons = Theme.Clamp(NextInt(), 1, 200); break;
                case "--scale": o.Scale = Theme.Clamp(NextInt(), 1, 6); break;
                case "--speed": o.Speed = Theme.Clamp(NextInt(), 25, 400); break;
                case "--squish": o.Squish = true; break;
                case "--no-blood": case "--noblood": o.NoBlood = true; break;
                case "--no-angels": case "--noangels": o.Angels = false; break;
                case "-t": case "--theme": o.ThemeDir = Path.GetFullPath(Next()); break;
                case "--lang": SetLanguage(Next()); break;
                case "--seconds": o.Seconds = Theme.Clamp(NextInt(), 1, 86400); break;
                case "--log": o.LogFile = Path.GetFullPath(Next()); break;
                case "--list-windows": o.ListWindows = true; break;
                case "--selftest": o.SelfTest = true; break;
                case "-h": case "--help": case "/?": o.Help = true; break;
                default: throw new ArgumentException(Strings.UnknownOption(args[i]));
            }
        }
        return o;
    }

    private static void SetLanguage(string lang)
    {
        switch (lang.ToLowerInvariant())
        {
            case "en": Strings.Use("en"); break;
            case "pt": case "pt-br": Strings.Use("pt"); break;
            default: throw new ArgumentException(Strings.UnknownLanguage(lang));
        }
    }

    public void ApplyTo(Settings s)
    {
        if (Toons.HasValue) s.Count = Toons.Value;
        if (Scale.HasValue) s.Scale = Scale.Value;
        if (Speed.HasValue) s.SpeedPercent = Speed.Value;
        if (Squish.HasValue) s.Squish = Squish.Value;
        if (NoBlood.HasValue) s.NoBlood = NoBlood.Value;
        if (Angels.HasValue) s.Angels = Angels.Value;
        if (OverridesSettings) s.Transient = true;
    }
}

internal static class Log
{
    public static string File;

    public static void Write(string line)
    {
        if (File == null) return;
        try { System.IO.File.AppendAllText(File, $"[{DateTime.Now:HH:mm:ss.fff}] {line}\r\n"); }
        catch (IOException) { }
    }
}
