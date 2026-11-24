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

    public static IEnumerable<string> UserSaveDirs(string exeDir, string? userFolder)
    {
        var save = Path.Combine(userFolder ?? Path.Combine(GameFolder(exeDir), "user"), "save");
        if (Directory.Exists(save))
            yield return save;
    }

    public static IEnumerable<string> Nx2naviSaveDirs(string exeDir, string? userFolder)
    {
        var profile = Path.Combine(exeDir, "profile");
        if (Directory.Exists(profile))
            yield return profile;
    }

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
