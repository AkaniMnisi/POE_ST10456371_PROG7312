@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Install the .NET 8 SDK, then run this file again.
 pause
 exit /b 1
)
echo Smart-X starts at http://localhost:5080
 echo Wait for 'Now listening on' then open that address in your browser.
dotnet run --project src\SmartX.Api
pause
