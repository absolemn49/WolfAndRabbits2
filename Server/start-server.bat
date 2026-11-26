@echo off
cd /d "%~dp0"
echo Starting Wolf and Rabbits Server in DEVELOPMENT mode (Port 5000 - ws://localhost:5000/ws)...
set GAME_ENV=Development
dotnet run -- --mode=development
pause
