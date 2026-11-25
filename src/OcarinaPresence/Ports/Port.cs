namespace OcarinaPresence;

public sealed record Port(string Name, string ProcessName, string WindowTitle, string? MarkerFile, Func<PortLaunch, IEnumerable<string>> SaveDirs, bool OwnPresence = true, string? DiscordThread = null)
{
    public static readonly IReadOnlyList<Port> All = new[]
    {
        new Port("Hookshot", "hookshot", "Hookshot", "hookshot_vk.dll", SaveFolders.UserSaveDirs),
        new Port("Triforce", "Triforce", "Triforce", "triforce_vk.dll", SaveFolders.UserSaveDirs),
        new Port("HeyListen", "HeyListen", "HeyListen", "hookshot_vk.dll", SaveFolders.UserSaveDirs),
        new Port("Longshot", "longshot", "Longshot", "longshot_vk.dll", SaveFolders.LongshotSaveDirs),
        new Port("Project ReLink", "relink-game", "Project ReLink", "relink_vk.dll", SaveFolders.UserSaveDirs, OwnPresence: false),
        new Port("Ocarina Reborn", "ocarina_reborn", "Ocarina Reborn", "ocarina_reborn_vk.dll", SaveFolders.UserSaveDirs),
        new Port("NX2navi", "ocarina", "NX2navi", null, SaveFolders.ProfileSaveDirs("NX2NAVI_PROFILE_DIR"), DiscordThread: "discord"),
        new Port("NX2Saria", "nx2saria", "NX2Saria", @"romfs\romfs.bin", SaveFolders.ProfileSaveDirs("NX2SARIA_PROFILE_DIR"), DiscordThread: "discord"),
        new Port("Rekarina", "Rekarina", "Rekarina", @"exefs\main.npdm", SaveFolders.ProfileSaveDirs("REKARINA_PROFILE_DIR"), DiscordThread: "discord")
    };

    public bool Matches(string exeDir, string windowTitle) =>
        (MarkerFile is null || File.Exists(Path.Combine(exeDir, MarkerFile)))
        && windowTitle.Contains(WindowTitle, StringComparison.OrdinalIgnoreCase);
}

public sealed record PortLaunch(string ExeDir, string[]? Arguments, IReadOnlyDictionary<string, string>? Environment)
{
    public string? ArgumentPath(string name, string basePath)
    {
        string? value = null;
        if (Arguments is not null)
            for (var i = 1; i + 1 < Arguments.Length; i++)
                if (Arguments[i] == name)
                    value = Arguments[i + 1];
        return FullPath(value, basePath);
    }

    public string? EnvironmentPath(string name, string basePath) =>
        Environment is not null && Environment.TryGetValue(name, out var value) ? FullPath(value, basePath) : null;

    static string? FullPath(string? path, string basePath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.GetFullPath(path.Trim(), basePath);
        }
        catch
        {
            return null;
        }
    }
}
