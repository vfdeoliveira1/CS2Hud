using CounterStrike2GSI;
using CounterStrike2GSI.Nodes;
using CS2HudExporter;
using Newtonsoft.Json.Linq;

// -----------------------------------------------------------------------
// 1) Estado compartilhado: sempre que o CS2 mandar um novo GameState,
//    convertemos pra HudStateDto e guardamos aqui. O endpoint HTTP só lê
//    esse valor (não bloqueia esperando o jogo mandar dado nenhum).
// -----------------------------------------------------------------------
HudStateDto? latestHudState = null;
var stateLock = new object();

// -----------------------------------------------------------------------
// 2) Geração do arquivo .cfg do GSI. Usamos a classe GameStateListener só
//    pra esse detalhe (ela sabe achar a pasta do CS2 sozinha) - NÃO usamos
//    o Start()/listener HTTP dela pra receber os dados, porque a lib tem
//    um bug conhecido: qualquer erro de rede ou corpo vazio derruba o
//    processo inteiro (o método interno só trata ObjectDisposedException).
//    Por isso recebemos o POST do CS2 com o nosso próprio endpoint
//    ASP.NET abaixo, com try/catch de verdade.
// -----------------------------------------------------------------------
var cfgHelper = new GameStateListener(3000);
if (!cfgHelper.GenerateGSIConfigFile("HudExporter"))
{
    Console.WriteLine("[AVISO] Não consegui gerar o arquivo de configuração do GSI automaticamente.");
    Console.WriteLine("        Crie manualmente em: <CS2>/game/csgo/cfg/gamestate_integration_hudexporter.cfg");
    Console.WriteLine("        (veja o modelo no README.md deste projeto).");
}

// -----------------------------------------------------------------------
// 3) Um único servidor ASP.NET, escutando em duas portas:
//    - 3000: recebe os POSTs do CS2 (GSI)
//    - 5000: nossa API própria (pra HUD consumir)
// -----------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:3000", "http://localhost:5000");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});
// Evita que o log padrão do ASP.NET fique poluindo o console a cada POST
// que o CS2 manda (várias vezes por segundo).
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Times (nome + logo) configurados pelo painel control.html. Ficam salvos
// num teams.json na pasta do projeto, pra sobreviver a reinícios.
var teams = new TeamsStore(Path.Combine(builder.Environment.ContentRootPath, "teams.json"));

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => "CS2 HUD Exporter rodando. Use GET /api/hud para pegar o estado atual.");

app.MapGet("/api/hud", () =>
{
    lock (stateLock)
    {
        if (latestHudState is null)
            return Results.Json(new { message = "Nenhum dado recebido do CS2 ainda." }, statusCode: 204);
        latestHudState.Teams = teams.Resolve(latestHudState.Players);
        return Results.Ok(latestHudState);
    }
});

// ---- Painel de times (control.html) ----
List<PlayerHudDto> CurrentPlayers()
{
    lock (stateLock) { return latestHudState?.Players ?? new List<PlayerHudDto>(); }
}

app.MapGet("/api/teams", () => Results.Ok(teams.Resolve(CurrentPlayers(), includeLogoData: true)));

app.MapPost("/api/teams", (TeamsDto input) =>
{
    teams.Set(input, CurrentPlayers());
    return Results.Ok(teams.Resolve(CurrentPlayers(), includeLogoData: true));
});

app.MapPost("/api/teams/swap", () =>
{
    teams.Swap(CurrentPlayers());
    return Results.Ok(teams.Resolve(CurrentPlayers(), includeLogoData: true));
});

app.MapDelete("/api/teams", () =>
{
    teams.Clear();
    return Results.Ok(teams.Resolve(CurrentPlayers(), includeLogoData: true));
});

app.MapGet("/api/teams/logo/{id}", (string id) =>
{
    var logo = teams.GetLogo(id);
    return logo is null ? Results.NotFound() : Results.File(logo.Value.Bytes, logo.Value.ContentType);
});

// Endpoint que recebe o POST do próprio jogo (configurado no .cfg pra
// apontar pra http://localhost:3000/). Tudo entre try/catch: se vier um
// corpo vazio, malformado, ou a conexão cair no meio, a gente só ignora
// e responde 200 - nunca derruba o servidor.
app.MapPost("/", async (HttpRequest request) =>
{
    try
    {
        string body;
        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Results.Ok(); // heartbeat vazio ou corpo cortado - ignora
        }

        var json = JObject.Parse(body);
        var gs = new CounterStrike2GSI.GameState(json);
        var hud = MapGameStateToHud(gs);
        hud.Grenades = MapGrenades(json);

        lock (stateLock)
        {
            latestHudState = hud;
        }
    }
    catch (Exception ex)
    {
        // Loga mas NUNCA deixa a exceção subir - é exatamente isso que
        // faltava na lib e derrubava o servidor.
        Console.WriteLine("[AVISO] Ignorando POST do GSI que não pôde ser processado: " + ex.Message);
    }

    return Results.Ok();
});

