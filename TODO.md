---
type: TODO
rules:
  top-level-categories:
    - Quick Wins
    - Features
    - Concerns
    - Fixes
    - Testing
    - Features in Consideration
  sub-categories: Group tasks by the feature or part of the app they touch. When a task needs another one, the prerequisite comes first.
  quick-wins: Simple, uncontentious tasks that fit in one pass. This category takes precedence over the others, except Concerns and Features in Consideration, which need their category for context. Its only sub-categories are Features and Fixes, and entries about neighbouring components sit next to each other.
---
# Icon Picker TODO

## Features

- [ ] **Designed app icon.** `src/IconPicker.App/Assets/AppIcon.ico` is a generated placeholder: a folder with a picture badge.

## Fixes


---

## Testing

*Checked by hand on the test library: mode detection, single and batch apply, picture conversion, Remove, Deep search, refused folders, libraries and Rescan all, the right-click entry's registry keys, the second-launch hand-off and the published build. These need a person at the keyboard.*

- **Browse.** The file picker, and an .exe or .dll with several icons opening its own grid.
- **Retry as admin.** The UAC prompt on `test-library\Locked`, and declining it.
- **Right-click entry in Explorer.** On a folder, a folder background and a drive, with the app closed and open.
- **Drag and drop** of a folder from Explorer onto the window.
- **Link mode.** Icons linked to an .exe in a subfolder show in Explorer, and the version-folder warning on `Updater`.
- **Restart Explorer.**
- **Icons in Explorer.** The icons set on the test library appear in Explorer after F5.

---

## Features in Consideration

*Left out of v1 in [PROJECT.md](./PROJECT.md).*

### Shell Integration

- **Modern Windows 11 context menu.** A top-level right-click entry instead of one under "Show more options". Needs package identity (MSIX or a sparse package), a signing certificate and a shell-extension component.
- **Background watcher.** A tray process that watches the libraries and sets icons for newly installed games automatically, or notifies to review them.

### Picking

- **Crop tool for images.** Non-square images are padded today, never cropped.
- **Restore replaced icons.** Keep the previous icon setting so a replaced icon can be brought back.
- **Remember excluded folders.** Unticked rows are proposed again on every scan.
- **Edit detection rules in Settings.** Ignored exes, skipped folders and bin-style patterns are built in.
- **Drive icons.** Drives can't take a folder icon; they would need `autorun.inf` or the registry.

### Distribution

- **Code signing.** Free signing through the SignPath Foundation once the first release is out: [Code Signing](DOCUMENTATION/Specs/Code%20Signing.md).
- **winget or the Store.** winget can install straight from the GitHub release zips.
- **Windows 10 support.** Builds for it, but untested.
- **More languages.** The UI text is already in resource files; French would come first.
