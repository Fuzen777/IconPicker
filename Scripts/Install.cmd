@echo off
rem Publishes Icon Picker (Release, self-contained) to %LocalAppData%\Programs\Icon Picker and adds a Start menu shortcut.
rem Run it again to update. The right-click entry is turned on from the app's Settings.
setlocal
set "installFolder=%LOCALAPPDATA%\Programs\Icon Picker"

tasklist /FI "IMAGENAME eq IconPicker.exe" | find /I "IconPicker.exe" >nul
if not errorlevel 1 (
    echo Close Icon Picker first, then run this again.
    exit /b 1
)

dotnet publish "%~dp0..\src\IconPicker.App\IconPicker.App.csproj" -c Release -r win-x64 --self-contained true -o "%installFolder%" || exit /b 1

powershell -NoProfile -ExecutionPolicy Bypass -Command "$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut([IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'Icon Picker.lnk')); $shortcut.TargetPath = Join-Path $env:LOCALAPPDATA 'Programs\Icon Picker\IconPicker.exe'; $shortcut.WorkingDirectory = Join-Path $env:LOCALAPPDATA 'Programs\Icon Picker'; $shortcut.Save()" || exit /b 1

echo.
echo Installed to %installFolder%, with a Start menu shortcut.
echo Turn on the right-click entry in Icon Picker's Settings.
