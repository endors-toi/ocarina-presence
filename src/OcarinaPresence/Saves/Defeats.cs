using System.Buffers.Binary;

namespace OcarinaPresence;

public sealed record Defeat(string Boss, DateTime At);

public sealed record BossLog(Defeat? Waking, Defeat? Dream, DateTime DreamEntered);

public static class Defeats
{
    const int RecordSize = 24;
    const uint NamedEvent = 0x10;
    const uint SceneChange = 0x19;
    const uint DreamHub = 0xc02dcd82;
    static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    static readonly Dictionary<uint, string> Bosses = new()
    {
        [0x9c6df677] = "Dark Link",
        [0xb3893560] = "Queen Gohma",
        [0x66fc3502] = "King Dodongo",
        [0xa5acd677] = "Barinade",
        [0x4b05c956] = "Phantom Ganon",
        [0xb390efc1] = "Volvagia",
        [0xb7b485b3] = "Morpha",
        [0x9feec3b9] = "Bongo Bongo",
        [0x87484d5d] = "Twinrova",
        [0xb7edd31f] = "Ganondorf",
        [0x8efe6ac2] = "Ganon"
    };

    public static BossLog? Read(FileInfo options)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(options.FullName);
        }
        catch
        {
            return null;
        }

        var records = Records(data);
        Defeat? waking = null, dream = null;
        var dreamEntered = DateTime.MinValue;
        (uint time, uint type, uint from, uint to) last = default;
        for (var i = 0; i < records.Count; i++)
        {
            var (time, type, from, to) = records[i];
            if (type == SceneChange)
            {
                if (to == DreamHub && !(last.from == DreamHub && last.to == from))
                    dreamEntered = At(time);
                last = records[i];
                continue;
            }

            var defeat = new Defeat(Bosses[from], At(time));
            var before = last;
            var after = records.Skip(i + 1).FirstOrDefault(r => r.type == SceneChange);
            if (before.from == DreamHub && after.to == DreamHub && before.to == after.from)
                dream = defeat;
            else
                waking = defeat;
        }
        return new BossLog(waking, dream, dreamEntered);
    }

    static List<(uint time, uint type, uint from, uint to)> Records(byte[] data)
    {
        var records = new List<(uint time, int offset, uint type, uint from, uint to)>();
        for (var o = 0x20; o + RecordSize <= data.Length; o += 4)
        {
            var time = U32(data, o);
            if (time == 0 || time >= 0x80000000)
                continue;
            var type = U32(data, o + 4);
            uint a = U32(data, o + 8), b = U32(data, o + 12);
            var boss = type == NamedEvent && b == 0 && U32(data, o + 16) == 0 && U32(data, o + 20) == 0 && Bosses.ContainsKey(a);
            var hub = type == SceneChange && a != b && (a == DreamHub || b == DreamHub);
            if (boss || hub)
                records.Add((time, o, type, a, b));
        }
        return records.OrderBy(r => r.time).ThenBy(r => r.offset).Select(r => (r.time, r.type, r.from, r.to)).ToList();
    }

    static DateTime At(uint time) => Epoch.AddSeconds(time).ToLocalTime();

    static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
}
