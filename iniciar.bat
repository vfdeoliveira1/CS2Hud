@echo off
REM ==================================================================
REM  CS2 HUD - inicia tudo de uma vez:
REM   1) dotnet restore do exportador (so se nao tiver Node.js)
REM   2) exportador rodando numa janela propria (run.bat: Node.js ou
REM      dotnet run, reinicia sozinho se cair)
REM   3) quando a API responder, abre no Google Chrome:
REM      HUD, painel de controle e veto de mapas
REM ==================================================================
setlocal
cd /d "%~dp0"
set "ROOT=%~dp0"

REM Com Node.js instalado o exportador roda pelo exporter.js (nao precisa
REM de restore nem de compilar nada). Sem Node, usa o .NET.
where node >nul 2>nul && goto skiprestore
echo [1/3] dotnet restore...
pushd "%ROOT%CS2HudExporter"
dotnet restore
if errorlevel 1 (
  echo.
  echo [ERRO] O dotnet restore falhou. Veja a mensagem acima.
  popd
  pause
  exit /b 1
)
popd
goto started
:skiprestore
echo [1/3] Node.js encontrado: exportador sem compilar.
:started

echo [2/3] Iniciando o exportador em outra janela...
start "CS2 HUD Exporter" /d "%ROOT%CS2HudExporter" cmd /k run.bat

echo [3/3] Esperando a API (http://localhost:5000) responder...
set /a tries=0
:wait
curl -s -o nul http://localhost:5000/ && goto ready
set /a tries+=1
if %tries% geq 60 (
  echo [AVISO] A API nao respondeu em 60s. Abrindo as paginas mesmo assim.
  goto ready
)
timeout /t 1 /nobreak >nul
goto wait

:ready
REM com Node.js o exportador serve as paginas por http://localhost:5000
REM (no OBS a camera do VDO.Ninja so funciona assim); sem Node, abre os arquivos
set "P1=%ROOT%CS2Hud\hud.html"
set "P2=%ROOT%CS2Hud\control.html"
set "P3=%ROOT%CS2MapVeto\index.html"
where node >nul 2>nul && (
  set "P1=http://localhost:5000/hud/hud.html"
  set "P2=http://localhost:5000/hud/control.html"
  set "P3=http://localhost:5000/veto/veto_index.html"
)
REM acha o Chrome (instalado para todos os usuarios ou so para o seu)
set "CHROME="
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" set "CHROME=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not defined CHROME if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" set "CHROME=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"
if not defined CHROME if exist "%LocalAppData%\Google\Chrome\Application\chrome.exe" set "CHROME=%LocalAppData%\Google\Chrome\Application\chrome.exe"

if defined CHROME (
  start "" "%CHROME%" --new-window "%P1%" "%P2%" "%P3%"
) else (
  echo [AVISO] Google Chrome nao encontrado. Abrindo no navegador padrao.
  start "" "%P1%"
  start "" "%P2%"
  start "" "%P3%"
)

echo Pronto. O exportador continua rodando na janela "CS2 HUD Exporter".
timeout /t 5 >nul
endlocal
