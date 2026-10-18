# Builds a throwaway game library for trying Icon Picker by hand, covering each case the app handles.
# Run it again to start over: the previous library is deleted first.
#   -Path   where to build it (default %TEMP%\IconPicker\test-library)
param(
    [string]$Path = (Join-Path ([System.IO.Path]::GetTempPath()) "IconPicker\test-library")
)

Add-Type -AssemblyName System.Drawing

# Executables with icons of their own, so each game looks different. Missing ones fall back to the .NET host.
$fallbackExe = Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
$iconExes = @(
    (Join-Path $env:ProgramFiles "Git\git-bash.exe"),
    (Join-Path $env:ProgramFiles "Mozilla Firefox\firefox.exe"),
    (Join-Path $env:ProgramFiles "Google\Chrome\Application\chrome.exe"),
    $fallbackExe
) | ForEach-Object { if (Test-Path -LiteralPath $_) { $_ } else { $fallbackExe } }

function Copy-IconExe([int]$exeNumber, [string]$relativePath) {
    $target = Join-Path $Path $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath $iconExes[$exeNumber % $iconExes.Count] -Destination $target
}

function New-Picture([string]$relativePath, [int]$width, [int]$height, [System.Drawing.Color]$color) {
    $target = Join-Path $Path $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($color)
    $graphics.FillEllipse([System.Drawing.Brushes]::White, [int]($width / 4), [int]($height / 4), [int]($width / 2), [int]($height / 2))
    $graphics.Dispose()
    $bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

if (Test-Path -LiteralPath $Path) {
    # Locked's permissions keep even its owner from deleting it until they are reset.
    $lockedFolder = Join-Path $Path "Locked"
    if (Test-Path -LiteralPath $lockedFolder) {
        icacls $lockedFolder /reset /T /Q | Out-Null
    }
    Get-ChildItem -LiteralPath $Path -Recurse -Force | ForEach-Object { $_.Attributes = "Normal" }
    Remove-Item -LiteralPath $Path -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $Path | Out-Null

# 1. A plain game, with an uninstaller that must never be picked.
Copy-IconExe 0 "GameA\GameA.exe"
Copy-IconExe 3 "GameA\unins000.exe"
# 2. The game's exe sits in bin\x64.
Copy-IconExe 1 "GameB\bin\x64\GameB.exe"
# 3. No exe at all, only a wide cover picture: converted with transparent padding.
New-Picture "GameC\cover.png" 600 300 ([System.Drawing.Color]::SteelBlue)
New-Picture "GameC\Screenshots\shot1.png" 320 180 ([System.Drawing.Color]::DarkOliveGreen)
# 4. Already has a hand-set icon from elsewhere: shown with it, unticked in batch.
Copy-IconExe 2 "GameD\GameD.exe"
$externalIcons = Join-Path (Split-Path $Path) "test-icons"
New-Item -ItemType Directory -Force -Path $externalIcons | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "..\src\IconPicker.App\Assets\AppIcon.ico") -Destination (Join-Path $externalIcons "custom.ico") -Force
$gameD = Join-Path $Path "GameD"
Set-Content -LiteralPath (Join-Path $gameD "desktop.ini") -Value "[.ShellClassInfo]`r`nIconResource=$externalIcons\custom.ico,0" -Encoding Unicode
(Get-Item -LiteralPath (Join-Path $gameD "desktop.ini") -Force).Attributes = "Hidden, System"
(Get-Item -LiteralPath $gameD).Attributes = "Directory, ReadOnly"
# 5. The only app sits in an odd subfolder: Tools gets its icon too in the deep search.
Copy-IconExe 3 "Tools\Release\tool.exe"
# 6. Steam holding a game in its library.
Copy-IconExe 2 "Steam\steam.exe"
Copy-IconExe 0 "Steam\steamapps\common\GameE\GameE.exe"
# 7. An auto-updating app in a version folder: linking to it warns that the next update breaks the icon.
Copy-IconExe 3 "Updater\app-1.2.3\updater.exe"
# 8. Only admins may write here, so the app is refused and offers to retry as admin.
Copy-IconExe 1 "Locked\Locked.exe"
$lockedPath = Join-Path $Path "Locked"
icacls $lockedPath /inheritance:r /grant:r "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-18:(OI)(CI)F" "$($env:USERNAME):(OI)(CI)RX" /Q | Out-Null
# 9. Only shortcuts to an app installed elsewhere: a .lnk, a Steam-style .url pointing at an .ico, and an uninstaller shortcut that must never be picked. "Include shortcuts" hides them.
$shortcutApp = Join-Path $externalIcons "ShortcutApp.exe"
Copy-Item -LiteralPath $iconExes[1] -Destination $shortcutApp -Force
New-Item -ItemType Directory -Force -Path (Join-Path $Path "Shortcuts") | Out-Null
$shell = New-Object -ComObject WScript.Shell
foreach ($shortcutName in "Shortcuts.lnk", "Uninstall Shortcuts.lnk") {
    $shortcut = $shell.CreateShortcut((Join-Path $Path "Shortcuts\$shortcutName"))
    $shortcut.TargetPath = $shortcutApp
    $shortcut.Save()
}
Set-Content -LiteralPath (Join-Path $Path "Shortcuts\Play.url") -Value "[InternetShortcut]`r`nURL=steam://rungameid/1`r`nIconIndex=0`r`nIconFile=$externalIcons\custom.ico" -Encoding Ascii
# 10. No exe, only SVGs: the square logo ranks first and is drawn at every size; the two-color badge keeps its colors in the builder; the template (drawn only once colored) and the sprite sheet (symbols, nothing drawn) are left out.
$svgFolder = Join-Path $Path "Vectors"
New-Item -ItemType Directory -Force -Path (Join-Path $svgFolder "ui") | Out-Null
$svgFiles = @{
    "logo.svg"        = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="#2E7D32" d="M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20zm-1 14-4-4 1.4-1.4 2.6 2.6 5.6-5.6L18 9z"/></svg>'
    "ui\badge.svg"    = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="6" fill="#C62828"/><circle cx="16" cy="16" r="7" fill="#FFFFFF"/></svg>'
    "ui\template.svg" = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16"><path fill="context-fill" d="M2 2h12v12H2z"/></svg>'
    "ui\sprites.svg"  = '<svg xmlns="http://www.w3.org/2000/svg"><defs><symbol id="star" viewBox="0 0 24 24"><path d="M12 2l3 7h7l-6 4 2 8-6-5-6 5 2-8-6-4h7z"/></symbol></defs></svg>'
}
foreach ($svgFile in $svgFiles.GetEnumerator()) {
    Set-Content -LiteralPath (Join-Path $svgFolder $svgFile.Key) -Value $svgFile.Value -Encoding UTF8
}
# 11. A shortcut to a game kept outside the library: "Each subfolder" lists it as a row searched in its folder, and applying gives the folder its icon and points the shortcut at it.
$linkedGame = Join-Path (Split-Path $Path) "test-linked\LinkedGame"
New-Item -ItemType Directory -Force -Path $linkedGame | Out-Null
Copy-Item -LiteralPath $iconExes[3 % $iconExes.Count] -Destination (Join-Path $linkedGame "LinkedGame.exe") -Force
$folderShortcut = $shell.CreateShortcut((Join-Path $Path "Linked Game.lnk"))
$folderShortcut.TargetPath = $linkedGame
$folderShortcut.Save()
# 12. Nothing to take an icon from: "No icon found", hidden by "Hide folders where no icon was found".
New-Item -ItemType Directory -Force -Path (Join-Path $Path "Empty\Saves") | Out-Null

Write-Host "Test library ready at $Path"
