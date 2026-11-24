namespace OcarinaPresence;

public sealed class Logger
{
    readonly string _path;
    readonly object _lock = new();

    public Logger(string path)
    {
        _path = path;
        try
        {
            File.WriteAllText(path, "");
        }
        catch
        {
        }
    }

    public string Path => _path;

    public void Write(string message)
    {
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }
    }
}
