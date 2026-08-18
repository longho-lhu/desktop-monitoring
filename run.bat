@echo off
title Windows Monitor BLE
echo Dang khoi dong ung dung Windows Monitor BLE...
cd /d "%~dp0\WindowsMonitorBLE"
dotnet run
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Co loi xay ra khi chay ung dung.
    pause
)
