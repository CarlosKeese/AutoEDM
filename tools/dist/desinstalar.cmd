@echo off
setlocal
rem ============================================================================
rem  AutoEDM - remove o add-in do usuario atual.
rem
rem  Apaga as chaves COM que o AutoEDM.Register.exe gravou em
rem  HKCU\Software\Classes. Nao mexe em area de maquina - nunca escreveu la.
rem ============================================================================

cd /d "%~dp0"
echo.
echo  AutoEDM - remocao
echo  =================
echo.

tasklist /FI "IMAGENAME eq Edge.exe" 2>nul | find /I "Edge.exe" >nul
if not errorlevel 1 (
    echo  [X] O Solid Edge esta ABERTO.
    echo.
    echo      Feche o Solid Edge e rode este desinstalador de novo.
    echo.
    pause
    exit /b 1
)

if not exist "%~dp0AutoEDM.Register.exe" (
    echo  [X] Nao achei o AutoEDM.Register.exe nesta pasta.
    echo.
    pause
    exit /b 1
)

echo  Removendo o registro...
echo.
"%~dp0AutoEDM.Register.exe" /u
set RC=%ERRORLEVEL%
echo.

if not "%RC%"=="0" (
    echo  [X] A remocao FALHOU ^(codigo %RC%^).
    echo.
    pause
    exit /b %RC%
)

echo  ============================================================
echo   Add-in removido. Reinicie o Solid Edge para descarregar.
echo  ============================================================
echo.
echo   Sobraram, e voce pode apagar a mao se quiser:
echo     %%LOCALAPPDATA%%\AutoEDM\addin    ^(os binarios copiados^)
echo     %%LOCALAPPDATA%%\AutoEDM\config.json  ^(sua configuracao^)
echo     %%LOCALAPPDATA%%\AutoEDM\logs     ^(logs^)
echo.
pause
exit /b 0
