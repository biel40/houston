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

# PawnIO's official installer, pinned and hash-checked: the MSI runs it silently so CPU temperatures work
# out of the box. Downloaded once into installer/deps (git-ignored); bump all three values to update it.
$pawnIOVersion = '2.2.0'
$pawnIOSha256 = '1F519A22E47187F70A1379A48CA604981C4FCF694F4E65B734AAA74A9FBA3032'
$pawnIOSetup = Join-Path $root "installer\deps\PawnIO_setup-$pawnIOVersion.exe"
if (-not (Test-Path $pawnIOSetup)) {
    Write-Host "==> Downloading PawnIO $pawnIOVersion" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path (Split-Path $pawnIOSetup) | Out-Null
    Invoke-WebRequest "https://github.com/namazso/PawnIO.Setup/releases/download/$pawnIOVersion/PawnIO_setup.exe" `
        -OutFile "$pawnIOSetup.part" -UseBasicParsing
    Move-Item "$pawnIOSetup.part" $pawnIOSetup -Force
}
if ((Get-FileHash $pawnIOSetup -Algorithm SHA256).Hash -ne $pawnIOSha256) {
    Remove-Item $pawnIOSetup -Force
    throw "PawnIO_setup.exe does not match the pinned SHA-256; deleted it. Check the release before retrying."
}

Invoke-Step 'Building UI' { npm --prefix (Join-Path $root 'ui') run build }

# Start clean so files removed from the app never linger in the installer.
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
Invoke-Step 'Publishing host' {
    dotnet publish (Join-Path $root 'src\Houston.App') -c Release -r win-x64 --self-contained -o $publishDir -p:DebugType=none
}

Invoke-Step 'Building installer' {
    dotnet build (Join-Path $root 'installer\Houston.Installer.wixproj') -c Release "-p:PawnIOSetup=$pawnIOSetup"
}

$version = (Get-Item (Join-Path $publishDir 'Houston.exe')).VersionInfo.ProductVersion -replace '\+.*$', ''
$msi = Join-Path $root "publish\Houston-$version.msi"
Copy-Item (Join-Path $root 'installer\bin\x64\Release\Houston.msi') $msi -Force
Write-Host "==> $msi" -ForegroundColor Green
