# CS2 Hud

Projeto para exibir informações em tempo real de partidas de Counter-Strike 2 em uma HUD web, consumindo dados enviados pelo jogo via Game State Integration (GSI).

O repositório está dividido em duas partes:

- `CS2HudExporter/`: serviço em .NET que escuta os dados do CS2 e expõe um endpoint JSON.
- `CS2Hud/`: HUD em HTML/CSS/JS para mostrar o estado da partida em overlay.

## Visão geral

A arquitetura é simples:

1. O CS2 envia dados de partida para o `CS2HudExporter` via GSI.
2. O serviço converte esses dados em um modelo próprio e os disponibiliza em `http://localhost:5000/api/hud`.
3. A HUD em `CS2Hud/hud.html` consulta esse endpoint e renderiza placar, rodada, bomba, jogadores e radar.

## Estrutura do projeto

```text
CS2Hud/
├── README.md
├── iniciar.bat
├── CS2Hud/
│   ├── hud.html
│   ├── control.html
│   ├── icons/
│   └── README.md
├── CS2HudExporter/
│   ├── Program.cs
│   ├── GsiConfig.cs
│   ├── HudModels.cs
│   ├── TeamsStore.cs
│   ├── CS2HudExporter.csproj
│   ├── readme.md
│   └── run.bat
└── CS2MapVeto/
    ├── index.html
    └── maps/          (imagens dos mapas)
```

## Requisitos

