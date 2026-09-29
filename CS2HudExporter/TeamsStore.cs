using System.Text.Json;

namespace CS2HudExporter;

/// <summary>
/// Nome + logo de um time, como a HUD e o painel de controle enxergam.
/// </summary>
public class TeamInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty; // data URL (data:image/png;base64,...) ou vazio - só no /api/teams (painel)
    public string LogoUrl { get; set; } = string.Empty; // caminho da imagem no servidor - é o que a HUD usa (leve pra consultar 30x/s)
}

/// <summary>
/// Times pelo lado em que estão AGORA. null = sem time configurado nesse
/// lado (a HUD usa o padrão: COUNTER / TERRORIST).
/// </summary>
public class TeamsDto
{
    public TeamInfoDto? CT { get; set; }
    public TeamInfoDto? T { get; set; }
}

/// <summary>
/// Time salvo em disco. Além do nome/logo, guarda os steamIds dos
/// jogadores: é assim que o nome/logo acompanha o TIME quando ele troca de
/// lado no intervalo (ou no overtime), sem depender de regra de MR12/MR3.
/// </summary>
public class StoredTeam
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public string Side { get; set; } = "CT"; // lado em que foi visto por último
    public List<string> SteamIds { get; set; } = new();
}

/// <summary>
/// Guarda os times configurados no painel (control.html) num teams.json ao
/// lado do projeto, e descobre de que lado cada um está a partir da lista
/// de jogadores que o CS2 manda.
/// </summary>
public class TeamsStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private List<StoredTeam> _teams = new();
    private static readonly JsonSerializerOptions FileJson = new() { WriteIndented = true };

    public TeamsStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(_path))
                _teams = JsonSerializer.Deserialize<List<StoredTeam>>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[AVISO] Não consegui ler " + _path + ": " + ex.Message);
            _teams = new();
        }
    }

    /// <summary>
    /// Times pelo lado atual. Chamado a cada GET da HUD: se a maioria dos
    /// jogadores de um time agora está do outro lado, o time trocou de lado.
    /// </summary>
    public TeamsDto Resolve(IReadOnlyList<PlayerHudDto> players, bool includeLogoData = false)
    {
        lock (_lock)
        {
            var ct = IdsOnSide(players, "CT");
            var t = IdsOnSide(players, "T");
            var changed = false;

            foreach (var team in _teams)
            {
                if (team.SteamIds.Count == 0)
                {
                    // salvo antes da partida ter jogadores: grava o elenco
                    // assim que eles aparecerem no lado escolhido
                    var ids = team.Side == "CT" ? ct : t;
                    if (ids.Count > 0) { team.SteamIds = ids.ToList(); changed = true; }
                    continue;
                }

                var inCt = team.SteamIds.Count(ct.Contains);
                var inT = team.SteamIds.Count(t.Contains);
                if (inCt == inT) continue; // sem informação suficiente: mantém
                var side = inCt > inT ? "CT" : "T";
                if (side != team.Side) { team.Side = side; changed = true; }
            }

            // dois times no mesmo lado não faz sentido (ex: dados incompletos
            // no meio da troca de lado): mantém o primeiro e ajusta o segundo
            if (_teams.Count == 2 && _teams[0].Side == _teams[1].Side)
            {
                _teams[1].Side = _teams[0].Side == "CT" ? "T" : "CT";
                changed = true;
            }

            // jogador novo (entrou depois / substituto) passa a contar pro
            // time do lado dele, se ainda não é de nenhum time
            if (_teams.Count > 0)
            {
                var known = _teams.SelectMany(x => x.SteamIds).ToHashSet();
                foreach (var team in _teams)
                {
                    var ids = team.Side == "CT" ? ct : t;
                    foreach (var id in ids)
                    {
                        if (known.Add(id)) { team.SteamIds.Add(id); changed = true; }
                    }
                }
            }

            if (changed) Save();
            return ToDto(includeLogoData);
        }
    }

    /// <summary>Salva os times do painel, pelo lado em que estão agora.</summary>
    public void Set(TeamsDto input, IReadOnlyList<PlayerHudDto> players)
    {
        lock (_lock)
        {
            _teams = new();
            AddTeam(input.CT, "CT", players);
            AddTeam(input.T, "T", players);
            Save();
        }
    }

    /// <summary>Inverte manualmente os lados (caso a detecção automática erre).</summary>
    public void Swap(IReadOnlyList<PlayerHudDto> players)
    {
        lock (_lock)
        {
            foreach (var team in _teams)
            {
                team.Side = team.Side == "CT" ? "T" : "CT";
                team.SteamIds = IdsOnSide(players, team.Side).ToList();
            }
            Save();
        }
    }

    /// <summary>Volta pro padrão (COUNTER / TERRORIST, sem logos).</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _teams = new();
            Save();
        }
    }

    private void AddTeam(TeamInfoDto? info, string side, IReadOnlyList<PlayerHudDto> players)
    {
        if (info == null) return;
        var name = (info.Name ?? string.Empty).Trim();
        if (name.Length > 40) name = name[..40];
        var logo = info.Logo ?? string.Empty;
        if (!logo.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) logo = string.Empty;
        if (name.Length == 0 && logo.Length == 0) return; // lado sem nada = padrão

        _teams.Add(new StoredTeam
        {
            Name = name,
            Logo = logo,
            Side = side,
            SteamIds = IdsOnSide(players, side).ToList(),
        });
    }

    private static HashSet<string> IdsOnSide(IReadOnlyList<PlayerHudDto> players, string side)
    {
        return players
            .Where(p => p.Team == side && !string.IsNullOrEmpty(p.SteamId))
            .Select(p => p.SteamId)
            .ToHashSet();
    }

    /// <summary>Imagem da logo (bytes + tipo) a partir do data URL salvo.</summary>
    public (byte[] Bytes, string ContentType)? GetLogo(string id)
    {
        lock (_lock)
        {
            var team = _teams.FirstOrDefault(x => x.Id == id);
            if (team == null || team.Logo.Length == 0) return null;
            // formato: data:image/png;base64,AAAA...
            var comma = team.Logo.IndexOf(',');
            var semi = team.Logo.IndexOf(';');
            if (comma < 0 || semi < 0 || semi > comma) return null;
            try
            {
                var bytes = Convert.FromBase64String(team.Logo[(comma + 1)..]);
                return (bytes, team.Logo[5..semi]);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }

    private TeamsDto ToDto(bool includeLogoData)
    {
        var dto = new TeamsDto();
        foreach (var team in _teams)
        {
            var info = new TeamInfoDto
            {
                Name = team.Name,
                Logo = includeLogoData ? team.Logo : string.Empty,
                // id muda a cada "Salvar", então a HUD recarrega a imagem nova
                LogoUrl = team.Logo.Length > 0 ? "/api/teams/logo/" + team.Id : string.Empty,
            };
            if (team.Side == "CT") dto.CT = info; else dto.T = info;
        }
        return dto;
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_teams, FileJson));
        }
        catch (Exception ex)
        {
            Console.WriteLine("[AVISO] Não consegui salvar " + _path + ": " + ex.Message);
        }
    }
}