Console.WriteLine("Ouvindo o CS2 (GSI) em http://localhost:3000/ ...");
Console.WriteLine("API da HUD disponível em http://localhost:5000/api/hud ...");

app.Run();


// =========================================================================
// Mapeamento GameState (lib) -> HudStateDto (o que a gente expõe)
// Obs: a lib tem dois tipos "GameState" (CounterStrike2GSI.GameState e
// CounterStrike2GSI.Nodes.GameState), por isso qualificamos totalmente aqui
// pra evitar ambiguidade.
// =========================================================================
static HudStateDto MapGameStateToHud(CounterStrike2GSI.GameState gs)
{
    var hud = new HudStateDto
    {
        UpdatedAt = DateTime.UtcNow,
        Map = gs.Map?.Name ?? string.Empty,
        Phase = gs.Map?.Phase.ToString() ?? string.Empty,
        Round = gs.Map?.Round ?? 0,
        BombState = gs.Round?.BombState.ToString() ?? string.Empty,
        Scoreboard = new ScoreboardDto
        {
            CT = new TeamScoreDto
            {
                TeamName = gs.Map?.CTStatistics?.Name ?? "CT",
                Score = gs.Map?.CTStatistics?.Score ?? 0,
                ConsecutiveRoundLosses = gs.Map?.CTStatistics?.ConsecutiveRoundLosses ?? 0,
                TimeoutsRemaining = gs.Map?.CTStatistics?.RemainingTimeouts ?? 0,
            },
            T = new TeamScoreDto
            {
                TeamName = gs.Map?.TStatistics?.Name ?? "T",
                Score = gs.Map?.TStatistics?.Score ?? 0,
                ConsecutiveRoundLosses = gs.Map?.TStatistics?.ConsecutiveRoundLosses ?? 0,
                TimeoutsRemaining = gs.Map?.TStatistics?.RemainingTimeouts ?? 0,
            }
        }
    };

    // Tempo restante da fase atual (round / freezetime / bomba). Vem do nó
    // "phase_countdowns" do GSI, que só é preenchido espectando/observando.
    hud.PhaseSecondsLeft = gs.PhaseCountdowns?.PhaseEndTime;
    hud.PhaseCountdownName = gs.PhaseCountdowns?.Phase.ToString() ?? string.Empty;

    // Estado + posição exata da bomba (node dedicado, muito mais confiável
    // do que tentar adivinhar pela arma ativa do jogador).
    if (gs.Bomb != null)
    {
        hud.Bomb = new BombDto
        {
            State = gs.Bomb.State.ToString(),
            Countdown = gs.Bomb.Countdown,
            Position = new PositionDto
            {
                X = gs.Bomb.Position.X,
                Y = gs.Bomb.Position.Y,
                Z = gs.Bomb.Position.Z,
            }
        };
    }

    // gs.Player é sempre "a câmera ativa": você mesmo jogando, ou a pessoa
    // que o GOTV/observer está olhando agora. Se ninguém está sendo
    // observado, o GSI simplesmente não manda essa parte do JSON.
    hud.SpectatedPlayer = gs.Player != null ? MapPlayer(gs.Player) : null;
    hud.RoundPhase = gs.Round?.Phase.ToString() ?? string.Empty;

    // AllPlayers só vem preenchido quando você está espectando/observando
    // (ou em modo overview). Jogando, o GSI só te dá os dados do seu
    // próprio jogador (gs.Player). Por isso o fallback abaixo.
    // Declaramos o tipo explicitamente como IEnumerable<Player> porque os
    // dois lados do "?:" abaixo são tipos concretos diferentes
    // (Dictionary<...>.ValueCollection de um lado, Player[] do outro).
    IEnumerable<Player> players = (gs.AllPlayers != null && gs.AllPlayers.Count > 0)
        ? gs.AllPlayers.Values
        : (gs.Player != null ? new[] { gs.Player } : Array.Empty<Player>());

    foreach (var player in players)
    {
        hud.Players.Add(MapPlayer(player));
    }

    return hud;
}

