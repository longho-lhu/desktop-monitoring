@echo off
title Windows Monitor BLE
cd /d "%~dp0"
dotnet run --project WindowsMonitorBLE\WindowsMonitorBLE.csproj
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Co loi xay ra khi chay ung dung.
    pause
)
