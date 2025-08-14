@echo off
echo Building CefSharp.fastBOT...

REM Clean solution
echo Cleaning solution...
dotnet clean CefSharp.sln --configuration Release

REM Restore packages
echo Restoring NuGet packages...
dotnet restore CefSharp.sln

REM Build solution
echo Building solution...
dotnet build CefSharp.sln --configuration Release --no-restore

if %ERRORLEVEL% EQU 0 (
    echo Build completed successfully!
) else (
    echo Build failed!
    exit /b 1
)

pause