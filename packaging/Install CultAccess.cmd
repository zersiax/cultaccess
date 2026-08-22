@echo off
rem Double-click entry point for the CultAccess installer.
rem The supporting scripts live in installer\ so this is the only file to click.
rem Self-elevates: the game normally lives under Program Files, which needs permission.
setlocal
set "SCRIPT=%~dp0installer\installer-gui.ps1"

if not exist "%SCRIPT%" (
  echo Could not find installer\installer-gui.ps1 next to this file.
  echo Extract the whole zip and keep its folders together, then try again.
  pause
  goto :eof
)

net session >nul 2>&1
if %errorlevel%==0 goto run

rem The path must carry its own quotes. Start-Process joins ArgumentList with spaces into
rem one command line, so an unquoted path under, say, C:\games\cult of the lamb\ arrives at
rem the elevated PowerShell as -File C:\games\cult and the installer never starts.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','\"%SCRIPT%\"'"
goto :eof

:run
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%"
