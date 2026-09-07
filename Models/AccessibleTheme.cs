namespace ClinicalDataExplorer.Models;

public static class AccessibleTheme
{
    // These colors are used both as text on pale panels and behind white text.
    // Darken only the rendered palette; preserve the administrator's saved colors.
    public static string Color(string? value, string fallback)
    {
        if (value is null || value.Length != 7 || value[0] != '#' || !value.Skip(1).All(Uri.IsHexDigit)) value = fallback;
        var r = Convert.ToInt32(value.Substring(1, 2), 16);
        var g = Convert.ToInt32(value.Substring(3, 2), 16);
        var b = Convert.ToInt32(value.Substring(5, 2), 16);
        while (ContrastWithWhite(r, g, b) < 7)
        {
            r = (int)(r * .95); g = (int)(g * .95); b = (int)(b * .95);
        }
        return $"#{r:X2}{g:X2}{b:X2}";
    }
    private static double ContrastWithWhite(int r, int g, int b)
    {
        static double Linear(int channel) { var s = channel / 255d; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return 1.05 / (.2126 * Linear(r) + .7152 * Linear(g) + .0722 * Linear(b) + .05);
    }
}
