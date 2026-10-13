@echo off
REM Inicia o CS2 HUD Exporter e reinicia sozinho se ele cair.
REM
REM Usa a versao em Node.js (exporter.js) se o Node estiver instalado:
REM ela nao e bloqueada pelo Controle Inteligente de Aplicativos do
REM Windows, que bloqueia a DLL compilada pelo "dotnet run".
REM Sem Node, usa a versao .NET (dotnet run).

REM Roda sempre a partir da pasta deste .bat, nao importa de onde foi aberto.
cd /d "%~dp0"

set "MODE=dotnet"
where node >nul 2>nul && set "MODE=node"
if "%MODE%"=="node" (echo Usando Node.js ^(exporter.js^)) else (echo Node.js nao encontrado: usando .NET ^(dotnet run^). Instale o Node LTS em https://nodejs.org se o Windows bloquear a DLL.)

:loop
echo [%date% %time%] Iniciando CS2 HUD Exporter...
if "%MODE%"=="node" (node exporter.js) else (dotnet run)
echo [%date% %time%] O processo encerrou (codigo %errorlevel%). Reiniciando em 2s...
timeout /t 2 /nobreak >nul
goto loop
