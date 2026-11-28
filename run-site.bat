@echo off
setlocal
cd /d "%~dp0"
if not defined PORT set "PORT=5085"
set "Cookies__Secure=false"

echo Building Fresh ^& Care website...
dotnet build "%~dp0Pressings.csproj"
if errorlevel 1 goto failed

echo.
echo Fresh ^& Care is starting on port %PORT%.
echo On this computer: http://localhost:%PORT%
echo Keep this window open while the website is in use.
dotnet run --no-build --project "%~dp0Pressings.csproj"
if errorlevel 1 goto failed
goto end

:failed
echo Fresh ^& Care website could not be started. Correct the error shown above.
pause
:end
endlocal
