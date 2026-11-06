#!/usr/bin/env bash
# Atualiza o CS2 (quando sair patch do jogo) e recoloca o Metamod no gameinfo.gi.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
"$HOME/steamcmd/steamcmd.sh" +force_install_dir "$HOME/cs2server" +login anonymous +app_update 730 +quit
bash "$HERE/patch-gameinfo.sh"
echo "Atualizado. Se o servidor não carregar os plugins depois de um patch grande, espere sair"
echo "versão nova do Metamod/CounterStrikeSharp e rode 'bash install.sh' de novo."
