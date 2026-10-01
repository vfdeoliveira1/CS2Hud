using System.Text.Json;

namespace CS2HudExporter;

/// <summary>
/// Um mapa da série (vindo do veto em CS2MapVeto/index.html).
/// </summary>
public class SeriesMapDto
{
    public string Id { get; set; } = string.Empty;       // "mirage", "dust2"...
    public string Name { get; set; } = string.Empty;     // "Mirage", "Dust II"...
    public string PickedBy { get; set; } = string.Empty; // "A", "B" ou "" (mapa decisivo)
    public int? ScoreA { get; set; }                     // placar final do time A (null = não jogado)
    public int? ScoreB { get; set; }
    public string Winner { get; set; } = string.Empty;   // "A", "B" ou "" (ainda não definido)
}

/// <summary>
/// Série inteira: formato, nomes dos times (como no veto), mapas em ordem e
/// qual está sendo jogado agora.
/// </summary>
public class SeriesDto
{
    public int BestOf { get; set; }                       // 1, 3, 5 (0 = sem série)
    public string TeamA { get; set; } = string.Empty;
    public string TeamB { get; set; } = string.Empty;
    public List<SeriesMapDto> Maps { get; set; } = new();
    public int Current { get; set; } = -1;                // índice em Maps do mapa em jogo (-1 = nenhum)
}

/// <summary>
/// Guarda a série num series.json ao lado do projeto (sobrevive a reinícios).
/// </summary>
public class SeriesStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private SeriesDto? _series;
    private static readonly JsonSerializerOptions FileJson = new() { WriteIndented = true };

    public SeriesStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(_path))
                _series = JsonSerializer.Deserialize<SeriesDto>(File.ReadAllText(_path));
        }
        catch (Exception ex)
        {
            Console.WriteLine("[AVISO] Não consegui ler " + _path + ": " + ex.Message);
        }
    }

    public SeriesDto? Get()
    {
        lock (_lock) { return _series; }
    }

    public void Set(SeriesDto? series)
    {
        lock (_lock)
        {
            if (series != null)
            {
                series.TeamA = Trim(series.TeamA);
                series.TeamB = Trim(series.TeamB);
                series.Maps ??= new();
                if (series.Maps.Count > 7) series.Maps = series.Maps.Take(7).ToList();
                foreach (var m in series.Maps)
                {
                    m.Id = Trim(m.Id);
                    m.Name = Trim(m.Name);
                    m.PickedBy = m.PickedBy is "A" or "B" ? m.PickedBy : string.Empty;
                    m.Winner = m.Winner is "A" or "B" ? m.Winner : string.Empty;
                }
                if (series.Current < -1 || series.Current >= series.Maps.Count) series.Current = -1;
                if (series.Maps.Count == 0) series = null;
            }
            _series = series;
            try
            {
                if (_series == null) { if (File.Exists(_path)) File.Delete(_path); }
                else File.WriteAllText(_path, JsonSerializer.Serialize(_series, FileJson));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AVISO] Não consegui salvar " + _path + ": " + ex.Message);
            }
        }
    }

    private static string Trim(string? s)
    {
        s = (s ?? string.Empty).Trim();
        return s.Length > 40 ? s[..40] : s;
    }
}
