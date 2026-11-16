# Extrait les outils de compilation depuis les archives suivies dans tools\.
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$tools = $PSScriptRoot
$roslynPackage = Join-Path $tools "roslyn.nupkg"
$roslynTemp = Join-Path $tools "roslyn-unpack"
$roslyn = Join-Path $tools "roslyn"
$refasm = Join-Path $tools "refasm\.NETFramework\v4.8"
$refasmPackage = Join-Path $tools "refasm.nupkg"

if (-not (Test-Path $roslynPackage)) {
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.14.0/microsoft.net.compilers.toolset.4.14.0.nupkg" -OutFile $roslynPackage
}
if (-not (Test-Path $refasmPackage)) {
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net48/1.0.3/microsoft.netframework.referenceassemblies.net48.1.0.3.nupkg" -OutFile $refasmPackage
}
if (-not (Test-Path (Join-Path $roslyn "csc.exe"))) {
    if (Test-Path $roslynTemp) { Remove-Item $roslynTemp -Recurse -Force }
    [System.IO.Compression.ZipFile]::ExtractToDirectory($roslynPackage, $roslynTemp)
    New-Item -ItemType Directory -Force -Path $roslyn | Out-Null
    Get-ChildItem (Join-Path $roslynTemp "tasks\net472") |
        Copy-Item -Destination $roslyn -Recurse -Force
    Remove-Item $roslynTemp -Recurse -Force
}

if (-not (Test-Path $refasm)) {
    & (Join-Path $tools "setup-refasm.ps1")
    if (-not (Test-Path $refasm)) { throw "Extraction des références .NET 4.8 échouée." }
}

Write-Host "Outils prêts : Roslyn et références .NET Framework 4.8." -ForegroundColor Green
