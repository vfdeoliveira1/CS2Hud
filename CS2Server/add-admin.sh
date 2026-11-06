#!/usr/bin/env bash
# Deixa um jogador como admin (comandos .forcepause, .forceunpause, .restore...).
#   bash add-admin.sh 76561198000000000 "Vinicius"
# O número é o steamID64 (veja em steamid.io colando o link do seu perfil).
set -euo pipefail
ID="${1:-}"; NAME="${2:-admin}"
if ! [[ "$ID" =~ ^7656[0-9]{13}$ ]]; then echo "Use: bash add-admin.sh <steamID64> [nome]   (ex: 76561198000000000)"; exit 1; fi
CSGO="$HOME/cs2server/game/csgo"
CSS="$CSGO/addons/counterstrikesharp/configs/admins.json"   # admins do CounterStrikeSharp (o MatchZy respeita)
MZ="$CSGO/cfg/MatchZy/admins.json"                           # lista própria do MatchZy
mkdir -p "$(dirname "$CSS")" "$(dirname "$MZ")"
python3 - "$CSS" "$MZ" "$ID" "$NAME" <<'PY'
import json, sys, os
css, mz, sid, name = sys.argv[1:5]
def load(p):
    try:
        with open(p, encoding="utf-8") as f: return json.load(f)
    except Exception: return {}
a = load(css); a[name] = {"identity": sid, "flags": ["@css/root"]}
with open(css, "w", encoding="utf-8") as f: json.dump(a, f, indent=2)
b = load(mz)
if not isinstance(b, dict): b = {}
b[sid] = ""
with open(mz, "w", encoding="utf-8") as f: json.dump(b, f, indent=2)
print("Admin adicionado:", name, sid)
PY
echo "Reinicie o servidor pra valer."
