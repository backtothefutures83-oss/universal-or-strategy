@echo off
TITLE Screenpipe Launch
echo --- SCREENPIPE V12 LAUNCHER (v0.3.232+) ---
echo Starting Screenpipe recording and API on port 11435...

:: Kill any existing instances first
taskkill /F /IM screenpipe.exe /T 2>nul

:: Start Screenpipe
:: Using 'record' subcommand for version 0.3.232+
:: Port 11435 is used for V12 strategies
screenpipe record --port 11435 --use-all-monitors

pause
