---
type: Agent Instructions
title: AGENTS
---

## Introduction

- Icon Picker is a native Windows app (WinUI 3, C#) that gives folders a custom icon picked from the icons and images inside them, one folder at a time or for every subfolder at once.
- It grew out of `C:\Google Drive\Coding\PowerShell\Set Folder Icons.ps1` and stays compatible with the icons that script writes. [PROJECT.md](./PROJECT.md) holds the original spec and its decisions.

## Rules

- All UI text lives in `src/IconPicker.App/Strings/en-US/Resources.resw`, in English: `x:Uid` in XAML, the `Strings` helper in code. Never hard-code UI text.
- `IconPicker.Core` has no UI dependency. Scanning, ranking, conversion and file writing go there, with unit tests; the app only presents and orchestrates.
- Keep the on-disk format compatible with the script: a copied icon is `folder-icon.ico` (hidden, system) referenced as `IconResource=folder-icon.ico,0`.
- Meta files (docs, scripts) go at the project root. `src/` holds only what ends up in the build, `tests/` the tests and their fixtures.
- After implementing a feature, update [FEATURES](./FEATURES.md) and remove the item from [TODO](./TODO.md) if it is listed.

## Layout

| Path | Contents |
| --- | --- |
| `src/IconPicker.Core` | UI-free logic: command line, rules, detection, candidates, icons, desktop.ini, applying |
| `src/IconPicker.App` | The WinUI 3 app (assembly `IconPicker.exe`) |
| `tests/IconPicker.Core.Tests` | MSTest tests for the Core library |
| `Scripts` | Build, check, run and install scripts |

## Building and Testing

| Script | What it does |
| --- | --- |
| `Scripts\Build.cmd` | Builds the solution in Debug |
| `Scripts\Check.cmd` | Builds with warnings as errors, runs the tests and checks formatting. Run it before every commit. |
| `Scripts\Run.cmd [folder]` | Builds and starts the app, optionally on a folder |
| `Scripts\Install.cmd` | Publishes Release, self-contained, to `%LocalAppData%\Programs\Icon Picker` and adds a Start menu shortcut |
| `Scripts\Package.cmd` | Makes the release zip, `IconPicker-<version>-win-x64.zip` holding an `Icon Picker` folder, and its `.sha256`, in the build root's `release` folder; needs PowerShell 7 |
| `Scripts\Uninstall.cmd` | Removes the right-click entry, the shortcut and the install folder, first moving a portable `Data` folder back to `%LocalAppData%` |
| `tests\Make-TestLibrary.ps1` | Builds a throwaway game library in `%TEMP%\IconPicker\test-library` (or `-Path`) covering each case the app handles, for trying it by hand |
| `Scripts\Build-FluentIconLibrary.ps1` | Rebuilds the built-in icons asset (`src\IconPicker.App\Assets\FluentIcons.zip` and its license) from Microsoft's Fluent UI System Icons; downloads the npm package and the repository's metadata files |

- Build output goes to `artifacts\` unless `IconPickerBuildRoot` is set. On this PC the git-ignored `Directory.Build.user.props` sets it to `D:\Dev\icon-picker`, since the project sits in Google Drive, which would sync the output and lock files mid-build.
- Package versions are central in `Directory.Packages.props`.
- Releases: set `<Version>` in `IconPicker.App.csproj`, commit, then push a matching tag (`git tag v0.2.0`, `git push origin v0.2.0`). `.github/workflows/release.yml` then runs Check, makes the zip and publishes the GitHub release (a pre-release for 0.x) with `.github/release-notes.md` above the generated notes. Running the workflow by hand builds the zip without publishing. Code signing comes later: [Code Signing](DOCUMENTATION/Specs/Code%20Signing.md).
- SVGs are drawn with Svg.Skia and SkiaSharp, referenced by Core. `Directory.Build.targets` keeps the native packages' `.pdb` files (about 120 MB) out of every build and publish, and the tests project sets the win-x64 `RuntimeIdentifier` so only those natives are copied. SkiaSharp 4 marks the paint-only `DrawImage`/`DrawBitmap` overloads obsolete, which fails the build: use the `SKSamplingOptions` ones.
- The project is its own git repository, public at https://github.com/Fuzen777/IconPicker; the Coding monorepo it sits in ignores this folder. Commits use the GitHub noreply address set in this repository's git config, so no personal email is published. `README.md` and the other files GitHub shows stay standard GFM, without Obsidian admonitions.

## Technical Notes

- Unpackaged, self-contained Windows App SDK app, x64 only through its `RuntimeIdentifier` (the projects stay AnyCPU so every build lands in the same folder). `Program.cs` replaces the XAML-generated `Main` (`DISABLE_XAML_GENERATED_MAIN`) to run the elevated helper and the single-instance redirect before XAML starts.
- The command line is read raw with `GetCommandLine`: .NET's own version starts with `IconPicker.dll` and splits `"D:\"` the standard way, which breaks drive paths.
- View models are public (the XAML compiler needs them); their constructors are internal because they take internal services.
- The data folder is `%LocalAppData%\Icon Picker`, or a `Data` folder next to `IconPicker.exe` when one exists (portable). `DataLocation` decides it once at startup; turning portable on or off in Settings restarts the app with `--move-data`, and the new copy moves the data (`DataFolderMover`) before anything reads it, once the old one has exited. In it: `settings.json`, the elevated helper's `jobs` (never moved), `error.log`, saved icons in `My icons\<id>\` and built icons not saved in `Built\` (deleted after a day).
- Every SVG goes through `SvgFile` (which removes anything that reaches outside the file) and `SvgRenderer` (which turns off Svg.Skia's own external loading); never hand Svg.Skia a file or stream directly. See [SVG Rendering](DOCUMENTATION/Docs/SVG%20Rendering.md).
- WinUI shows one `ContentDialog` at a time: custom dialogs go through `DialogService.ShowAsync`, and nothing opens a dialog from inside one (the builder swaps its own panes instead).
- The right-click entry is written under `HKCU\Software\Classes\{Directory,Directory\Background,Drive}\shell\IconPicker` and points at whichever copy turned it on, so turning it on from a Debug build points it at the build folder.
- A `DirectoryInfo` from a folder listing caches its attributes: call `Refresh()` before reading them after a change (`FolderIconState.Read` does).
- Win32 interop goes through CsWin32, with a `NativeMethods.txt` per project.
- Trimming is off.
