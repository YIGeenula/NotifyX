@echo off
setlocal
cd /d "%~dp0"
title Rebuilding NotifyX Payment Reminder EXE...

echo ========================================================
echo    REBUILDING NOTIFYX PAYMENT REMINDER (.NET 10 WPF)
echo ========================================================
echo.

echo [1/3] Publishing Release single-file executable...
dotnet publish "src\NotifyX.Wpf\NotifyX.Wpf.csproj" -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o publish

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed! Check the output above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/3] Updating installer and distribution packages...
if not exist "installer" mkdir "installer"
xcopy /y /q "publish\*" "installer\" >nul
xcopy /y /q "publish\*" "installer_dist\" >nul

echo.
echo [3/3] Build completed successfully!
echo.
echo  Output Location:
echo    - publish\NotifyX.exe
echo    - installer\NotifyX.exe
echo.
pause
