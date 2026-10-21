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
    Compila também o servidor MCP (src\AutoEDM.Mcp, Release x64), instala em
    %LOCALAPPDATA%\AutoEDM\mcp e o registra no Claude Code no escopo do usuário. Necessário
    sempre que o CATÁLOGO de ferramentas mudar: o Claude Code lê a lista do servidor, não do
    add-in, e os dois carregam CÓPIAS diferentes do AutoEDM.Core.dll.

    A instalação exige o Claude Code FECHADO: enquanto a sessão vive, o processo AutoEDM.Mcp
    está de pé e trava o exe instalado e o Core.dll ao lado dele.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\deploy-dev.ps1
    powershell -ExecutionPolicy Bypass -File tools\deploy-dev.ps1 -Configuration Release
    powershell -ExecutionPolicy Bypass -File tools\deploy-dev.ps1 -Configuration Release -IncludeMcp
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
    # O servidor MCP é um processo SEPARADO, com a sua própria cópia do Core. Ele roda da pasta
    # instalada (%LOCALAPPDATA%\AutoEDM\mcp), não do bin: compilar não trava mais, só a CÓPIA
    # exige o Claude Code fechado — com ele aberto o exe instalado está de pé.
    $mcpProj = Join-Path $repo 'src\AutoEDM.Mcp\AutoEDM.Mcp.csproj'
    Write-Host "`nCompilando o servidor MCP (Release x64)..." -ForegroundColor Cyan
    dotnet build $mcpProj -c Release -p:Platform=x64 --nologo -v m
    if ($LASTEXITCODE -ne 0) { throw "A compilação do servidor MCP falhou." }

    $mcp = @(Get-Process -Name 'AutoEDM.Mcp' -ErrorAction SilentlyContinue)
    if ($mcp.Count -gt 0) {
        throw "Feche o Claude Code antes de instalar o servidor MCP (AutoEDM.Mcp PID $($mcp.Id -join ', ') está de pé e trava o exe)."
    }

    $mcpOut = Join-Path $repo 'src\AutoEDM.Mcp\bin\x64\Release\net8.0-windows'
    $mcpTarget = Join-Path $env:LOCALAPPDATA 'AutoEDM\mcp'
    New-Item -ItemType Directory -Force $mcpTarget | Out-Null
    Copy-Item (Join-Path $mcpOut '*') $mcpTarget -Recurse -Force
    $mcpExe = Join-Path $mcpTarget 'AutoEDM.Mcp.exe'
    Write-Host "Servidor MCP instalado: $mcpExe" -ForegroundColor Green

    # Registro no escopo do USUÁRIO: o servidor aparece em qualquer pasta que o Claude Code
    # abrir (U:\Code inclusive), não só dentro do repositório. Refazer a cada deploy é inofensivo.
    # Sem 'claude' no PATH, usa o binário que vem com a extensão do VS Code (a versão mais nova).
    $claudeCli = (Get-Command claude -ErrorAction SilentlyContinue).Source
    if (-not $claudeCli) {
        $claudeCli = Get-ChildItem "$env:USERPROFILE\.vscode\extensions" -Directory -Filter 'anthropic.claude-code-*' -ErrorAction SilentlyContinue |
            Sort-Object { [version]($_.Name -replace '^anthropic\.claude-code-([\d.]+).*$', '$1') } -Descending |
            ForEach-Object { Join-Path $_.FullName 'resources\native-binary\claude.exe' } |
            Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if ($claudeCli) {
        & $claudeCli mcp remove autoedm --scope user 2>$null | Out-Null
        & $claudeCli mcp add --scope user autoedm -- $mcpExe
        if ($LASTEXITCODE -eq 0) { Write-Host "Registrado no Claude Code (escopo usuário) como 'autoedm'." -ForegroundColor Green }
        else { Write-Warning "Falha ao registrar. Rode à mão: claude mcp add --scope user autoedm -- `"$mcpExe`"" }
    }
    else {
        Write-Warning "CLI 'claude' não encontrada (nem no PATH nem na extensão do VS Code). Registre à mão: claude mcp add --scope user autoedm -- `"$mcpExe`""
    }
}
