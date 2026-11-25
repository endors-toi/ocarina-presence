using System.Runtime.InteropServices;

namespace OcarinaPresence;

static class PortNative
{
    const uint ThreadSuspendResume = 0x2, ThreadGetContext = 0x8, ThreadQuery = 0x40, ThreadQueryLimited = 0x800;
    const uint ProcessVmRead = 0x10, ProcessDupHandle = 0x40, ProcessQuery = 0x400, ProcessQueryLimited = 0x1000;
    const uint DuplicateCloseSource = 0x1, DuplicateSameAccess = 0x2;
    const int StillActive = 259;
    const int ProcessBasicInformation = 0;
    const int ProcessBasicInformationSize = 48;
    const int PebOffset = 8;
    const int PebParametersOffset = 0x20;
    const int ParametersEnvironmentOffset = 0x80;
    const int ParametersEnvironmentSizeOffset = 0x3F0;
    const int EnvironmentLimit = 1 << 20;
    const int ProcessHandleInformation = 51;
    const int ProcessCommandLineInformation = 60;
    const int ThreadWin32StartAddress = 9;
    const int ObjectNameInformation = 1;
    const int ObjectTypeInformation = 2;
    const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    const int HandleEntriesOffset = 16;
    const int HandleEntrySize = 40;
    const int ContextBufferSize = 1232;
    const int ContextAlignment = 16;
    const int ContextFlagsOffset = 0x30;
    const int ContextRipOffset = 0xF8;
    const int ContextControl = 0x100001;
    const uint PipeAccessDuplex = 3;
    const uint GenericReadWrite = 0xC0000000;
    const uint OpenExisting = 3;
    const long DelayStubLength = 0x20;
    const string PipePrefix = @"\Device\NamedPipe\discord-ipc-";
    static readonly IntPtr Invalid = new(-1);
    static readonly long DelayStub = GetProcAddress(GetModuleHandle("ntdll"), "NtDelayExecution").ToInt64();

