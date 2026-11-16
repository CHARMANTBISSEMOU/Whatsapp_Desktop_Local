# Build de P2PChat — compile via le compilateur Roslyn local (csc), sans droits admin.
# Usage : powershell -NoProfile -ExecutionPolicy Bypass -File build\build.ps1 [-Configuration Release|Debug]
param(
    [string]$Configuration = "Release"
)
$root = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot "compile.ps1") -Configuration $Configuration
exit $LASTEXITCODE
