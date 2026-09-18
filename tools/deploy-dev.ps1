<#
.SYNOPSIS
    Compila e instala o AutoEDM na pasta de onde o Solid Edge realmente carrega o add-in.

.DESCRIPTION
    O add-in é COM in-process registrado por usuário, e o CodeBase no registro aponta para
    %LOCALAPPDATA%\AutoEDM\addin — NÃO para src\AutoEDM.AddIn\bin\Debug. Um `dotnet build`
    sozinho não muda nada do que a SE executa: ela continua rodando o DLL da última
    INSTALAÇÃO. Isso já custou uma rodada inteira de teste em 2026-09-04, com log de um
    build antigo sendo lido como se fosse o novo.

    Este script faz o ciclo curto de desenvolvimento: compila, confere que o Solid Edge está
    fechado (com a SE aberta os DLLs estão travados e a cópia falharia pela metade) e
    substitui só os dois assemblies do AutoEDM — as dependências (Interop, Community.AddIn,
    System.Text.Json) já estão instaladas e não mudam.

    Nada de registro é mexido: quem registra é o AutoEDM.Register, uma vez só.

.PARAMETER Configuration
    Debug (padrão) ou Release.

.PARAMETER Target
    Pasta instalada. Padrão: %LOCALAPPDATA%\AutoEDM\addin (o CodeBase registrado).

.PARAMETER IncludeMcp
    Compila também o servidor MCP (src\AutoEDM.Mcp, Release x64 — o caminho que o .mcp.json
    aponta). Necessário sempre que o CATÁLOGO de ferramentas mudar: o Claude Code lê a lista
    do servidor, não do add-in, e os dois carregam CÓPIAS diferentes do AutoEDM.Core.dll.

    Exige o Claude Code FECHADO: enquanto a sessão vive, o processo AutoEDM.Mcp está de pé e
    trava o próprio exe e o Core.dll ao lado dele.

.EXAMPLE
    pwsh tools\deploy-dev.ps1
    pwsh tools\deploy-dev.ps1 -Configuration Release
    pwsh tools\deploy-dev.ps1 -Configuration Release -IncludeMcp
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Target = '',
    [switch]$IncludeMcp
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($Target)) {
    $Target = Join-Path $env:LOCALAPPDATA 'AutoEDM\addin'
}

# O executável do Solid Edge chama-se Edge.exe (o navegador da Microsoft é msedge.exe, não
# confundir). Com ele aberto o add-in está carregado no processo e os DLLs ficam travados.
$se = @(Get-Process -Name 'Edge' -ErrorAction SilentlyContinue)
if ($se.Count -gt 0) {
    throw "Feche o Solid Edge antes (PID $($se.Id -join ', ')): com ele aberto os DLLs do add-in estão travados."
}

if (-not (Test-Path $Target)) {
    throw "Pasta instalada não encontrada: $Target. Rode o instalador do pacote (tools\pack.ps1) uma vez antes."
}

$proj = Join-Path $repo 'src\AutoEDM.AddIn\AutoEDM.AddIn.csproj'
Write-Host "Compilando ($Configuration)..." -ForegroundColor Cyan
dotnet build $proj -c $Configuration --nologo -v m
if ($LASTEXITCODE -ne 0) { throw "A compilação falhou — nada foi copiado." }

$out = Join-Path $repo "src\AutoEDM.AddIn\bin\$Configuration\net472"
$files = @('AutoEDM.AddIn.dll', 'AutoEDM.Core.dll', 'AutoEDM.AddIn.pdb', 'AutoEDM.Core.pdb')

foreach ($f in $files) {
    $src = Join-Path $out $f
    if (-not (Test-Path $src)) { continue }   # pdb não existe em Release
    Copy-Item $src (Join-Path $Target $f) -Force
}

Write-Host "`nInstalado em $Target :" -ForegroundColor Green
Get-ChildItem $Target -Filter 'AutoEDM.*.dll' |
    Select-Object Name, @{ n = 'Compilado'; e = { $_.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') } } |
    Format-Table -AutoSize

Write-Host "Abra o Solid Edge e confira a primeira linha do log (%LOCALAPPDATA%\AutoEDM\logs):" -ForegroundColor Yellow
Write-Host "o 'Build carregado' tem de bater com as datas acima." -ForegroundColor Yellow

if ($IncludeMcp) {
    # O servidor MCP é um processo SEPARADO, com a sua própria cópia do Core. Enquanto o Claude
    # Code está aberto ele está rodando, e o build falha na cópia com MSB3027 — o que parece erro
    # de compilação e não é.
    $mcp = @(Get-Process -Name 'AutoEDM.Mcp' -ErrorAction SilentlyContinue)
    if ($mcp.Count -gt 0) {
        throw "Feche o Claude Code antes de usar -IncludeMcp (AutoEDM.Mcp PID $($mcp.Id -join ', ') está de pé e trava o exe)."
    }

    $mcpProj = Join-Path $repo 'src\AutoEDM.Mcp\AutoEDM.Mcp.csproj'
    Write-Host "`nCompilando o servidor MCP (Release x64)..." -ForegroundColor Cyan
    dotnet build $mcpProj -c Release -p:Platform=x64 --nologo -v m
    if ($LASTEXITCODE -ne 0) { throw "A compilação do servidor MCP falhou." }

    $mcpExe = Join-Path $repo 'src\AutoEDM.Mcp\bin\x64\Release\net8.0-windows\AutoEDM.Mcp.exe'
    Write-Host "Servidor MCP pronto: $mcpExe" -ForegroundColor Green
    Write-Host "(é o caminho que o .mcp.json usa — o Claude Code sobe este exe ao abrir)" -ForegroundColor Yellow
}
