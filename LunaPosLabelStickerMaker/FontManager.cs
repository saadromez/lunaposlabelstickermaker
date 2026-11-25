using System.Drawing;
using System.Drawing.Text;

namespace LunaPosLabelStickerMaker;

public static class FontManager
{
    private static List<string>? _cachedFontNames;

    public static List<string> GetInstalledFontNames()
    {
        if (_cachedFontNames != null) return _cachedFontNames;

        try
        {
            using var installedFonts = new InstalledFontCollection();
            var allNames = installedFonts.Families
                .Select(f => f.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(n => n)
                .ToList();

            // Highlight top Sinhala and standard packaging fonts
            var priorityFonts = new[]
            {
                "Nirmala UI",
                "Iskoola Pota",
                "FMAbhaya",
                "FMBindumathi",
                "FMDerana",
                "FMGanganee",
                "FMSamantha",
                "Segoe UI",
                "Arial",
                "Calibri",
                "Tahoma"
            };

            var result = new List<string>();
            foreach (var pf in priorityFonts)
            {
                if (allNames.Contains(pf, StringComparer.OrdinalIgnoreCase) && !result.Contains(pf, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(pf);
                }
            }

            foreach (var f in allNames)
            {
                if (!result.Contains(f, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(f);
                }
            }

            _cachedFontNames = result;
            return _cachedFontNames;
        }
        catch
        {
            return ["Nirmala UI", "Iskoola Pota", "Segoe UI", "Arial"];
        }
    }

    public static Font CreateFont(string familyName, float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(familyName))
            {
                var font = new Font(familyName, size, style, unit);
                if (font.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase) ||
                    font.FontFamily.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                {
                    return font;
                }
                font.Dispose();
            }
        }
        catch { }

        // Fallbacks
        string[] fallbacks = ["Nirmala UI", "Iskoola Pota", "Segoe UI", "Arial"];
        foreach (var fb in fallbacks)
        {
            try
            {
                return new Font(fb, size, style, unit);
            }
            catch { }
        }

        return new Font(FontFamily.GenericSansSerif, size, style, unit);
    }
}
