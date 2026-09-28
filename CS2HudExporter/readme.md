# CS2 HUD Exporter

Pequeno serviço em C#/.NET 8 que:

1. Usa a lib [`CounterStrike2GSI`](https://www.nuget.org/packages/CounterStrike2GSI) para receber os dados que o CS2 envia via **Game State Integration (GSI)**.
2. Converte esses dados num formato simples (`HudStateDto`).
3. Expõe esse estado num endpoint HTTP JSON (`/api/hud`), pra sua HUD (web, OBS overlay, app externo etc) consumir.

## Como rodar

```bash
cd CS2HudExporter
dotnet restore
dotnet run
```

Na primeira execução, o próprio programa tenta gerar o arquivo de configuração do GSI dentro da pasta do jogo
(`.../Counter-Strike Global Offensive/game/csgo/cfg/gamestate_integration_hudexporter.cfg`).

Se a detecção automática falhar (ex: instalação em local não padrão), crie o arquivo manualmente com este conteúdo:

```
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
    }
}
```

Depois de salvar o `.cfg`, é preciso reiniciar o CS2 (ou trocar de mapa) pra ele carregar a integração.

## Portas usadas

- **3000** — porta interna que recebe os dados do jogo (GSI). Não precisa acessar diretamente.
- **5000** — API que você (ou sua HUD) consome.

Se alguma delas já estiver em uso na sua máquina, troque os números em `Program.cs`
(`new GameStateListener(3000)` e `app.Run("http://localhost:5000")`) e também no `.cfg` (campo `"uri"`).

## Endpoint

### `GET /api/hud`

Retorna o estado mais recente da partida:

```json
{
  "updatedAt": "2026-08-17T20:15:00Z",
  "map": "de_mirage",
  "phase": "live",
  "round": 12,
  "bombState": "planted",
  "scoreboard": {
    "ct": { "teamName": "CT", "score": 7, "consecutiveRoundLosses": 0, "timeoutsRemaining": 1 },
    "t":  { "teamName": "T",  "score": 5, "consecutiveRoundLosses": 2, "timeoutsRemaining": 1 }
  },
  "players": [
    {
      "steamId": "76561198000000000",
      "nick": "playerNick",
      "team": "CT",
      "health": 100,
      "armor": 100,
      "hasArmor": true,
      "hasHelmet": true,
      "hasDefuseKit": true,
      "activeWeapon": "AK-47",
      "kills": 12,
      "deaths": 8,
      "assists": 3,
      "roundKills": 1,
      "money": 3200,
      "equipmentValue": 4750,
      "utility": ["Flashbang", "Smoke Grenade", "High Explosive Grenade"],
      "position": { "x": 123.4, "y": -456.7, "z": 64.0 }
    }
  ]
}
```

Se ainda não chegou nenhum dado do jogo, o endpoint responde `204 No Content`.

## Observações importantes sobre os dados do GSI

- **Posições e dados de todos os jogadores** (`AllPlayers`) só vêm preenchidos quando você está **espectando/observando** a partida
  (ex: como caster, ou observador num servidor de scrim/campeonato). Jogando normalmente, o CS2 só expõe os dados do **seu próprio jogador**
  — por isso o código tem um fallback: se `AllPlayers` vier vazio, ele usa `gs.Player` (só você) na lista.
- Isso é limitação do próprio GSI da Valve, não da lib nem do código — não tem como contornar.
- O campo `utility` filtra as armas do tipo granada/C4 (`WeaponType` contendo "Grenade" ou "C4" no nome).
- `activeWeapon` é sempre a arma que o jogador está segurando no momento.

## Consumindo de uma HUD web

De qualquer página HTML/JS (o CORS já está liberado):

```js
setInterval(async () => {
  const res = await fetch("http://localhost:5000/api/hud");
  if (res.status === 204) return; // ainda sem dados
  const state = await res.json();
  console.log(state.scoreboard, state.players);
}, 500);
```