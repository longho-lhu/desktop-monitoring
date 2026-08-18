@echo off
title Windows Monitor BLE (Administrator)
:: Kiem tra quyen Admin
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Dang yeu cau quyen Administrator de doc sensor nhiet do CPU/GPU...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"
if exist "bin\WindowsMonitorBLE.exe" (
    echo Dang khoi dong Windows Monitor BLE voi quyen Administrator...
    start "" "bin\WindowsMonitorBLE.exe"
) else (
    cd /d "%~dp0\WindowsMonitorBLE"
    dotnet run
)
