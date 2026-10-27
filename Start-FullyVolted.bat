@echo off
rem Starts the results API and the web portal, then opens the portal in the browser.
rem Unity is not started here: open the project from Unity Hub as usual.
cd /d "%~dp0"

sc query MySQL84 | find "RUNNING" >nul
if errorlevel 1 (
    echo MySQL is not running. Start it from an Administrator window:  net start MySQL84
    pause
    exit /b 1
)

start "FullyVolted API" cmd /k "cd /d %~dp0Api && .venv\Scripts\python -m uvicorn app.main:app"
start "FullyVolted Portal" cmd /k "cd /d %~dp0Portal && npm run dev"

rem give both a few seconds to come up before the browser opens
timeout /t 6 /nobreak >nul
start "" http://localhost:5173
