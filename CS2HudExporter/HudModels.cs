namespace CS2HudExporter;

/// <summary>
/// Estado completo da HUD, é isso que fica disponível no endpoint HTTP.
/// </summary>
public class HudStateDto
{
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Map { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;   // warmup, live, intermission, gameover...
    public int Round { get; set; }
    public string BombState { get; set; } = string.Empty;

    public ScoreboardDto Scoreboard { get; set; } = new();
    public List<PlayerHudDto> Players { get; set; } = new();

    // Tempo restante da fase atual (round em andamento, freezetime, ou
    // contagem da bomba depois de plantada) em segundos. Só vem preenchido
    // quando alguém está espectando/observando a partida (limitação do
    // próprio GSI da Valve - "Only valid for GOTV or spectators").
    public double? PhaseSecondsLeft { get; set; }
    public string PhaseCountdownName { get; set; } = string.Empty; // ex: "bomb", "live", "freezetime"

    // Estado da bomba (Carried/Dropped/Planting/Planted/Defusing/Exploded/
    // Defused) com posição exata no mapa. Só vem preenchido espectando
    // (limitação do próprio GSI - precisa de "bomb" "1" no .cfg).
    public BombDto Bomb { get; set; } = new();

    // Jogador que o GOTV/espectador está observando no momento (a "câmera
    // ativa"). Fica null se ninguém estiver sendo observado.
    public PlayerHudDto? SpectatedPlayer { get; set; }

    // Fase do round atual (Freezetime / Live / Over / Bomb / Defuse...).
    // Ao contrário de PhaseSecondsLeft, esse dado NÃO é restrito a
    // espectador - vem certinho mesmo jogando. É a base pro timer
    // estimado no front-end.
    public string RoundPhase { get; set; } = string.Empty;

    // Granadas/utilitários existindo no mapa agora (em voo, fumaças ativas,
    // fogo queimando). Só vem espectando e com "allgrenades" "1" no .cfg.
    public List<GrenadeDto> Grenades { get; set; } = new();

    // Como cada round da partida terminou (nó map.round_wins do GSI), em
    // ordem. Result: ct_win_elimination, t_win_elimination, ct_win_defuse,
    // t_win_bomb, ct_win_time...
    public List<RoundWinDto> RoundWins { get; set; } = new();

    // Nome/logo dos times pelo lado atual (configurados no control.html).
    // null no lado = padrão (COUNTER / TERRORIST).
    public TeamsDto? Teams { get; set; }
}

/// <summary>
/// Resultado de um round.
/// </summary>
public class RoundWinDto
{
    public int Round { get; set; }
    public string Result { get; set; } = string.Empty;
}

/// <summary>
/// Uma granada do nó "grenades" do GSI.
/// </summary>
public class GrenadeDto
{
    public string Id { get; set; } = string.Empty;    // id da entidade (estável enquanto ela existir)
    public string Owner { get; set; } = string.Empty; // steamId de quem jogou
    public string Type { get; set; } = string.Empty;  // smoke, decoy, firebomb, inferno, flashbang, frag
    public PositionDto? Position { get; set; }
    public PositionDto? Velocity { get; set; }
    public double Lifetime { get; set; }   // segundos desde que foi lançada
    public double EffectTime { get; set; } // segundos desde que o efeito começou (fumaça estourou); 0 = ainda não
    public List<PositionDto> Flames { get; set; } = new(); // só "inferno": posição de cada foco de fogo
}

/// <summary>
/// Placar da partida (CT x T).
/// </summary>
public class ScoreboardDto
{
    public TeamScoreDto CT { get; set; } = new();
    public TeamScoreDto T { get; set; } = new();
}

public class TeamScoreDto
{
    public string TeamName { get; set; } = string.Empty; // nome/clan do time, se disponível
    public int Score { get; set; }
    public int ConsecutiveRoundLosses { get; set; }
    public int TimeoutsRemaining { get; set; }
}

/// <summary>
/// Posição no mapa (coordenadas do jogo, X/Y/Z).
/// </summary>
public class PositionDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

/// <summary>
/// Estado da bomba: onde ela está e o que está acontecendo com ela.
/// </summary>
public class BombDto
{
    public string State { get; set; } = string.Empty; // Carried, Dropped, Planting, Planted, Defusing, Exploded, Defused, Undefined
    public PositionDto? Position { get; set; }
    public double Countdown { get; set; } // segundos até explodir (só relevante quando Planted)
}

/// <summary>
/// Dados de um jogador para a HUD.
/// </summary>
public class PlayerHudDto
{
    public string SteamId { get; set; } = string.Empty;
    public string Nick { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;   // "CT" ou "T"

    public int Health { get; set; }
    public int Armor { get; set; }
    public bool HasArmor { get; set; }
    public bool HasHelmet { get; set; }
    public bool HasDefuseKit { get; set; }

    public string ActiveWeapon { get; set; } = string.Empty;
    public int AmmoClip { get; set; }
    public int AmmoClipMax { get; set; }
    public int AmmoReserve { get; set; }

    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int RoundKills { get; set; }

    public int Money { get; set; }
    public int EquipmentValue { get; set; }

    public List<string> Utility { get; set; } = new(); // granadas / itens utilitários na mão
    public string Primary { get; set; } = string.Empty;   // arma principal no inventário (rifle, SMG, sniper...), mesmo sem estar na mão
    public string Secondary { get; set; } = string.Empty; // pistola no inventário

    public PositionDto? Position { get; set; }
}