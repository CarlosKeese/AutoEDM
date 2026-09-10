@echo off
setlocal
rem ============================================================================
rem  AutoEDM - instalador para a maquina do usuario.
rem
rem  O registro do add-in e por USUARIO (HKCU), entao nao ha MSI nem regasm:
rem  este .cmd so cerca as tres coisas que fazem uma instalacao "silenciosamente
rem  errada", e depois chama o AutoEDM.Register.exe, que e quem de fato registra.
rem ============================================================================

cd /d "%~dp0"
echo.
echo  AutoEDM - instalacao
echo  ====================
echo.

rem --- 1. Solid Edge aberto trava os DLLs -------------------------------------
rem  Sem esta guarda, a copia falha em silencio e o usuario segue na versao
rem  antiga achando que atualizou.
tasklist /FI "IMAGENAME eq Edge.exe" 2>nul | find /I "Edge.exe" >nul
if not errorlevel 1 (
    echo  [X] O Solid Edge esta ABERTO.
    echo.
    echo      Feche o Solid Edge por completo e rode este instalador de novo.
    echo      Com ele aberto os arquivos do add-in ficam travados, e a
    echo      instalacao terminaria sem efeito.
    echo.
    pause
    exit /b 1
)

rem --- 2. Mark of the Web -----------------------------------------------------
rem  ZIP vindo de e-mail, rede ou download carrega o Mark of the Web. O CLR se
rem  recusa a carregar um assembly marcado in-process, e o add-in simplesmente
rem  NAO APARECE na ribbon, sem mensagem de erro nenhuma. Causa numero 1 de
rem  "instalei e nao apareceu".
echo  [1/2] Desbloqueando os arquivos (Mark of the Web)...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Get-ChildItem -LiteralPath '%~dp0' -Recurse -File | Unblock-File -ErrorAction SilentlyContinue" 2>nul

rem --- 3. o registro em si ----------------------------------------------------
if not exist "%~dp0AutoEDM.Register.exe" (
    echo  [X] Nao achei o AutoEDM.Register.exe nesta pasta.
    echo      Extraia o ZIP inteiro antes de rodar o instalador.
    echo.
    pause
    exit /b 1
)

echo  [2/2] Registrando o add-in no Solid Edge...
echo.
"%~dp0AutoEDM.Register.exe"
set RC=%ERRORLEVEL%
echo.

if not "%RC%"=="0" (
    echo  [X] O registro FALHOU ^(codigo %RC%^).
    echo      Mande a mensagem acima junto com o conteudo de
    echo      %%LOCALAPPDATA%%\AutoEDM\logs
    echo.
    pause
    exit /b %RC%
)

echo  ============================================================
echo   Pronto. Abra o Solid Edge: a aba "AutoEDM" estara na
echo   faixa de opcoes.
echo  ============================================================
echo.
echo   Para remover:    desinstalar.cmd
echo   Configuracao:    %%LOCALAPPDATA%%\AutoEDM\config.json
echo   Logs (suporte):  %%LOCALAPPDATA%%\AutoEDM\logs
echo.
pause
exit /b 0
