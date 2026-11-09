# Gera um .res Win32 com os recursos RT_BITMAP (IDs 1..5 e 7..30) para os botões do ribbon — um ícone
# por aplicação (2026-11-09). Sem rc.exe: monta o binário .res à mão (formato documentado) + bitmaps via
# System.Drawing. Cores por família: cobre = eletrodo, cinza = placa/peça, azul = água/dado,
# verde = criar/ligar, vermelho = revisão/desligar, laranja = escrita.
Add-Type -AssemblyName System.Drawing

$outRes = $args[0]
if (-not $outRes) { throw "uso: make_res.ps1 <caminho .res>" }

function Col([int]$r, [int]$gg, [int]$b) { [System.Drawing.Color]::FromArgb($r, $gg, $b) }
function PenOf($c, [float]$w) { New-Object System.Drawing.Pen ($c), $w }
function BrushOf($c) { New-Object System.Drawing.SolidBrush ($c) }
function Pts([int[]]$xy) {
    $a = New-Object 'System.Drawing.Point[]' ($xy.Length / 2)
    for ($i = 0; $i -lt $a.Length; $i++) { $a[$i] = [System.Drawing.Point]::new($xy[2 * $i], $xy[2 * $i + 1]) }
    return ,$a
}
$cu    = Col 200 110 50     # cobre (eletrodo)
$cuDk  = Col 140 70 30
$gray  = Col 130 130 130
$light = Col 215 215 215
$blue  = Col 0 110 200
$green = Col 0 150 60
$red   = Col 200 30 30
$orng  = Col 230 140 0
$black = Col 30 30 30

# Eletrodo: haste + cabeça em cobre (a forma que se repete nos botões de eletrodo)
function Draw-Electrode($g, [int]$x, [int]$y) {
    $g.FillRectangle((BrushOf $cu), $x + 3, $y, 3, 6)
    $g.FillPolygon((BrushOf $cu), (Pts @(($x), ($y + 6), ($x + 9), ($y + 6), ($x + 8), ($y + 11), ($x + 1), ($y + 11))))
    $g.DrawPolygon((PenOf $cuDk 1), (Pts @(($x), ($y + 6), ($x + 9), ($y + 6), ($x + 8), ($y + 11), ($x + 1), ($y + 11))))
}

# Folha de desenho/relatório: retângulo branco com borda
function Draw-Sheet($g) {
    $g.FillRectangle((BrushOf ([System.Drawing.Color]::White)), 2, 1, 11, 14)
    $g.DrawRectangle((PenOf $gray 1), 2, 1, 11, 14)
}

# Símbolo de liga/desliga sobre um disco
function Draw-Power($g, $c) {
    $g.FillEllipse((BrushOf $c), 1, 1, 14, 14)
    $w = PenOf ([System.Drawing.Color]::White) 2
    $g.DrawArc($w, 4, 4, 8, 8, -50, 280)
    $g.DrawLine($w, 8, 3, 8, 8)
}

