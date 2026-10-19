# Features

## Opening a Folder

- **Home**: a drop zone with "Choose Folder". Folders can also be dropped anywhere in the window, on any page.
- **Command line**: `IconPicker.exe "D:\Games\Some Game"` opens that folder. A drive passed as `"D:\"` (as Explorer does) is read correctly.
- **One window**: launching the app again while it is open hands the folder to the running window and brings it to the front.
- **Mode detection**: a folder holding a game or app (a usable .exe directly in it, or in a bin-style or version subfolder such as `bin\x64` or `app-1.2.3`) opens in "This Folder"; a drive opens in "Each Subfolder" with Deep Search on; anything else opens in "Each Subfolder". The switch under the folder's path changes mode at any time. "This Folder" is unavailable for a drive, which can't take a folder icon.
- **Shortcuts to folders**: dropping a shortcut to a folder, or passing one on the command line, opens its folder in "This Folder", with the shortcut named under the path. Apply gives the folder its icon and points the shortcut at it, since Explorer doesn't reliably show a folder's new icon on shortcuts made before it; Remove Icon takes both off (an icon given to the shortcut by hand from elsewhere stays).
- **Folder tab**: the folder opened last gets a tab in the navigation pane, after My Icons, named after it (its full path on hover). Visiting Home, My Icons or Settings keeps it exactly as it was: search results, ticks, icons picked by hand, the selected row, and a search still running. The tab or Back returns to it. Opening another folder replaces it, and Back no longer leads to the old one. It lasts until the app closes.
- **Unapplied picks**: opening another folder, switching mode or turning Deep Search on or off after picking an icon or ticking a folder by hand asks before discarding it, as long as something is still ticked to apply or remove. The question names the folder when it isn't on screen.

## This Folder

- **Current and new icon**: the folder's icon as it is (standard, copied, linked, from elsewhere, or broken when its file is missing) next to the icon picked for it, with a line saying whether the new one will be copied, converted or linked.
- **Apply and Remove Icon**: Apply writes the icon; Remove Icon takes off any custom icon, keeps the folder's other settings, and deletes `folder-icon.ico` only when the app or the script made it.
- **Results bar**: what happened, each failure with its reason, and a reminder to press F5 in Explorer when it still shows the old icon.
- **Retry as Admin**: folders Windows refuses are listed with a "Retry as Admin" button, which applies just those through a short-lived elevated copy of the app (one UAC prompt). The window itself never runs as admin, so dropping folders from Explorer keeps working.

## Each Subfolder

