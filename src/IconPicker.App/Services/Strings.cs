using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace IconPicker.App.Services;

/// <summary>UI text from Strings\en-US\Resources.resw, for text set in code. XAML uses x:Uid instead.</summary>
internal static class Strings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key)
    {
        return Loader.GetString(key);
    }

    public static string Format(string key, params object?[] arguments)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
    }

    /// <summary>The singular text for a count of 1, the plural one (with the count) otherwise.</summary>
    public static string Count(int count, string singularKey, string pluralKey)
    {
        return count == 1 ? Get(singularKey) : Format(pluralKey, count);
    }
}
