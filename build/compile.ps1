# Compilation DIRECTE de P2PChat via le compilateur Roslyn local (tools\roslyn\csc.exe).
# Aucun droit administrateur requis, aucune installation Visual Studio complementaire.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $root "tools\roslyn\csc.exe"
$ref = Join-Path $root "tools\refasm\.NETFramework\v4.8"
if (-not (Test-Path $csc) -or -not (Test-Path $ref)) {
    throw "Outils absents. Lancez d'abord tools\setup-tools.ps1."
}
$sipDll = Join-Path $root "packages\SIPSorcery\lib\net462\SIPSorcery.dll"
if (-not (Test-Path $sipDll)) { & (Join-Path $root "tools\setup-audio.ps1") }

$define = "/define:DEBUG;TRACE"
$opt = "/optimize-"
$debug = "/debug:full"
if ($Configuration -eq "Release") {
    $define = "/define:TRACE"
    $opt = "/optimize+"
    $debug = "/debug:pdbonly"
}

function Invoke-Csc {
    param([string]$out, [string]$target, [string]$projDir, [string[]]$refs, [string[]]$extra)
    $sources = @(Get-ChildItem -LiteralPath $projDir -Recurse -Filter *.cs -File |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        ForEach-Object { $_.FullName })
    if ($sources.Count -eq 0) { throw "Aucun fichier source dans $projDir" }
    $argv = @('/nologo', '/warn:4', '/langversion:7.3', '/codepage:65001', $debug,
              "/target:$target", "/out:$out") + $refs + $extra + $sources
    foreach ($a in $argv) { if ($a -eq $null) { throw "Argument null dans $projDir" } }
    & $csc @argv
    if ($LASTEXITCODE -ne 0) { throw "Echec de compilation -> $out" }
    Write-Host "OK  $out ($($sources.Count) fichiers)" -ForegroundColor Green
}

$dirs = @(
    "src\P2PChat.Core\bin\$Configuration",
    "src\P2PChat.Net\bin\$Configuration",
    "src\P2PChat.App\bin\$Configuration",
    "src\P2PChat.Tests\bin\$Configuration"
)
foreach ($d in $dirs) { New-Item -ItemType Directory -Force -Path (Join-Path $root $d) | Out-Null }

$coreBin = Join-Path $root "src\P2PChat.Core\bin\$Configuration"
$netBin = Join-Path $root "src\P2PChat.Net\bin\$Configuration"
$appBin = Join-Path $root "src\P2PChat.App\bin\$Configuration"
$testsBin = Join-Path $root "src\P2PChat.Tests\bin\$Configuration"

Write-Host "=== Compilation P2PChat ($Configuration, csc local) ===" -ForegroundColor Cyan
Write-Host "Compilateur : $csc"

# 1) P2PChat.Core
Invoke-Csc -out (Join-Path $coreBin "P2PChat.Core.dll") -target library `
    -projDir (Join-Path $root "src\P2PChat.Core") `
    -refs @("/r:$ref\System.dll", "/r:$ref\System.Core.dll") `
    -extra @($define, $opt)

# 2) P2PChat.Net
Invoke-Csc -out (Join-Path $netBin "P2PChat.Net.dll") -target library `
    -projDir (Join-Path $root "src\P2PChat.Net") `
    -refs @("/r:$ref\System.dll", "/r:$ref\System.Core.dll",
            "/r:$coreBin\P2PChat.Core.dll") `
    -extra @($define, $opt)

# 3) Application autonome : Core + Net + App dans un unique p2p.exe.
$appSources = @(
    "src\P2PChat.Core",
    "src\P2PChat.Net",
    "src\P2PChat.App"
) | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $root $_) -Recurse -Filter *.cs -File |
        Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
        ForEach-Object { $_.FullName }
}
$appOut = Join-Path $appBin "p2p.exe"
$appArgs = @('/nologo', '/warn:4', '/langversion:7.3', '/codepage:65001', $debug,
             '/target:winexe', "/out:$appOut") +
           @("/r:$ref\System.dll", "/r:$ref\System.Core.dll",
             "/r:$ref\System.Windows.Forms.dll", "/r:$ref\System.Drawing.dll",
             "/r:$ref\Facades\netstandard.dll",
             "/r:$sipDll",
             "/r:$root\packages\SIPSorceryMedia.Abstractions\lib\netstandard2.0\SIPSorceryMedia.Abstractions.dll",
             "/r:$root\packages\NAudio.Core\lib\netstandard2.0\NAudio.Core.dll",
             "/r:$root\packages\NAudio.WinMM\lib\netstandard2.0\NAudio.WinMM.dll",
             "/r:$root\packages\AForge\lib\AForge.dll",
             "/r:$root\packages\AForge.Video\lib\AForge.Video.dll",
             "/r:$root\packages\AForge.Video.DirectShow\lib\AForge.Video.DirectShow.dll",
             "/r:$root\packages\System.Memory\lib\net462\System.Memory.dll",
             "/r:$root\packages\System.ValueTuple\lib\net462\System.ValueTuple.dll",
             $define, $opt, "/win32icon:$root\assets\whatsapp.ico",
             "/win32manifest:$root\src\P2PChat.App\app.manifest") + $appSources
