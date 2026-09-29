@echo off
REM Reinicia o CS2 HUD Exporter automaticamente se ele cair.
REM Assim, se o processo cair por qualquer motivo, ele volta sozinho em
REM vez de precisar apertar "dotnet run" de novo.
 
REM Roda sempre a partir da pasta deste .bat (onde esta o .csproj),
REM nao importa de onde ele foi aberto.
cd /d "%~dp0"

:loop
echo [%date% %time%] Iniciando CS2 HUD Exporter...
dotnet run
echo [%date% %time%] O processo encerrou (codigo %errorlevel%). Reiniciando em 2s...
timeout /t 2 /nobreak >nul
goto loop
 