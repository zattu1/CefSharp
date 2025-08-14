@echo off
echo Publishing CefSharp.fastBOT...

set OUTPUT_DIR=.\Publish\
set PROJECT_PATH=.\CefSharp.csproj

echo Creating output directory...
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Publishing for Windows x64...
dotnet publish "%PROJECT_PATH%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output "%OUTPUT_DIR%win-x64" ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:PublishTrimmed=false

if %ERRORLEVEL% EQU 0 (
    echo Publish completed successfully!
    echo Output directory: %OUTPUT_DIR%win-x64
    
    REM Copy additional files
    echo Copying additional files...
    copy "Documentation\README.md" "%OUTPUT_DIR%win-x64\"
    copy "Documentation\SETUP.md" "%OUTPUT_DIR%win-x64\"
    
    REM Create zip archive
    echo Creating zip archive...
    powershell -Command "Compress-Archive -Path '%OUTPUT_DIR%win-x64\*' -DestinationPath '%OUTPUT_DIR%CefSharp.fastBOT-win-x64.zip' -Force"
    
    echo Archive created: %OUTPUT_DIR%CefSharp.fastBOT-win-x64.zip
) else (
    echo Publish failed!
    exit /b 1
)

pause