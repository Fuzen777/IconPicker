# Icon Builder

> [!abstract]
> A new **My icons** tab where you build reusable folder icons: pick an SVG (from a built-in icon library or any file), color it, and set it on a background shape, including the Windows folder itself with the icon as an emblem. Built icons are saved as editable recipes and appear in a new picker next to Browse, above the built-in library. Browsing to an SVG opens the same builder. Any other icon (a picture, an `.ico`, an exe icon) can also be put on a background and saved.

> [!info]
> Implemented on 2026-10-18 in four commits (SVG icons, the builder groundwork, the My icons tab, the builder in the picker). How the SVG parts work is in [SVG Rendering](../Docs/SVG%20Rendering.md). Where the build differs from this spec:
> 1. The bundled set is called **Built-in icons** on screen, since "library" already means a folder rescanned often; the picker button is **Icons…**.
> 2. One dialog with two states (the icon list, then the builder) stands for the picker and the builder dialog, since WinUI shows one dialog at a time.
> 3. An exe or dll icon is saved as its extracted `source.ico`, not the whole executable.
> 4. An icon used for a folder without being saved is written to `%LocalAppData%\Icon Picker\Built` (deleted after a day) for the folder to copy.
> 5. An SVG tile shows "Scalable" instead of a pixel size; for ranking it reports 1,024 px.
> 6. Whether an SVG draws anything is only checked for the first 50 candidates; a search opens at most 100 SVGs and skips those over 1 MB.
> 7. Customize… starts on a rounded square, and on an icon built earlier reopens it as it was.
> 8. Shapes keep a thin margin (4% of the icon), like Windows' own icons.
> 9. A recipe keeps its source as `source.<ext>` (a built-in icon's SVG as `source.svg`) with the built-in icon's id and style when it came from one, and no icon index (an exe's icon is already extracted).
> 10. Built-in tiles show the bare glyph in the theme's text color; a picked one starts white on an accent-colored rounded square.
> 11. SVGs with masks count as several colors and keep them, since recoloring would fill in their cut-outs.
> 12. A saved icon picked for a folder is copied among the built icons at that moment, so saving changes to it later doesn't alter picks already made.

## Goal

Icon Picker gives folders the icon of what is inside them, which works for games and apps but leaves every other folder (Music, Projects, Photos, a modpack) with nothing good to pick. SVG icon sets solve that, but an SVG glyph alone is a black shape on transparency: it needs a color and usually a background to read as a folder icon. The builder turns any SVG into a finished, consistent icon, keeps it for reuse, and lets the same styling dress up icons that already exist.

## Resolved Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Where it lives | A new "My icons" tab: the builder plus your saved icons | User's design |
| Builder input | An SVG, or any icon the app can already use (picture, `.ico`, exe or dll icon) | User's design; "background for any icon" |
| Recoloring | Single-color SVGs only, as clean vector shapes; multi-color SVGs keep their colors | User's choice |
| Backgrounds | None, square, rounded square, circle, or the Windows folder with the icon as an emblem; for any icon | User's choice |
| Saving | Editable recipe (a copy of the source plus its settings) with the finished `.ico` cached beside it | User's choice; reopen and tweak later, re-render cleanly at every size |
| Built-in library | Fluent UI System Icons (Microsoft, MIT), searchable | User's choice; matches the Windows 11 look |
| Picking from the library | Opens the builder prefilled, to choose colors and a shape; saving is optional | User's choice; library icons are bare glyphs |
| New picker | A button next to Browse: your saved icons on top, the library below | User's design |
| Browsing to an SVG | Opens the builder, with an option to save the result | User's design |
| Renderer | Svg.Skia with SkiaSharp | Rendered 37 of 40 real SVGs from this PC correctly against 20 of 40 for Windows' built-in Direct2D (see Evidence) |
| Spec location | `DOCUMENTATION/Specs/Icon Builder.md` | User's choice |

## Assumptions

> [!info]
> Defaults taken without asking. Any of them can be overridden.

### Builder

1. The color picker for the icon only appears when the SVG is single-color: the app renders it once and checks that every visible pixel has the same color (with a small tolerance for anti-aliasing). Otherwise the builder says the SVG has several colors and keeps them.
2. Recoloring edits the SVG before rendering: every solid paint (in `fill`, `stroke`, `stop-color` and `color` attributes, in `style=""` and in `<style>` rules) becomes the picked color, `none` and gradient references stay, and the root gets the color too. This handles `currentColor`, plain fills, CSS classes and SVGs with no color at all, and also brings back template SVGs that draw nothing until colored (`fill="context-fill"`, `_COLOR_VAR_`).
3. Colors are chosen with Windows' own color picker, plus a row of preset swatches and your last 8 colors (kept in settings).
4. An **Icon size** slider sets how much of the background the icon fills (40 to 100%), with a default per shape: 100% with no background, 60% on a shape, about 55% as a folder emblem.
5. The rounded square uses one fixed corner radius, close to Windows' own app tiles; the circle and square need no setting.
6. The **Windows folder** shape uses Windows' own folder image (the one already shown for folders without an icon) at each size, with the icon centered on its front. The background color doesn't apply to it, so the folder keeps its Windows look.
7. Every size of the finished `.ico` (256, 128, 64, 48, 32, 24 and 16 px) is rendered from the vector, not shrunk from the 256 px image, so small sizes stay sharp.
8. A preview shows the result at 256 px next to 48, 32 and 16 px, on light and dark backdrops.

### Opening the Builder

9. From the **My icons** tab: **New icon** opens it empty, with Library and Browse as sources. Opening a saved icon reopens it with its settings, to edit or duplicate.
10. From a folder's picker (single mode or the batch side panel): it opens as a large dialog with **Use for this folder** and a **Save to My icons** checkbox. Choosing an SVG with Browse, picking a library icon, or choosing **Customize…** on any selected tile all open it.
11. SVG files found inside a folder are candidates like pictures, ranked with them (a square `viewBox` counts as square and sharp), and apply as drawn; **Customize…** on the selected tile opens the builder with them.

### My Icons

12. Saved icons are stored in `%LocalAppData%\Icon Picker\My icons`, one folder each: `recipe.json`, a copy of the source (`source.svg`, or the picture or icon file), the finished `icon.ico` and a `preview.png`. The copy makes each recipe self-contained, so deleting or moving the original file doesn't break it.
13. The tab shows them in a grid with their name, most recently used first, with **Edit**, **Duplicate**, **Rename** and **Delete** (confirmed).
14. In the picker next to Browse, your saved icons come first, then the library. A saved icon is used as is; a library icon opens the builder.
15. Built icons are always copied into the folder as `folder-icon.ico`, whatever the Icon target setting says: Explorer can't link to an SVG or to a recipe.

### Library

16. The Fluent icons ship inside the app as one compressed file (one variant size per icon, regular and filled), with Microsoft's license beside it. The grid searches by name and by the keywords Microsoft publishes for each icon, with a Regular / Filled switch.
17. Library and SVG thumbnails are drawn by the same renderer as the finished icons, so the grid shows exactly what will be written.

## Scope

### In Scope

- The My icons tab: saved icons, New icon, Edit, Duplicate, Rename, Delete.
- The builder: sources (library, Browse, an existing icon), icon color for single-color SVGs, background shape and color, icon size, live preview, Save, Use for this folder.
- The bundled Fluent library with search and the Regular / Filled switch.
- The picker button next to Browse, in single mode and the batch side panel.
- SVG as a source everywhere: Browse, the library, and SVG files found in folders.
- Customize… on any selected candidate.

### Out of Scope

- Recoloring multi-color SVGs, or tinting pictures and exe icons to one color.
- Gradient or picture backgrounds, outlines, shadows, custom shapes.
- Text or badges on icons.
- Other bundled sets (Simple Icons brand logos, Material Symbols, Tabler).
- Editing SVG shapes.
- Syncing My icons between PCs, or importing and exporting them.

---

## Architecture

### Core (UI-Free)

| Component | Responsibility |
| --- | --- |
| `Icons/SvgRenderer` | Renders SVG text to premultiplied BGRA pixels at a given size, fitted and centered, with Svg.Skia. One renderer object per render (Svg.Skia isn't documented as thread-safe). |
| `Icons/SvgRecolorer` | Replaces the solid paints of an SVG with one color (Assumption 2). |
| `Builder/IconRecipe` | The recipe: source (library id, or a copied file and its icon index), icon color, shape, background color, icon size, name, dates. |
| `Builder/IconComposer` | Draws the background shape and the icon on top at each size, then builds the `.ico` with the existing `IcoFileBuilder`. |
| `Builder/MyIconsStore` | Saves, lists, renames, duplicates and deletes recipes in `%LocalAppData%\Icon Picker\My icons`. |
| `Library/FluentIconLibrary` | Reads the bundled library: the index (names, keywords, regular and filled) and each icon's SVG text. |
| `Candidates` (existing) | A new `Vector` source kind for `.svg`. The candidate search lists SVGs and reads their `viewBox`; they rank with pictures. |
| `Icons/IconPreviewRenderer`, `Applying/IconTargetResolver` (existing) | Draw and convert `Vector` sources through `SvgRenderer`; a `Vector` source is always copied. |

### App

| Component | Responsibility |
| --- | --- |
| `Views/MyIconsPage` | The new tab: saved icons grid and New icon. |
| `Controls/IconBuilderControl` | The builder, shared by the tab and the dialog. |
| `IconBuilderDialog` | Hosts the builder from a folder's picker, with Use for this folder and Save to My icons. |
| `Controls/IconLibraryPicker` | The picker opened next to Browse: My icons first, then the searchable library. |
| `Controls/IconPickerControl` (existing) | Gains the new picker button and Customize… on the selected tile. |
| `Services/SettingsStore` (existing) | Gains the recent colors. |

### Packages

- **Svg.Skia** and **SkiaSharp** (with their Windows native assets, and HarfBuzzSharp for text), referenced by `IconPicker.Core`. All MIT, with Skia's native library under BSD-3-Clause. They add about 25 MB to the installed app.
- **Fluent UI System Icons**, bundled as an asset of `IconPicker.App`.

## Data and State

1. **`recipe.json`** per saved icon: name, created and last-used dates, source (`library` with the icon's id and style, or `file` with the copied file's name and icon index), icon color (or none), shape (`none`, `square`, `rounded`, `circle`, `folder`), background color, icon size.
2. **Settings:** the last 8 colors used.
3. **Library asset:** one compressed file in the app folder, with an index of names, keywords and styles.
4. **Folders:** unchanged. A built icon arrives as an ordinary copied `folder-icon.ico`, so the script, Remove and everything else treat it like any other copy.

## Behavior and UX

### My Icons Tab

1. A grid of saved icons (preview and name), most recently used first, with New icon.
2. Selecting one shows Edit, Duplicate, Rename and Delete; double-clicking opens it in the builder.
3. An empty state invites you to build a first icon from the library.

### Builder

1. **Preview** on the left: 256 px, plus 48, 32 and 16 px, on light and dark backdrops.
2. **Source:** Library…, Browse…, or the icon it was opened with.
3. **Icon color:** shown only for single-color SVGs.
4. **Background:** shape buttons (none, square, rounded, circle, folder) and a background color (hidden for none and folder).
5. **Icon size** slider.
6. **Name** (prefilled from the source's name) and **Save to My icons**; from a folder, **Use for this folder** too.

### Flows

1. **Build from the tab:** New icon → Library… → pick a glyph → color, shape → Save.
2. **Browse to an SVG from a folder:** Browse… → an `.svg` → the builder dialog → Use for this folder (and optionally save).
3. **Pick from the library for a folder:** the new picker → a library icon → the builder dialog, prefilled → Use for this folder.
4. **Reuse a saved icon:** the new picker → a saved icon → it is picked like any candidate (no dialog).
5. **Dress up an existing icon:** select a game's exe icon → Customize… → circle, background color → Use for this folder.
6. **Edit a saved icon:** My icons → Edit → change the color → Save. Folders already using the old version keep their copied icon until it is applied again.

## Edge Cases and Risks

1. **Unsupported SVG content:** Svg.Skia still missed one masking case out of 40 real files. The preview always shows exactly what will be written.
2. **SVGs that aren't one icon:** sprite sheets and SVG fonts draw nothing; the builder says the file has nothing to draw. Malformed XML gets a clear message instead of an error.
3. **Large SVGs:** big Inkscape files take up to about 70 ms per size, about half a second for a full `.ico`; rendering runs off the UI thread.
4. **Two-tone icons:** an icon with white details counts as multi-color and keeps its colors, so the white isn't lost to recoloring.
5. **App size:** about 25 MB more for the Skia libraries.
6. **Elevated helper:** it can render SVGs too (Svg.Skia needs no window), so a refused folder can be retried as admin with an SVG or built icon.
7. **Editing a saved icon** doesn't update folders that already use it, since each folder holds its own copy. Updating them would be a separate feature.

## Evidence

> [!example]
> A survey of the PC found 7,754 SVG files, about 6,600 of them icon-like (GIMP, VS Code, Epic Games, Google Drive, Adobe, Vivaldi, Proton VPN…; almost none in game folders). A test set of 44 covering every feature found was rendered at 256 px with both engines and compared by eye with Edge's rendering.

| | Svg.Skia | Direct2D (built into Windows) |
| --- | --- | --- |
| Correct, of 40 real icons | 37 | 20 |
| Typical failures | One alpha-type mask | `<style>` CSS (black blobs), masks, filters, `<pattern>`, `<text>`, `<symbol>`/`<use>` |
| Recoloring by editing the SVG | 7 of 7 clean | 7 of 7 clean |
| Time per 256 px icon | about 1 to 2 ms typical, up to 70 ms for large files | about 0.5 to 1.5 ms |
| Added to the installed app | about 25 MB | none |

## Open Questions

None.
