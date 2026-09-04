<#
.SYNOPSIS
    Empacota o AutoEDM para distribuição: um ZIP portátil que instala sem admin.

.DESCRIPTION
    O add-in é COM in-process e o registro é por usuário (HKCU), então não existe
    MSI nem regasm no caminho: o pacote é a própria pasta de saída do
    AutoEDM.Register — ela já sai com Register.exe + AutoEDM.AddIn.dll +
    AutoEDM.Core.dll + Interop.SolidEdge + SolidEdge.Community.AddIn + as deps do
    System.Text.Json. Este script só compila em Release, tira o que não vai
    (pdb/logs), junta os arquivos do operador (instalar.cmd, LEIAME.txt) e zipa.

.PARAMETER Version
    Versão do pacote. Vira FileVersion/InformationalVersion (aparece no log do
    add-in) e o nome do ZIP. Padrão: a data de hoje, ex. 2026.8.14.
    A AssemblyVersion NÃO muda — ver Directory.Build.props.

.PARAMETER OutDir
    Onde gravar o ZIP. Padrão: dist\ na raiz do repositório.

.EXAMPLE
    pwsh tools\pack.ps1
    pwsh tools\pack.ps1 -Version 2026.8.20
#>
param(
    [string]$Version = (Get-Date -Format 'yyyy.M.d'),
    [string]$OutDir  = ''
)

$ErrorActionPreference = 'Stop'

$repo = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($OutDir)) { $OutDir = Join-Path $repo 'dist' }

# FileVersion exige 4 números de 0..65535; "2026.8.14" + ".0" resolve. Barra cedo
# um -Version bonito mas inválido, que só quebraria lá no msbuild.
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Versão inválida: '$Version'. Use três números, ex. 2026.8.14."
}

Write-Host "AutoEDM — empacotando v$Version" -ForegroundColor Cyan

# --- 1. build -----------------------------------------------------------------
# Compilar o Register arrasta AddIn -> Core por ProjectReference, então a saída
# dele já é o pacote inteiro. Só o net472 interessa: o net8.0-windows do Core
# existe para os testes rodarem sem Solid Edge instalado, não para distribuir.
$proj = Join-Path $repo 'src\AutoEDM.Register\AutoEDM.Register.csproj'
Write-Host '[1/4] dotnet build -c Release...'
dotnet build $proj -c Release -f net472 -p:AutoEdmVersion=$Version --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build falhou — pacote não gerado.' }

$bin = Join-Path $repo 'src\AutoEDM.Register\bin\Release\net472'
if (-not (Test-Path (Join-Path $bin 'AutoEDM.AddIn.dll'))) {
    throw "Não achei AutoEDM.AddIn.dll em $bin."
}

# --- 2. staging ---------------------------------------------------------------
Write-Host '[2/4] Montando a pasta do pacote...'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) "AutoEDM-pkg-$Version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

# Fora: .pdb (símbolos de debug) e a pasta logs\ que o Register cria ao rodar
# no bin durante o desenvolvimento — logs de OUTRA máquina não vão no pacote.
Get-ChildItem -LiteralPath $bin -File |
    Where-Object { $_.Extension -ne '.pdb' } |
    Copy-Item -Destination $stage

Copy-Item (Join-Path $PSScriptRoot 'dist\*') -Destination $stage

# Carimbo para o suporte saber o que a máquina recebeu sem depender do log.
"AutoEDM $Version — empacotado em $(Get-Date -Format 'yyyy-MM-dd HH:mm')" |
    Set-Content (Join-Path $stage 'VERSAO.txt') -Encoding UTF8

# --- 3. zip -------------------------------------------------------------------
Write-Host '[3/4] Compactando...'
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
$zip = Join-Path $OutDir "AutoEDM-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item $stage -Recurse -Force

# --- 4. pronto ----------------------------------------------------------------
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 2)
Write-Host "[4/4] Pronto: $zip ($mb MB)" -ForegroundColor Green
Write-Host ''
Write-Host 'Na máquina de destino: fechar o Solid Edge, extrair numa pasta do'
Write-Host 'usuário (não em Arquivos de Programas) e rodar instalar.cmd.'
