# Gera um .res Win32 com 4 recursos RT_BITMAP (IDs 1..4) para os botões do ribbon.
# Sem rc.exe: monta o binário .res à mão (formato documentado) + bitmaps via System.Drawing.
Add-Type -AssemblyName System.Drawing

$outRes = $args[0]
if (-not $outRes) { throw "uso: make_res.ps1 <caminho .res>" }

# --- desenha os 4 ícones (16x16, 24-bit, fundo branco) ---
function New-IconDib([int]$id) {
    $bmp = New-Object System.Drawing.Bitmap 16,16,([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::White)
    switch ($id) {
        1 { # Criar eletrodos: "+" verde
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,150,60)), 3
            $g.DrawLine($p, 8,3, 8,13); $g.DrawLine($p, 3,8, 13,8) }
        2 { # Coordenadas: grade azul
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,110,200)), 1
            $g.DrawRectangle($p, 2,2, 11,11); $g.DrawLine($p, 2,7, 13,7); $g.DrawLine($p, 7,2, 7,13) }
        3 { # Analisar (Z): lupa roxa
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(110,70,150)), 2
            $g.DrawEllipse($p, 3,3, 7,7); $g.DrawLine($p, 10,10, 14,14) }
        4 { # Ficha: linhas laranja
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210,120,20)), 2
            $g.DrawLine($p, 3,4, 13,4); $g.DrawLine($p, 3,8, 13,8); $g.DrawLine($p, 3,12, 10,12) }
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $bmp.Dispose()
    $bytes = $ms.ToArray()
    # RT_BITMAP quer o DIB (sem os 14 bytes do BITMAPFILEHEADER).
    $dib = New-Object 'byte[]' ($bytes.Length - 14)
    [Array]::Copy($bytes, 14, $dib, 0, $dib.Length)
    Write-Output (,$dib)
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out

function Write-ResHeader([UInt32]$dataSize, [UInt16]$typeOrd, [UInt16]$nameOrd, [UInt16]$memFlags) {
    $bw.Write([UInt32]$dataSize)   # DataSize
    $bw.Write([UInt32]32)          # HeaderSize (type+name ordinais -> 32)
    $bw.Write([UInt16]0xFFFF); $bw.Write([UInt16]$typeOrd)   # Type (ordinal)
    $bw.Write([UInt16]0xFFFF); $bw.Write([UInt16]$nameOrd)   # Name (ordinal)
    $bw.Write([UInt32]0)           # DataVersion
    $bw.Write([UInt16]$memFlags)   # MemoryFlags
    $bw.Write([UInt16]0x0409)      # LanguageId (en-US)
    $bw.Write([UInt32]0)           # Version
    $bw.Write([UInt32]0)           # Characteristics
}

# Cabeçalho nulo obrigatório no início do .res
Write-ResHeader 0 0 0 0

for ($id = 1; $id -le 4; $id++) {
    [byte[]]$dib = New-IconDib $id
    Write-Output ("  id {0}: DIB {1} bytes" -f $id, $dib.Length)
    Write-ResHeader ([UInt32]$dib.Length) 2 ([UInt16]$id) 0x1030  # type 2 = RT_BITMAP
    $bw.Write($dib)
    while (($out.Length % 4) -ne 0) { $bw.Write([byte]0) }        # alinha em DWORD
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($outRes, $out.ToArray())
Write-Output ("OK: {0} ({1} bytes)" -f $outRes, $out.Length)
