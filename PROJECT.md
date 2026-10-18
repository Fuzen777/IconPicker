# Icon Picker

> [!abstract]
> A native Windows app (WinUI 3) that gives folders a proper icon. Point it at a game or app folder and it ranks every icon it finds inside (exe icons, `.ico` files, images) so you can pick one. Point it at a library and it does the same for every subfolder at once, showing its predictions for review before anything is written. It grows out of `C:\Google Drive\Coding\PowerShell\Set Folder Icons.ps1` and stays compatible with the icons that script makes.

## Goal

The script works, but it is all-or-nothing: it picks one exe per folder, never shows the icon before writing it, can't use an image, and asks a single Y/N for the whole batch. Icon Picker keeps the script's detection and careful file handling, and adds a visual review step, a picker to override any choice, image-to-icon conversion, and remembered libraries for quick rescans.

## Resolved Decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Core model | Single mode (one folder, pick from ranked candidates) and batch mode (each subfolder gets a prediction, overridable with the same picker) | User's design |
| Choosing the mode | Detected from the picked folder, with a switch to the other mode | User's choice |
| Batch rows | Direct subfolders by default; a Deep toggle uses the script's app-folder search at any depth | Keeps "Games → game1, game2" while drive and Steam roots still work |
| Folders that already have an icon | Shown in batch with their current icon, never skipped | User's design |
| Icon target | One global setting: copy into the folder (`folder-icon.ico`) or link to the source file | User's choice |
| Image files | Converted to a multi-size `.ico` | User's design |
| Removing | Any folder's custom icon can be removed, not only the app's own | User's design |
| Right-click entry | Classic entry under HKCU, toggled from Settings; unpackaged app | Simplest, no signing; can be upgraded to the modern menu later |
| Unticked rows | Not remembered between scans | User's choice |
| Automation | Remembered libraries with one-click rescan; nothing runs in the background | User's choice |

## Assumptions

> [!info]
> Defaults taken without asking, or settled by the script. Any of them can be overridden.

### Stack and Structure

1. C# on .NET 10 with the latest stable Windows App SDK (WinUI 3), unpackaged and self-contained, MVVM via CommunityToolkit.Mvvm. Targets Windows 11; Windows 10 is untested.
2. All scanning, ranking, conversion and file writing lives in a UI-free `IconPicker.Core` library with unit tests; the WinUI app is a thin layer on top. The script's embedded C# helper (icon extraction, Explorer notifications) ports nearly unchanged.
3. English UI with every string in resource files from day one, so French can be added later without refactoring.
4. Personal use first. Public distribution (signing, winget, Store) is out of scope for v1.
5. Detection rules (ignored exes, skipped folders, bin-style and version patterns) are built in and kept in one place, with no Settings editor in v1.

### Mode Detection

1. A drive root opens in batch with Deep on, since a drive itself can't take a folder icon.
2. A folder that is an app folder by the script's rule (a usable exe directly in it, or in its bin-style or version subfolders) opens in single.
3. Anything else opens in batch.
4. The mode switch is always visible, so a wrong guess (Steam holds both an exe and a library) is one click away from the other mode.

### Candidates and Ranking

1. Sources are icons inside exes, `.ico` files, and images Windows can decode (PNG, JPG, BMP, GIF, TIFF, and WebP where the codec is installed), searched in the folder and everything below it.
2. The script's skip rules apply to the search below the picked folder (junctions, hidden system folders, `.git`, `node_modules`, redistributables, Windows-owned Program Files folders…), never to the picked folder itself. Ignored exes (uninstallers, crash handlers, anti-cheat…) are left out.
3. Ranking extends the script's, in this order (refined since; see Implementation Notes 6 to 10 and 12):
   1. Exe icons and `.ico` files above images.
   2. Named like the folder (exact, then partial), or with a generic icon name (`icon`, `logo`), above other names.
   3. Fewer levels below the folder above deeper ones.
   4. For images: square and at least 256 px above others. For exes: larger files above smaller ones.
4. An exe holding several icons offers each one; the first (the one Explorer shows) ranks highest.
5. The picker shows the best ~50 with a "Show all"; thumbnails load as you scroll.
6. Cloud-only files (Google Drive or OneDrive placeholders not on disk) are skipped, so a scan never triggers downloads.

### Writing Icons

