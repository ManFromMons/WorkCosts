using System.Globalization;
using System.Text.RegularExpressions;

namespace WorkCosts.Helpers;

public static class DurationHelper
{
    private static readonly Regex HhMm = new(@"^\s*(\d{1,3})\s*:\s*(\d{1,2})\s*$", RegexOptions.Compiled);

    public static string ToDisplay(int totalMinutes)
    {
        if (totalMinutes < 0)
        {
            totalMinutes = 0;
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return $"{hours}:{minutes:D2}";
    }

    public static bool TryParse(string? text, out int totalMinutes)
    {
        totalMinutes = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var match = HhMm.Match(text);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours))
        {
            return false;
        }

        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return false;
        }

        if (minutes is < 0 or > 59 || hours < 0)
        {
            return false;
        }

        totalMinutes = (hours * 60) + minutes;
        return true;
    }
}
