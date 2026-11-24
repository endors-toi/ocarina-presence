using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace OcarinaPresence;

public sealed record UpdateInfo(Version Version, string Tag, string Url, string? Sha256);

public sealed class Updater
{
    const string Repo = "endors-toi/ocarina-presence";
    const string DownloadPrefix = "https://github.com/" + Repo + "/releases/download/";
    const string ExeName = "OcarinaPresence.exe";
    const string OldSuffix = ".old";
    const string UpdatedArgument = "--updated";

    static readonly HttpClient Http = CreateClient();

    public static Version Current { get; } = Normalize(Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0));

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OcarinaPresence");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    public static void WaitForPreviousInstance(string[] args)
    {
        var index = Array.IndexOf(args, UpdatedArgument);
        if (index < 0 || index + 1 >= args.Length || !int.TryParse(args[index + 1], out var pid))
            return;
        try
        {
            using var previous = Process.GetProcessById(pid);
            previous.WaitForExit(15000);
        }
        catch
        {
        }
    }

    public static void CleanUp()
    {
        try
        {
            File.Delete(Environment.ProcessPath + OldSuffix);
        }
        catch
        {
        }
    }

    public async Task<UpdateInfo?> CheckAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest", cts.Token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cts.Token));
        var root = document.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var parsed))
            return null;
        var version = Normalize(parsed);
        if (version <= Current)
            return null;

        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !url.StartsWith(DownloadPrefix, StringComparison.Ordinal))
                continue;
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            var sha256 = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : null;
            return new UpdateInfo(version, tag, url, sha256);
        }
        return null;
    }

    public async Task InstallAsync(UpdateInfo update)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running program.");
        var download = Path.Combine(Path.GetTempPath(), $"OcarinaPresence-update-{Guid.NewGuid():N}.zip");
        var staged = exe + ".new";
        var old = exe + OldSuffix;
        try
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            using (var response = await Http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead, cts.Token))
            {
                response.EnsureSuccessStatusCode();
                await using var file = File.Create(download);
                await response.Content.CopyToAsync(file, cts.Token);
            }

            if (update.Sha256 is not null)
            {
                await using var file = File.OpenRead(download);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
                if (!string.Equals(hash, update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded update does not match its checksum.");
            }

            using (var archive = ZipFile.OpenRead(download))
            {
                var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.Name, ExeName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException($"The update does not contain {ExeName}.");
                entry.ExtractToFile(staged, true);
            }

            File.Move(exe, old, true);
            try
            {
                File.Move(staged, exe);
            }
            catch
            {
                File.Move(old, exe, true);
                throw;
            }
        }
        finally
        {
            TryDelete(download);
            TryDelete(staged);
        }

        Process.Start(new ProcessStartInfo(exe, $"{UpdatedArgument} {Environment.ProcessId}") { UseShellExecute = false });
    }

    static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }
}