& $csc @appArgs
if ($LASTEXITCODE -ne 0) { throw "Echec de compilation -> $appOut" }
$mergeDll = Join-Path $root "packages\ILRepack\tools\ILRepack.exe"
$mergeInputs = @(
    $appOut,
    $sipDll,
    "$root\packages\SIPSorceryMedia.Abstractions\lib\netstandard2.0\SIPSorceryMedia.Abstractions.dll",
    "$root\packages\NAudio.Core\lib\netstandard2.0\NAudio.Core.dll",
    "$root\packages\NAudio.WinMM\lib\netstandard2.0\NAudio.WinMM.dll",
    "$root\packages\AForge\lib\AForge.dll",
    "$root\packages\AForge.Video\lib\AForge.Video.dll",
    "$root\packages\AForge.Video.DirectShow\lib\AForge.Video.DirectShow.dll",
    "$root\packages\Concentus\lib\netstandard1.1\Concentus.dll",
    "$root\packages\BouncyCastle.Cryptography\lib\net461\BouncyCastle.Cryptography.dll",
    "$root\packages\DnsClient\lib\net472\DnsClient.dll",
    "$root\packages\Makaretu.Dns\lib\net472\Makaretu.Dns.dll",
    "$root\packages\Makaretu.Dns.Multicast\lib\net461\Makaretu.Dns.Multicast.dll",
    "$root\packages\SIPSorcery.WebSocketSharp\lib\net452\websocket-sharp.dll",
    "$root\packages\Microsoft.Extensions.Logging.Abstractions\lib\net462\Microsoft.Extensions.Logging.Abstractions.dll",
    "$root\packages\Microsoft.Extensions.DependencyInjection.Abstractions\lib\net462\Microsoft.Extensions.DependencyInjection.Abstractions.dll",
    "$root\packages\Microsoft.Bcl.AsyncInterfaces\lib\net462\Microsoft.Bcl.AsyncInterfaces.dll",
    "$root\packages\Microsoft.Bcl.HashCode\lib\net462\Microsoft.Bcl.HashCode.dll",
    "$root\packages\System.Diagnostics.DiagnosticSource\lib\net462\System.Diagnostics.DiagnosticSource.dll",
    "$root\packages\System.Buffers\lib\net462\System.Buffers.dll",
    "$root\packages\System.Memory\lib\net462\System.Memory.dll",
    "$root\packages\System.Numerics.Vectors\lib\net462\System.Numerics.Vectors.dll",
    "$root\packages\System.Runtime.CompilerServices.Unsafe\lib\net462\System.Runtime.CompilerServices.Unsafe.dll",
    "$root\packages\System.ValueTuple\lib\net462\System.ValueTuple.dll",
    "$root\packages\System.Net.Security\lib\net46\System.Net.Security.dll",
    "$root\packages\System.Net.WebSockets.Client\lib\net46\System.Net.WebSockets.Client.dll"
)
$merged = Join-Path $appBin "p2p-merged.exe"
& $mergeDll "/out:$merged" "/target:winexe" "/lib:$root\packages" "/internalize" $mergeInputs
if ($LASTEXITCODE -ne 0) { throw "Échec de l'assemblage des dépendances dans p2p.exe." }
Move-Item -LiteralPath $merged -Destination $appOut -Force
Write-Host "OK  $appOut ($($appSources.Count) fichiers, dépendances incluses)" -ForegroundColor Green

# 4) P2PChat.Tests (console)
Invoke-Csc -out (Join-Path $testsBin "P2PChat.Tests.exe") -target exe `
    -projDir (Join-Path $root "src\P2PChat.Tests") `
    -refs @("/r:$ref\System.dll", "/r:$ref\System.Core.dll",
            "/r:$coreBin\P2PChat.Core.dll",
            "/r:$netBin\P2PChat.Net.dll") `
    -extra @($define, $opt)

# Copie des DLL de dependances a cote des executables
foreach ($bin in @($netBin, $testsBin)) {
    Copy-Item (Join-Path $coreBin "P2PChat.Core.dll") $bin -Force
}
foreach ($bin in @($testsBin)) {
    Copy-Item (Join-Path $netBin "P2PChat.Net.dll") $bin -Force
}

# Assemblage du dossier dist
$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item $appOut $dist -Force
Copy-Item (Join-Path $root "README.md") $dist -Force
Copy-Item (Join-Path $root "CONFIGURATION.md") $dist -Force
Copy-Item (Join-Path $root "SECURITY.md") $dist -Force

Write-Host ""
Remove-Item (Join-Path $dist "P2PChat.exe"), (Join-Path $dist "P2PChat.Core.dll"), `
            (Join-Path $dist "P2PChat.Net.dll"), (Join-Path $dist "P2PChat.pdb"), `
            (Join-Path $dist "p2p.pdb"), (Join-Path $dist "whatsapp.ico"), `
            (Join-Path $dist "whatsapp.png") -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $appBin "P2PChat.exe"), (Join-Path $appBin "P2PChat.Core.dll"), `
            (Join-Path $appBin "P2PChat.Net.dll") -Force -ErrorAction SilentlyContinue
Write-Host "Termine. Application autonome : $dist\p2p.exe" -ForegroundColor Green
exit 0
