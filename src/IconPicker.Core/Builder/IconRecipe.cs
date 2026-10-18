using System.Text.Json.Serialization;
using IconPicker.Core.Library;

namespace IconPicker.Core.Builder;

/// <summary>What a built icon's glyph sits on.</summary>
public enum IconShape
{
    None,
    Square,
    Rounded,
    Circle,

    /// <summary>Windows' own folder, with the glyph on its front as an emblem. The background color doesn't apply.</summary>
    Folder,
}

/// <summary>How a built icon looks: its glyph's color, the shape behind it and how much of the shape the glyph fills.</summary>
/// <param name="IconColor">The color an SVG drawn in one color is recolored to (#RRGGBB), or null to keep its colors.</param>
/// <param name="Shape">The background shape.</param>
/// <param name="BackgroundColor">The shape's color (#RRGGBB); unused for <see cref="IconShape.None"/> and <see cref="IconShape.Folder"/>.</param>
/// <param name="IconScale">How much of the icon the glyph fills, from <see cref="MinimumScale"/> to <see cref="MaximumScale"/>.</param>
public sealed record IconStyle(string? IconColor, IconShape Shape, string BackgroundColor, double IconScale)
{
    public const double MinimumScale = 0.4;
    public const double MaximumScale = 1.0;

    public static IconStyle Default { get; } = new(null, IconShape.None, "#0078D4", DefaultScale(IconShape.None));

    /// <summary>The glyph fills the icon with no background, and sits well inside a shape or on the folder's front.</summary>
    public static double DefaultScale(IconShape shape)
    {
        return shape switch
        {
            IconShape.None => 1.0,
            IconShape.Folder => 0.55,
            _ => 0.6,
        };
    }

    /// <summary>Whether the background color is used.</summary>
    public bool UsesBackgroundColor => Shape is not (IconShape.None or IconShape.Folder);

    public double ClampedScale => Math.Clamp(double.IsFinite(IconScale) ? IconScale : 1.0, MinimumScale, MaximumScale);
}

/// <summary>A saved icon's recipe: its look, where its glyph came from, and when it was made and last used. Kept as recipe.json beside the source it was built from.</summary>
public sealed record IconRecipe
{
    public required string Name { get; init; }

    /// <summary>The source's file name in the saved icon's folder: source.svg, source.ico, source.png…</summary>
    public required string SourceFileName { get; init; }

    /// <summary>For a built-in icon, which one, so the builder can say so. Its SVG is the source file all the same.</summary>
    public string? BuiltInIconId { get; init; }

    public BuiltInIconStyle? BuiltInIconStyle { get; init; }

    public required IconStyle Style { get; init; }

    public DateTimeOffset Created { get; init; }

    public DateTimeOffset LastUsed { get; init; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(IconRecipe))]
internal sealed partial class IconRecipeJsonContext : JsonSerializerContext;
