# SVG Rendering

> [!abstract]
> Icon Picker draws SVGs with Svg.Skia rather than Windows' own Direct2D renderer, never lets an SVG load anything outside itself, and always copies what it draws into the folder as an ordinary `folder-icon.ico`. The same renderer draws SVG candidates, the built-in icons and every icon made in the builder.

## Renderer Choice

Windows can draw SVGs through Direct2D with no extra download, but it misses much of what real SVG files use. Svg.Skia (with SkiaSharp) reads far more of them, for about 21 MB more in the installed app.

> [!example]
> A survey of the PC found 7,754 SVG files, about 6,600 of them icon-like (GIMP, VS Code, Epic Games, Google Drive, Adobe, Vivaldi, Proton VPN…, almost none in game folders). A test set of 44 covering every feature found was rendered at 256 px with both engines and compared by eye with Edge's rendering.

| | Svg.Skia | Direct2D |
| --- | --- | --- |
| Correct, of 40 real icons | 37 | 20 |
| Typical failures | One alpha-type mask | `<style>` CSS (black blobs), masks, filters, `<pattern>`, `<text>`, `<symbol>`/`<use>` |
| Recoloring by editing the SVG | 7 of 7 clean | 7 of 7 clean |
| Time per 256 px icon | About 1 to 2 ms typical, up to 70 ms for large files | About 0.5 to 1.5 ms |

The app's renderer reproduces the research renderer pixel for pixel on that set.

## Nothing Loaded From Outside

A folder search never starts a download, and the elevated helper re-renders the sources it is handed with admin rights, so an SVG must never reach another file or the network. With default settings, Svg.Skia does load linked and imported stylesheets from disk, and other references besides. Two layers stop that:

1. **The library's own switches**, on every render: `SvgDocumentLoadOptions` with `ProcessingMode = SecureStatic` and `ExternalResources = SameDocumentAndDataOnly`, broken-image placeholders and JavaScript off, and the library-wide `SvgDocument.ResolveExternalImages`, `ResolveExternalElements` and `ResolveExternalXmlEntites` set to `ExternalType.None`, since one lookup path ignores the load options.
2. **Cleaning the XML** before Svg.Skia sees it (`SvgFile`): `href` and `xlink:href` values that aren't `#…` or `data:`, every `url()` that isn't `#…`, `<?xml-stylesheet?>` instructions, `<script>` elements, `on…` attributes, `@import` and `@font-face` rules, and the DOCTYPE, whose entities are expanded first with no resolver and a size cap.

Links, `url()` references and stylesheets each have a test pointing at a real file whose content would change the pixels if it were loaded; fonts, scripts, event handlers and the DOCTYPE are checked on the cleaned XML.

Svg.Skia also draws nested elements and same-file references (`<use>`, patterns, gradients) recursively, so `SvgFile` refuses a document deeper than 256 levels or drawing more than 100,000 elements once references are expanded, and treats a reference loop the same way. Such a file counts as unreadable: a deeply nested SVG would otherwise overflow the stack, which ends the process, and a few chained `<use>` take minutes and gigabytes.

## Failures Stay Contained

`SvgFile` and `SvgRenderer` catch every failure per file and report it as a result (unreadable, or nothing visible drawn), so no SVG can stop a folder search, a batch scan, a preview or the elevated helper. An SVG that draws nothing fails to apply with its own reason ("the SVG can't be read, or draws nothing") rather than giving an invisible icon.

## SVGs Among a Folder's Candidates

1. **Size**: an SVG is sharp at any size, so it reports its viewBox scaled to 1,024 px on its longest side. A square SVG therefore counts as 256 px or more and ranks with the sharpest square pictures, always after executables and `.ico` files of 48 px or more.
2. **Cheap search**: the search reads only the root element's viewBox, width and height, skips SVGs over 1 MB, and opens at most 100 per folder, listed after pictures of the same name rank. Apps carry hundreds of interface SVGs (GIMP 2,571, Logi 848, Proton VPN 544), which would otherwise take the inspection slots and the time.
3. **Checked where seen**: whether an SVG draws anything (sprite sheets, SVG fonts and templates waiting for a color draw nothing) is only known by drawing it, so only the vectors among the first 50 candidates, the picker's first page, are drawn once at 64 px and dropped when empty.
4. **Effect**: across the PC's 306 direct and 353 deep game and app folders, no top pick changed, and searches took 5 to 13% longer in total, at most about 0.4 s more for folders full of interface SVGs.

## Colors

An SVG counts as drawn in one color when every fairly opaque pixel of a 64 px render has the same color within a small tolerance; one that draws nothing yet counts too, so templates come back once colored. Recoloring edits the XML: every solid paint in `fill`, `stroke`, `stop-color`, `flood-color` and `color` attributes, in `style=""` and in `<style>` rules becomes the chosen color, `none`, `transparent`, `inherit` and `url()` stay, and the root gets the color and a fill. Two-tone icons (white details on a color) count as several colors, so recoloring never erases their details, and so does any SVG with a mask, whose own black and white paints decide what shows.

## Built Icons Are Always Copied

Explorer can only show an `.ico`, `.exe` or `.dll` through `IconResource`, never an SVG or a recipe, so whatever the builder makes reaches a folder as a finished `.ico`: a saved icon's `icon.ico` in `%LocalAppData%\Icon Picker\My icons\<id>\`, or an unsaved one in `%LocalAppData%\Icon Picker\Built\` (deleted after a day). Both are marked as built (`IconSource.IsBuilt`), so they are copied as `folder-icon.ico` even under the Link setting and even into a folder that holds the app's data, Remove and the script treat them like any other copy, and the elevated helper needs nothing new. Editing a saved icon doesn't change folders already using it, since each holds its own copy.

## Built-In Icons

The built-in icons are Microsoft's Fluent UI System Icons (MIT), shipped as `src\IconPicker.App\Assets\FluentIcons.zip` with their license beside it. `Scripts\Build-FluentIconLibrary.ps1` makes the zip: the SVGs come from the npm package `@fluentui/svg-icons`, the names, descriptions and keywords from the `metadata.json` files of github.com/microsoft/fluentui-system-icons (a shallow, blobless, sparse clone), and each icon keeps its largest size in its regular and filled styles. The zip holds `index.json` and `icons/<id>_<style>.svg`; entries are read under a lock, since thumbnails are drawn several at a time.
