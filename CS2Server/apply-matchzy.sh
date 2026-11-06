#!/usr/bin/env bash
# Ajusta o cfg/MatchZy/config.cfg (criado pelo MatchZy) para o nosso 5x5:
#   - round de faca ligado (.stay / .switch)
#   - 10 jogadores prontos pra começar
#   - pausa técnica só do admin (.forcepause / .forceunpause, sem limite de tempo)
# Só troca o valor de uma opção se ela EXISTIR no arquivo (os nomes mudam entre
# versões do MatchZy); o que não existir aparece como aviso, sem quebrar nada.
set -euo pipefail
CFG="$HOME/cs2server/game/csgo/cfg/MatchZy/config.cfg"
if [ ! -f "$CFG" ]; then echo "Ainda não existe $CFG (inicie o servidor uma vez e rode de novo)."; exit 1; fi

set_cvar(){ # nome valor
  if grep -qE "^[[:space:]]*$1([[:space:]]|$)" "$CFG"; then
    sed -i -E "s|^[[:space:]]*$1([[:space:]].*)?$|$1 $2|" "$CFG"
    echo "  $1 = $2"
  else
    echo "  [aviso] '$1' não existe nesta versão do MatchZy (procure a opção equivalente no config.cfg)"
  fi
}

echo "Ajustando $CFG"
set_cvar matchzy_knife_enabled_default true
set_cvar matchzy_minimum_ready_required 10
set_cvar matchzy_enable_tech_pause false     # jogadores não pedem pausa técnica: só o admin, com .forcepause
set_cvar matchzy_both_teams_unpause_required true
set_cvar matchzy_demo_recording_enabled true
echo "Feito. Reinicie o servidor (ou troque de mapa) pra valer."