- **Rows**: one row per direct subfolder (Games → Game 1, Game 2…), each with a checkbox, its current icon → the predicted one, its name, and where the icon comes from. Rows appear at once and fill in as four folders at a time are searched, with progress and Cancel; folders not searched before Cancel say so. A folder that is a git repository (often a downloaded tool) gets a row like any other; a hidden folder (such as Google Drive's `.tmp.drivedownload`) gets none, in the deep search too. A shortcut to a folder (Games → Steam.lnk) gets a row of its own, "Steam (shortcut)", searched in the folder it opens and applied as in "This Folder" (not in the deep search); it starts ticked while the shortcut doesn't show the folder's icon.
- **Deep Search**: the toggle switches to the script's search, which finds game and app folders at any depth: games in a Steam library, apps in `Program Files` and `AppData\Local\Programs`, and a wrapper folder whose only app sits in an odd subfolder (Tools for `Tools\Release\tool.exe`), which gets a row above its app. A git repository counts only when its exe sits at its top: the exes deeper in a code project are build outputs. It is on by default for a drive.
- **Starting ticks**: folders without an icon, or whose icon is broken, start ticked. Folders that already have an icon show it, start unticked, and show the prediction faded; ticking one replaces its icon. Folders where nothing was found say "No icon found" and can only be ticked to remove an icon they have.
- **Side panel**: selecting a row shows its full icon picker, with Browse, Icons and Customize; picking an icon there ticks the row. It fits three icons to a row to start with; the divider beside it resizes it by whole tiles, one more or less to a row each step (dragged, or with the arrow keys), a double-click on the divider puts back three, and the width is remembered. A narrow window drops tiles so the rows keep room. Picking one while the row is still being searched stops that row's search (the other rows carry on).
- **Filters**: three chips after "Show", all on to start with: **Already Set** (folders that already have a working icon), **Only Pictures** (every icon found is a picture or SVG, with no exe, .ico or shortcut icon) and **No Match** (no icon found). Turning one off hides those folders; their tooltips spell this out. A row whose icon was picked by hand is never hidden as one with only pictures. Hidden rows keep their tick but are left out of Apply, Remove icons and Select all; the summary says how many are hidden. While a search runs, the filters take the rows it finished once it ends.
- **Select All**: a box above the rows, half-ticked when some are ticked. Clicking it unticks every shown row when any is ticked, otherwise ticks every shown row that can be.
- **Apply (N)** sets the icons of the ticked rows; **Remove Icons (N)** removes the icons of the ticked rows that have one, after a confirmation. Each row then says what happened ("Icon set", "Needs admin rights", "Failed: …"), the results bar sums it up, and refused folders can be retried as admin as in "This Folder". Both work while the search is still going: they act on the rows ticked so far, and rows still being searched can be applied once they are done.
- **Version folder warning**: a row whose icon would be linked into a version folder shows a warning sign explaining why.

## Icon Picker

- **Ranked candidates**: every icon the folder can take from the files inside it, searched in the folder and everything below it: icons inside .exe files (each icon of an .exe with several), a .dll named exactly like the folder (apps started through dotnet keep their icon there, as tModLoader does), .ico files, pictures (PNG, JPG, BMP, GIF, TIFF, WebP), SVGs, and the icons shortcuts show (see Shortcuts below). Best first:
  1. Executables and .ico files before pictures and SVGs, unless their icon is under 48 px: those would look blurry as a folder icon and rank with the pictures.
  2. An executable's main icon before its secondary ones (document icons, variants), which come after every other main icon but before pictures, and never above their own executable's main icon.
  3. Files named like the folder, executables whose product name is the folder's name (`SandMan.exe` is "Sandboxie-Plus"), or files whose name ends with icon or logo (`app_icon.png`, `GameLogo.png`, but not `lexicon.png` or `IconSet.png`). A size at the end of an .ico or picture name doesn't count against it (`tModLoader256.bmp`, `icon_48.png`, `Game@2x.png`, `GameHD.png`).
  4. Files closer to the folder, except among files named exactly like the folder, where the best copy wins wherever it sits.
  5. Square icons and pictures of 256 px or more (sides within 5% count as square), then square ones of 48 px or more, then smaller square ones, then the rest; then sharper and larger. A square SVG counts as 256 px or more, since it is sharp at any size.
- **Left out**: uninstallers, crash handlers, anti-cheat and other helper executables; placeholder icons smaller than 16 px; other .dll files (Browse can still pick them); developer and redistributable folders; Steam libraries (when searching for Steam's own icon); cloud files that aren't on this PC, so a search never starts a download.
- **Shortcuts**: a `.lnk` or `.url` shortcut inside the folder offers the icon it shows in Explorer: the icon it was given, otherwise its target's when the target is an .exe. Steam's game shortcuts (`.url`) offer the game's icon from Steam's folder. Its tile carries the shortcut's name and ranks like the file its icon comes from, counting as named like the folder when either name matches. Left out: a shortcut to an icon the search found anyway (the file's own tile stays), repeats of another shortcut's icon, shortcuts to uninstallers and other helpers, shortcuts showing a document's or web page's icon, and broken shortcuts. The icon is copied as `folder-icon.ico` like any icon from outside the folder.
- **Include Shortcuts**: a checkbox above the icons, on by default and remembered, that lists shortcut icons or hides them. It applies to every folder at once: in "Each Subfolder", rows not changed by hand get their prediction and starting tick again. An icon picked by hand stays picked.
- **SVGs**: SVGs over 1 MB are left out, and so are those that draw nothing (sprite sheets, SVG fonts, templates that only show once colored) among the first 50 tiles. An SVG never loads anything outside itself (other files, web addresses, linked stylesheets, fonts or scripts), so a search never starts a download.
- **Tiles**: each tile shows the icon, a badge (EXE, DLL, ICO, IMG, SVG, LNK, URL, or MINE for an icon built in the app), the file name (a built icon's own name) and its size ("Scalable" for an SVG); hovering shows its path, and for a shortcut where its icon comes from. Thumbnails are drawn as tiles scroll into view.
- **Show All**: the best 50 are shown first; "Show All" lists the rest. Large folders only open their best 500 files.
- **Browse**: any .ico, .exe, .dll, picture or SVG from anywhere. An .exe or .dll holding several icons opens its own grid to pick from, with "Back to the Folder's Icons". An SVG opens the builder (see My Icons), to color it and set it on a background first. Picking an icon while the folder is still being searched stops the search, and the picker says so: its results would only have replaced the pick. A search that finishes at the same moment never overrides a pick either.
- **Icons**: your saved icons, then the built-in icons, in one window. A saved icon is picked as it is now (editing it later doesn't change this pick) and moves to the top of My Icons; a built-in icon opens the builder.
- **Customize**: opens the picked icon in the builder, on a rounded square to start with: any exe, .ico, picture or SVG icon can be set on a background, and an SVG drawn in one color recolored. An icon built earlier reopens as it was.
- **The builder from a folder**: Use for This Folder picks the result like any other icon (it is always copied); Save to My Icons keeps it as well, or saves the changes to a saved icon being customized.

## Writing Icons

- **Copy or link**: the Icon Target setting decides what desktop.ini points at when the icon comes from an .exe or .ico inside the folder: a copy written as `folder-icon.ico` (default), or a relative link to the file itself. Pictures and SVGs are always converted and copied, and so are files from outside the folder, so every icon survives the folder moving or the drive letter changing.
- **Version folder warning**: linking to a file inside a version folder (`app-1.2.3`) warns that the app's next update will break the icon.
- **Picture conversion**: pictures are fitted into a square with transparent padding (never cropped) and saved with sizes from 16 to 256 px. An SVG is drawn afresh at each size, so even 16 px stays sharp; one that draws nothing fails with a reason instead of giving an invisible icon.
- **Compatible with Set Folder Icons.ps1**: copies use the same `folder-icon.ico` and `IconResource=folder-icon.ico,0`, so the app and the script recognise and remove each other's icons. desktop.ini keeps its encoding and every setting the app doesn't touch.

## My Icons

- **My Icons tab**: icons built in the app and kept for any folder, most recently used first, three to a row to start with. The divider between the list and the builder resizes the list by whole tiles, one more or less to a row each step (dragged, or with the arrow keys; a double-click puts back three), and its width is remembered; a narrow window drops tiles so the builder keeps room. New Icon starts the builder afresh; a selected icon offers Edit (also on double-click), Duplicate, Rename and Delete (asked first; folders already using it keep their icon). With no icons yet, the tab offers to start from the built-in icons.
- **Builder**: a glyph from the built-in icons, an SVG, a picture, an .ico or an .exe or .dll icon (one holding several shows them to pick from), with a live preview at 256 px and at 48, 32 and 16 px on light and dark backdrops. When the window is narrow, a smaller preview sits above the settings, so the tab never scrolls sideways.
  - **Icon Color**: an SVG drawn in one color takes any color; one in several colors keeps them, and the builder says so. Templates that draw nothing until colored come back once colored.
  - **Background**: none, square, rounded square, circle, or Windows' own folder with the glyph on its front, picked from tiles drawing each shape (named on hover and to screen readers). Square, rounded square and circle take a background color.
  - **Icon Size**: how much of the background the glyph fills, from 40 to 100%, with a natural size for each shape until the slider is moved. The mouse wheel over the slider moves it by 5%, and a right-click puts back the shape's natural size.
  - **Colors**: preset swatches, the last 8 colors used on a saved or applied icon, and Windows' color picker.
  - **Name**: the source's name until another is typed.
  - Every size of the finished icon is drawn afresh, so 16 px stays sharp.
- **Built-In Icons**: Microsoft's Fluent UI System Icons, 2,977 of them in regular and filled, searchable by name, description and Microsoft's keywords, and drawn in the theme's text color.
- **Saving**: Save to My Icons keeps the icon with its own copy of its source (only the chosen icon of an .exe), so it can be edited later even after the original file moves. Editing a saved icon doesn't change folders already using it. Leaving the tab with unsaved changes asks first.

---

## Libraries

- **Library button**: on a folder's page, marks the folder as a library (a folder rescanned often, such as a Steam library), or unmarks it.
- **On Home**: the libraries are listed with Rescan, which opens one in "Each Subfolder", and Rescan All, which opens every library in one list grouped by library. A library that no longer exists is marked "Not found".
- **In Settings**: add libraries with "Add Library" and remove them.

## Settings

- **Theme**: Use Windows Setting (default, following Windows when it switches between light and dark), Light or Dark, applied at once to the window, its dialogs and lists, and the window frame.
- **Icon Target**: Copy into the Folder, or Link to the File.
- **Right-Click Entry**: turns on "Pick Folder Icon…" when right-clicking a folder, the empty space inside one, or a drive (under "Show more options" on Windows 11), opening the app on that folder. When the entry opens another copy of the app (an older install, a build folder), "Use This Copy" points it here.
- **Keep Data with the App (Portable)**: off by default, with settings and saved icons in `%LocalAppData%\Icon Picker`. Turned on, they move to a `Data` folder next to IconPicker.exe, so the app can be carried with its data; turned off, they move back. Either way the app asks, moves the data and restarts in Settings, and says so if the move failed. Unavailable when the app's folder can't be written to, as in Program Files. A copy whose folder holds a `Data` folder starts portable.
- **Restart Explorer**: restarts this session's Explorer after a confirmation, so it redraws icons it has cached.
- **About**: the app's version.
- **Window**: its size, position and maximized state are remembered.

## Installing

- **Release zip**: each GitHub release has `IconPicker-<version>-win-x64.zip`, an `Icon Picker` folder to extract anywhere and run, with nothing installed; extracting a newer one over it updates it.
- **Scripts\Install.cmd** publishes the app to `%LocalAppData%\Programs\Icon Picker` and adds a Start menu shortcut; run it again to update.
- **Scripts\Uninstall.cmd** removes the right-click entry, the shortcut and the install folder; settings, saved icons (moved back to `%LocalAppData%` when kept next to the app) and the icons given to folders are kept.
