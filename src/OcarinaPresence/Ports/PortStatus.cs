using System.Diagnostics;

namespace OcarinaPresence;

public sealed class PortStatus
{
    readonly Action<string> _log;
    readonly object _lock = new();
    (string path, long size, DateTime written)? _scanned;
    HashSet<uint> _functions = new();
    int _pid;
    int _missing;
    IntPtr _thread;
    string _name = "";

    public PortStatus(Action<string> log) => _log = log;

    public void Apply(Process process, Port? port)
    {
        if (port is null)
            Restore();
        else
            Silence(process, port);
    }

    void Silence(Process process, Port port)
    {
        lock (_lock)
        {
            if (_thread != IntPtr.Zero)
            {
                if (_pid == process.Id && PortNative.Alive(_thread))
                    return;
                PortNative.Resume(_thread);
                _thread = IntPtr.Zero;
            }

            var candidate = DiscordThread(process, port, out var searched);
            if (candidate is null)
            {
                if (searched && _missing != process.Id)
                {
                    _missing = process.Id;
                    _log($"Could not find {port.Name}'s Discord thread; its own status stays on.");
                }
                return;
            }
            if (candidate.ThreadState != System.Diagnostics.ThreadState.Wait || candidate.WaitReason != ThreadWaitReason.ExecutionDelay)
                return;
            var thread = PortNative.SuspendSleeping(candidate.Id);
            if (thread == IntPtr.Zero)
                return;
            if (PortNative.CutDiscordPipes(process.Id, port.Name, _log) < 0)
            {
                PortNative.Resume(thread);
                return;
            }
            _pid = process.Id;
            _thread = thread;
            _name = port.Name;
            _log($"Silenced {port.Name}'s own Discord status.");
        }
    }

    ProcessThread? DiscordThread(Process process, Port port, out bool searched)
    {
        searched = true;
        if (port.DiscordThread is { } name)
            return process.Threads.Cast<ProcessThread>().FirstOrDefault(t => PortNative.Description(t.Id) == name);

        searched = false;
        ProcessModule? exe;
        try
        {
            exe = process.MainModule;
        }
        catch
        {
            return null;
        }
        if (exe is null || !Scan(exe.FileName, port.Name))
            return null;

        searched = true;
        var module = exe.BaseAddress.ToInt64();
        foreach (ProcessThread candidate in process.Threads)
        {
            var start = PortNative.StartAddress(candidate.Id) - module;
            if (start >= 0 && start <= uint.MaxValue && _functions.Contains((uint)start))
                return candidate;
        }
        return null;
    }

    bool Scan(string path, string name)
    {
        var info = new FileInfo(path);
        var stamp = (path, info.Length, info.LastWriteTimeUtc);
        if (stamp != _scanned)
        {
            _scanned = stamp;
            try
            {
                _functions = PortScan.DiscordFunctions(path);
            }
            catch (Exception e)
            {
                _functions = new();
                _log($"Could not read {path}: {e.Message}");
            }
            if (_functions.Count == 0)
                _log($"Could not find {name}'s Discord code; its own status stays on.");
        }
        return _functions.Count > 0;
    }

    public void Restore()
    {
        lock (_lock)
        {
            if (_thread == IntPtr.Zero)
                return;
            if (PortNative.Resume(_thread))
                _log($"Restored {_name}'s own Discord status.");
            _thread = IntPtr.Zero;
            _pid = 0;
        }
    }
}
