#!/usr/bin/env bash
# Coloca o Metamod no gameinfo.gi do CS2. Toda atualização do jogo volta esse
# arquivo ao original, então o start.sh e o update.sh rodam isto sempre.
set -euo pipefail
GI="$HOME/cs2server/game/csgo/gameinfo.gi"
if [ ! -f "$GI" ]; then echo "gameinfo.gi não encontrado em $GI"; exit 1; fi
if grep -q "csgo/addons/metamod" "$GI"; then
  echo "gameinfo.gi: Metamod já está configurado."
  exit 0
fi
# a linha do Metamod entra logo depois de "Game_LowViolence csgo_lv"
if ! grep -qE '^[[:space:]]*Game_LowViolence[[:space:]]+csgo_lv' "$GI"; then
  echo "Não achei a linha 'Game_LowViolence csgo_lv' no gameinfo.gi: adicione manualmente"
  echo "    Game    csgo/addons/metamod"
  echo "logo abaixo dela, dentro de SearchPaths."
  exit 1
fi
cp "$GI" "$GI.bak"
sed -i -E '/^[[:space:]]*Game_LowViolence[[:space:]]+csgo_lv/a\			Game	csgo/addons/metamod' "$GI"
echo "gameinfo.gi: Metamod adicionado (backup em gameinfo.gi.bak)."
