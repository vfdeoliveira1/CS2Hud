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
- .NET 8 SDK
- Navegador para abrir a HUD
- Opcional: OBS para usar a HUD como overlay transparente

## Como rodar

### 1) Iniciar o exporter

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
