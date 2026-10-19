# Builds publish/Houston-<version>.msi: UI build, self-contained publish of the host, then the WiX package.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-installer.ps1
# The publish is self-contained so the target PC needs no .NET runtime; WebView2 ships with Windows 11.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDir = Join-Path $root 'publish\Houston'

function Invoke-Step([string] $title, [scriptblock] $command) {
    Write-Host "==> $title" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$title failed (exit code $LASTEXITCODE)." }
}

Invoke-Step 'Building UI' { npm --prefix (Join-Path $root 'ui') run build }

# Start clean so files removed from the app never linger in the installer.
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
Invoke-Step 'Publishing host' {
    dotnet publish (Join-Path $root 'src\Houston.App') -c Release -r win-x64 --self-contained -o $publishDir -p:DebugType=none
}

Invoke-Step 'Building installer' {
    dotnet build (Join-Path $root 'installer\Houston.Installer.wixproj') -c Release
}

$version = (Get-Item (Join-Path $publishDir 'Houston.exe')).VersionInfo.ProductVersion -replace '\+.*$', ''
$msi = Join-Path $root "publish\Houston-$version.msi"
Copy-Item (Join-Path $root 'installer\bin\x64\Release\Houston.msi') $msi -Force
Write-Host "==> $msi" -ForegroundColor Green
