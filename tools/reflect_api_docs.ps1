<#
  reflect_api_docs.ps1

  Gera documentação Markdown de módulos da API COM do Solid Edge por REFLEXÃO do
  Interop.SolidEdge.dll — SEM precisar de uma instância do Solid Edge rodando.

  É complementar ao tools/generate_api_docs.py (que parseia o dump da typelib):
  algumas libs — em especial SolidEdgeGeometry (Body/Face/Edge/Vertex) — NÃO são
  capturadas pelo walk do dump, então a reflexão do interop tipado é a única fonte
  completa. Os tipos aqui são a PROJEÇÃO .NET; [out]/[ref] marcam parâmetros de saída
  (a armadilha de marshaling do late binding — ver GUIA_SOLID_EDGE_COM.md).

  Uso:
    pwsh tools/reflect_api_docs.ps1                       # gera Geometry + Assembly
    pwsh tools/reflect_api_docs.ps1 -Namespaces SolidEdgeGeometry
#>
param(
  [string[]] $Namespaces = @('SolidEdgeGeometry', 'SolidEdgeAssembly'),
  [string]   $Dll = "$PSScriptRoot/../src/AutoEDM/bin/Debug/net472/Interop.SolidEdge.dll",
  [string]   $OutDir = "$PSScriptRoot/../docs/api"
)

$ErrorActionPreference = 'Stop'
$asm = [Reflection.Assembly]::LoadFrom((Resolve-Path $Dll))
$ver = $asm.GetName().Version
Write-Host "Interop.SolidEdge v$ver  ->  $OutDir"

function Esc([string]$s) { if ($null -eq $s) { return '' } $s.Replace('|', '\|') }

function ShortType($t) {
  if ($null -eq $t) { return 'void' }
  $n = $t.Name
  if ($n -eq 'Void') { return 'void' }
  return $n.TrimEnd('&')   # ByRef marca com & — a direção vira [out]/[ref] no chamador
}

function ParamStr($p) {
  $dir = ''
  if ($p.IsOut) { $dir = '[out] ' } elseif ($p.ParameterType.IsByRef) { $dir = '[ref] ' }
  $opt = if ($p.IsOptional) { '[opt] ' } else { '' }
  "$opt$dir$($p.Name): $(ShortType $p.ParameterType)"
}

function Keep($t) {
  $n = $t.Name
  if ($n.StartsWith('_')) { return $false }        # dispinterfaces cru (redundantes)
  if ($n.EndsWith('Class')) { return $false }      # RCW interno
  if ($n -match '(Event$|Events$|EventsSink$|_SinkHelper$|EventHandler$)') { return $false }
  return $true
}

foreach ($ns in $Namespaces) {
  $types = $asm.GetTypes() | Where-Object { $_.Namespace -eq $ns -and (Keep $_) }
  $enums  = $types | Where-Object { $_.IsEnum } | Sort-Object Name
  $ifaces = $types | Where-Object { -not $_.IsEnum } | Sort-Object Name

  $sb = [System.Text.StringBuilder]::new()
  $null = $sb.AppendLine("# Type Library ``$ns`` (reflexão do Interop.SolidEdge v$ver — SE 2023)")
  $null = $sb.AppendLine('')
  $null = $sb.AppendLine("> **Origem:** reflexão de ``Interop.SolidEdge.dll`` (não do dump da typelib — a lib de geometria não é capturada pelo walk). Tipos são a **projeção .NET**; ``[out]``/``[ref]`` marcam parâmetros de saída — em **late binding** eles precisam de ``ParameterModifier`` by-ref ou voltam vazios (ver [GUIA_SOLID_EDGE_COM.md](../GUIA_SOLID_EDGE_COM.md)). Geometria/modelagem usam **metros/radianos**; coleções são **1-based**.")
  $null = $sb.AppendLine('')
  $null = $sb.AppendLine("- **Objetos/interfaces:** $($ifaces.Count)  ·  **Enums:** $($enums.Count)")
  $null = $sb.AppendLine('')

  # ---- Enums ----
  if ($enums.Count -gt 0) {
    $null = $sb.AppendLine('## Enums / constantes')
    $null = $sb.AppendLine('')
    foreach ($e in $enums) {
      $null = $sb.AppendLine("### ``$($e.Name)``")
      $null = $sb.AppendLine('')
      $null = $sb.AppendLine('| Constante | Valor |')
      $null = $sb.AppendLine('|-----------|-------|')
      foreach ($name in $e.GetEnumNames()) {
        $val = [int64][enum]::Parse($e, $name)
        $null = $sb.AppendLine("| ``$name`` | $val |")
      }
      $null = $sb.AppendLine('')
    }
  }

  # ---- Índice de objetos ----
  $null = $sb.AppendLine('## Objetos / interfaces')
  $null = $sb.AppendLine('')
  $null = $sb.AppendLine('| Tipo | Propriedades | Métodos |')
  $null = $sb.AppendLine('|------|-------------|---------|')
  $meta = @{}
  foreach ($t in $ifaces) {
    $props = @($t.GetProperties('Public,Instance') | Sort-Object Name -Unique)
    $meths = @($t.GetMethods('Public,Instance') | Where-Object { -not $_.IsSpecialName } |
               Sort-Object { $_.Name } -Unique)
    $meta[$t.Name] = @{ Props = $props; Meths = $meths }
    $anchor = ($t.Name.ToLower())
    $null = $sb.AppendLine("| [$($t.Name)](#$anchor) | $($props.Count) | $($meths.Count) |")
  }
  $null = $sb.AppendLine('')

  # ---- Detalhe por objeto ----
  foreach ($t in $ifaces) {
    $anchor = ($t.Name.ToLower())
    $props = $meta[$t.Name].Props
    $meths = $meta[$t.Name].Meths
    if ($props.Count -eq 0 -and $meths.Count -eq 0) { continue }

    $null = $sb.AppendLine("### <a name=""$anchor""></a>``$($t.Name)``")
    $null = $sb.AppendLine('')

    if ($props.Count -gt 0) {
      $null = $sb.AppendLine('| Propriedade | Tipo | Acesso |')
      $null = $sb.AppendLine('|-------------|------|--------|')
      foreach ($p in $props) {
        $idx = ''
        $ip = $p.GetIndexParameters()
        if ($ip.Count -gt 0) { $idx = '[' + (($ip | ForEach-Object { ParamStr $_ }) -join ', ') + ']' }
        $acc = @()
        if ($p.CanRead)  { $acc += 'get' }
        if ($p.CanWrite) { $acc += 'set' }
        $null = $sb.AppendLine("| ``$(Esc ($p.Name + $idx))`` | ``$(Esc (ShortType $p.PropertyType))`` | $($acc -join '/') |")
      }
      $null = $sb.AppendLine('')
    }

    if ($meths.Count -gt 0) {
      $null = $sb.AppendLine('| Método | Retorno |')
      $null = $sb.AppendLine('|--------|---------|')
      foreach ($m in $meths) {
        $ps = ($m.GetParameters() | ForEach-Object { ParamStr $_ }) -join ', '
        $sig = "$($m.Name)($ps)"
        $null = $sb.AppendLine("| ``$(Esc $sig)`` | ``$(Esc (ShortType $m.ReturnType))`` |")
      }
      $null = $sb.AppendLine('')
    }
  }

  $out = Join-Path $OutDir "$ns.md"
  [System.IO.File]::WriteAllText($out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
  Write-Host "  escrito: $out  ($($ifaces.Count) objetos, $($enums.Count) enums)"
}
