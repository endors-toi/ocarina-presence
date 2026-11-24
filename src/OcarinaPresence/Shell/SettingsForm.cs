namespace OcarinaPresence;

public sealed class SettingsForm : Form
{
    const string SaveFolderTip = """
        The folder that holds the game's saves. It looks like this:

          save
            5a3c9e1f0b7d24680000000000000001
              account.sav
              option.sav
              slot_00
                caption.sav
                progress.sav
              slot_01
                ...

        Hookshot and Triforce keep it as user\save in the game folder,
        or inside the folder given to --user. Emulators keep it under
        their own data folder. Pick the save folder itself, not the
        user folder above it.

        Ocarina Presence only reads caption.sav, progress.sav and
        option.sav. It never changes them.
        """;

    readonly Config _config;
    readonly ToggleSwitch _showPresence;
    readonly CheckBox _startWithWindows;
    readonly CheckBox _silencePort;
    readonly Label _status;
    readonly Label _detected;
    readonly Label _detailsPending;
    readonly FlowLayoutPanel _targets;
    readonly TextBox _saveFolder;
    readonly CheckBox _detectSaveFolder;
    readonly Label _noSave;
    readonly ToolTip _saveTip;
    readonly Font _bold;
    string _targetsKey = "";
    string _shownFolder = "";
    string? _detectedFolder;
    bool _updating;

    public event Action? SettingsChanged;
    public event Action<bool>? StartWithWindowsChanged;

    public SettingsForm(Config config, string status, Detection detected, bool detailsPending, Action openLog)
    {
        _config = config;
        Text = "Ocarina Presence";
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = SystemFonts.MessageBoxFont;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        _bold = new Font(Font, FontStyle.Bold);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill
        };

