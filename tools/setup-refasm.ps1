# Extrait les assemblys de reference .NET Framework 4.8 du paquet NuGet
# microsoft.netframework.referenceassemblies.net48 vers tools\refasm
# Structure finale attendue par MSBuild (TargetFrameworkRootPath) :
#   tools\refasm\.NETFramework\v4.8\*.dll  +  RedistList\FrameworkList.xml
param()

$tools = $PSScriptRoot
$nupkg = Join-Path $tools 'refasm.nupkg'
$unpack = Join-Path $tools 'refasm-unpack'
$refasm = Join-Path $tools 'refasm'

if (-not (Test-Path $nupkg)) {
    Write-Host "Paquet introuvable : $nupkg" -ForegroundColor Red
    Write-Host "Telechargez-le :" -ForegroundColor Yellow
    Write-Host "  curl -L -o `"$nupkg`" `"https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net48/1.0.3/microsoft.netframework.referenceassemblies.net48.1.0.3.nupkg`""
    exit 1
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $unpack) { Remove-Item $unpack -Recurse -Force }
Write-Host "Extraction du paquet..."
[System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $unpack)

$list = Get-ChildItem $unpack -Recurse -Filter FrameworkList.xml |
    Where-Object { $_.FullName -like '*v4.8*' } | Select-Object -First 1
if (-not $list) {
    Write-Host "FrameworkList.xml (v4.8) introuvable dans le paquet." -ForegroundColor Red
    exit 1
}

$dir48 = $list.Directory.Parent.FullName
if (Test-Path $refasm) { Remove-Item $refasm -Recurse -Force }
New-Item -ItemType Directory -Force -Path $refasm | Out-Null
Copy-Item $dir48 (Join-Path $refasm '.NETFramework\v4.8') -Force -Recurse
Remove-Item $unpack -Recurse -Force

$count = @(Get-ChildItem (Join-Path $refasm '.NETFramework\v4.8') -Filter *.dll).Count
Write-Host "Assemblys de reference installes : $count fichiers -> $refasm"
exit 0
