@echo off
powershell.exe -NoProfile -STA -File "%~dp0scripts\Show-PresentationTeacherAccess.ps1" -Staff
if errorlevel 1 pause
