using System.Diagnostics;
using System.Text.Json.Nodes;

namespace OcarinaPresence;

public sealed class PresenceService
{
    readonly Config _config;
    readonly Logger _log;
    readonly PortStatus _port;
    readonly GameFinder _finder;
    readonly ActivityBuilder _activity;
    readonly ManualResetEventSlim _wake = new();
    volatile bool _stopping;

    DiscordIpc? _ipc;
    string? _sent;
    string? _lastLocation;
    (string path, DateTime written)? _captionStamp;
    Location? _location;
    string? _ageImage;
    (string path, DateTime written)? _optionsStamp;
    BossLog? _bosses;
    string? _lastAction;
    static readonly TimeSpan DefeatShown = TimeSpan.FromMinutes(10);
    static readonly TimeSpan SendGap = TimeSpan.FromSeconds(4);
    DateTime _lastSend = DateTime.MinValue;
    DateTime? _sendDue;
    const string DreamWorld = "DreamWorld";
    string _status = "";
    string _detectedKey = "";
    bool _detailsPending;
    int _sessionPid;
    DateTime _sessionStart;
    DateTime _emulatorStart;
    bool _firstTick = true;
    readonly Dictionary<int, PortLaunch> _launches = new();
    string? _saveDirsKey;

    public event Action<string>? StatusChanged;
    public event Action<Detection>? DetectedChanged;
    public event Action<bool>? DetailsPendingChanged;

    public PresenceService(Config config, Logger log)
    {
        _config = config;
        _log = log;
        _port = new PortStatus(log.Write);
        _finder = new GameFinder(config);
        _activity = new ActivityBuilder(config);
    }

    public void Refresh() => _wake.Set();

    public void RestorePort() => _port.Restore();

    public void Stop()
    {
        _stopping = true;
        _wake.Set();
    }

