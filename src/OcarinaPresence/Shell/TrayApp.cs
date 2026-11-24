using System.Diagnostics;
using Microsoft.Win32;

namespace OcarinaPresence;

public sealed class TrayApp : ApplicationContext
{
    readonly Config _config;
    readonly string _configPath;
    readonly Logger _log;
    readonly PresenceService _service;
    readonly Thread _worker;
    readonly NotifyIcon _icon;
    readonly ToolStripMenuItem _statusItem;
    readonly ToolStripMenuItem _enablePresence;
    readonly ToolStripMenuItem _startWithWindows;
    readonly SynchronizationContext _ui;
    readonly Updater _updater = new();
    readonly ToolStripMenuItem _updateItem;
    readonly System.Windows.Forms.Timer _updateTimer;
    UpdateInfo? _update;
    bool _installing;
    SettingsForm? _settings;
    string _status = "Starting…";
    Detection _detected = new("", Array.Empty<string>(), null, Array.Empty<string>(), null, true);
    bool _detailsPending;

    public TrayApp(Config config, string configPath, Logger log, bool firstRun)
    {
        _config = config;
        _configPath = configPath;
        _log = log;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _statusItem = new ToolStripMenuItem(_status) { Enabled = false };
        _enablePresence = new ToolStripMenuItem("Enable Presence") { CheckOnClick = true, Checked = config.ShowPresence };
        _enablePresence.CheckedChanged += (_, _) => SetShowPresence(_enablePresence.Checked);
        _startWithWindows = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = config.StartWithWindows };
        _startWithWindows.CheckedChanged += (_, _) => SetStartWithWindows(_startWithWindows.Checked);

        _updateItem = new ToolStripMenuItem("Check for updates");
        _updateItem.Click += (_, _) => _ = UpdateClicked();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enablePresence);
        menu.Items.Add(_startWithWindows);
        menu.Items.Add("Settings…", null, (_, _) => OpenSettings());
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Quit());

        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application,
            Text = "Ocarina Presence",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                OpenSettings();
        };

        ApplyAutostart();

        _service = new PresenceService(config, log);
        _service.StatusChanged += status => _ui.Post(_ => ShowStatus(status), null);
        _service.DetectedChanged += detected => _ui.Post(_ => ShowDetected(detected), null);
        _service.DetailsPendingChanged += pending => _ui.Post(_ => ShowDetailsPending(pending), null);
        SystemEvents.SessionEnding += OnSessionEnding;
        _worker = new Thread(_service.Run) { IsBackground = true, Name = "Presence" };
        _worker.Start();
        _updateTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _updateTimer.Tick += (_, _) =>
        {
            _updateTimer.Interval = (int)TimeSpan.FromHours(6).TotalMilliseconds;
            if (_config.CheckForUpdates)
                _ = CheckForUpdate(false);
        };
        _updateTimer.Start();
        if (firstRun)
            _icon.ShowBalloonTip(8000, "Ocarina Presence is running",
                "It lives here in the tray and updates your Discord status whenever the game is open.",
                ToolTipIcon.Info);
    }

    void ShowStatus(string status)
    {
        _status = status;
        _statusItem.Text = status;
        var text = $"Ocarina Presence · {status}";
        _icon.Text = text.Length > 63 ? text[..63] : text;
        _settings?.ShowStatus(status);
    }

    void ShowDetected(Detection detected)
    {
        _detected = detected;
        _settings?.ShowDetected(detected);
    }

    void ShowDetailsPending(bool pending)
    {
        _detailsPending = pending;
        _settings?.ShowDetailsPending(pending);
    }

    void OpenSettings()
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }

        _settings = new SettingsForm(_config, _status, _detected, _detailsPending, () => Open(_log.Path));
        _settings.SettingsChanged += () =>
        {
            _enablePresence.Checked = _config.ShowPresence;
            SaveConfig();
            _service.Refresh();
        };
        _settings.StartWithWindowsChanged += enabled => _startWithWindows.Checked = enabled;
        _settings.FormClosed += (_, _) => _settings = null;
        _settings.Show();
        _settings.Activate();
    }

    void SetShowPresence(bool enabled)
    {
        if (_config.ShowPresence == enabled)
            return;
        _config.ShowPresence = enabled;
        _settings?.SetShowPresence(enabled);
        SaveConfig();
        _service.Refresh();
    }

    void SetStartWithWindows(bool enabled)
    {
        _config.StartWithWindows = enabled;
        _settings?.SetStartWithWindows(enabled);
        SaveConfig();
        ApplyAutostart();
    }

    void SaveConfig()
    {
        try
        {
            _config.Save(_configPath);
        }
        catch (Exception e)
        {
            _log.Write($"Could not save settings: {e.Message}");
        }
    }

    void ApplyAutostart()
    {
        try
        {
            Autostart.Apply(_config.StartWithWindows);
        }
        catch (Exception e)
        {
            _log.Write($"Could not update Start with Windows: {e.Message}");
        }
    }

    static void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    Task UpdateClicked() => _update is null ? CheckForUpdate(true) : InstallUpdate();

    async Task CheckForUpdate(bool manual)
    {
        if (_installing || (_update is not null && !manual))
            return;
        try
        {
            var update = await _updater.CheckAsync();
            if (update is null)
            {
                if (manual)
                    _icon.ShowBalloonTip(5000, "Ocarina Presence", "You're up to date.", ToolTipIcon.Info);
                return;
            }
            _update = update;
            _updateItem.Text = $"Install update {update.Tag}";
            _log.Write($"Update {update.Tag} is available (running {Updater.Current}).");
            _icon.ShowBalloonTip(8000, "Ocarina Presence update available",
                $"{update.Tag} is ready. Choose \"Install update\" in the tray menu.", ToolTipIcon.Info);
        }
        catch (Exception e)
        {
            _log.Write($"Update check failed: {e.Message}");
            if (manual)
                _icon.ShowBalloonTip(5000, "Ocarina Presence", "Couldn't check for updates.", ToolTipIcon.Warning);
        }
    }

    async Task InstallUpdate()
    {
        if (_installing || _update is null)
            return;
        _installing = true;
        _updateItem.Enabled = false;
        _updateItem.Text = "Downloading update…";
        try
        {
            _log.Write($"Installing update {_update.Tag}.");
            await _updater.InstallAsync(_update);
            Quit();
        }
        catch (Exception e)
        {
            _log.Write($"Update failed: {e.Message}");
            _installing = false;
            _updateItem.Enabled = true;
            _updateItem.Text = $"Install update {_update.Tag}";
            _icon.ShowBalloonTip(5000, "Ocarina Presence", "The update failed. Nothing was changed.", ToolTipIcon.Warning);
        }
    }

    void OnSessionEnding(object? sender, SessionEndingEventArgs e) => _service.RestorePort();

    void Quit()
    {
        SystemEvents.SessionEnding -= OnSessionEnding;
        _updateTimer.Stop();
        _settings?.Close();
        _service.Stop();
        _worker.Join(TimeSpan.FromSeconds(3));
        _icon.Visible = false;
        _icon.Dispose();
        ExitThread();
    }
}
