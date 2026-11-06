# Servidor 5x5 de CS2 no notebook (WSL2)

O servidor dedicado oficial do CS2 roda num **Ubuntu dentro do Windows (WSL2)**, com:

- **Metamod:Source** e **CounterStrikeSharp**: base para plugins;
- **MatchZy**: partida competitiva com round de faca (`.stay` / `.switch`), pausas, backups de round e gravação de demo.

Por que Linux dentro do Windows: os plugins são `.dll` sem assinatura, e o **Controle Inteligente de Aplicativos** do Windows os bloquearia (como bloqueou o exportador .NET). Dentro do WSL2 ele não interfere.

> Os scripts não foram testados com um servidor real (não tenho CS2 aqui). O primeiro teste é no notebook: se algum passo der erro, me mande a mensagem.

## Passo a passo

### 0) Antes de começar
- Windows 11 (22H2 ou mais novo), **~60 GB livres** no disco C:, notebook **na tomada e no cabo** de rede.
- No Windows: **Configurações → Sistema → Energia** → nunca suspender quando ligado na tomada.

### 1) Instalar o Ubuntu (WSL2)
No **PowerShell como administrador**:
```powershell
wsl --install -d Ubuntu-24.04
```
Reinicie o PC. Ao abrir o **Ubuntu** pela primeira vez, ele pede para criar um usuário e uma senha do Linux (anote, o `sudo` vai pedir).

### 2) Rede do WSL e firewall do Windows
Ainda no **PowerShell como administrador**, na pasta `CS2Server` do projeto:
```powershell
powershell -ExecutionPolicy Bypass -File .\windows-setup.ps1
```
Isso deixa o Ubuntu com o mesmo IP do Windows (rede **espelhada**) e libera a porta **UDP 27015**.

### 3) Copiar esta pasta para dentro do Ubuntu
No **Ubuntu** (troque o caminho pelo da sua pasta do projeto; o `C:` aparece como `/mnt/c`):
```bash
cp -r "/mnt/c/Users/Vinic/OneDrive/Documentos/Github/CS2Hud/CS2Server" ~/
cd ~/CS2Server
sed -i 's/\r$//' *.sh      # garante final de linha do Linux nos scripts
```

### 4) Instalar o servidor
```bash
bash install.sh
```
Ele instala o SteamCMD, baixa o CS2 (~40 GB, demora na primeira vez), o **Metamod**, o **MatchZy com o CounterStrikeSharp** (sempre a versão mais recente) e coloca o `server.cfg`.

### 5) Token do servidor (GSLT)
Sem ele o servidor só funciona na rede local.
1. Entre em **https://steamcommunity.com/dev/managegameservers** (a conta Steam não pode ter limitação).
2. **App ID: 730**, memo: "servidor 5x5". Copie o token.
3. Edite `~/CS2Server/start.sh` (`nano start.sh`) e coloque o token em `GSLT="..."`.

### 6) Senhas e nome do servidor
```bash
nano ~/cs2server/game/csgo/cfg/server.cfg
```
Troque `hostname`, `sv_password` (senha para entrar) e `rcon_password` (só sua).

### 7) Você como admin
Pegue seu **steamID64** (cole o link do seu perfil em steamid.io) e:
```bash
bash add-admin.sh 76561198XXXXXXXXX "Vinicius"
```

### 8) Ligar o servidor
```bash
bash start.sh
```
Na **primeira vez**, depois que ele terminar de carregar, pare com **Ctrl+C** e rode:
```bash
bash apply-matchzy.sh     # faca ligado, 10 prontos, pausa técnica só do admin
bash start.sh
```
Para conferir se os plugins carregaram, digite no console do servidor:
```
meta list            # deve aparecer CounterStrikeSharp
css_plugins list     # deve aparecer MatchZy
```

### 9) Abrir para a internet (roteador)
1. Descubra o IP do notebook na rede local: `ipconfig` no Windows ("Endereço IPv4", ex: `192.168.0.50`).
2. No painel do roteador, crie um **redirecionamento de porta**: **UDP 27015** → esse IP.
3. **Teste de CGNAT:** compare o "IP da WAN" no painel do roteador com o IP que aparece em sites de "meu IP". **Se forem diferentes**, a operadora usa CGNAT e o redirecionamento não funciona. Peça **IP público** à operadora ou use um túnel UDP (ex: playit.gg).

**Como os jogadores entram**, pelo console do CS2:
```
connect SEU_IP_PUBLICO:27015; password senha-dos-jogadores
```
Você, no próprio notebook: `connect 127.0.0.1:27015; password ...`

## Na partida (MatchZy)

| Comando | Quem | O que faz |
|---|---|---|
| `.ready` / `.unready` | jogadores | marca pronto; com os 10 prontos, começa o **round de faca** |
| `.stay` / `.switch` | time que ganhou a faca | fica no lado / troca de lado |
| `.forcepause` (`.fp`) | **admin** | **pausa técnica sem limite de tempo** |
| `.forceunpause` (`.fup`) | **admin** | tira a pausa |
| `.tac` | jogadores | timeout tático (o normal do jogo) |
| `.stop` | os dois times | volta para o começo do round atual (os dois times precisam pedir) |
| `.restore <round>` | admin | volta para o backup de um round |
| `.map de_inferno` | admin | troca de mapa |
| `.restart` | admin | reinicia a partida |

Digite `.help` no chat para ver a lista exata da versão instalada (os nomes podem variar um pouco entre versões do MatchZy).

O **rollback só se ninguém tomou dano** e as informações extras do kill feed (**matou pulando, sem mira, através da fumaça**) vêm no plugin próprio, na próxima etapa.

## Manutenção

- **Patch do CS2:** pare o servidor e rode `bash update.sh`. Ele atualiza o jogo e recoloca o Metamod no `gameinfo.gi`, que todo patch apaga. Depois de patch grande, o Metamod e o CounterStrikeSharp podem ficar uns dias quebrados até sair versão nova: aí rode `bash install.sh` de novo.
- **Arquivos do servidor:** `~/cs2server/game/csgo` (cfg, addons, demos). No Windows, aparecem no Explorador em `\\wsl$\Ubuntu-24.04\home\SEU_USUARIO\cs2server`.

## Problemas comuns

| Sintoma | Causa provável |
|---|---|
| Ninguém de fora consegue entrar | porta não redirecionada, CGNAT, ou falta o token GSLT |
| `meta list` não mostra nada | `gameinfo.gi` sem o Metamod: rode `bash patch-gameinfo.sh` |
| Plugins pararam depois de um patch | Metamod/CounterStrikeSharp desatualizados: `bash install.sh` |
| `steamclient.so` não encontrado | rode `bash install.sh` de novo (ele cria o link) |
