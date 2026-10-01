using System.Globalization;
using System.Text.Json.Nodes;
using CS2HudExporter;

// Obs: o projeto NÃO usa bibliotecas de terceiros (antes usava a
// CounterStrike2GSI + Newtonsoft). O JSON que o CS2 manda é lido direto
// aqui com o System.Text.Json do próprio .NET. Motivo: o Controle
// Inteligente de Aplicativos do Windows bloqueia DLLs baixadas sem
// assinatura digital, e o exportador nem abria.

// -----------------------------------------------------------------------
// 1) Estado compartilhado: sempre que o CS2 mandar um novo estado,
//    convertemos pra HudStateDto e guardamos aqui. O endpoint HTTP só lê
//    esse valor (não bloqueia esperando o jogo mandar dado nenhum).
// -----------------------------------------------------------------------
HudStateDto? latestHudState = null;
var stateLock = new object();

// -----------------------------------------------------------------------
// 2) Arquivo .cfg do GSI na pasta do CS2 (acha o jogo pela Steam).
// -----------------------------------------------------------------------
try
{
    if (GsiConfig.Write() == null)
    {
        Console.WriteLine("[AVISO] Não achei a pasta do CS2 pra gerar a configuração do GSI.");
        Console.WriteLine("        Crie manualmente em: <CS2>/game/csgo/cfg/" + GsiConfig.FileName);
        Console.WriteLine("        (veja o modelo no README.md deste projeto).");
    }
}
catch (Exception ex)
{
    Console.WriteLine("[AVISO] Não consegui gravar a configuração do GSI: " + ex.Message);
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
// Série de mapas enviada pelo veto (CS2MapVeto/index.html).
var series = new SeriesStore(Path.Combine(builder.Environment.ContentRootPath, "series.json"));
// Webcams dos jogadores (VDO.Ninja), configuradas no control.html.
var cams = new CamsStore(Path.Combine(builder.Environment.ContentRootPath, "cams.json"));

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
        latestHudState.Series = series.Get();
        latestHudState.Cams = cams.Get();
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

// ---- Webcams (control.html) ----
app.MapGet("/api/cams", () => Results.Ok(cams.Get()));

app.MapPost("/api/cams", (CamsDto input) =>
{
    cams.Set(input);
    return Results.Ok(cams.Get());
});

// ---- Série de mapas (CS2MapVeto) ----
app.MapGet("/api/series", () => Results.Ok(series.Get()));

app.MapPost("/api/series", (SeriesDto input) =>
{
    series.Set(input);
    return Results.Ok(series.Get());
});

app.MapDelete("/api/series", () =>
{
    series.Set(null);
    return Results.Ok();
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

        if (JsonNode.Parse(body) is not JsonObject json)
        {
            return Results.Ok();
        }
        var hud = MapGameStateToHud(json);
        hud.Grenades = MapGrenades(json);

        lock (stateLock)
        {
            latestHudState = hud;
        }
    }
    catch (Exception ex)
    {
        // Loga mas NUNCA deixa a exceção subir: um POST ruim não pode
        // derrubar o servidor.
        Console.WriteLine("[AVISO] Ignorando POST do GSI que não pôde ser processado: " + ex.Message);
    }

    return Results.Ok();
});

Console.WriteLine("Ouvindo o CS2 (GSI) em http://localhost:3000/ ...");
Console.WriteLine("API da HUD disponível em http://localhost:5000/api/hud ...");

app.Run();


