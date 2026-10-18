@echo off
rem Builds and starts the app in Debug. Arguments are passed on, so "Run.cmd D:\Games" opens that folder.
dotnet run --project "%~dp0..\src\IconPicker.App\IconPicker.App.csproj" -c Debug -- %*
