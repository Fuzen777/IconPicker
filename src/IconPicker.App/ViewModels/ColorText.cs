using System.Globalization;
using Windows.UI;

namespace IconPicker.App.ViewModels;

/// <summary>Colors as the #RRGGBB text recipes and settings keep.</summary>
internal static class ColorText
{
    public static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static Color FromHex(string hexColor, Color fallback)
    {
        var digits = hexColor.TrimStart('#');
        return digits.Length == 6 && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : fallback;
    }
}
