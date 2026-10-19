@echo off
rem Publishes Icon Picker (Release, self-contained, win-x64) in its release layout to the folder given:
rem   IconPicker.exe     the launcher, which starts lib\IconPicker.exe
rem   LICENSE.txt, THIRD-PARTY-NOTICES.md
rem   lib\               the app and its runtime
rem Used by Package.cmd and Install.cmd.
setlocal
set "target=%~1"
if not defined target (
    echo Usage: Publish.cmd ^<folder^>
    exit /b 1
)
set "root=%~dp0.."

dotnet publish "%root%\src\IconPicker.App\IconPicker.App.csproj" -c Release -r win-x64 --self-contained true -o "%target%\lib" || exit /b 1
dotnet publish "%root%\src\IconPicker.Launcher\IconPicker.Launcher.csproj" -c Release -o "%target%" || exit /b 1
copy /y "%root%\LICENSE" "%target%\LICENSE.txt" >nul || exit /b 1
copy /y "%root%\THIRD-PARTY-NOTICES.md" "%target%\THIRD-PARTY-NOTICES.md" >nul || exit /b 1
