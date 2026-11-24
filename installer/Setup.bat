@echo off
title NotifyX Payment Reminder - Setup
echo Launching Installer with Administrator privileges...
powershell -ExecutionPolicy Bypass -NoProfile -File "%~dp0Install-NotifyX.ps1"
pause
