using System.Text.Json;

namespace CS2HudExporter;

/// <summary>
/// Configuração das webcams (VDO.Ninja), feita no control.html.
/// </summary>
public class CamsDto
{
    // false = nenhuma câmera aparece na HUD
    public bool Enabled { get; set; }

    // senha da sala do VDO.Ninja (&password=...), igual à usada pelos
    // jogadores no link deles. Vazio = sem senha própria.
    public string Password { get; set; } = string.Empty;

    // steamId do jogador -> ID da câmera dele no VDO.Ninja (?push=ID)
    public Dictionary<string, string> Ids { get; set; } = new();
}

/// <summary>
/// Guarda a configuração das câmeras num cams.json ao lado do projeto.
/// </summary>
public class CamsStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private CamsDto _cams = new();
    private static readonly JsonSerializerOptions FileJson = new() { WriteIndented = true };

    public CamsStore(string path)
    {
        _path = path;
        try
        {
            if (File.Exists(_path))
                _cams = JsonSerializer.Deserialize<CamsDto>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[AVISO] Não consegui ler " + _path + ": " + ex.Message);
        }
    }

    public CamsDto Get()
    {
        lock (_lock) { return _cams; }
    }

    public void Set(CamsDto input)
    {
        lock (_lock)
        {
            var ids = new Dictionary<string, string>();
            foreach (var kv in input.Ids ?? new())
            {
                var id = Clean(kv.Value);
                if (!string.IsNullOrEmpty(kv.Key) && id.Length > 0) ids[kv.Key] = id;
            }
            _cams = new CamsDto
            {
                Enabled = input.Enabled,
                Password = Clean(input.Password),
                Ids = ids,
            };
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(_cams, FileJson));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[AVISO] Não consegui salvar " + _path + ": " + ex.Message);
            }
        }
    }

    // IDs/senha do VDO.Ninja: só letras, números, _ e - (vão na URL)
    private static string Clean(string? s)
    {
        var chars = (s ?? string.Empty).Trim().Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray();
        var r = new string(chars);
        return r.Length > 60 ? r[..60] : r;
    }
}
