@echo off
rem Builds with warnings as errors, runs the tests and checks formatting. Stops at the first failure.
rem The build starts from scratch: an incremental one skips projects an earlier build already compiled, and their warnings with them.
set "solution=%~dp0..\IconPicker.slnx"
dotnet build "%solution%" -c Debug -warnaserror --no-incremental || exit /b 1
dotnet test --solution "%solution%" -c Debug --no-build || exit /b 1
dotnet format "%solution%" --verify-no-changes --no-restore || exit /b 1
echo All checks passed.
