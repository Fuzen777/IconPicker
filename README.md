# Icon Picker

Icon Picker gives Windows folders a proper icon. Point it at a game or app folder, and it ranks every icon it finds inside (exe icons, `.ico` files, pictures, SVGs, shortcuts), picks the best one, and sets it as the folder's icon. Point it at a folder full of games, such as a Steam library, and it does every subfolder at once.

It's a native, portable Windows app (WinUI 3).

<img width="2047" height="1556" alt="Clipboard Image" src="https://github.com/user-attachments/assets/9c01ce54-eeab-4fb3-a569-fcf54f6e064b" />

## Features

- **Single** mode, for one folder: its current icon, the best icon found inside it, and every other candidate to choose from. Apply sets it; Remove Icon takes it off. The last icons you applied wait under Apply, to give another folder the same one.
- **Batch** mode, for every subfolder at once: one row per subfolder with its predicted icon, ticked when it has none yet. Apply sets them all, and a deep search finds game and app folders at any depth.
- **Ranking**: executables named like the folder first, sharp square icons before blurry or wide ones, and uninstallers, crash handlers, and redistributables left out.
- **My Icons**: build your own icons from Microsoft's Fluent icons, an SVG, a picture, or an exe icon, set on a square, circle, or Windows folder background in your own colors.
- **Shortcuts**: a shortcut inside a folder provides the icon it shows, and a shortcut to a folder can get an icon of its own.
- **Copy or link**: icons are copied into the folder as `folder-icon.ico` by default, so they survive the folder moving or the drive letter changing.
- **Right-Click Entry** (optional): "Pick Folder Icon…" on folders and drives in Explorer.
- **Portable**: keep the app's settings and saved icons next to it, to carry it with its data.

The full list is in [FEATURES.md](FEATURES.md).

## Download

1. Download the latest `IconPicker-<version>-win-x64.zip` from [Releases](https://github.com/Fuzen777/IconPicker/releases).
2. Extract it anywhere and run `IconPicker.exe` in the `Icon Picker` folder it holds. The app itself and its runtime sit in the `lib` folder beside it.

The app isn't code-signed yet, so Windows SmartScreen may say "Windows protected your PC" the first time: choose **More info**, then **Run anyway**. You can also right-click the zip, choose **Properties** and tick **Unblock** before extracting it.

To update, extract the new zip to the same place, over the old `Icon Picker` folder. To remove the app, turn off the right-click entry in Settings if you turned it on, then delete the folder (and `%LocalAppData%\Icon Picker`, unless you kept the data next to the app).

### Requirements

- Windows 11, 64-bit (x64). Windows 10 version 2004 or later should work, but hasn't been tested.

## Building From Source

You need Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

| Script | What it does |
| --- | --- |
| `Scripts\Build.cmd` | Builds the solution in Debug |
| `Scripts\Run.cmd [folder]` | Builds and starts the app, optionally on a folder |
| `Scripts\Check.cmd` | Builds with warnings as errors, runs the tests and checks formatting |
| `Scripts\Install.cmd` | Publishes a release build to `%LocalAppData%\Programs\Icon Picker` with a Start menu shortcut |
| `Scripts\Package.cmd` | Makes the release zip and its SHA-256 in `artifacts\release` |

Build output goes to `artifacts\` in the repository. To build somewhere else, create a `Directory.Build.user.props` next to `Directory.Build.props` (it's ignored by git) that sets `IconPickerBuildRoot`.

[PROJECT.md](PROJECT.md) explains how the app came to be and why it works the way it does; [AGENTS.md](AGENTS.md) holds the conventions for working on it.

## License

Icon Picker is released under the [MIT License](LICENSE). It includes third-party components under their own licenses, listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