1. Same on-disk format as the script: a copy is written as `folder-icon.ico` (hidden, system), `desktop.ini` gets `IconResource=folder-icon.ico,0`, and the folder gets the ReadOnly attribute Explorer needs to read `desktop.ini`. The script and the app recognise and remove each other's icons.
2. The Icon target setting defaults to Copy. In Link mode, `desktop.ini` points at the source with a path relative to the folder, such as `IconResource=bin\Game.exe,0`.
3. Images are always copied, since they have to be converted. Files picked from outside the folder are always copied too, so every icon stays relative and survives the folder moving or the drive letter changing.
4. Link mode warns when the source sits in a version folder (`app-1.2.3`), since the next update will break it.
5. Non-square images are fitted into a square with transparent padding, never cropped. The `.ico` holds sizes from 16 to 256 px.
6. `desktop.ini` edits keep the file's encoding and every unrelated setting, as the script does.
7. Remove deletes `folder-icon.ico` only when it is the app's (or the script's) own; for any other icon only the `desktop.ini` lines go and the icon file stays. If `desktop.ini` ends up empty, it is deleted and the ReadOnly attribute cleared.
8. Replaced icons can't be restored in v1.

### Running

1. The app never runs elevated. Folders Windows refuses are retried by a short-lived elevated copy of the app (one UAC prompt) that applies only those and reports back. Elevating the whole window would stop drag-and-drop from Explorer, which Windows blocks into elevated windows.
2. Single instance: a second launch (for example a right-click while the app is open) hands its folder to the running window, asking first if there are unapplied changes.
3. Explorer is notified after every change; a Restart Explorer button covers stubborn icon caches.

## Scope

### In Scope

- Single and batch modes, with mode detection and a switch.
- A ranked candidate picker shared by both modes, plus Browse for any image, `.ico`, `.exe` or `.dll`.
- Image-to-icon conversion.
- Setting and removing icons, on one folder or on the selected batch rows.
- The Deep toggle in batch.
- Remembered libraries: add, remove, rescan one, rescan all.
- Settings: icon target, right-click entry on/off, libraries, Restart Explorer.
- Elevated retry for refused folders.
- Launching from the Start menu, by drag-and-drop, from the command line, and from the right-click entry.

### Out of Scope

- The modern Windows 11 top-level context menu (needs package identity).
- A background watcher or automatic apply.
- Remembering unticked rows or per-folder exclusions.
- A crop tool for images.
- Restoring replaced icons, or any history.
- Drive icons.
- Editing detection rules in the UI.
- Public distribution, Windows 10 support, languages other than English.

---

## Architecture

```text
Icon Picker/
├─ IconPicker.slnx
├─ src/
│  ├─ IconPicker.Core/          scanning, ranking, extraction, conversion, desktop.ini, apply/remove
│  └─ IconPicker.App/           WinUI 3 app: views, view models, settings, shell integration
└─ tests/
   └─ IconPicker.Core.Tests/
```

### Core Components

| Component | Responsibility | Script origin |
| --- | --- | --- |
| `FolderSearchRules` | Skip rules, ignored exe patterns, bin-style and version patterns | Section 1, `Test-IsSkippedFolder` |
| `AppFolderDetector` | Whether a folder is an app folder; deep search for app folders | `Find-AppFolders`, `Get-CandidateExes` |
| `CandidateFinder` | Lists exe icons, `.ico` files and images below a folder | Extends `Get-CandidateExes` |
| `CandidateRanker` | Orders candidates by relevance | `Select-IconExe`, `Get-NameMatchRank` |
| `ExeIconExtractor` | Lists an exe's icons and rebuilds each as `.ico` bytes | `FolderIconNative` |
| `ImageIconConverter` | Image → padded, multi-size `.ico` | New |
| `DesktopIniFile` | Reads, edits and writes `desktop.ini`, keeping its encoding | Section 5 |
| `FolderIconWriter` | Applies and removes icons, attributes, Explorer notifications | `Set-FolderIcon`, `Remove-FolderIcon` |

### App Components

| Component | Responsibility |
| --- | --- |
| Home view | Drop zone, Choose folder, remembered libraries |
| Single view | Current icon, ranked candidate grid, Apply, Remove |
| Batch view | Review list, Deep toggle, filter, Apply, Remove |
| `IconPickerControl` | The ranked grid plus Browse, shared by Single and Batch |
| Settings view | Icon target, right-click entry, libraries, Restart Explorer |
| `SettingsStore` | JSON in `%LocalAppData%\Icon Picker\settings.json` |
| `ContextMenuRegistration` | Adds and removes the HKCU right-click entry |
| `ElevatedRetry` | Hands refused changes to an elevated copy and reads back the result |
| `SingleInstance` | Redirects a second launch to the running window |

