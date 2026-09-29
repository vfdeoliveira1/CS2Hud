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
│   ├── icons/
│   └── README.md
└── CS2HudExporter/
    ├── Program.cs
    ├── HudModels.cs
    ├── CS2HudExporter.csproj
    ├── readme.md
    ├── run.bat
    └── bin/
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
- **Padrão** volta para COUNTER / TERRORIST com os ícones CT e TR.
- A configuração fica salva em `CS2HudExporter/teams.json` (não vai para o git).

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
- `POST /api/teams`: salva `{ "ct": { "name", "logo" }, "t": { "name", "logo" } }` (logo em data URL).
- `POST /api/teams/swap`: inverte os lados manualmente.
- `DELETE /api/teams`: volta ao padrão.
- `GET /api/teams/logo/{id}`: imagem da logo.

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
