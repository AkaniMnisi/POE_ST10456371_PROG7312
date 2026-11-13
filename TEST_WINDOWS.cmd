@echo off
cd /d "%~dp0"
dotnet run --project tests\SmartX.Tests
pause
