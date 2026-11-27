@echo off
setlocal
cd /d "%~dp0"
set "ROOT=%~dp0"
set "OUT=%ROOT%release"

echo Publishing Fresh ^& Care backend...
dotnet publish "%ROOT%Pressings.csproj" -c Release -r win-x64 --self-contained true -o "%OUT%\backend"
if errorlevel 1 goto failed

echo Publishing Fresh ^& Care desktop...
dotnet publish "%ROOT%Pressings.Desktop\Pressings.Desktop.csproj" -c Release -r win-x64 --self-contained true -o "%OUT%\FreshCare"
if errorlevel 1 goto failed

if not exist "%OUT%\FreshCare\backend" mkdir "%OUT%\FreshCare\backend"
xcopy "%OUT%\backend\*" "%OUT%\FreshCare\backend" /E /I /Y >nul
echo.
echo Published: %OUT%\FreshCare\Pressings.Desktop.exe
echo Open that file to start Fresh ^& Care. It creates a private database and keeps secrets in LocalAppData.
echo Microsoft Access and WebView2 Runtime are required.
goto end

:failed
echo Publish failed. Read the error above for the missing prerequisite.
pause
:end
endlocal
