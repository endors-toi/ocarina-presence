namespace OcarinaPresence;

static class Program
{
    [STAThread]
    static void Main()
    {
        Updater.WaitForPreviousInstance(Environment.GetCommandLineArgs());
        using var mutex = new Mutex(true, @"Local\OcarinaPresence", out var owned);
        if (!owned)
            return;
        Updater.CleanUp();

        ApplicationConfiguration.Initialize();

        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "OcarinaPresence.json");
        Config config;
        bool firstRun;
        try
        {
            (config, firstRun) = Config.Load(configPath);
        }
        catch (Exception e)
        {
            MessageBox.Show(
                $"Ocarina Presence couldn't read or create its settings file:\n\n{e.Message}\n\nUnpack it to a folder you can write to (not Program Files) and try again.",
                "Ocarina Presence", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(config.ApplicationId))
        {
            MessageBox.Show("ApplicationId is empty in OcarinaPresence.json.", "Ocarina Presence",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var log = new Logger(Path.Combine(baseDir, "OcarinaPresence.log"));
        Application.Run(new TrayApp(config, configPath, log, firstRun));
    }
}