    [DllImport("ntdll")] static extern int NtQueryInformationThread(IntPtr thread, int cls, out IntPtr info, int length, IntPtr returned);
    [DllImport("ntdll")] static extern int NtQueryInformationProcess(IntPtr process, int cls, IntPtr buffer, int length, out int returned);
    [DllImport("ntdll")] static extern int NtQueryObject(IntPtr handle, int cls, IntPtr buffer, int length, out int returned);
    [DllImport("kernel32", SetLastError = true)] static extern IntPtr OpenThread(uint access, bool inherit, int id);
    [DllImport("kernel32", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, IntPtr size, out IntPtr read);
    [DllImport("kernel32", SetLastError = true)] static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess, out IntPtr target, uint access, bool inherit, uint options);
    [DllImport("kernel32")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32")] static extern int SuspendThread(IntPtr thread);
    [DllImport("kernel32")] static extern int ResumeThread(IntPtr thread);
    [DllImport("kernel32")] static extern bool GetThreadContext(IntPtr thread, IntPtr context);
    [DllImport("kernel32")] static extern bool GetExitCodeThread(IntPtr thread, out int code);
    [DllImport("kernel32")] static extern int GetThreadDescription(IntPtr thread, out IntPtr description);
    [DllImport("kernel32")] static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32", CharSet = CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateNamedPipe(string name, uint openMode, uint pipeMode, uint instances, uint outSize, uint inSize, uint timeout, IntPtr security);
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("shell32", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CommandLineToArgvW(string commandLine, out int count);

    public static string[]? CommandLine(int processId)
    {
        var process = OpenProcess(ProcessQueryLimited, false, processId);
        if (process == IntPtr.Zero)
            return null;
        try
        {
            for (var size = 1 << 12; size <= 1 << 18; size *= 2)
            {
                var buffer = Marshal.AllocHGlobal(size);
                try
                {
                    var status = NtQueryInformationProcess(process, ProcessCommandLineInformation, buffer, size, out _);
                    if (status == StatusInfoLengthMismatch)
                        continue;
                    if (status != 0)
                        return null;
                    var length = (ushort)Marshal.ReadInt16(buffer);
                    var text = Marshal.ReadIntPtr(buffer, 8);
                    return text == IntPtr.Zero || length == 0 ? null : Arguments(Marshal.PtrToStringUni(text, length / 2));
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            return null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static Dictionary<string, string>? Environment(int processId)
    {
        var process = OpenProcess(ProcessQueryLimited | ProcessVmRead, false, processId);
        if (process == IntPtr.Zero)
            return null;
        try
        {
            var info = Marshal.AllocHGlobal(ProcessBasicInformationSize);
            IntPtr peb;
            try
            {
                if (NtQueryInformationProcess(process, ProcessBasicInformation, info, ProcessBasicInformationSize, out _) != 0)
                    return null;
                peb = Marshal.ReadIntPtr(info, PebOffset);
            }
            finally
            {
                Marshal.FreeHGlobal(info);
            }
            if (ReadPointer(process, peb + PebParametersOffset) is not { } parameters
                || ReadPointer(process, parameters + ParametersEnvironmentOffset) is not { } block
                || ReadPointer(process, parameters + ParametersEnvironmentSizeOffset) is not { } size
                || size.ToInt64() <= 0 || size.ToInt64() > EnvironmentLimit)
                return null;
            var bytes = new byte[size.ToInt64()];
            if (!ReadProcessMemory(process, block, bytes, size, out _))
                return null;
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in System.Text.Encoding.Unicode.GetString(bytes).Split('\0'))
            {
                var equals = entry.Length > 1 ? entry.IndexOf('=', 1) : -1;
                if (equals > 0)
                    variables.TryAdd(entry[..equals], entry[(equals + 1)..]);
            }
            return variables;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    static IntPtr? ReadPointer(IntPtr process, IntPtr address)
    {
        var buffer = new byte[IntPtr.Size];
        return address != IntPtr.Zero && ReadProcessMemory(process, address, buffer, IntPtr.Size, out _)
            ? new IntPtr(BitConverter.ToInt64(buffer))
            : null;
    }

    static string[]? Arguments(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var count);
        if (argv == IntPtr.Zero)
            return null;
        try
        {
            var arguments = new string[count];
            for (var i = 0; i < count; i++)
                arguments[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size)) ?? "";
            return arguments;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    public static long StartAddress(int threadId)
    {
        var thread = OpenThread(ThreadQuery, false, threadId);
        if (thread == IntPtr.Zero)
            return -1;
        try
        {
            return NtQueryInformationThread(thread, ThreadWin32StartAddress, out var start, IntPtr.Size, IntPtr.Zero) == 0 ? start.ToInt64() : -1;
        }
        finally
        {
            CloseHandle(thread);
        }
    }

    public static string? Description(int threadId)
    {
        var thread = OpenThread(ThreadQueryLimited, false, threadId);
        if (thread == IntPtr.Zero)
            return null;
        try
        {
            if (GetThreadDescription(thread, out var text) < 0)
                return null;
            try
            {
                return Marshal.PtrToStringUni(text);
            }
            finally
            {
                LocalFree(text);
            }
        }
        finally
        {
            CloseHandle(thread);
        }
    }

    public static IntPtr SuspendSleeping(int threadId)
    {
        var thread = OpenThread(ThreadSuspendResume | ThreadGetContext | ThreadQueryLimited, false, threadId);
        if (thread == IntPtr.Zero)
            return IntPtr.Zero;
        if (SuspendThread(thread) < 0)
        {
            CloseHandle(thread);
            return IntPtr.Zero;
        }
        if (Sleeping(thread))
            return thread;
        ResumeThread(thread);
        CloseHandle(thread);
        return IntPtr.Zero;
    }

    static bool Sleeping(IntPtr thread)
    {
        var raw = Marshal.AllocHGlobal(ContextBufferSize + ContextAlignment);
        try
        {
            var context = new IntPtr((raw.ToInt64() + ContextAlignment - 1) & ~(ContextAlignment - 1L));
            Marshal.WriteInt32(context, ContextFlagsOffset, ContextControl);
            if (!GetThreadContext(thread, context))
                return false;
            var rip = Marshal.ReadInt64(context, ContextRipOffset);
            return rip >= DelayStub && rip < DelayStub + DelayStubLength;
        }
        finally
        {
            Marshal.FreeHGlobal(raw);
        }
    }

    public static bool Alive(IntPtr thread) => GetExitCodeThread(thread, out var code) && code == StillActive;

    public static bool Resume(IntPtr thread)
    {
        var resumed = Alive(thread) && ResumeThread(thread) >= 0;
        CloseHandle(thread);
        return resumed;
    }

    public static int CutDiscordPipes(int processId, string portName, Action<string> log)
    {
        var process = OpenProcess(ProcessDupHandle | ProcessQuery, false, processId);
        if (process == IntPtr.Zero)
            return -1;
        var cut = 0;
        try
        {
            foreach (var handle in Handles(process))
            {
                if (!DuplicateHandle(process, handle, GetCurrentProcess(), out var local, 0, false, DuplicateSameAccess))
                    continue;
                var name = TypeName(local) == "File" ? Name(local) : "";
                if (name is not null)
                    CloseHandle(local);
                if (name is null || !name.StartsWith(PipePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Swap(process, handle))
                    cut++;
                else
                    log($"Could not swap {portName}'s Discord pipe 0x{handle.ToInt64():X}.");
            }
        }
        finally
        {
            CloseHandle(process);
        }
        return cut;
    }

    static List<IntPtr> Handles(IntPtr process)
    {
        var handles = new List<IntPtr>();
        for (var size = 1 << 16; size <= 1 << 26; size *= 2)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQueryInformationProcess(process, ProcessHandleInformation, buffer, size, out _);
                if (status == StatusInfoLengthMismatch)
                    continue;
                if (status != 0)
                    return handles;
                var count = Marshal.ReadInt64(buffer);
                for (long i = 0; i < count; i++)
                    handles.Add(Marshal.ReadIntPtr(buffer, (int)(HandleEntriesOffset + i * HandleEntrySize)));
                return handles;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        return handles;
    }

    static string TypeName(IntPtr handle)
    {
        var buffer = Marshal.AllocHGlobal(4096);
        try
        {
            if (NtQueryObject(handle, ObjectTypeInformation, buffer, 4096, out _) != 0)
                return "";
            return Marshal.PtrToStringUni(Marshal.ReadIntPtr(buffer, 8), Marshal.ReadInt16(buffer) / 2);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    static string? Name(IntPtr handle)
    {
        string? name = "";
        var worker = new Thread(() =>
        {
            var buffer = Marshal.AllocHGlobal(4096);
            try
            {
                if (NtQueryObject(handle, ObjectNameInformation, buffer, 4096, out _) == 0 && Marshal.ReadIntPtr(buffer, 8) is var text && text != IntPtr.Zero)
                    name = Marshal.PtrToStringUni(text, Marshal.ReadInt16(buffer) / 2);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }) { IsBackground = true };
        worker.Start();
        return worker.Join(250) ? name : null;
    }

    static bool Swap(IntPtr process, IntPtr handle)
    {
        var name = @"\\.\pipe\ocarina-presence-" + Guid.NewGuid().ToString("N");
        var server = CreateNamedPipe(name, PipeAccessDuplex, 0, 1, 0, 0, 0, IntPtr.Zero);
        if (server == Invalid)
            return false;
        var dead = CreateFile(name, GenericReadWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        CloseHandle(server);
        if (dead == Invalid)
            return false;
        try
        {
            if (!DuplicateHandle(process, handle, IntPtr.Zero, out _, 0, false, DuplicateCloseSource))
                return false;
            return DuplicateHandle(GetCurrentProcess(), dead, process, out var placed, 0, false, DuplicateSameAccess) && placed == handle;
        }
        finally
        {
            CloseHandle(dead);
        }
    }
}
