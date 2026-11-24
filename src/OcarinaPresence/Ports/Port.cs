namespace OcarinaPresence;

public sealed record Port(string Name, string ProcessName, string WindowTitle, string? MarkerFile, Func<string, string?, IEnumerable<string>> SaveDirs, bool ReadsUserArgument, string? DiscordThread)
{
    public static readonly IReadOnlyList<Port> All = new[]
    {
        new Port("Hookshot", "hookshot", "Hookshot", "hookshot_vk.dll", SaveFolders.UserSaveDirs, true, null),
        new Port("Triforce", "Triforce", "Triforce", "triforce_vk.dll", SaveFolders.UserSaveDirs, true, null),
        new Port("NX2navi", "ocarina", "NX2navi", null, SaveFolders.Nx2naviSaveDirs, false, "discord")
    };

    public bool Matches(string exeDir, string windowTitle) =>
        (MarkerFile is null || File.Exists(Path.Combine(exeDir, MarkerFile)))
        && windowTitle.Contains(WindowTitle, StringComparison.OrdinalIgnoreCase);
}
