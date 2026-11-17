<#
.SYNOPSIS
    Roda ferramentas do AutoEDM (as mesmas do MCP) contra o Solid Edge ABERTO usando o Core
    recém-compilado — sem reinstalar o add-in e sem fechar o SE.

.DESCRIPTION
    O add-in instalado só muda com o SE fechado (tools\ciclo-se.ps1). Para iterar rápido numa
    ferramenta, este script carrega o AutoEDM.Core.dll (net472) do bin do repositório num processo
    PowerShell à parte, liga-se ao SE por GetActiveObject e chama SeToolRunner.Execute — o mesmo
    código que a ponte chama, só que de fora do processo do SE.

    Diferenças para a ponte real, para não se enganar:
      - roda FORA do processo do SE (chamadas COM atravessam processo: mais lentas);
      - o modo de escrita é o que você passar em -Mode, não o da ribbon;
      - cada execução é um processo novo: os identificadores $N de se_com_ler não sobrevivem.
    Vale como ensaio. A conferência final é pela ponte, depois do ciclo-se.ps1.

    Compile antes:  dotnet build src\AutoEDM.AddIn\AutoEDM.AddIn.csproj -c Release -p:Platform=x64
    Rode com o Windows PowerShell 5.1 (powershell.exe): o PowerShell 7 não tem Marshal.GetActiveObject.

.PARAMETER Tool
    Uma ou mais chamadas separadas por "||", cada uma "nome::{json}" (o JSON é opcional).
    Ex.: 'se_status||se_criar_eletrodos::{"unir":false}'

.PARAMETER Mode
    ReadOnly (padrão) ou Write. Ferramenta de escrita em ReadOnly é recusada, como na ponte.

.PARAMETER Quiet
    Esconde do console as linhas de log de diagnóstico ([DIAG], cores por face, furos conferidos…).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\se-harness.ps1 -Tool 'se_status'
    powershell -ExecutionPolicy Bypass -File tools\se-harness.ps1 -Mode Write -Quiet -Tool 'se_criar_eletrodos::{"confirmar":true}'
#>
param(
    [Parameter(Mandatory = $true)][string]$Tool,
    [ValidateSet('ReadOnly', 'Write')][string]$Mode = 'ReadOnly',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $repo "src\AutoEDM.AddIn\bin\x64\$Configuration\net472"
if (-not (Test-Path (Join-Path $bin 'AutoEDM.Core.dll'))) {
    Write-Output "Não achei $bin\AutoEDM.Core.dll — compile o add-in ($Configuration, x64) antes."
    exit 1
}

# O repositório costuma estar numa unidade de REDE, e o .NET Framework não carrega assembly de lá
# sem configuração extra: copia para uma pasta local (uma por execução, para não esbarrar em DLL presa).
$local = Join-Path $env:LOCALAPPDATA ("AutoEDM\harness\" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $local | Out-Null
Copy-Item (Join-Path $bin '*.dll') $local -Force
Get-ChildItem (Split-Path $local -Parent) -Directory |
    Where-Object { $_.FullName -ne $local -and $_.LastWriteTime -lt (Get-Date).AddHours(-2) } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$onResolve = [ResolveEventHandler] {
    param($s, $e)
    $name = (New-Object Reflection.AssemblyName $e.Name).Name
    $path = Join-Path $local "$name.dll"
    if (Test-Path $path) { [Reflection.Assembly]::LoadFrom($path) } else { $null }
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($onResolve)
[void][Reflection.Assembly]::LoadFrom((Join-Path $local 'AutoEDM.Core.dll'))

# O objeto COM fica num campo estático de uma classe C#: um bloco de script convertido em Func[object]
# devolveria PSObject, e o InvokeMember do Core procuraria o membro no PSObject.
Add-Type -TypeDefinition 'public static class AutoEdmHarnessHost { public static object App; public static System.Func<object> Get = () => App; }'
# Fora do processo da SE, chamada feita enquanto ela está ocupada volta RPC_E_CALL_REJECTED (medido na T5, 2026-11-16,
# logo depois de fechar uma peça): o filtro de mensagens OLE do Core faz a chamada esperar e tentar de novo.
try { [AutoEDM.Com.OleMessageFilter]::Register() } catch { Write-Output "Filtro de mensagens OLE não registrado: $($_.Exception.Message)" }
try { [AutoEdmHarnessHost]::App = [Runtime.InteropServices.Marshal]::GetActiveObject('SolidEdge.Application') }
catch { Write-Output 'O Solid Edge não está aberto (GetActiveObject falhou).'; exit 1 }

$bridgeMode = [AutoEDM.Mcp.BridgeMode]::$Mode
$runner = New-Object AutoEDM.Mcp.SeToolRunner ([AutoEdmHarnessHost]::Get), ([Func[AutoEDM.Mcp.BridgeMode]] { $bridgeMode }), ([Func[string]] { $null })

$noise = '\[DIAG\]|\] Cor |  Occ |   face \d+|\[verif\] cilindro|Raios mínimos: (face dá|furo Ø)|\[usinar eletrodo\]|^\s+• face'
$failed = $false
foreach ($call in ($Tool -split '\|\|')) {
    $name, $json = $call -split '::', 2
    if (-not $json) { $json = '{}' }
    $request = New-Object AutoEDM.Mcp.BridgeRequest
    $request.Id = 1; $request.Tool = $name.Trim(); $request.ArgsJson = $json

    # O log do Core sai pelo Console; com -Quiet as linhas de diagnóstico são filtradas.
    $response = $runner.Execute($request)
    Write-Output "=== $($name.Trim()) $json  ->  ok=$($response.Ok)"
    $text = $response.Text
    if ($Quiet) { $text = ($text -split "`r?`n" | Where-Object { $_ -notmatch $noise }) -join "`n" }
    Write-Output $text
    if ($response.ImageBase64) {
        $image = Join-Path $local 'captura.jpg'
        [IO.File]::WriteAllBytes($image, [Convert]::FromBase64String($response.ImageBase64))
        Write-Output "imagem: $image"
    }
    if (-not $response.Ok) { $failed = $true }
}
if ($failed) { exit 1 } else { exit 0 }
