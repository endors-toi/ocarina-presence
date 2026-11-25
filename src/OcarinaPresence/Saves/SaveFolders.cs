namespace OcarinaPresence;

public static class SaveFolders
{
    public const string CaptionFile = "caption.sav";
    public const string ProgressFile = "progress.sav";
    public const string OptionFile = "option.sav";

    static readonly string[] EmulatorDataFolders = { "lichii", "eden", "fritchii" };

    public static readonly EnumerationOptions Recursive = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    static IEnumerable<string> EmulatorDataDirs(string? exeDir)
    {
        if (exeDir is not null)
            yield return Path.Combine(exeDir, "user");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var folder in EmulatorDataFolders)
            yield return Path.Combine(appData, folder);
    }

    public static IEnumerable<string> EmulatorSaveDirs(string? exeDir, string titleId) =>
        EmulatorDataDirs(exeDir)
            .Select(dir => Path.Combine(dir, "nand", "user", "save"))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateDirectories(root, titleId, SearchOption.AllDirectories));

    public static string GameFolder(string exeDir)
    {
        var parent = Path.GetDirectoryName(exeDir);
        return parent is not null
            && string.Equals(Path.GetFileName(exeDir), "bin", StringComparison.OrdinalIgnoreCase)
            && File.Exists(Path.Combine(parent, "image.bin"))
            ? parent
            : exeDir;
    }

    static IEnumerable<string> Existing(params string?[] dirs) =>
        dirs.OfType<string>().Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> UserSaveDirs(PortLaunch launch)
    {
        var game = GameFolder(launch.ExeDir);
        return Existing(Path.Combine(launch.ArgumentPath("--user", game) ?? Path.Combine(game, "user"), "save"));
    }

    public static IEnumerable<string> LongshotSaveDirs(PortLaunch launch)
    {
        var game = GameFolder(launch.ExeDir);
        if (launch.EnvironmentPath("LONGSHOT_PROFILE", game) is { } profile)
            return Existing(Path.Combine(profile, "saves", "save"));
        if (launch.ArgumentPath("--user", game) is { } user)
            return Existing(Path.Combine(user, "save"));
        return Existing(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Longshot", "save"),
            Path.Combine(game, "user", "save"),
            launch.Environment is null ? Path.GetFullPath(Path.Combine(game, "..", "..", "profile", "saves", "save")) : null);
    }

    public static Func<PortLaunch, IEnumerable<string>> ProfileSaveDirs(string variable) => launch =>
        Existing(launch.ArgumentPath("--profile", launch.ExeDir)
            ?? launch.EnvironmentPath(variable, launch.ExeDir)
            ?? Path.Combine(launch.ExeDir, "profile"));

    public static IEnumerable<FileInfo> Newest(IEnumerable<string> saveDirs, string fileName) =>
        saveDirs
            .SelectMany(dir => Directory.EnumerateFiles(dir, fileName, Recursive))
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc);

    public static IEnumerable<FileInfo> Captions(IEnumerable<string> saveDirs) => Newest(saveDirs, CaptionFile);

    public static FileInfo? NewestCaption(IEnumerable<string> saveDirs, DateTime notBefore) =>
        Captions(saveDirs).FirstOrDefault(info => info.LastWriteTime >= notBefore);

    public static FileInfo? NewestOptions(IEnumerable<string> saveDirs) => Newest(saveDirs, OptionFile).FirstOrDefault();

    public static bool HasCaption(IEnumerable<string> saveDirs) =>
        saveDirs.Any(dir => Directory.EnumerateFiles(dir, CaptionFile, Recursive).Any());
}