## Data and State

1. **Settings file:** icon target (Copy or Link), remembered library paths, window size and position.
2. **Right-click entry:** "Pick folder icon…" under `HKCU\Software\Classes\Directory\shell`, `Directory\Background\shell` and `Drive\shell`, running the app with `"%V"`. The script's own `SetFolderIcons` entry is left alone and can coexist.
3. **Per folder on disk:** `folder-icon.ico` (copy mode only), the `[.ShellClassInfo]` `IconResource` line in `desktop.ini`, and the folder's ReadOnly attribute.
4. **Nothing else:** no database and no per-folder memory; the folders themselves are the source of truth.

## Behavior and UX

### Home

1. Shown when the app starts without a folder.
2. A drop zone and a Choose folder button.
3. Remembered libraries listed with Rescan on each and a Rescan all.

### Folder Header

1. The picked folder's path, Change folder, and the mode switch (This folder / Each subfolder).
2. Add to libraries (or remove, if it already is one).
3. In batch, the Deep toggle.

### Single Mode

1. The folder's current icon next to the selected candidate, both large.
2. The ranked candidate grid, each tile showing the icon, a type badge (EXE, ICO, IMG) and its path relative to the folder.
3. Browse… for any file anywhere.
4. Apply, and Remove icon (enabled when the folder has a custom icon).
5. No candidates: an empty state that points to Browse….

### Batch Mode

1. One row per folder: checkbox, current icon → predicted icon, folder name, source path.
2. Folders without an icon start ticked. Folders that already have one show it, start unticked, and show the prediction dimmed. Ticking one replaces its icon. Folders with no candidate show "No icon found" and start unticked.
3. Selecting a row opens the shared picker in a side panel; choosing a candidate there ticks the row.
4. A "Hide folders that already have an icon" filter, off by default.
5. The scan streams rows in with a progress indicator and Cancel.
6. Footer: counts, Apply (N), and Remove icons from the selected rows.
7. After Apply: a summary bar with the number set, each failure and its reason, and "N folders need admin rights → Retry as admin".

### Libraries

1. Rescan opens that library in batch.
2. Rescan all opens one batch list grouped by library.

### Settings

1. Icon target: Copy into the folder (default) or Link to the source file.
2. Right-click entry on or off.
3. Library list management.
4. Restart Explorer.

### Look

1. Fluent styling with a Mica backdrop, following the system light or dark theme.

## Edge Cases and Risks

1. **Stale Explorer icons:** Explorer caches icons aggressively. The app notifies it on every change; Restart Explorer is the fallback.
2. **Link mode breakage:** a linked exe that's renamed, moved or deleted leaves a blank icon. Remove still cleans it up; the version-folder warning covers the most common cause.
3. **Huge folders:** game asset folders can hold thousands of images. Scans are cancellable, the picker is capped with Show all, and thumbnails load lazily.
4. **Cloud placeholders:** reading a cloud-only file would download it, so those files are skipped.
5. **Protected folders:** Windows-owned folders are skipped in searches. If one is picked directly, the write fails, the elevated retry may still fail, and the failure is reported rather than hidden.
6. **Wrong mode guess:** Steam and similar launchers hold both an exe and a library. The always-visible switch is the fix.
7. **Unapplied changes:** a right-click while the app holds unapplied batch edits asks before replacing them.
8. **Coexistence with the script:** the script skips icons the app made (they count as custom), and its `-Remove` removes the app's copy-mode icons (same marker). Both are intended.
9. **Google Drive build output:** the project sits in Google Drive, which will sync `bin`/`obj` and can lock files mid-build. `bin`/`obj` stay out of git, and build output may need to be redirected outside Drive.

## Open Questions

None.

---

## Implementation Notes

> [!note]
> Where the build differs from the spec above, or settled what it left open.

