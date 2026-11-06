#!/usr/bin/env bash
# Instala o servidor dedicado de CS2 + Metamod + CounterStrikeSharp + MatchZy
# dentro do Ubuntu (WSL2). Rode de novo quando quiser: ele só baixa o que falta.
#
#   bash install.sh
#
# Tudo fica em ~/cs2server (servidor) e ~/steamcmd (baixador da Steam).
set -euo pipefail

SERVER_DIR="$HOME/cs2server"
STEAMCMD_DIR="$HOME/steamcmd"
CSGO_DIR="$SERVER_DIR/game/csgo"
HERE="$(cd "$(dirname "$0")" && pwd)"

say(){ printf '\n\033[1;32m==> %s\033[0m\n' "$*"; }
warn(){ printf '\033[1;33m[aviso] %s\033[0m\n' "$*"; }

say "1/6 Pacotes do Ubuntu"
sudo dpkg --add-architecture i386
sudo apt-get update -y
sudo apt-get install -y curl unzip tar ca-certificates lib32gcc-s1 lib32stdc++6 python3

say "2/6 SteamCMD"
mkdir -p "$STEAMCMD_DIR"
if [ ! -x "$STEAMCMD_DIR/steamcmd.sh" ]; then
  curl -fsSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar -xz -C "$STEAMCMD_DIR"
fi

say "3/6 Servidor do CS2 (app 730, ~75 GB: demora na primeira vez)"
if [ "${SKIP_CS2:-0}" = 1 ] && [ -f "$CSGO_DIR/gameinfo.gi" ]; then
  echo "    já instalado, pulando (SKIP_CS2=1)"
else
# download grande: se a Steam interromper (erro 0x402), tenta de novo (continua de onde parou)
ok=0
for try in 1 2 3 4 5; do
  if "$STEAMCMD_DIR/steamcmd.sh" +force_install_dir "$SERVER_DIR" +login anonymous +app_update 730 validate +quit \
     | tee /dev/stderr | grep -q "Success! App '730'"; then ok=1; break; fi
  warn "Download interrompido (tentativa $try de 5). Tentando de novo em 10s..."
  sleep 10
done
if [ "$ok" != 1 ]; then echo "Não consegui terminar o download do CS2. Confira o espaço livre (df -h ~) e rode de novo."; exit 1; fi
fi
# o servidor procura a steamclient.so aqui
mkdir -p "$HOME/.steam/sdk64"
ln -sf "$STEAMCMD_DIR/linux64/steamclient.so" "$HOME/.steam/sdk64/steamclient.so"

say "4/6 Metamod:Source 2 (versão mais recente)"
# 1º: arquivo baixado à mão (pasta CS2Server ou Downloads do Windows), se existir
MM_LOCAL="$(ls -1 "$HERE"/mmsource-*-linux.tar.gz /mnt/c/Users/*/Downloads/mmsource-*-linux.tar.gz 2>/dev/null | sort | tail -n 1 || true)"
if [ -n "$MM_LOCAL" ]; then
  echo "    usando o arquivo baixado: $MM_LOCAL"
  tar -xzf "$MM_LOCAL" -C "$CSGO_DIR"
else
  # 2º: baixa do site (tenta algumas vezes: o site às vezes derruba a conexão)
  MM_FILE="$(curl -fsSL --retry 5 --retry-delay 3 --retry-all-errors https://mms.alliedmods.net/mmsdrop/2.0/mmsource-latest-linux | tr -d '\r\n ' || true)"
  if [ -z "$MM_FILE" ] || ! curl -fsSL --retry 5 --retry-delay 3 --retry-all-errors "https://mms.alliedmods.net/mmsdrop/2.0/$MM_FILE" -o /tmp/mmsource.tar.gz; then
    cat <<'MSG'

Não consegui baixar o Metamod do site (mms.alliedmods.net). Baixe pelo navegador do Windows:
  1) Abra https://www.metamodsource.net/downloads.php?branch=dev
  2) Baixe o arquivo de LINUX: mmsource-2.0.0-gitXXXX-linux.tar.gz (fica na pasta Downloads)
  3) Rode de novo: bash install.sh   (ele acha o arquivo na pasta Downloads sozinho)
MSG
    exit 1
  fi
  echo "    $MM_FILE"
  tar -xzf /tmp/mmsource.tar.gz -C "$CSGO_DIR"
fi

say "5/6 MatchZy + CounterStrikeSharp (versão mais recente, pacote para Linux)"
MZ_URL="$(curl -fsSL --retry 5 --retry-delay 3 --retry-all-errors https://api.github.com/repos/shobhit-pathak/MatchZy/releases/latest \
  | python3 -c 'import json,sys; a=[x["browser_download_url"] for x in json.load(sys.stdin)["assets"] if x["name"].endswith("-with-cssharp-linux.zip")]; print(a[0] if a else "")')"
if [ -z "$MZ_URL" ]; then echo "Não achei o pacote do MatchZy para Linux na página de releases."; exit 1; fi
echo "    $MZ_URL"
TMP="$(mktemp -d)"
curl -fsSL --retry 5 --retry-delay 3 --retry-all-errors "$MZ_URL" -o "$TMP/matchzy.zip"
# -n: não sobrescreve configs que você já editou
unzip -n -q "$TMP/matchzy.zip" -d "$CSGO_DIR"
rm -rf "$TMP"

say "6/6 Configurações"
bash "$HERE/patch-gameinfo.sh"
mkdir -p "$CSGO_DIR/cfg"
for f in server.cfg; do
  if [ -f "$CSGO_DIR/cfg/$f" ]; then warn "cfg/$f já existe, não sobrescrevi (o modelo está em CS2Server/cfg/$f)";
  else cp "$HERE/cfg/$f" "$CSGO_DIR/cfg/$f"; fi
done
bash "$HERE/apply-matchzy.sh" || warn "Rode 'bash apply-matchzy.sh' depois de iniciar o servidor uma vez."

cat <<MSG

Pronto! Próximos passos:
  1) Edite $CSGO_DIR/cfg/server.cfg  (nome do servidor, senhas)
  2) Coloque seu token GSLT em CS2Server/start.sh (variável GSLT)
  3) bash start.sh
MSG
