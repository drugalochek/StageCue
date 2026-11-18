@echo off
setlocal
cd /d "%~dp0"
title StageCue Builder
echo ==========================================
echo             STAGECUE BUILDER
echo ==========================================
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" -Installer
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Read the error above.
  pause
  exit /b 1
)
echo.
echo Build completed.
echo Portable app: artifacts\portable\StageCue.exe
echo Installer:    artifacts\installer\StageCue-Setup-x64.exe
echo.
explorer.exe "%~dp0artifacts"
pause
