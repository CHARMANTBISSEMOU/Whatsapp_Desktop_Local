$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$nuget = Join-Path $PSScriptRoot "nuget.exe"
if (-not (Test-Path $nuget)) {
    Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile $nuget
}
& $nuget restore (Join-Path $root "src\P2PChat.App\packages.config") `
    -PackagesDirectory (Join-Path $root "packages") -NonInteractive
if ($LASTEXITCODE -ne 0) { throw "La restauration des dépendances audio/WebRTC a échoué." }
Write-Host "Dépendances audio/WebRTC prêtes." -ForegroundColor Green
