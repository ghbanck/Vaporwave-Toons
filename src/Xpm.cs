using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VaporwaveToons;

/// <summary>An ARGB image. Pixels are either fully opaque or fully transparent (0).</summary>
internal sealed class PixelImage
{
    public readonly int Width, Height;
    public readonly int[] Pixels;

    public PixelImage(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new int[width * height];
    }
}

/// <summary>
/// XPM3 reader. Handles any chars-per-pixel, C comments, the <c>c</c>/<c>m</c>/<c>g</c>/<c>s</c>
/// colour contexts, <c>None</c>, #RGB / #RRGGBB / #RRRRGGGGBBBB and the common X11 colour names.
/// </summary>
internal static class Xpm
{
    public static PixelImage Parse(string text, string nameForErrors)
    {
        var s = QuotedStrings(text);
        if (s.Count == 0) throw new FormatException($"{nameForErrors}: no XPM data");

        var head = s[0].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (head.Length < 4) throw new FormatException($"{nameForErrors}: bad XPM header \"{s[0]}\"");
        int w = int.Parse(head[0], CultureInfo.InvariantCulture);
        int h = int.Parse(head[1], CultureInfo.InvariantCulture);
        int ncolors = int.Parse(head[2], CultureInfo.InvariantCulture);
        int cpp = int.Parse(head[3], CultureInfo.InvariantCulture);
        if (w <= 0 || h <= 0 || cpp <= 0 || ncolors <= 0)
            throw new FormatException($"{nameForErrors}: bad XPM header \"{s[0]}\"");
        if (s.Count < 1 + ncolors + h)
            throw new FormatException($"{nameForErrors}: expected {ncolors} colours and {h} rows, file is short");

        var table = new Dictionary<string, int>(ncolors, StringComparer.Ordinal);
        var fast = cpp == 1 ? new int[65536] : null;
        var known = cpp == 1 ? new bool[65536] : null;
        for (int i = 0; i < ncolors; i++)
        {
            string line = s[1 + i];
            if (line.Length < cpp) throw new FormatException($"{nameForErrors}: bad colour line \"{line}\"");
            string key = line.Substring(0, cpp);
            int argb = ParseColourSpec(line.Substring(cpp));
            table[key] = argb;
            if (fast != null) { fast[key[0]] = argb; known[key[0]] = true; }
        }

        var img = new PixelImage(w, h);
        var px = img.Pixels;
        for (int y = 0; y < h; y++)
        {
            string row = s[1 + ncolors + y];
            if (row.Length < w * cpp)
                throw new FormatException($"{nameForErrors}: row {y} is {row.Length / cpp} px, expected {w}");
            int o = y * w;
            if (fast != null)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = row[x];
                    if (!known[c]) throw new FormatException($"{nameForErrors}: unknown pixel char '{c}' in row {y}");
                    px[o + x] = fast[c];
                }
            }
            else
            {
                for (int x = 0; x < w; x++)
                {
                    string key = row.Substring(x * cpp, cpp);
                    if (!table.TryGetValue(key, out int argb))
                        throw new FormatException($"{nameForErrors}: unknown pixel \"{key}\" in row {y}");
                    px[o + x] = argb;
                }
            }
        }
        return img;
    }

    /// <summary>Every "..." literal in the file, with C comments skipped.</summary>
    private static List<string> QuotedStrings(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        int i = 0, n = text.Length;
        while (i < n)
        {
            char c = text[i];
            if (c == '/' && i + 1 < n && text[i + 1] == '*')
            {
                int end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
            }
            else if (c == '/' && i + 1 < n && text[i + 1] == '/')
            {
                int end = text.IndexOf('\n', i);
                i = end < 0 ? n : end + 1;
            }
            else if (c == '"')
            {
                sb.Clear();
                i++;
                while (i < n && text[i] != '"')
                {
                    if (text[i] == '\\' && i + 1 < n) i++;
                    sb.Append(text[i]);
                    i++;
                }
                i++;
                list.Add(sb.ToString());
            }
            else i++;
        }
        return list;
    }

    private static bool IsContextKey(string t) => t == "c" || t == "m" || t == "s" || t == "g" || t == "g4";

    private static int ParseColourSpec(string spec)
    {
        var tok = spec.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        string colour = null, fallback = null;
        for (int i = 0; i < tok.Length; i++)
        {
            if (!IsContextKey(tok[i])) continue;
            string key = tok[i];
            var val = new StringBuilder();
            int j = i + 1;
            while (j < tok.Length && !IsContextKey(tok[j]))
            {
                if (val.Length > 0) val.Append(' ');
                val.Append(tok[j]);
                j++;
            }
            if (key == "c") colour = val.ToString();
            else if (key != "s" && fallback == null) fallback = val.ToString();
            i = j - 1;
        }
        return ColourFromName(colour ?? fallback ?? "None");
    }

    public static int ColourFromName(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Equals("none", StringComparison.OrdinalIgnoreCase)) return 0;
        if (name[0] == '#')
        {
            string hex = name.Substring(1);
            int per = hex.Length / 3;
            if (per >= 1 && per <= 4 && hex.Length == per * 3 &&
                int.TryParse(hex.Substring(0, per), NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.Substring(per, per), NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.Substring(2 * per, per), NumberStyles.HexNumber, null, out int b))
            {
                return Pack(Scale(r, per), Scale(g, per), Scale(b, per));
            }
            return Pack(255, 0, 255);
        }
        string key = name.Replace(" ", "").ToLowerInvariant();
        if (Named.TryGetValue(key, out int rgb)) return unchecked((int)0xFF000000) | rgb;
        if ((key.StartsWith("gray") || key.StartsWith("grey")) &&
            int.TryParse(key.Substring(4), NumberStyles.Integer, CultureInfo.InvariantCulture, out int pct) &&
            pct >= 0 && pct <= 100)
        {
            int v = (pct * 255 + 50) / 100;
            return Pack(v, v, v);
        }
        return Pack(255, 0, 255);   // unknown name: loud magenta, so it gets noticed
    }

    // Scale an n-hex-digit channel to 8 bits: #F -> FF, #FFFF -> FF.
    private static int Scale(int v, int digits) => digits switch
    {
        1 => v * 17,
        2 => v,
        3 => v >> 4,
        _ => v >> 8,
    };

    private static int Pack(int r, int g, int b) => unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;

    private static readonly Dictionary<string, int> Named = new()
    {
        ["black"] = 0x000000, ["white"] = 0xFFFFFF, ["red"] = 0xFF0000, ["green"] = 0x00FF00,
        ["blue"] = 0x0000FF, ["yellow"] = 0xFFFF00, ["cyan"] = 0x00FFFF, ["magenta"] = 0xFF00FF,
        ["gray"] = 0xBEBEBE, ["grey"] = 0xBEBEBE, ["darkgray"] = 0xA9A9A9, ["darkgrey"] = 0xA9A9A9,
        ["lightgray"] = 0xD3D3D3, ["lightgrey"] = 0xD3D3D3, ["dimgray"] = 0x696969, ["dimgrey"] = 0x696969,
        ["orange"] = 0xFFA500, ["pink"] = 0xFFC0CB, ["purple"] = 0xA020F0, ["brown"] = 0xA52A2A,
        ["navy"] = 0x000080, ["navyblue"] = 0x000080, ["gold"] = 0xFFD700, ["silver"] = 0xC0C0C0,
        ["darkred"] = 0x8B0000, ["darkgreen"] = 0x006400, ["darkblue"] = 0x00008B, ["skyblue"] = 0x87CEEB,
        ["lightblue"] = 0xADD8E6, ["salmon"] = 0xFA8072, ["tan"] = 0xD2B48C, ["beige"] = 0xF5F5DC,
        ["khaki"] = 0xF0E68C, ["violet"] = 0xEE82EE, ["maroon"] = 0xB03060, ["orchid"] = 0xDA70D6,
        ["wheat"] = 0xF5DEB3, ["turquoise"] = 0x40E0D0, ["coral"] = 0xFF7F50, ["tomato"] = 0xFF6347,
        ["ivory"] = 0xFFFFF0, ["snow"] = 0xFFFAFA, ["lavender"] = 0xE6E6FA, ["plum"] = 0xDDA0DD,
    };
}
