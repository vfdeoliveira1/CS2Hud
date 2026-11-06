#!/usr/bin/env bash
# Inicia o servidor 5x5 e reinicia sozinho se ele cair (Ctrl+C duas vezes pra parar).
set -uo pipefail

GSLT=""            # <- seu token de servidor (steamcommunity.com/dev/managegameservers, App ID 730)
MAP="de_mirage"
PORT=27015
MAXPLAYERS=12      # 10 jogadores + espectadores (você/observador)

HERE="$(cd "$(dirname "$0")" && pwd)"
SERVER_DIR="$HOME/cs2server"
bash "$HERE/patch-gameinfo.sh" || exit 1

if [ -z "$GSLT" ]; then
  echo "[aviso] Sem token GSLT: só funciona na rede local. Coloque o token em start.sh pra jogarem pela internet."
fi

cd "$SERVER_DIR/game/bin/linuxsteamrt64"
while true; do
  echo "[$(date '+%d/%m %H:%M:%S')] Iniciando servidor CS2 (porta $PORT, mapa $MAP)..."
  ./cs2 -dedicated -console -usercon -port "$PORT" -maxplayers "$MAXPLAYERS" \
    +game_type 0 +game_mode 1 +map "$MAP" \
    ${GSLT:++sv_setsteamaccount "$GSLT"} +exec server.cfg
  echo "Servidor encerrou. Reiniciando em 5s (Ctrl+C pra sair)..."
  sleep 5
done
