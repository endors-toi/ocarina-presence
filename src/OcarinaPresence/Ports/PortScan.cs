using System.Buffers.Binary;
using System.Text;

namespace OcarinaPresence;

public static class PortScan
{
    static readonly byte[] PipeName = Encoding.ASCII.GetBytes("\\\\.\\pipe\\discord-ipc-%d\0");
    const int Chunk = 16 << 20;
    const int HeaderSize = 4096;
    const int PeOffsetField = 0x3C;
    const int SectionCountField = 6;
    const int OptionalHeaderSizeField = 20;
    const int OptionalHeaderStart = 24;
    const int DataDirectoriesOffset = 112;
    const int DataDirectorySize = 8;
    const int ExceptionDirectory = 3;
    const int SectionHeaderSize = 40;
    const uint ExecutableSection = 0x20000000;
    const int RuntimeFunctionSize = 12;
    const byte LeaOpcode = 0x8D;

    record Section(uint Rva, uint RawSize, uint RawOffset, bool Code);

    public static HashSet<uint> DiscordFunctions(string exePath)
    {
        var found = new HashSet<uint>();
        using var file = File.OpenRead(exePath);
        var header = Read(file, 0, HeaderSize);
        var pe = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(PeOffsetField));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(pe + SectionCountField));
        var optSize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(pe + OptionalHeaderSizeField));
        var opt = pe + OptionalHeaderStart;
        var directory = opt + DataDirectoriesOffset + ExceptionDirectory * DataDirectorySize;
        var pdataRva = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(directory));
        var pdataSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(directory + 4));
        var sections = new List<Section>();
        for (var i = 0; i < count; i++)
        {
            var s = header.AsSpan(opt + optSize + i * SectionHeaderSize);
            sections.Add(new Section(
                BinaryPrimitives.ReadUInt32LittleEndian(s[12..]),
                BinaryPrimitives.ReadUInt32LittleEndian(s[16..]),
                BinaryPrimitives.ReadUInt32LittleEndian(s[20..]),
                (BinaryPrimitives.ReadUInt32LittleEndian(s[36..]) & ExecutableSection) != 0));
        }

        uint? text = null;
        foreach (var section in sections.Where(s => !s.Code))
        {
            var data = Read(file, section.RawOffset, (int)section.RawSize);
            var at = data.AsSpan().IndexOf(PipeName);
            if (at >= 0 && (at == 0 || data[at - 1] == 0))
            {
                text = section.Rva + (uint)at;
                break;
            }
        }
        if (text is null || pdataSize == 0)
            return found;

        var pdata = Read(file, Offset(sections, pdataRva), (int)pdataSize);
        foreach (var section in sections.Where(s => s.Code))
        {
            for (long start = 0; start < section.RawSize; start += Chunk)
            {
                var length = (int)Math.Min(Chunk + 8, section.RawSize - start);
                var data = Read(file, section.RawOffset + start, length);
                var span = data.AsSpan();
                var limit = (int)Math.Min(Chunk, length - 4);
                var i = 0;
                while (i < limit)
                {
                    var hit = span[i..limit].IndexOf(LeaOpcode);
                    if (hit < 0)
                        break;
                    i += hit + 1;
                    if (i + 5 > length || (span[i] & 0xC7) != 0x05)
                        continue;
                    var rva = section.Rva + (uint)(start + i + 1);
                    var target = rva + 4 + BinaryPrimitives.ReadInt32LittleEndian(span[(i + 1)..]);
                    if (target == text && Function(file, sections, pdata, rva) is { } function)
                        found.Add(function);
                }
            }
        }
        return found;
    }

    static uint? Function(FileStream file, List<Section> sections, byte[] pdata, uint rva)
    {
        int lo = 0, hi = pdata.Length / RuntimeFunctionSize - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var begin = BinaryPrimitives.ReadUInt32LittleEndian(pdata.AsSpan(mid * RuntimeFunctionSize));
            var end = BinaryPrimitives.ReadUInt32LittleEndian(pdata.AsSpan(mid * RuntimeFunctionSize + 4));
            if (rva < begin)
                hi = mid - 1;
            else if (rva >= end)
                lo = mid + 1;
            else
                return Primary(file, sections, begin, BinaryPrimitives.ReadUInt32LittleEndian(pdata.AsSpan(mid * RuntimeFunctionSize + 8)));
        }
        return null;
    }

    static uint Primary(FileStream file, List<Section> sections, uint begin, uint unwind)
    {
        for (var depth = 0; depth < 32; depth++)
        {
            var info = Read(file, Offset(sections, unwind), 4);
            if ((info[0] >> 3 & 0x4) == 0)
                return begin;
            var chained = Read(file, Offset(sections, unwind) + 4 + ((info[2] + 1) & ~1) * 2, 12);
            begin = BinaryPrimitives.ReadUInt32LittleEndian(chained);
            unwind = BinaryPrimitives.ReadUInt32LittleEndian(chained.AsSpan(8));
        }
        return begin;
    }

    static long Offset(List<Section> sections, uint rva)
    {
        var section = sections.First(s => rva >= s.Rva && rva < s.Rva + s.RawSize);
        return section.RawOffset + rva - section.Rva;
    }

    static byte[] Read(FileStream file, long offset, int length)
    {
        var data = new byte[length];
        file.Position = offset;
        file.ReadExactly(data);
        return data;
    }
}