- Counter-Strike 2 instalado
- **Node.js LTS** (recomendado, https://nodejs.org) — ou .NET 8 SDK
- Navegador para abrir a HUD
- Opcional: OBS para usar a HUD como overlay transparente

## Como rodar

### Atalho: `iniciar.bat` (Windows)

Dê dois cliques em `iniciar.bat`, na raiz do projeto. Ele:

1. roda o `dotnet restore` do exportador (só se não tiver Node.js);
2. abre o exportador numa janela própria ("CS2 HUD Exporter", que reinicia sozinho se cair; feche essa janela para parar);
3. quando a API responder, abre no Google Chrome a HUD (`hud.html`), o painel (`control.html`) e o veto (`CS2MapVeto/index.html`). Sem Chrome instalado, usa o navegador padrão.

Com Node.js, as páginas abrem por endereço http (o exportador serve as pastas):

- HUD: `http://localhost:5000/hud/hud.html`
- Painel: `http://localhost:5000/hud/control.html`
- Veto: `http://localhost:5000/veto/index.html`

**No OBS**, adicione a HUD como Fonte de navegador com **"Arquivo local" desmarcado** e a URL `http://localhost:5000/hud/hud.html`, largura 1920 e altura 1080. Como arquivo local, o OBS não toca o vídeo das câmeras do VDO.Ninja dentro da HUD. Ative também, nas configurações do OBS (Avançado), a aceleração de hardware da fonte de navegador.

Ou, passo a passo:

### 1) Iniciar o exporter

O exportador existe em duas versões que fazem exatamente a mesma coisa:

- **Node.js (`CS2HudExporter/exporter.js`)** — recomendada. Não compila nada, então o **Controle Inteligente de Aplicativos** do Windows não bloqueia (o erro "Uma política de Controle de Aplicativo bloqueou este arquivo" é da DLL do .NET). Instale o Node.js LTS e rode `CS2HudExporter/run.bat` (ou `node exporter.js` dentro da pasta). Não precisa de `npm install`.
- **.NET 8 (`Program.cs`)** — usada pelo `run.bat` só se o Node não estiver instalado:

Abra o terminal na pasta do projeto e execute:

```bash
cd CS2HudExporter
dotnet restore
dotnet run
```

O programa tenta gerar automaticamente o arquivo de configuração do GSI no caminho do jogo. Se isso falhar, será necessário criar manualmente o arquivo:

`.../Counter-Strike Global Offensive/game/csgo/cfg/gamestate_integration_hudexporter.cfg`

Conteúdo recomendado:

```ini
"HudExporter Integration Configuration"
{
    "uri"          "http://localhost:3000/"
    "timeout"      "5.0"
    "buffer"       "0.1"
    "throttle"     "0.1"
    "heartbeat"    "10.0"
    "data"
    {
        "provider"                  "1"
        "map"                       "1"
        "map_round_wins"            "1"
        "round"                     "1"
        "player_id"                 "1"
        "player_state"              "1"
        "player_weapons"            "1"
        "player_match_stats"        "1"
        "player_position"           "1"
        "allplayers_id"             "1"
        "allplayers_state"          "1"
        "allplayers_match_stats"    "1"
        "allplayers_weapons"        "1"
        "allplayers_position"       "1"
        "allgrenades"               "1"
        "bomb"                      "1"
    }
}
```

Depois de salvar o arquivo, reinicie o CS2 ou troque de mapa para carregar a integração.

### 2) Abrir a HUD

Abra o arquivo:

```text
CS2Hud/hud.html
```

No navegador, ou use o arquivo em um Browser Source no OBS.

A HUD acessa automaticamente a API:

```text
http://localhost:5000/api/hud
```

### 3) Nomes e logos dos times (opcional)

Abra no navegador:

```text
CS2Hud/control.html
```

Preencha o nome e a logo do time que está de **CT agora** e do que está de **TR agora** e clique em **Salvar**. A HUD passa a mostrar o nome no placar e a logo no lugar do ícone CT/TR, e o popup de vencedor do round usa esse nome e essa logo.

- Na troca de lado (intervalo e overtime) o nome e a logo acompanham o time sozinhos, pelos jogadores de cada um. Se aparecerem trocados, use **Inverter lados**.
- **Coach**: nome do coach de cada time, mostrado na tela de pausa.
- **Forçar tela de pausa**: mostra a tela de pausa agora. Na pausa técnica e no timeout do jogo ela aparece sozinha (espectando), com valor de equipamento, loss bonus, timeouts restantes, armas/granadas/dinheiro de cada jogador e a contagem do timeout.
- **Padrão** volta para COUNTER / TERRORIST com os ícones CT e TR, sem série.
- A configuração fica salva em `CS2HudExporter/teams.json` (não vai para o git).

### Modo X1 (1 contra 1)

No `control.html`, marque **Modo X1 (1 contra 1)** e clique em **Salvar**:

- os 5 cards de cada lado e o painel "espectando" dão lugar a **um card por jogador**, um em cada canto de baixo (esquerda = time A, direita = time B), com a câmera do jogador em cima, se configurada; o card de quem está sendo espectado ganha a borda na cor do lado e a etiqueta **ESPECTANDO**;
- o placar mostra o **nick do jogador** (ou o nome digitado no campo "Nome no placar") e a imagem vira o **brasão** do jogador;
- **Mostrar radar** e **Mostrar série de mapas (picks e bans)** ligam/desligam o radar e a série (painel do tempo de compra e quadrados do placar) — desligue para mapas da comunidade ou X1 sem veto;
- a tela de pausa mostra brasão e nome grande de cada jogador, kills, mortes, K/D, timeouts e o equipamento; as câmeras dos dois continuam na tela, acima dela;
- "vivos" e o card de compra somem (não fazem sentido no 1x1).

#### Configurando o X1 online (remoto)

1. **Servidor dedicado alugado** (não lobby de um dos jogadores, que dá vantagem a quem hospeda), na região que dê ping parecido aos dois (ex.: São Paulo). Hospedagens de CS2 costumam instalar com um clique o plugin **MatchZy** (CounterStrikeSharp): `.ready`, `.pause`/`.unpause`, `.tech`, round de faca, gravação de demo e backup de rounds.
2. **Você entra no servidor como espectador** (com senha de espectador) e transmite pelo seu PC, onde roda o exportador e a HUD. Os dados de todos os jogadores só chegam espectando.
3. **Anti-ghosting:** no OBS, em Configurações → Avançado → **Atraso na transmissão**, use 60–120 s. Assim o atraso vale para tudo junto (jogo, HUD e câmeras ficam sincronizados) e os jogadores não ganham nada vendo a live.
4. **Config do servidor** (ajuste ao regulamento), por exemplo num `x1.cfg`:

```
mp_autoteambalance 0
mp_limitteams 0
bot_quota 0
bot_kick
mp_maxrounds 16            // MR8: primeiro a 9
mp_match_can_clinch 1
mp_overtime_enable 1
mp_overtime_maxrounds 6
mp_freezetime 5
mp_buytime 20
mp_roundtime 1.5
mp_roundtime_defuse 1.5
mp_team_timeout_max 3
mp_team_timeout_time 30
mp_give_player_c4 0        // mapa de aim sem bomba; em mapa oficial com bomba, deixe 1
sv_allow_votes 0
mp_spectators_max 4
```

5. **Mapa:** oficial com `map de_mirage`; da comunidade (Workshop) com `host_workshop_map <id do mapa>`. Mapa da comunidade não tem calibração de radar: desligue o radar no `control.html`.
6. Se usar `mp_team_timeout_max` diferente de 3, mude `TIMEOUTS_PER_TEAM` no `hud.html`.

### Câmeras dos jogadores (VDO.Ninja)

A HUD pode mostrar a webcam do jogador em destaque, acima da barra "espectando".

1. Cada jogador abre no navegador o link dele (botão **Copiar link do jogador** no `control.html`), algo como `https://vdo.ninja/?push=ID&webcam&password=SENHA`, escolhe a câmera e clica em começar. A aba precisa ficar aberta durante o jogo.
2. No `control.html`, em **Câmeras dos jogadores**: marque **Mostrar a câmera do jogador em destaque**, defina uma senha (opcional, a mesma dos links) e o **ID da câmera** de cada jogador (ou **Usar steamId**), e clique em **Salvar câmeras**.
3. Para desligar tudo, desmarque a opção e salve: nenhuma câmera é carregada.

As câmeras de todos ficam carregadas (escondidas) pra troca ser instantânea: cada uma consome até ~1,5 Mbps de download na máquina da transmissão.

### Imagens do radar

Coloque o radar oficial de cada mapa em `CS2Hud/icons/maps/` como `de_<mapa>.png` (quadrado, sem corte; ex.: `de_mirage.png`). Mapas calibrados: dust2, mirage, inferno, nuke, overpass, vertigo, ancient, anubis, train e cache.

Nuke e Vertigo têm dois andares: coloque também `de_nuke_lower.png` / `de_vertigo_lower.png` e o radar troca de andar sozinho conforme o jogador espectado, deixando apagado quem está no outro andar. Sem a imagem `_lower`, fica só a imagem principal.

### 4) Veto de mapas (picks e bans)

Abra no navegador:

```text
CS2MapVeto/index.html
```

1. Digite os nomes dos times (o Time A começa) e escolha MD1, MD3 ou MD5.
2. Os times banem/escolhem clicando nos mapas, na ordem:
   - **MD1**: A bane, B bane... até sobrar um mapa.
   - **MD3**: A bane, B bane, A escolhe (B escolhe o lado), B escolhe (A escolhe o lado), A bane, B bane; o que sobra é o decisivo.
   - **MD5**: A bane, B bane, depois A e B escolhem alternado (o outro time escolhe o lado) até sobrar o decisivo.
3. No fim aparecem os mapas da série na ordem, os banidos e a ordem completa.
4. Em **Série na HUD**, digite o placar final de cada mapa (o vencedor é marcado sozinho) e marque o mapa em jogo com **AO VIVO**. Isso vai para a HUD (o exportador precisa estar rodando): no tempo de compra aparece a série no canto superior direito (mapa, pick, vencedor, placar), e os quadrados de mapas vencidos ao lado do placar saem daqui. Use nos times os mesmos nomes do `control.html`, para a HUD ligar logo e quadrados.

Imagens dos mapas: coloque em `CS2MapVeto/maps/` com os nomes `mirage`, `dust2`, `cache`, `inferno`, `anubis`, `ancient`, `nuke` (`.jpg`, `.png` ou `.webp`). O veto em andamento fica salvo no navegador (sobrevive ao F5); **Desfazer** volta um passo.

## Endpoints

### GET /api/hud

Retorna o estado mais recente da partida em JSON.

Exemplo de resposta:

```json
{
  "updatedAt": "2026-08-17T20:15:00Z",
  "map": "de_mirage",
  "phase": "live",
  "round": 12,
  "bombState": "planted",
  "scoreboard": {
    "ct": { "teamName": "CT", "score": 7 },
    "t": { "teamName": "T", "score": 5 }
  },
  "players": []
}
```

Se ainda não houver dados do jogo, a API pode responder `204 No Content`.

### Times (usados pelo `control.html`)

- `GET /api/teams`: times configurados, pelo lado atual.
- `POST /api/teams`: salva `{ "bestOf", "forcePause", "ct": { "name", "logo", "mapWins", "coach" }, "t": { ... } }` (logo em data URL; `bestOf` 0/1/3/5/7).
- `POST /api/teams/swap`: inverte os lados manualmente.
- `DELETE /api/teams`: volta ao padrão.
- `GET /api/teams/logo/{id}`: imagem da logo.

### Série (usada pelo `CS2MapVeto`)

- `GET /api/series` / `POST /api/series` / `DELETE /api/series`: série de mapas (formato, times, mapas com pick, placar e vencedor, mapa atual). Fica salva em `CS2HudExporter/series.json`.

## Funcionalidades

- placar da partida
- informações da rodada e fase atual
- estado da bomba
- dados de jogadores
- arsenal e utilitários
- radar de posições (quando disponível em observação/espectador)
- trajetória de utilitários, fumaças e fogo no radar
- nomes e logos dos times (painel `control.html`) e popup de vencedor do round
- HUD pronta para uso em navegador ou OBS

## Observações importantes

- Dados de posições e de todos os jogadores geralmente só aparecem quando você está assistindo ou observando a partida.
- Em partida normal, o CS2 geralmente só entrega dados do seu próprio jogador.
- O projeto usa o endpoint local da API para manter a HUD leve e fácil de integrar.

## Solução de problemas

- Se a HUD não atualizar, verifique se o `CS2HudExporter` está rodando.
- Certifique-se de que o arquivo GSI foi criado corretamente na pasta do jogo.
- Verifique se as portas `3000` e `5000` não estão em uso.
- Caso necessário, ajuste as portas em `Program.cs` e no arquivo `.cfg`.

## Licença

Este projeto foi criado para uso pessoal e aprendizagem. Ajuste conforme necessário para o seu ambiente e objetivos.
