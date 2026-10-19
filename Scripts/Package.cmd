@echo off
rem Makes a release zip: publishes the release layout (see Publish.cmd) and zips it as IconPicker-<version>-win-x64.zip with its SHA-256,
rem in the build root's release folder (artifacts\release, or where Directory.Build.user.props points). The zip holds an "Icon Picker" folder,
rem so extracting a newer one to the same place updates it and keeps a portable Data folder. Run Check.cmd first; the release workflow does.
setlocal
set "project=%~dp0..\src\IconPicker.App\IconPicker.App.csproj"

where pwsh >nul 2>&1
if errorlevel 1 (
    echo PowerShell 7 ^(pwsh^) is needed to make the zip.
    exit /b 1
)

for /f "usebackq delims=" %%v in (`dotnet msbuild "%project%" -getProperty:Version`) do set "version=%%v"
for /f "usebackq delims=" %%r in (`dotnet msbuild "%project%" -getProperty:IconPickerBuildRoot`) do set "buildRoot=%%r"
if not defined version exit /b 1
if not defined buildRoot exit /b 1

set "releaseFolder=%buildRoot%\release"
set "stagingFolder=%releaseFolder%\staging"
set "zipName=IconPicker-%version%-win-x64.zip"
if exist "%stagingFolder%" rmdir /s /q "%stagingFolder%"

call "%~dp0Publish.cmd" "%stagingFolder%\Icon Picker" || exit /b 1

rem Zipped by .NET in PowerShell 7: Windows PowerShell 5.1 writes backslashes into zip entry names, which other tools read as flat file names.
pwsh -NoProfile -Command "$ErrorActionPreference = 'Stop'; $zip = Join-Path $env:releaseFolder $env:zipName; Remove-Item -LiteralPath $zip, \"$zip.sha256\" -ErrorAction SilentlyContinue; [IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $env:stagingFolder 'Icon Picker'), $zip, 'Optimal', $true); $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant(); Set-Content -LiteralPath \"$zip.sha256\" -Value \"$hash  $env:zipName\" -Encoding ascii" || exit /b 1

rmdir /s /q "%stagingFolder%"
echo.
echo Made %releaseFolder%\%zipName%
