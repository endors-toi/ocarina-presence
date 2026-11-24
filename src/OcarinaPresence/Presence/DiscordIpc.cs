using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OcarinaPresence;

public sealed class DiscordIpc : IDisposable
{
    const int OpHandshake = 0;
    const int OpFrame = 1;
    const int OpClose = 2;
    const int OpPing = 3;
    const int OpPong = 4;

    readonly NamedPipeClientStream _pipe;
    readonly object _writeLock = new();
    volatile bool _alive = true;

    public bool Alive => _alive && _pipe.IsConnected;

    DiscordIpc(NamedPipeClientStream pipe) => _pipe = pipe;

    public static DiscordIpc? Connect(string applicationId)
    {
        for (var i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(250);
            }
            catch
            {
                pipe.Dispose();
                continue;
            }

            var ipc = new DiscordIpc(pipe);
            try
            {
                ipc.Write(OpHandshake, new JsonObject { ["v"] = 1, ["client_id"] = applicationId });
                var (op, payload) = ipc.Read();
                if (op == OpFrame && payload?["evt"]?.GetValue<string>() == "READY")
                {
                    _ = Task.Run(ipc.ReadLoop);
                    return ipc;
                }
            }
            catch
            {
            }
            ipc.Dispose();
        }
        return null;
    }

    public void SetActivity(int pid, JsonObject? activity)
    {
        Write(OpFrame, new JsonObject
        {
            ["cmd"] = "SET_ACTIVITY",
            ["nonce"] = Guid.NewGuid().ToString(),
            ["args"] = new JsonObject { ["pid"] = pid, ["activity"] = activity }
        });
    }

    void ReadLoop()
    {
        try
        {
            while (_alive)
            {
                var (op, payload) = Read();
                if (op == OpPing)
                    Write(OpPong, payload ?? new JsonObject());
                else if (op == OpClose)
                    break;
            }
        }
        catch
        {
        }
        _alive = false;
    }

    void Write(int op, JsonNode payload)
    {
        var body = Encoding.UTF8.GetBytes(payload.ToJsonString());
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(0, 4), op);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4, 4), body.Length);
        body.CopyTo(frame, 8);
        lock (_writeLock)
        {
            _pipe.Write(frame);
            _pipe.Flush();
        }
    }

    (int op, JsonNode? payload) Read()
    {
        var header = new byte[8];
        _pipe.ReadExactly(header);
        var op = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
        var len = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
        var body = new byte[len];
        _pipe.ReadExactly(body);
        return (op, len == 0 ? null : JsonNode.Parse(body));
    }

    public void Dispose()
    {
        _alive = false;
        _pipe.Dispose();
    }
}