# --- desenha os ícones (16x16, 24-bit, fundo branco) ---
function New-IconDib([int]$id) {
    $bmp = New-Object System.Drawing.Bitmap 16,16,([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::White)
    switch ($id) {
        1 { # Criar eletrodos: eletrodo de cobre + "+" verde
            Draw-Electrode $g 0 3
            $p = PenOf $green 2
            $g.DrawLine($p, 13, 1, 13, 7); $g.DrawLine($p, 10, 4, 16, 4) }
        2 { # Coordenadas: grade azul
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,110,200)), 1
            $g.DrawRectangle($p, 2,2, 11,11); $g.DrawLine($p, 2,7, 13,7); $g.DrawLine($p, 7,2, 7,13) }
        3 { # Analisar (Z): lupa roxa
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(110,70,150)), 2
            $g.DrawEllipse($p, 3,3, 7,7); $g.DrawLine($p, 10,10, 14,14) }
        4 { # Ficha: linhas laranja
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210,120,20)), 2
            $g.DrawLine($p, 3,4, 13,4); $g.DrawLine($p, 3,8, 13,8); $g.DrawLine($p, 3,12, 10,12) }
        5 { # Lista de corte: lâmina de serra cinza (corpo + dentes)
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70,70,70)), 1
            $b = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(160,160,160))
            $g.FillRectangle($b, 1,3, 14,5); $g.DrawRectangle($p, 1,3, 14,5)
            $pts = New-Object 'System.Drawing.Point[]' 8
            for ($i = 0; $i -lt 8; $i++) {
                $y = if ($i % 2) { 12 } else { 8 }
                $pts[$i] = [System.Drawing.Point]::new(1 + 2 * $i, $y)
            }
            $g.DrawLines($p, $pts) }
        7 { # WEDM: perfil azul atravessado pelo fio vermelho vertical
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,110,200)), 2
            $shape = [System.Drawing.Point[]]@([System.Drawing.Point]::new(3,5), [System.Drawing.Point]::new(12,3),
                                               [System.Drawing.Point]::new(13,12), [System.Drawing.Point]::new(5,13))
            $g.DrawPolygon($p, $shape)
            $w = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(200,30,30)), 1
            $g.DrawLine($w, 8,0, 8,15) }
        8 { # Curvas das superfícies: parede cinza entre os contornos de topo e de fundo (verdes)
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(150,150,150)), 1
            $g.DrawLine($p, 2,4, 3,12); $g.DrawLine($p, 13,3, 12,11)
            $c = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,150,60)), 2
            $g.DrawEllipse($c, 2,1, 11,5)     # contorno do topo
            $g.DrawEllipse($c, 3,9, 9,5) }    # contorno do fundo
        9 { # Desenho 2D: perfil de eixo escalonado (preto) + cota azul com setas
            $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40,40,40)), 1
            $g.DrawRectangle($p, 1,6, 5,6); $g.DrawRectangle($p, 6,7, 8,4)
            $c = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,90,200)), 1
            $g.DrawLine($c, 1,2, 14,2); $g.DrawLine($c, 1,1, 1,5); $g.DrawLine($c, 14,1, 14,5)
            $g.DrawLine($c, 2,1, 3,2); $g.DrawLine($c, 2,3, 3,2); $g.DrawLine($c, 13,1, 12,2); $g.DrawLine($c, 13,3, 12,2) }
        10 { # Ordenar PathFinder: 3 linhas de lista crescendo (cinza) + seta verde para baixo
            $b = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90,90,90))
            $g.FillRectangle($b, 1,2, 4,2); $g.FillRectangle($b, 1,7, 6,2); $g.FillRectangle($b, 1,12, 8,2)
            $a = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(0,150,60)), 2
            $g.DrawLine($a, 12,1, 12,13)
            $tip = [System.Drawing.Point[]]@([System.Drawing.Point]::new(9,10), [System.Drawing.Point]::new(15,10), [System.Drawing.Point]::new(12,15))
            $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0,150,60))), $tip) }
        11 { # Porta-molde: pilha de placas (cinza) com os calços e a extratora azul entre eles
            $b = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120,120,120))
            $g.FillRectangle($b, 1,1, 14,2); $g.FillRectangle($b, 2,4, 12,3); $g.FillRectangle($b, 2,8, 12,2)
            $g.FillRectangle($b, 2,11, 3,3); $g.FillRectangle($b, 11,11, 3,3); $g.FillRectangle($b, 1,14, 14,2)
            $e = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0,110,200))
            $g.FillRectangle($e, 6,12, 4,2) }
        12 { # Alívio de extratores: placa em corte (cinza), furo justo em cima (guia) e largo embaixo (alívio), pino azul
            $b = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(140,140,140))
            $g.FillRectangle($b, 1,3, 14,11)
            $w = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
            $g.FillRectangle($w, 5,7, 6,7)                 # alívio (largo, do lado de trás)
            $e = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(0,110,200))
            $g.FillRectangle($e, 7,0, 2,16) }              # extrator: justo na guia, folgado no alívio
        13 { # Nova peça: bloco cinza + "+" verde
            $g.FillRectangle((BrushOf $light), 1, 5, 10, 10); $g.DrawRectangle((PenOf $gray 1), 1, 5, 10, 10)
            $p = PenOf $green 2
            $g.DrawLine($p, 12, 1, 12, 7); $g.DrawLine($p, 9, 4, 15, 4) }
        14 { # Refrigeração: placa cinza com o canal de água azul e as bocas
            $g.FillRectangle((BrushOf $light), 1, 2, 14, 12)
            $g.DrawLines((PenOf $blue 2), (Pts @(1, 5, 10, 5, 10, 11, 15, 11)))
            $g.FillEllipse((BrushOf $blue), 0, 4, 3, 3); $g.FillEllipse((BrushOf $blue), 13, 10, 3, 3) }
        15 { # Alojamento de O'ring: peça cinza com o anel preto no canal
            $g.FillRectangle((BrushOf $light), 1, 7, 14, 8)
            $g.DrawEllipse((PenOf $black 2), 2, 3, 12, 7) }
        16 { # Limpar importação: duas cópias da mesma peça, a repetida riscada
            $g.FillRectangle((BrushOf $light), 6, 6, 9, 9); $g.DrawRectangle((PenOf $gray 1), 6, 6, 9, 9)
            $g.DrawRectangle((PenOf $gray 1), 1, 1, 8, 8)
            $x = PenOf $red 2
            $g.DrawLine($x, 2, 2, 8, 8); $g.DrawLine($x, 8, 2, 2, 8) }
        17 { # Criar eletrodo (manual): eletrodo + seta do mouse (faces clicadas)
            Draw-Electrode $g 0 1
            $arrow = Pts @(9, 6, 9, 15, 11, 13, 13, 16, 15, 15, 13, 12, 15, 11)
            $g.FillPolygon((BrushOf ([System.Drawing.Color]::White)), $arrow); $g.DrawPolygon((PenOf $black 1), $arrow) }
        18 { # Eletrodo de interpolação: cilindro de cobre + giro azul em Z
            $g.FillRectangle((BrushOf $cu), 4, 5, 8, 9)
            $g.FillEllipse((BrushOf $cuDk), 4, 3, 8, 4)
            $g.DrawArc((PenOf $blue 1.5), 1, 1, 14, 6, 200, 300) }
        19 { # Duplicar eletrodo: dois eletrodos, o segundo deslocado
            $g.FillRectangle((BrushOf $cuDk), 1, 1, 8, 8)
            $g.FillRectangle((BrushOf $cu), 6, 6, 9, 9); $g.DrawRectangle((PenOf $cuDk 1), 6, 6, 9, 9) }
        20 { # Criar Base: suporte cinza com furo de fixação + bloco de cobre em cima
            $g.FillRectangle((BrushOf $gray), 1, 8, 14, 7)
            $g.FillRectangle((BrushOf $cu), 3, 2, 10, 6)
            $g.FillEllipse((BrushOf ([System.Drawing.Color]::White)), 7, 10, 3, 3) }
        21 { # Unir superfícies: superfície de cobre ondulada descendo (verde) até o bloco
            $g.DrawCurve((PenOf $cu 2), (Pts @(1, 4, 5, 2, 10, 5, 15, 3)))
            $g.DrawLine((PenOf $green 2), 8, 6, 8, 9)
            $g.FillPolygon((BrushOf $green), (Pts @(5, 9, 11, 9, 8, 12)))
            $g.FillRectangle((BrushOf $gray), 2, 12, 12, 4) }
        22 { # Aplicar GAP: eletrodo de cobre e o contorno do sobremetal (faísca) tracejado
            $g.FillRectangle((BrushOf $cu), 4, 4, 8, 8)
            $d = PenOf $red 1; $d.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
            $g.DrawRectangle($d, 1, 1, 14, 14) }
        23 { # Desenho de processo: folha com a peça original e a cópia de processo
            Draw-Sheet $g
            $g.FillRectangle((BrushOf $gray), 4, 3, 5, 4)
            $g.FillRectangle((BrushOf $cu), 6, 9, 5, 4)
            $g.DrawLine((PenOf $blue 1), 6, 7, 8, 9) }
        24 { # Lista de modificações: folha com linhas e o triângulo de revisão vermelho
            Draw-Sheet $g
            $l = PenOf $gray 1
            $g.DrawLine($l, 4, 4, 11, 4); $g.DrawLine($l, 4, 7, 11, 7); $g.DrawLine($l, 4, 10, 8, 10)
            $g.FillPolygon((BrushOf $red), (Pts @(9, 15, 15, 15, 12, 9))) }
        25 { # Malha → sólido: triângulos da malha (cinza) virando sólido (azul)
            $m = PenOf $gray 1
            $g.DrawPolygon($m, (Pts @(0, 13, 5, 2, 10, 13)))
            $g.DrawLine($m, 2, 8, 8, 8); $g.DrawLine($m, 2, 8, 5, 13); $g.DrawLine($m, 8, 8, 5, 13)
            $g.FillRectangle((BrushOf $blue), 10, 7, 6, 7) }
        26 { # MCP — Ligar ponte: liga/desliga em verde
            Draw-Power $g $green }
        27 { # MCP — Status da ponte: "i" branco no disco azul
            $g.FillEllipse((BrushOf $blue), 1, 1, 14, 14)
            $w = BrushOf ([System.Drawing.Color]::White)
            $g.FillRectangle($w, 7, 7, 2, 5); $g.FillRectangle($w, 7, 4, 2, 2) }
        28 { # MCP — Liberar escrita: lápis laranja
            $g.DrawLine((PenOf $orng 3), 4, 12, 12, 4)
            $g.FillPolygon((BrushOf $black), (Pts @(2, 14, 3, 10, 6, 13))) }
        29 { # MCP — Somente leitura: cadeado cinza
            $g.DrawArc((PenOf $gray 2), 5, 2, 6, 8, 180, 180)
            $g.FillRectangle((BrushOf $gray), 3, 7, 10, 8)
            $g.FillRectangle((BrushOf ([System.Drawing.Color]::White)), 7, 10, 2, 3) }
        30 { # MCP — Desligar ponte: liga/desliga em vermelho
            Draw-Power $g $red }
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

foreach ($id in @(1, 2, 3, 4, 5) + @(7..30)) {   # o 6 não existe (era da sonda de malha, aposentada)
    [byte[]]$dib = New-IconDib $id
    Write-Output ("  id {0}: DIB {1} bytes" -f $id, $dib.Length)
    Write-ResHeader ([UInt32]$dib.Length) 2 ([UInt16]$id) 0x1030  # type 2 = RT_BITMAP
    $bw.Write($dib)
    while (($out.Length % 4) -ne 0) { $bw.Write([byte]0) }        # alinha em DWORD
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($outRes, $out.ToArray())
Write-Output ("OK: {0} ({1} bytes)" -f $outRes, $out.Length)
