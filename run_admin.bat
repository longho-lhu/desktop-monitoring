@echo off
title Windows Monitor BLE (Administrator)

:: Kiem tra va tu dong xin quyen Administrator
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"
dotnet run --project WindowsMonitorBLE\WindowsMonitorBLE.csproj
