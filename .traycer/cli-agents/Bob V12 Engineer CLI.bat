@echo off
REM ================================
REM Bob V12 Engineer CLI Template
REM ================================
REM This script is invoked by Traycer to perform surgical refactors.
REM ================================

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
"$shortPrompt = 'Execute the surgical extraction defined in docs/brain/implementation_plan.md. Read the plan first then implement exactly.'; ^
 cmd /c \"bob v12-engineer --prompt \"\"$shortPrompt\"\" --mode advanced --yes --system-prompt \"\"$env:TRAYCER_SYSTEM_PROMPT\"\"\""