// =========================================================================
// Mapeamento JSON do GSI -> HudStateDto (o que a gente expõe)
// Formato do GSI (resumido):
//   map:      { name, phase, round, team_ct: { score, name,
//               consecutive_round_losses, timeouts_remaining }, team_t: {...} }
//   round:    { phase: freezetime/live/over, bomb: planted/exploded/defused }
//   phase_countdowns: { phase: live/bomb/defuse/paused/timeout_ct/..., phase_ends_in }
//   bomb:     { state, position: "x, y, z", countdown }
//   player:   jogador da câmera (ou você, jogando)
//   allplayers: { "<steamid>": { name, team, state, match_stats, weapons, position } }
// =========================================================================
static HudStateDto MapGameStateToHud(JsonObject gs)
{
    var map = gs["map"] as JsonObject;
    var round = gs["round"] as JsonObject;

    var hud = new HudStateDto
    {
        UpdatedAt = DateTime.UtcNow,
        Map = Str(map?["name"]),
        Phase = Str(map?["phase"]),
        Round = Int(map?["round"]),
        BombState = Str(round?["bomb"]),
        RoundPhase = Str(round?["phase"]),
        Scoreboard = new ScoreboardDto
        {
            CT = MapTeam(map?["team_ct"] as JsonObject),
            T = MapTeam(map?["team_t"] as JsonObject),
        }
    };

    // Como cada round terminou: "round_wins": { "1": "ct_win_elimination", ... }
    if (map?["round_wins"] is JsonObject wins)
    {
        foreach (var kv in wins)
        {
            if (int.TryParse(kv.Key, out var n))
                hud.RoundWins.Add(new RoundWinDto { Round = n, Result = Str(kv.Value) });
        }
        hud.RoundWins.Sort((a, b) => a.Round.CompareTo(b.Round));
    }

    // Tempo restante da fase atual (round / freezetime / bomba / timeout).
    // Só vem preenchido espectando/observando.
    if (gs["phase_countdowns"] is JsonObject pc)
    {
        hud.PhaseCountdownName = Str(pc["phase"]);
        hud.PhaseSecondsLeft = pc["phase_ends_in"] != null ? Num(pc["phase_ends_in"]) : null;
    }

    // Estado + posição exata da bomba
    if (gs["bomb"] is JsonObject bomb)
    {
        hud.Bomb = new BombDto
        {
            State = Str(bomb["state"]),
            Countdown = Num(bomb["countdown"]),
            Position = ParseVector(Str(bomb["position"])),
        };
    }

    // "player" é sempre a câmera ativa: você mesmo jogando, ou quem o
    // GOTV/observer está olhando agora.
    var player = gs["player"] as JsonObject;
    hud.SpectatedPlayer = player != null ? MapPlayer(Str(player["steamid"]), player) : null;

    // "allplayers" só vem espectando/observando. Jogando, o GSI só manda o
    // seu próprio jogador - por isso o fallback.
    if (gs["allplayers"] is JsonObject all && all.Count > 0)
    {
        foreach (var kv in all)
        {
            if (kv.Value is JsonObject p) hud.Players.Add(MapPlayer(kv.Key, p));
        }
    }
    else if (hud.SpectatedPlayer != null)
    {
        hud.Players.Add(hud.SpectatedPlayer);
    }

    return hud;
}

static TeamScoreDto MapTeam(JsonObject? team)
{
    return new TeamScoreDto
    {
        TeamName = Str(team?["name"]),
        Score = Int(team?["score"]),
        ConsecutiveRoundLosses = Int(team?["consecutive_round_losses"]),
        TimeoutsRemaining = Int(team?["timeouts_remaining"]),
    };
}

