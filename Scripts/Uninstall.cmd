@echo off
rem Removes Icon Picker: its right-click entry, its Start menu shortcut and its install folder.
rem Settings and saved icons are kept in %LocalAppData%\Icon Picker (moved there when they were kept next to the app), and so are the icons it gave folders.
setlocal
set "installFolder=%LOCALAPPDATA%\Programs\Icon Picker"

tasklist /FI "IMAGENAME eq IconPicker.exe" | find /I "IconPicker.exe" >nul
if not errorlevel 1 (
    echo Close Icon Picker first, then run this again.
    exit /b 1
)

rem Data kept next to the app (portable) goes back to %LocalAppData%, so settings and saved icons are kept as promised.
if exist "%installFolder%\Data" (
    robocopy "%installFolder%\Data" "%LOCALAPPDATA%\Icon Picker" /E /XD jobs /NFL /NDL /NJH /NJS /NP >nul
    if errorlevel 8 (
        echo Couldn't move the data in "%installFolder%\Data" to "%LOCALAPPDATA%\Icon Picker". Nothing was removed.
        exit /b 1
    )
    echo Moved the app's data to %LOCALAPPDATA%\Icon Picker.
)

reg delete "HKCU\Software\Classes\Directory\shell\IconPicker" /f >nul 2>&1
reg delete "HKCU\Software\Classes\Directory\Background\shell\IconPicker" /f >nul 2>&1
reg delete "HKCU\Software\Classes\Drive\shell\IconPicker" /f >nul 2>&1
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Icon Picker.lnk" >nul 2>&1
if exist "%installFolder%" rmdir /s /q "%installFolder%"

echo Icon Picker was removed.
