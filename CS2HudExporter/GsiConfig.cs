using System.Text.RegularExpressions;

namespace CS2HudExporter;

/// <summary>
/// Cria o arquivo de configuração do Game State Integration na pasta cfg
/// do CS2, pra ele saber pra onde mandar os dados. Acha o CS2 pelas
/// bibliotecas da Steam (libraryfolders.vdf), em qualquer disco.
/// </summary>
public static class GsiConfig
{
    public const string FileName = "gamestate_integration_hudexporter.cfg";

    private static readonly string Content = string.Join("\n", new[]
    {
        "\"HudExporter Integration Configuration\"",
        "{",
        "    \"uri\"          \"http://localhost:3000/\"",
        "    \"timeout\"      \"5.0\"",
        "    \"buffer\"       \"0.1\"",
        "    \"throttle\"     \"0.1\"",
        "    \"heartbeat\"    \"10.0\"",
        "    \"data\"",
        "    {",
        "        \"provider\"                  \"1\"",
        "        \"map\"                       \"1\"",
        "        \"map_round_wins\"            \"1\"",
        "        \"round\"                     \"1\"",
        "        \"player_id\"                 \"1\"",
        "        \"player_state\"              \"1\"",
        "        \"player_weapons\"            \"1\"",
        "        \"player_match_stats\"        \"1\"",
        "        \"player_position\"           \"1\"",
        "        \"allplayers_id\"             \"1\"",
        "        \"allplayers_state\"          \"1\"",
        "        \"allplayers_match_stats\"    \"1\"",
        "        \"allplayers_weapons\"        \"1\"",
        "        \"allplayers_position\"       \"1\"",
        "        \"phase_countdowns\"          \"1\"",
        "        \"allgrenades\"               \"1\"",
        "        \"bomb\"                      \"1\"",
        "    }",
        "}",
        "",
    });

    /// <summary>
    /// Grava (ou atualiza) o .cfg. Retorna o caminho gravado, ou null se não
    /// achou o CS2.
    /// </summary>
    public static string? Write()
    {
        var cfgDir = FindCs2CfgDir();
        if (cfgDir == null) return null;

        var path = Path.Combine(cfgDir, FileName);
        var current = File.Exists(path) ? File.ReadAllText(path) : null;
        if (current != Content)
        {
            File.WriteAllText(path, Content);
            Console.WriteLine("[GSI] Configuração gravada em " + path);
            Console.WriteLine("      Se o CS2 já estava aberto, reinicie o jogo pra ele carregar.");
        }
        return path;
    }

    private static string? FindCs2CfgDir()
    {
        foreach (var steam in SteamRoots())
        {
            foreach (var library in LibraryFolders(steam))
            {
                var cfg = Path.Combine(library, "steamapps", "common", "Counter-Strike Global Offensive", "game", "csgo", "cfg");
                if (Directory.Exists(cfg)) return cfg;
            }
        }
        return null;
    }

    // Pasta da Steam: registro do Windows + locais padrão
    private static IEnumerable<string> SteamRoots()
    {
        var roots = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string p) roots.Add(p.Replace('/', Path.DirectorySeparatorChar));
            }
            catch
            {
                // sem acesso ao registro: segue com os caminhos padrão
            }
            roots.Add(@"C:\Program Files (x86)\Steam");
            roots.Add(@"C:\Program Files\Steam");
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".steam", "steam"));
            roots.Add(Path.Combine(home, ".local", "share", "Steam"));
        }
        return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // Todas as bibliotecas da Steam (o CS2 pode estar em outro disco)
    private static IEnumerable<string> LibraryFolders(string steamRoot)
    {
        var result = new List<string> { steamRoot };
        var text = ReadOrNull(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"));
        if (text == null) return result;
        foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
        {
            result.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
        }
        return result;
    }

    private static string? ReadOrNull(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }
}
