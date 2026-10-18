namespace IconPicker.Core.Builder;

/// <summary>What a built icon picked for a folder was made from, so its tile shows its name and Customize… reopens it as it was.</summary>
/// <param name="Source">The glyph it was drawn from, or null when a saved icon's copy of it can't be read.</param>
/// <param name="Style">Its look.</param>
/// <param name="Name">Its name.</param>
/// <param name="SavedIconId">Its id in My icons, or null when it wasn't saved.</param>
public sealed record BuiltIconOrigin(GlyphSource? Source, IconStyle Style, string Name, string? SavedIconId);
