using System.Diagnostics;

namespace OcarinaPresence;

readonly record struct Game(Process Process, string? ExeDir, Port? Port, string Name);

sealed class GameFinder
{
    readonly Config _config;

    public GameFinder(Config config) => _config = config;

    public List<Game> Find(List<string> seen)
    {
        var games = new List<Game>();
        foreach (var name in _config.ProcessNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                if (WindowTitle(process).Contains(_config.WindowTitleContains, StringComparison.OrdinalIgnoreCase))
                {
                    games.Add(new Game(process, ExeDir(process), null, Capitalized(name)));
                    seen.Add(Capitalized(name));
                }
                else
                {
                    seen.Add($"{Capitalized(name)} (no game open)");
                    process.Dispose();
                }
            }
        }

        foreach (var port in Port.All)
        {
            foreach (var process in Process.GetProcessesByName(port.ProcessName))
            {
                var exeDir = ExeDir(process);
                if (exeDir is not null && port.Matches(exeDir, WindowTitle(process)))
                {
                    games.Add(new Game(process, exeDir, port, port.Name));
                    seen.Add(port.Name);
                }
                else
                {
                    process.Dispose();
                }
            }
        }
        return games;
    }

    static string Capitalized(string name) => name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];

    static string? ExeDir(Process process)
    {
        try
        {
            return Path.GetDirectoryName(process.MainModule?.FileName);
        }
        catch
        {
            return null;
        }
    }

    static string WindowTitle(Process process)
    {
        try
        {
            return process.MainWindowTitle;
        }
        catch
        {
            return "";
        }
    }
}