    public void Run()
    {
        _log.Write($"Watching for \"{_config.WindowTitleContains}\" in {string.Join(", ", _config.ProcessNames.Select(n => n + ".exe"))}, and for {string.Join(", ", Port.All.Select(p => $"{p.Name} ({p.ProcessName}.exe)"))}");
        while (!_stopping)
        {
            _sendDue = null;
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                _log.Write($"Tick failed: {e.Message}");
            }
            _firstTick = false;
            var delay = TimeSpan.FromSeconds(Math.Max(1, _config.PollSeconds));
            if (_sendDue is { } due && due - DateTime.UtcNow < delay)
                delay = due - DateTime.UtcNow > TimeSpan.Zero ? due - DateTime.UtcNow : TimeSpan.Zero;
            _wake.Wait(delay);
            _wake.Reset();
        }
        Clear();
        _port.Restore();
    }

    void Tick()
    {
        var seen = new List<string>();
        var games = _finder.Find(seen);
        try
        {
            foreach (var pid in _launches.Keys.Where(pid => games.All(g => g.Process.Id != pid)).ToList())
                _launches.Remove(pid);
            Game? shown = games.Count == 0 ? null : games[Math.Max(0, games.FindIndex(g => g.Name == _config.Target))];
            var detected = shown is { } game ? SaveDirs(game) : Array.Empty<string>();
            var saveDirs = EffectiveSaveDirs(detected);
            SetDetected(seen, games, shown, DetectedFolder(detected), shown is null || SaveFolders.HasCaption(saveDirs));
            Update(shown, saveDirs);
        }
        finally
        {
            foreach (var game in games)
                game.Process.Dispose();
        }
    }

    void Update(Game? shown, IReadOnlyCollection<string> saveDirs)
    {
        if (!_config.ShowPresence)
        {
            _port.Restore();
            Clear();
            SetDetailsPending(false);
            SetStatus("Hidden from Discord");
            return;
        }

        if (shown is not { } found)
        {
            if (_sessionPid != 0)
                _log.Write("Game closed; clearing presence.");
            _port.Restore();
            Clear();
            _sessionPid = 0;
            _saveDirsKey = null;
            SetDetailsPending(false);
            SetStatus("Waiting for the game");
            return;
        }

        var game = found.Process;
        _port.Apply(game, _config.SilencePortStatus && found.Port is { OwnPresence: true } ? found.Port : null);
        if (game.Id != _sessionPid)
        {
            _sessionPid = game.Id;
            _ageImage = null;
            _emulatorStart = StartTime(game);
            _sessionStart = _firstTick ? _emulatorStart : DateTime.Now;
            _log.Write($"Game detected in {game.ProcessName}.exe (pid {game.Id}).");
        }

        UseSaveDirs(saveDirs);
        var save = CurrentLocation(saveDirs);
        SetDetailsPending(save is null);

        if (_ipc is not null && !_ipc.Alive)
        {
            _log.Write("Lost connection to Discord.");
            Clear();
        }

        if (_ipc is null)
        {
            _ipc = DiscordIpc.Connect(_config.ApplicationId);
            _sent = null;
            if (_ipc is null)
            {
                SetStatus("Waiting for Discord");
                return;
            }
        }

        var location = _config.ShowLocation && save is not null ? _activity.PlaceName(save) : null;
        if (location != _lastLocation)
        {
            _log.Write($"Location: {location ?? "(none yet)"}");
            _lastLocation = location;
        }

        var action = _config.ShowDefeats ? CurrentAction(saveDirs, save) : null;
        if (action != _lastAction)
        {
            _log.Write($"Action: {action ?? "(none)"}");
            _lastAction = action;
        }

        var activity = _activity.Build(action, location, save, _ageImage, _sessionStart);
        var key = $"{game.Id}|{activity.ToJsonString()}";
        if (key != _sent && DateTime.UtcNow - _lastSend < SendGap)
        {
            _sendDue = _lastSend + SendGap;
        }
        else if (key != _sent)
        {
            try
            {
                _ipc.SetActivity(game.Id, activity);
                _sent = key;
                _lastSend = DateTime.UtcNow;
                _log.Write("Presence updated.");
            }
            catch (Exception e)
            {
                _log.Write($"SetActivity failed: {e.Message}");
                Clear();
                return;
            }
        }

        SetStatus(location is null ? "Showing presence" : $"Showing: {location}");
    }

    void SetDetected(List<string> seen, List<Game> games, Game? shown, string? saveFolder, bool saveFound)
    {
        var names = seen.Distinct().ToList();
        var text = names.Count switch
        {
            0 => "No game open",
            1 => $"I see {names[0]}",
            _ => $"I see {string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}"
        };
        var targets = games.Select(g => g.Name).Distinct().ToArray();
        var target = shown?.Name;
        var ports = games.Where(g => g.Port is { OwnPresence: true }).Select(g => g.Name).Distinct().ToArray();
        var detection = new Detection(text, targets, target, ports, saveFolder, saveFound);
        var key = $"{text}|{string.Join(",", targets)}|{target}|{string.Join(",", ports)}|{saveFolder}|{saveFound}";
        if (key == _detectedKey)
            return;
        _detectedKey = key;
        DetectedChanged?.Invoke(detection);
    }

    IReadOnlyCollection<string> SaveDirs(Game game) =>
        (game.Port is { } port && game.ExeDir is not null
            ? port.SaveDirs(Launch(game.Process, port, game.ExeDir))
            : SaveFolders.EmulatorSaveDirs(game.ExeDir, _config.TitleId)).ToList();

    IReadOnlyCollection<string> EffectiveSaveDirs(IReadOnlyCollection<string> detected)
    {
        var manual = _config.SaveFolder;
        if (manual.Length == 0)
            return detected;
        return Directory.Exists(manual) ? new[] { manual } : Array.Empty<string>();
    }

    static string? DetectedFolder(IReadOnlyCollection<string> roots) =>
        roots.Count <= 1
            ? roots.FirstOrDefault()
            : roots.OrderByDescending(root => SaveFolders.Captions(new[] { root }).FirstOrDefault()?.LastWriteTimeUtc ?? DateTime.MinValue).First();

    void UseSaveDirs(IReadOnlyCollection<string> saveDirs)
    {
        var key = string.Join("|", saveDirs);
        if (key == _saveDirsKey)
            return;
        _saveDirsKey = key;
        _captionStamp = null;
        _location = null;
        _optionsStamp = null;
        _bosses = null;
        _ageImage = null;
        _log.Write(saveDirs.Count > 0 ? $"Reading saves from {string.Join(", ", saveDirs)}."
            : _config.SaveFolder.Length > 0 ? $"The save folder {_config.SaveFolder} does not exist."
            : "No save folder found yet.");
    }

    PortLaunch Launch(Process process, Port port, string exeDir)
    {
        if (_launches.TryGetValue(process.Id, out var launch) && launch.ExeDir == exeDir)
            return launch;
        var arguments = PortNative.CommandLine(process.Id);
        var environment = PortNative.Environment(process.Id);
        if (arguments is null || environment is null)
            _log.Write($"Could not read {port.Name}'s {(arguments is null ? "command line" : "environment")}; looking for saves in its usual places.");
        launch = new PortLaunch(exeDir, arguments, environment);
        _launches[process.Id] = launch;
        return launch;
    }

    Location? CurrentLocation(IReadOnlyCollection<string> saveDirs)
    {
        var caption = SaveFolders.NewestCaption(saveDirs, _emulatorStart);
        if (caption is null)
        {
            _captionStamp = null;
            _location = null;
            return null;
        }

        var stamp = (caption.FullName, caption.LastWriteTimeUtc);
        if (stamp == _captionStamp)
            return _location;
        var read = SaveReader.Read(caption);
        if (read is null)
            return _location;
        _captionStamp = stamp;
        _location = read;
        _ageImage = _activity.AgeImage(read) ?? _ageImage ?? LastKnownAge(saveDirs);
        return read;
    }

    string? CurrentAction(IReadOnlyCollection<string> saveDirs, Location? save)
    {
        var options = SaveFolders.NewestOptions(saveDirs);
        if (options is not null)
        {
            var stamp = (options.FullName, options.LastWriteTimeUtc);
            if (stamp != _optionsStamp && Defeats.Read(options) is { } bosses)
            {
                _optionsStamp = stamp;
                _bosses = bosses;
            }
        }

        if (_bosses is null)
            return null;
        var defeat = save?.SubArea == DreamWorld
            ? _bosses.Dream is { } dream && dream.At >= _bosses.DreamEntered ? dream : null
            : _bosses.Waking is { } waking && DateTime.Now - waking.At <= DefeatShown ? waking : null;
        return defeat is null || defeat.At < _emulatorStart ? null : $"{defeat.Boss} defeated!";
    }

    string? LastKnownAge(IReadOnlyCollection<string> saveDirs) =>
        SaveFolders.Captions(saveDirs)
            .Select(SaveReader.Read)
            .Select(location => location is null ? null : _activity.AgeImage(location))
            .FirstOrDefault(key => key is not null);

    static DateTime StartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    void Clear()
    {
        _ipc?.Dispose();
        _ipc = null;
        _sent = null;
        _lastLocation = null;
    }

    void SetDetailsPending(bool pending)
    {
        if (pending == _detailsPending)
            return;
        _detailsPending = pending;
        DetailsPendingChanged?.Invoke(pending);
    }

    void SetStatus(string status)
    {
        if (status == _status)
            return;
        _status = status;
        StatusChanged?.Invoke(status);
    }
}

public sealed record Detection(string Text, IReadOnlyList<string> Targets, string? Target, IReadOnlyList<string> Ports, string? SaveFolder, bool SaveFound);
