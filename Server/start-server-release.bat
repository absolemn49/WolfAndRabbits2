@echo off
cd /d "%~dp0"
echo Starting Wolf and Rabbits Server in RELEASE mode (Port 80 - ws://localhost/ws)...
set GAME_ENV=Release
dotnet run -c Release -- --mode=release
pause
