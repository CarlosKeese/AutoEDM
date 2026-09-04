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

.EXAMPLE
    pwsh tools\deploy-dev.ps1
    pwsh tools\deploy-dev.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [string]$Target = ''
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
