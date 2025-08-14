# CefSharp.fastBOT Setup Script
param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild = $false,
    [switch]$OpenVS = $false
)

Write-Host "CefSharp.fastBOT Setup Script" -ForegroundColor Green
Write-Host "=============================" -ForegroundColor Green

# Check if .NET 6 is installed
Write-Host "Checking .NET 6 installation..." -ForegroundColor Yellow
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: .NET 6 SDK is not installed!" -ForegroundColor Red
    Write-Host "Please install .NET 6 SDK from: https://dotnet.microsoft.com/download/dotnet/6.0" -ForegroundColor Red
    exit 1
}
Write-Host "Found .NET version: $dotnetVersion" -ForegroundColor Green

# Check Visual C++ 2022 Redistributable
Write-Host "Checking Visual C++ 2022 Redistributable..." -ForegroundColor Yellow
$vcRedist = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*" | 
            Where-Object { $_.DisplayName -like "*Visual C++ 2022*" -and $_.DisplayName -like "*x64*" }
if (-not $vcRedist) {
    Write-Host "Warning: Visual C++ 2022 Redistributable (x64) may not be installed!" -ForegroundColor Yellow
    Write-Host "CefSharp v138+ requires VC++ 2022. Download from:" -ForegroundColor Yellow
    Write-Host "https://aka.ms/vs/17/release/vc_redist.x64.exe" -ForegroundColor Yellow
} else {
    Write-Host "Visual C++ 2022 Redistributable found" -ForegroundColor Green
}

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore CefSharp.sln
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Failed to restore NuGet packages!" -ForegroundColor Red
    exit 1
}

# Build solution (unless skipped)
if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build CefSharp.sln --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Build failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "Build completed successfully!" -ForegroundColor Green
}

# Create necessary directories
$appDataPath = "$env:LOCALAPPDATA\fastBOT"
Write-Host "Creating application data directory: $appDataPath" -ForegroundColor Yellow
if (-not (Test-Path $appDataPath)) {
    New-Item -ItemType Directory -Path $appDataPath -Force | Out-Null
}

# Set up development environment
Write-Host "Setting up development environment..." -ForegroundColor Yellow

# Create launch settings if not exists
$launchSettingsPath = "Properties\launchSettings.json"
$launchSettingsDir = Split-Path $launchSettingsPath
if (-not (Test-Path $launchSettingsDir)) {
    New-Item -ItemType Directory -Path $launchSettingsDir -Force | Out-Null
}

if (-not (Test-Path $launchSettingsPath)) {
    $launchSettings = @{
        profiles = @{
            "CefSharp.fastBOT" = @{
                commandName = "Project"
                commandLineArgs = "--debug"
                workingDirectory = "$(ProjectDir)"
                environmentVariables = @{
                    "CEFSHARP_DEBUG" = "1"
                }
            }
        }
    } | ConvertTo-Json -Depth 3

    Set-Content -Path $launchSettingsPath -Value $launchSettings -Encoding UTF8
    Write-Host "Created launch settings" -ForegroundColor Green
}

Write-Host ""
Write-Host "Setup completed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Open CefSharp.sln in Visual Studio" -ForegroundColor White
Write-Host "2. Ensure platform is set to x64" -ForegroundColor White
Write-Host "3. Press F5 to run" -ForegroundColor White
Write-Host ""

if ($OpenVS) {
    Write-Host "Opening Visual Studio..." -ForegroundColor Yellow
    Start-Process "CefSharp.sln"
}