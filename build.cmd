@echo off

:: Initialise
cd /d "%~dp0"

:: Build
dotnet build -c Release -nologo || goto error

:: Exit
powershell write-host -fore Green Build finished.
cd /d "%~dp0"
timeout /t 2 /nobreak >nul
exit /b

:error
pause