static PlayerHudDto MapPlayer(string steamId, JsonObject player)
{
    var state = player["state"] as JsonObject;
    var stats = player["match_stats"] as JsonObject;
    var armor = Int(state?["armor"]);

    var dto = new PlayerHudDto
    {
        SteamId = steamId,
        Nick = Str(player["name"]),
        Team = Str(player["team"]),

        Health = Int(state?["health"]),
        Armor = armor,
        HasArmor = armor > 0,
        HasHelmet = Bool(state?["helmet"]),
        HasDefuseKit = Bool(state?["defusekit"]),

        Kills = Int(stats?["kills"]),
        Deaths = Int(stats?["deaths"]),
        Assists = Int(stats?["assists"]),
        RoundKills = Int(state?["round_kills"]),
        RoundKillHs = Int(state?["round_killhs"]),
        Flashed = Int(state?["flashed"]),

        Money = Int(state?["money"]),
        EquipmentValue = Int(state?["equip_value"]),

        // Posição no mapa (allplayers_position; normalmente só espectando).
        // Sem o dado fica 0,0,0 - a HUD trata isso como "sem posição".
        Position = ParseVector(Str(player["position"])) ?? new PositionDto(),
    };

    if (player["weapons"] is JsonObject weapons)
    {
        foreach (var kv in weapons)
        {
            if (kv.Value is not JsonObject w) continue;
            var name = Str(w["name"]);
            // "Submachine Gun" -> "submachinegun", "SniperRifle" -> "sniperrifle"
            var type = Str(w["type"]).Replace(" ", string.Empty).ToLowerInvariant();

            // arma na mão + munição
            if (Str(w["state"]) == "active")
            {
                dto.ActiveWeapon = name;
                dto.AmmoClip = Int(w["ammo_clip"]);
                dto.AmmoClipMax = Int(w["ammo_clip_max"]);
                dto.AmmoReserve = Int(w["ammo_reserve"]);
            }

            // granadas e C4 / pistola / arma principal (tela de pausa)
            if (type is "grenade" or "c4") dto.Utility.Add(name);
            else if (type == "pistol") dto.Secondary = name;
            else if (type is "rifle" or "sniperrifle" or "submachinegun" or "shotgun" or "machinegun") dto.Primary = name;
        }
    }

    return dto;
}

// Granadas (nó "grenades"). Formato de cada entrada:
//   "123": { "owner": "7656...", "type": "smoke", "position": "x, y, z",
//            "velocity": "x, y, z", "lifetime": "1.2", "effecttime": "0.0",
//            "flames": { "flame_1": "x, y, z", ... } }   <- só no "inferno"
static List<GrenadeDto> MapGrenades(JsonObject json)
{
    var list = new List<GrenadeDto>();
    if (json["grenades"] is not JsonObject grenades) return list;

    foreach (var kv in grenades)
    {
        if (kv.Value is not JsonObject g) continue;
        var dto = new GrenadeDto
        {
            Id = kv.Key,
            Owner = Str(g["owner"]),
            Type = Str(g["type"]),
            Position = ParseVector(Str(g["position"])),
            Velocity = ParseVector(Str(g["velocity"])),
            Lifetime = Num(g["lifetime"]),
            EffectTime = Num(g["effecttime"]),
        };
        if (g["flames"] is JsonObject flames)
        {
            foreach (var flame in flames)
            {
                var pos = ParseVector(Str(flame.Value));
                if (pos != null) dto.Flames.Add(pos);
            }
        }
        list.Add(dto);
    }
    return list;
}

// ---- leitura tolerante: o GSI manda números às vezes como número, às
// vezes como texto ("12.5"), e booleanos como true/1 ----
static string Str(JsonNode? node)
{
    if (node is not JsonValue v) return string.Empty;
    return v.TryGetValue<string>(out var s) ? s : v.ToJsonString();
}

static double Num(JsonNode? node)
{
    if (node is not JsonValue v) return 0;
    if (v.TryGetValue<double>(out var d)) return d;
    return double.TryParse(Str(node), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
}

static int Int(JsonNode? node) => (int)Math.Round(Num(node));

static bool Bool(JsonNode? node)
{
    if (node is not JsonValue v) return false;
    if (v.TryGetValue<bool>(out var b)) return b;
    return Num(node) != 0 || Str(node).Equals("true", StringComparison.OrdinalIgnoreCase);
}

// "x, y, z" -> PositionDto (ponto como separador decimal, independente do
// idioma do Windows)
static PositionDto? ParseVector(string? text)
{
    if (string.IsNullOrWhiteSpace(text)) return null;
    var parts = text.Split(',');
    if (parts.Length != 3) return null;
    if (float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
        float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
        float.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
    {
        return new PositionDto { X = x, Y = y, Z = z };
    }
    return null;
}
