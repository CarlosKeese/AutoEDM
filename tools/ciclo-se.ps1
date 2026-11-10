<#
.SYNOPSIS
    Ciclo completo sem ninguém clicar: fecha o Solid Edge, instala o add-in recém-compilado,
    reabre o Solid Edge e os documentos que estavam abertos.

.DESCRIPTION
    Existe para o agente (Claude Code) conseguir testar uma alteração do add-in sozinho. O
    deploy exige a SE fechada, e reabrir à mão era a única etapa que ainda dependia do usuário.

    Regras de segurança, nesta ordem:
      1. Se algum documento aberto tem alteração não salva, PARA e não fecha nada — a menos
         que venha -Descartar. ATENÇÃO: "alterado" (Dirty) não significa que o modelo mudou —
         um simples View.Fit() já marca o documento (medido em 2026-11-10 no molde 15428: abrir,
         capturar e selecionar não marcam; enquadrar marca). -Descartar é a resposta quando se
         sabe que não há trabalho a perder.
      2. Fecha com Application.Quit() (nunca matando o processo).
      3. Reabre a SE pelo MESMO executável e só os documentos que já estavam SALVOS em disco.

    Com "McpAutoStart" no config.json a ponte MCP volta sozinha — mas só quando o primeiro
    documento abre: a SE carrega o add-in ao entrar num ambiente (peça, montagem...), não na
    tela inicial vazia. Por isso, sem documento para reabrir, a ponte fica fora do ar.

    Rode com o Windows PowerShell 5.1 (powershell.exe): o PowerShell 7 não tem
    Marshal.GetActiveObject.

.PARAMETER Configuration
    Debug ou Release (padrão Release). Repassado ao deploy-dev.ps1.

.PARAMETER Descartar
    Fecha mesmo com documento marcado como alterado, SEM salvar (Close(false) em cada um).

.PARAMETER SemDeploy
    Só reinicia a SE e reabre os documentos (por exemplo, depois de mudar o config.json).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\ciclo-se.ps1
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SemDeploy,
    [switch]$Descartar
)

$ErrorActionPreference = 'Stop'

function Get-SolidEdge {
    try { return [Runtime.InteropServices.Marshal]::GetActiveObject('SolidEdge.Application') }
    catch { return $null }
}

# Compila ANTES de fechar qualquer coisa: o bin do repositório não é travado pela SE (ela carrega de
# %LOCALAPPDATA%\AutoEDM\addin), e um erro de compilação não é motivo para derrubar a sessão do usuário.
if (-not $SemDeploy) {
    $proj = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\AutoEDM.AddIn\AutoEDM.AddIn.csproj'
    & dotnet build $proj -c $Configuration --nologo -v q | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Output 'ABORTADO: a compilação falhou. O Solid Edge NÃO foi fechado.'
        exit 1
    }
}

$exe = $null
$reabrir = @()
$ativo = $null

$app = Get-SolidEdge
if ($app) {
    $exe = (Get-Process -Name 'Edge' | Select-Object -First 1).Path
    $alterados = @()
    foreach ($d in $app.Documents) {
        if ($d.Dirty) { $alterados += $d.Name }
        if ($d.Path) { $reabrir += $d.FullName }
    }
    if ($alterados.Count -gt 0 -and -not $Descartar) {
        Write-Output "ABORTADO: marcado(s) como alterado(s): $($alterados -join '; '). Nada foi fechado."
        Write-Output "Mudar a vista (zoom, Fit) já marca o documento. Se não há trabalho a perder, repita com -Descartar."
        exit 1
    }
    try { $ativo = $app.ActiveDocument.FullName } catch { $ativo = $null }

    if ($alterados.Count -gt 0) {
        # Avisos desligados SÓ aqui, com -Descartar. Sem alteração não há o que a SE perguntar; se ela
        # perguntar mesmo assim (algo que o Dirty não mostrou), a pergunta aparece para o usuário e o
        # script para em "não encerrou" em vez de descartar trabalho em silêncio.
        $app.DisplayAlerts = $false
        # Fecha um a um SEM salvar, para o Quit não ter o que perguntar.
        Write-Output "Descartando alterações de: $($alterados -join '; ')"
        for ($i = $app.Documents.Count; $i -ge 1; $i--) {
            try { $app.Documents.Item($i).Close($false) } catch { Write-Output "  (fechar o documento $i falhou: $($_.Exception.GetBaseException().Message))" }
        }
    }
    try { $app.Quit() }
    catch {
        try { $app.DisplayAlerts = $true } catch { Write-Output '(não foi possível religar os avisos da SE)' }
        Write-Output "ABORTADO: Quit() foi recusado ($($_.Exception.GetBaseException().Message)). O Solid Edge segue aberto."
        exit 1
    }
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($app)
    $app = $null
    [GC]::Collect()

    $t = 0
    while ((Get-Process -Name 'Edge' -ErrorAction SilentlyContinue) -and $t -lt 60) { Start-Sleep -Seconds 1; $t++ }
    if (Get-Process -Name 'Edge' -ErrorAction SilentlyContinue) {
        Write-Output 'ABORTADO: o Solid Edge não encerrou em 60 s.'
        exit 1
    }
    Write-Output "Solid Edge encerrado ($($reabrir.Count) documento(s) para reabrir)."
}
else {
    Write-Output 'O Solid Edge não estava aberto.'
}

$falhou = $false
if (-not $SemDeploy) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'deploy-dev.ps1') -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        # Mesmo assim reabre: o add-in instalado continua sendo o anterior, que funcionava.
        Write-Output "O DEPLOY FALHOU (exit $LASTEXITCODE). Reabrindo o Solid Edge com o add-in anterior."
        $falhou = $true
    }
}

if (-not $exe) {
    Write-Output 'O Solid Edge não estava aberto, então não foi iniciado.'
    if ($falhou) { exit 1 } else { exit 0 }
}

Start-Process -FilePath $exe
$t = 0
$app = $null
while ($t -lt 180 -and -not $app) {
    Start-Sleep -Seconds 2; $t += 2
    $candidato = Get-SolidEdge
    if ($candidato) { try { if ($candidato.Visible) { $app = $candidato } } catch { $app = $null } }
}
if (-not $app) {
    Write-Output 'O Solid Edge foi iniciado mas não respondeu em 180 s.'
    exit 1
}
Write-Output "Solid Edge no ar após $t s."

# O ativo por último, para voltar a ser o documento em foco.
$ordem = @($reabrir | Where-Object { $_ -ne $ativo })
if ($ativo -and ($reabrir -contains $ativo)) { $ordem += $ativo }
foreach ($f in $ordem) {
    if (-not (Test-Path -LiteralPath $f)) { Write-Output "Não reaberto (arquivo sumiu): $f"; continue }
    try { [void]$app.Documents.Open($f); Write-Output "Reaberto: $f" }
    catch { Write-Output "Falha ao reabrir '$f': $($_.Exception.GetBaseException().Message)" }
}
if ($falhou) { exit 1 } else { exit 0 }