static PlayerHudDto MapPlayer(Player player)
{
    var dto = new PlayerHudDto
    {
        SteamId = player.SteamID ?? string.Empty,
        Nick = player.Name ?? string.Empty,
        Team = player.Team.ToString(),

        Health = player.State?.Health ?? 0,
        Armor = player.State?.Armor ?? 0,
        HasArmor = (player.State?.Armor ?? 0) > 0,
        HasHelmet = player.State?.HasHelmet ?? false,
        HasDefuseKit = player.State?.HasDefuseKit ?? false,

        Kills = player.MatchStats?.Kills ?? 0,
        Deaths = player.MatchStats?.Deaths ?? 0,
        Assists = player.MatchStats?.Assists ?? 0,
        RoundKills = player.State?.RoundKills ?? 0,

        Money = player.State?.Money ?? 0,
        EquipmentValue = player.State?.EquipmentValue ?? 0,
    };

    // Arma ativa + munição (clipe atual / clipe máximo / reserva)
    var active = player.GetActiveWeapon();
    dto.ActiveWeapon = active?.Name ?? string.Empty;
    dto.AmmoClip = active?.AmmoClip ?? 0;
    dto.AmmoClipMax = active?.AmmoClipMax ?? 0;
    dto.AmmoReserve = active?.AmmoReserve ?? 0;

    // Utilitários (granadas, flash, molotov, etc) que o jogador carrega
    foreach (var weapon in GetWeapons(player))
    {
        if (IsUtility(weapon))
        {
            dto.Utility.Add(weapon.Name ?? weapon.PaintKit ?? "unknown");
        }
    }

    // Posição no mapa (só vem preenchida em partidas com allplayers_position
    // habilitado, e normalmente só quando espectando). Position é uma struct
    // (Vector3D), então não dá pra comparar com null - se o dado não vier,
    // ela simplesmente fica zerada (0,0,0).
    dto.Position = new PositionDto
    {
        X = player.Position.X,
        Y = player.Position.Y,
        Z = player.Position.Z,
    };

    return dto;
}

// A lib expõe Player.Weapons; dependendo da versão isso é uma coleção
// (List<Weapon>) ou um dicionário (Dictionary<string, Weapon>). O helper
// abaixo lida com os dois casos sem quebrar.
static IEnumerable<Weapon> GetWeapons(Player player)
{
    if (player.Weapons is IDictionary<string, Weapon> dict)
        return dict.Values;

    if (player.Weapons is IEnumerable<Weapon> list)
        return list;

    return Enumerable.Empty<Weapon>();
}

// Granadas: lemos direto do JSON bruto do GSI (nó "grenades") em vez da
// API da lib, que muda entre versões. Formato de cada entrada:
//   "123": { "owner": "7656...", "type": "smoke", "position": "x, y, z",
//            "velocity": "x, y, z", "lifetime": "1.2", "effecttime": "0.0",
//            "flames": { "flame_1": "x, y, z", ... } }   <- só no "inferno"
static List<GrenadeDto> MapGrenades(JObject json)
{
    var list = new List<GrenadeDto>();
    if (json["grenades"] is not JObject grenades) return list;

    foreach (var prop in grenades.Properties())
    {
        if (prop.Value is not JObject g) continue;
        var dto = new GrenadeDto
        {
            Id = prop.Name,
            Owner = g["owner"]?.ToString() ?? string.Empty,
            Type = g["type"]?.ToString() ?? string.Empty,
            Position = ParseVector(g["position"]?.ToString()),
            Velocity = ParseVector(g["velocity"]?.ToString()),
            Lifetime = ParseDouble(g["lifetime"]?.ToString()),
            EffectTime = ParseDouble(g["effecttime"]?.ToString()),
        };
        if (g["flames"] is JObject flames)
        {
            foreach (var flame in flames.Properties())
            {
                var pos = ParseVector(flame.Value.ToString());
                if (pos != null) dto.Flames.Add(pos);
            }
        }
        list.Add(dto);
    }
    return list;
}

// "x, y, z" -> PositionDto (ponto como separador decimal, independente do
// idioma do Windows)
static PositionDto? ParseVector(string? text)
{
    if (string.IsNullOrWhiteSpace(text)) return null;
    var parts = text.Split(',');
    if (parts.Length != 3) return null;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    var style = System.Globalization.NumberStyles.Float;
    if (float.TryParse(parts[0].Trim(), style, inv, out var x) &&
        float.TryParse(parts[1].Trim(), style, inv, out var y) &&
        float.TryParse(parts[2].Trim(), style, inv, out var z))
    {
        return new PositionDto { X = x, Y = y, Z = z };
    }
    return null;
}

static double ParseDouble(string? text)
{
    return double.TryParse(text, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
}

static bool IsUtility(Weapon weapon)
{
    // Tipo vem como enum (ex: WeaponType.Grenade). Comparamos pelo texto
    // pra não depender do nome exato do enum na versão instalada.
    var typeName = weapon.Type.ToString();
    return typeName.Contains("Grenade", StringComparison.OrdinalIgnoreCase)
        || typeName.Contains("C4", StringComparison.OrdinalIgnoreCase);
}