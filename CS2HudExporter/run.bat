@echo off
REM Reinicia o CS2 HUD Exporter automaticamente se ele cair.
REM Motivo: a lib CounterStrike2GSI derruba o processo se alguem/algo
REM mandar uma requisicao sem corpo pra porta 3000 (ex: abrir a URL
REM no navegador). Isso reinicia sozinho em vez de precisar apertar
REM "dotnet run" de novo toda hora.
 
REM Roda sempre a partir da pasta deste .bat (onde esta o .csproj),
REM nao importa de onde ele foi aberto.
cd /d "%~dp0"

:loop
echo [%date% %time%] Iniciando CS2 HUD Exporter...
dotnet run
echo [%date% %time%] O processo encerrou (codigo %errorlevel%). Reiniciando em 2s...
timeout /t 2 /nobreak >nul
goto loop
 