1. **Steam library folders** never become an outer folder: the script gave `steamapps\common` the icon of its only game when it held a single one.
2. **.dll files** aren't searched automatically, since a game folder holds hundreds of them; Browse still picks them. The exception is a .dll named exactly like the folder: apps started through dotnet keep their icon there (`tModLoader.dll` holds tModLoader's 16 to 256 px icon).
3. **Inspection limit**: a folder's best 500 files (by type, name, depth and size) are opened, files named exactly like the folder always among them; "Show all" lists every candidate among them. Icons under 16 px are left out.
4. **Current icon** previews are drawn from what desktop.ini says rather than from Explorer's icon cache, so they show what is set even when Explorer hasn't caught up.
5. **Removing in batch** acts on the ticked rows that have a custom icon.
6. **Size markers** don't count against a name: an .ico or picture named like the folder plus a trailing size (`tModLoader256.bmp`, `icon_48.png`, `Game_256x256.png`, `Game@2x.png`, `GameHD.png`) is as well named as the plain copy, so the existing order (square 256 px or larger first, then larger first) picks its largest version. Only a whole listed size counts, so indexes, years, versions and resolutions are left alone (`Dish.016`, `Celeste2016`, `Game-1.16`, `1280x1024`). Executable names are compared as they are, since their digits are bitness or part of the title (`AutoHotkey64`, `DOOM64`), and a wide picture's marker only counts in the final order when the picture is square. The rule was tested against the naming of about 32,000 image files in the game and app folders on this PC.
7. **Low-resolution icons**: an exe or .ico whose largest size is under 48 px ranks with the pictures, since it would look blurry as a folder icon. "Square and 256 px or larger first" applies to exe and .ico icons too, so a 256 px .ico beats a 32 px-only exe of the same name. Quality has a second step at 48 px (square 256+, then square 48+, then square under 48, then not square), so a 128 px picture also beats a 32 px icon.
8. **Best copy named like the folder**: among files named exactly like the folder, depth no longer decides; quality and size do, and depth only breaks a tie (Bandizip's 256 px `icons\default\Bandizip.ico` over its 48 px `shellicons\bandizip.ico`). Partial and generic names keep "closer first", since each mod of a game can ship its own `icon.png`. Pictures whose sides are within 5% count as square.
9. **Generic names**: `icon`, `logo`, `appicon` and `favicon` only count as a name's last word (`app_icon.png`, `GameLogo.png`, `icon_256.png`), not inside another word (`lexicon`, `iconbg`) or followed by more (`IconSet`, `BuffIcon_01`, `logo_white`, `logo_英文`). Windows Store resource qualifiers and a stacked size and scale are removed first (`StoreLogo.scale-200.png`, `Square44x44Logo.targetsize-256.png`, `icon_512x512@2x.png`).
10. **Secondary icons**: an exe's other icons (document and file-type icons, variants) come after every main icon of the exe and .ico group, but before pictures, and never above their own exe's main icon, even when it is blurrier (Project Starborn's 256 px Unreal logo stays below the game's 32 px icon). The main icon is the exe's first readable one.
11. **Git repositories**: the script skipped every folder holding `.git` as a code project. The app lists a repository like any folder in "Each subfolder", since a downloaded tool is often a git clone (`IdleOn Injector` with `InjectCheatsUI.exe` at its top). Deep search counts a repository only when its exe sits at its top, and icon search still skips repositories nested inside a folder: the exes deeper in a code project are build outputs.
12. **Product names**: an exe whose product name (from its version information) equals the folder's name counts as partly named like the folder, so the main app can win when its file name doesn't say so (Sandboxie-Plus's `SandMan.exe` over the sharper helpers `Start.exe` and `MiniDump.exe`). Only an exact match counts, since helpers often carry part of the app's name (`Start.exe` is "Sandboxie").
13. **Hidden folders** get no row in "Each subfolder": they hold tool-managed data, such as Google Drive's `.tmp.drivedownload` and `.tmp.driveupload`. The deep search still walks through them, since apps can sit under a hidden folder (`AppData\Local\Programs`), but a hidden folder never becomes a row itself.
14. **SVG icons** are drawn with Svg.Skia rather than Windows' Direct2D (37 of 40 real SVGs right against 20), never load anything outside themselves, and rank with pictures; see [SVG Rendering](DOCUMENTATION/Docs/SVG%20Rendering.md).
15. **Icon builder**: the My icons tab and the builder in the picker follow [Icon Builder](DOCUMENTATION/Archives/Icon%20Builder.md), with the differences listed at its top. Built icons are always copied into the folder, since Explorer can't point at an SVG or a recipe.
