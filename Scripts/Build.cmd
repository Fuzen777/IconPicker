@echo off
rem Builds the whole solution in Debug. Output goes to artifacts\, or where Directory.Build.user.props says (see Directory.Build.props).
dotnet build "%~dp0..\IconPicker.slnx" -c Debug %*
