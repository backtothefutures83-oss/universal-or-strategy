@echo off
REM ================================
REM Bob V12 Engineer CLI Template
REM ================================
REM This script is invoked by Traycer to perform surgical refactors.
REM ================================

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$node = 'C:\PROGRA~1\nodejs\node.exe'; ^
 $bobJs = 'C:\Users\MOHAMM~1\AppData\Roaming\npm\node_modules\bobshell\bundle\bob.js'; ^
 $shortPrompt = 'Execute the surgical extraction defined in docs/brain/implementation_plan.md. Read the plan first then implement exactly.'; ^
 cmd /c \"$node $bobJs v12-engineer --prompt \"\"$shortPrompt\"\" --mode advanced --yes --system-prompt \"\"$env:TRAYCER_SYSTEM_PROMPT\"\"\""
