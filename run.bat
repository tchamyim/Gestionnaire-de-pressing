@echo off
setlocal
cd /d "%~dp0"

set "APP=%~dp0release\FreshCare\Pressings.Desktop.exe"
if exist "%APP%" (
    echo Starting Fresh ^& Care...
    start "" "%APP%"
    if errorlevel 1 goto failed
    goto end
)

echo Building Fresh ^& Care backend...
dotnet build "%~dp0Pressings.csproj" --no-restore -p:UseAppHost=false
if errorlevel 1 goto failed
echo Starting Fresh ^& Care desktop application...
dotnet run --project "%~dp0Pressings.Desktop\Pressings.Desktop.csproj" --no-restore
if errorlevel 1 goto failed
goto end

:failed
echo Fresh ^& Care could not be started. Correct the error shown above.
pause
:end
endlocal
