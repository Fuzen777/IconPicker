@echo off
rem Publishes Icon Picker in its release layout (see Publish.cmd) to %LocalAppData%\Programs\Icon Picker and adds a Start menu shortcut.
rem Run it again to update: the previous files go, but data kept next to the app (Data) and the folder's own icon stay. The right-click entry is turned on from the app's Settings.
setlocal
set "installFolder=%LOCALAPPDATA%\Programs\Icon Picker"

tasklist /FI "IMAGENAME eq IconPicker.exe" | find /I "IconPicker.exe" >nul
if not errorlevel 1 (
    echo Close Icon Picker first, then run this again.
    exit /b 1
)

if exist "%installFolder%" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath $env:installFolder -Force | Where-Object { $_.Name -notin 'Data', 'desktop.ini', 'folder-icon.ico' } | Remove-Item -Recurse -Force" || exit /b 1
)
call "%~dp0Publish.cmd" "%installFolder%" || exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -Command "$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'Icon Picker.lnk')); $shortcut.TargetPath = Join-Path $env:LOCALAPPDATA 'Programs\Icon Picker\IconPicker.exe'; $shortcut.WorkingDirectory = Join-Path $env:LOCALAPPDATA 'Programs\Icon Picker'; $shortcut.Save()" || exit /b 1

echo.
echo Installed to %installFolder%, with a Start menu shortcut.
echo Turn on the right-click entry in Icon Picker's Settings.