        _detected = new Label { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 12, 0) };
        _status = new Label { AutoSize = true, Anchor = AnchorStyles.Right, Font = _bold, Text = status, Margin = new Padding(3, 0, 6, 0) };
        _showPresence = new ToggleSwitch { Checked = config.ShowPresence, AccessibleName = "Show my status on Discord", Anchor = AnchorStyles.Right, Margin = new Padding(0) };
        _showPresence.CheckedChanged += (_, _) =>
        {
            PaintStatus();
            if (config.ShowPresence == _showPresence.Checked)
                return;
            config.ShowPresence = _showPresence.Checked;
            SettingsChanged?.Invoke();
        };
        PaintStatus();

        var toggle = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
        toggle.Controls.Add(_status, 0, 0);
        toggle.Controls.Add(_showPresence, 1, 0);

        _targets = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(3, 6, 3, 0), Visible = false };

        var header = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(_detected, 0, 0);
        header.Controls.Add(toggle, 1, 0);
        header.Controls.Add(_targets, 0, 1);
        header.SetColumnSpan(_targets, 2);

        var customization = Heading("Presence Customization", new Padding(3, 0, 3, 3));
        var customizationOptions = new[]
        {
            Option("Display \"(boss name) defeated!\" when you defeat a boss", config.ShowDefeats, value => config.ShowDefeats = value),
            Option("Show Link as a child or adult", config.ShowAge, value => config.ShowAge = value)
        };

        _detailsPending = new Label { Text = "Details will start showing on next autosave", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(0, 12, 3, 3) };
        var details = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        details.Controls.Add(Heading("Presence Details", new Padding(3, 12, 3, 3)));
        details.Controls.Add(_detailsPending);
        var detailOptions = new[]
        {
            Option("Location / Area you're in", config.ShowLocation, value => config.ShowLocation = value),
            Option("Total play time", config.ShowPlayTime, value => config.ShowPlayTime = value),
            Option("Collected Skulltulas", config.ShowSkulltulas, value => config.ShowSkulltulas = value),
            Option("Collected heart pieces", config.ShowHeartPieces, value => config.ShowHeartPieces = value),
            Option("Current and Max hearts", config.ShowHearts, value => config.ShowHearts = value),
            Option("Current magic", config.ShowMagic, value => config.ShowMagic = value)
        };

        _startWithWindows = new CheckBox { Text = "Start Ocarina Presence with Windows", AutoSize = true, Checked = config.StartWithWindows };
        _startWithWindows.CheckedChanged += (_, _) => StartWithWindowsChanged?.Invoke(_startWithWindows.Checked);
        var checkForUpdates = Option("Check for updates automatically", config.CheckForUpdates, value => config.CheckForUpdates = value);
        _silencePort = Option("Hide Hookshot's own presence", config.SilencePortStatus, value => config.SilencePortStatus = value);
        var general = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 12, 0, 12) };
        general.Controls.Add(_startWithWindows);
        general.Controls.Add(checkForUpdates);
        general.Controls.Add(_silencePort);

        var saveHeading = Heading("Save folder", new Padding(3, 0, 3, 3));
        _saveFolder = new TextBox { Width = 360, PlaceholderText = "Found when the game is open", Anchor = AnchorStyles.Left };
        _saveFolder.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.SuppressKeyPress = true;
            CommitSaveFolder(_saveFolder.Text);
        };
        _saveFolder.Leave += (_, _) => CommitSaveFolder(_saveFolder.Text);
        var browse = new Button { Text = "Browse…", AutoSize = true, Anchor = AnchorStyles.Left };
        browse.Click += (_, _) => BrowseSaveFolder();
        var saveRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        saveRow.Controls.Add(_saveFolder);
        saveRow.Controls.Add(browse);
        _detectSaveFolder = new CheckBox { Text = "Detect automatically", AutoSize = true, Anchor = AnchorStyles.Left, Checked = config.SaveFolder.Length == 0 };
        _detectSaveFolder.CheckedChanged += (_, _) => SetDetectSaveFolder(_detectSaveFolder.Checked);
        _noSave = new Label { Text = "No save files found in this folder", AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Visible = false };
        var saveOptions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        saveOptions.Controls.Add(_detectSaveFolder);
        saveOptions.Controls.Add(_noSave);
        var save = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 0, 0, 12) };
        save.Controls.Add(saveHeading);
        save.Controls.Add(saveRow);
        save.Controls.Add(saveOptions);
        _saveTip = new ToolTip { AutoPopDelay = 30000 };
        foreach (var control in new Control[] { saveHeading, _saveFolder, browse })
            _saveTip.SetToolTip(control, SaveFolderTip);
        if (config.SaveFolder.Length > 0)
            ShowSaveFolder(config.SaveFolder);

        var log = new LinkLabel { Text = "Open log", AutoSize = true, Anchor = AnchorStyles.Left };
        log.LinkClicked += (_, _) => openLog();
        var close = new Button { Text = "Close", AutoSize = true, Anchor = AnchorStyles.Right, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();
        CancelButton = close;
        var footer = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(log, 0, 0);
        footer.Controls.Add(close, 1, 0);

        layout.Controls.Add(header);
        layout.Controls.Add(customization);
        foreach (var option in customizationOptions)
            layout.Controls.Add(option);
        layout.Controls.Add(details);
        foreach (var option in detailOptions)
            layout.Controls.Add(option);
        layout.Controls.Add(general);
        layout.Controls.Add(save);
        layout.Controls.Add(footer);
        Controls.Add(layout);

        ShowDetected(detected);
        ShowDetailsPending(detailsPending);
    }

    Label Heading(string text, Padding margin) => new() { Text = text, AutoSize = true, Font = _bold, Margin = margin };

    CheckBox Option(string text, bool value, Action<bool> set)
    {
        var option = new CheckBox { Text = text, AutoSize = true, Checked = value };
        option.CheckedChanged += (_, _) =>
        {
            set(option.Checked);
            SettingsChanged?.Invoke();
        };
        return option;
    }

    void PaintStatus() => _status.ForeColor = _showPresence.Checked ? ToggleSwitch.OnColor : ToggleSwitch.OffColor;

    public void ShowStatus(string status) => _status.Text = status;

    public void ShowDetailsPending(bool pending) => _detailsPending.Visible = pending;

    public void ShowDetected(Detection detected)
    {
        var choosing = detected.Targets.Count > 1;
        _detected.Text = detected.Text;
        _detected.Font = choosing ? _bold : Font;
        _detected.ForeColor = choosing ? SystemColors.ControlText : SystemColors.GrayText;
        _silencePort.Visible = detected.Ports.Count > 0;
        if (detected.Ports.Count > 0)
            _silencePort.Text = $"Hide {string.Join(" and ", detected.Ports.Select(name => name + "'s"))} own presence";

        _detectedFolder = detected.SaveFolder;
        _noSave.Visible = detected.Target is not null && !detected.SaveFound;
        if (_config.SaveFolder.Length == 0 && !(_saveFolder.Focused && _saveFolder.Text.Trim() != _shownFolder))
            ShowSaveFolder(detected.SaveFolder ?? "");

        var key = string.Join("|", detected.Targets);
        if (key != _targetsKey)
        {
            _targetsKey = key;
            _targets.SuspendLayout();
            foreach (Control control in _targets.Controls)
                control.Dispose();
            _targets.Controls.Clear();
            if (choosing)
                foreach (var name in detected.Targets)
                    _targets.Controls.Add(TargetButton(name));
            _targets.ResumeLayout();
        }
        _targets.Visible = choosing;

        _updating = true;
        foreach (RadioButton button in _targets.Controls)
            button.Checked = (string)button.Tag! == detected.Target;
        _updating = false;
    }

    void ShowSaveFolder(string folder)
    {
        _shownFolder = folder;
        _saveFolder.Text = folder;
    }

    void CommitSaveFolder(string text)
    {
        text = text.Trim();
        if (_updating || text == _shownFolder)
            return;
        if (text.Length == 0)
        {
            UseDetectedFolder();
            return;
        }
        _config.SaveFolder = text;
        _updating = true;
        _detectSaveFolder.Checked = false;
        _updating = false;
        ShowSaveFolder(text);
        SettingsChanged?.Invoke();
    }

    void UseDetectedFolder()
    {
        var changed = _config.SaveFolder.Length > 0;
        _config.SaveFolder = "";
        _updating = true;
        _detectSaveFolder.Checked = true;
        _updating = false;
        ShowSaveFolder(_detectedFolder ?? "");
        if (changed)
            SettingsChanged?.Invoke();
    }

    void SetDetectSaveFolder(bool detect)
    {
        if (_updating)
            return;
        if (detect)
        {
            UseDetectedFolder();
            return;
        }
        var folder = _saveFolder.Text.Trim();
        if (folder.Length == 0)
        {
            _updating = true;
            _detectSaveFolder.Checked = true;
            _updating = false;
            _saveFolder.Focus();
            return;
        }
        _config.SaveFolder = folder;
        ShowSaveFolder(folder);
        SettingsChanged?.Invoke();
    }

    void BrowseSaveFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Pick the save folder", UseDescriptionForTitle = true };
        var current = _saveFolder.Text.Trim();
        if (Directory.Exists(current))
            dialog.SelectedPath = current;
        if (dialog.ShowDialog(this) == DialogResult.OK)
            CommitSaveFolder(dialog.SelectedPath);
    }

    RadioButton TargetButton(string name)
    {
        var button = new RadioButton
        {
            Text = name,
            Tag = name,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(0, 0, 6, 0),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = ToggleSwitch.OffColor;
        button.FlatAppearance.CheckedBackColor = ToggleSwitch.OnColor;
        button.CheckedChanged += (_, _) =>
        {
            button.ForeColor = button.Checked ? Color.White : SystemColors.ControlText;
            button.FlatAppearance.BorderColor = button.Checked ? ToggleSwitch.OnColor : ToggleSwitch.OffColor;
            if (_updating || !button.Checked)
                return;
            _config.Target = name;
            SettingsChanged?.Invoke();
        };
        return button;
    }

    public void SetShowPresence(bool enabled)
    {
        if (_showPresence.Checked != enabled)
            _showPresence.Checked = enabled;
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (_startWithWindows.Checked != enabled)
            _startWithWindows.Checked = enabled;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bold.Dispose();
            _saveTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
