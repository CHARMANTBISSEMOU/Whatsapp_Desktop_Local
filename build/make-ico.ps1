# Fabrique whatsapp.ico multi-tailles (16, 32, 48, 256) depuis assets\wa_src.png
$dir = 'C:\Users\EDITH-PARKERT\Desktop\COURS KEYCE\B2\Semestre 1\Programmation (C#)\P2PChat\assets'
$sizes = @(16, 32, 48, 256)
$pngs = @()
foreach ($s in $sizes) {
    $out = Join-Path $dir "wa$s.png"
    $vf = "scale=$($s):$($s)"
    cmd /c "ffmpeg -y -i `"$dir\wa_src.png`" -vf $vf `"$out`" 2>NUL" | Out-Null
    if (-not (Test-Path $out)) { throw "Echec ffmpeg pour la taille $s" }
    $pngs += $out
}
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type = ICO
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
$datas = @()
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $data = [System.IO.File]::ReadAllBytes($pngs[$i])
    $datas += , $data
    $wb = [byte]0
    if ($sizes[$i] -lt 256) { $wb = [byte]$sizes[$i] }
    $bw.Write($wb)            # largeur (0 = 256)
    $bw.Write($wb)            # hauteur
    $bw.Write([byte]0)        # couleurs
    $bw.Write([byte]0)        # reserve
    $bw.Write([UInt16]1)      # plans
    $bw.Write([UInt16]32)     # bits par pixel
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($data in $datas) { $bw.Write($data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $dir 'whatsapp.ico'), $ms.ToArray())
$bw.Close()
Copy-Item (Join-Path $dir 'wa256.png') (Join-Path $dir 'whatsapp.png') -Force
Write-Host ("whatsapp.ico OK : " + (Get-Item (Join-Path $dir 'whatsapp.ico')).Length + " octets")
