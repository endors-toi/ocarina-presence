using System.Buffers.Binary;
using System.Text;

namespace OcarinaPresence;

public sealed record Location(string SubArea, string Scene, DateTime SavedAt, SaveStats? Stats, bool? Child);

public sealed record SaveStats(int Hearts, int MaxHearts, int Magic, int MaxMagic, int Skulltulas, int HeartPieces, uint PlaySeconds);

public static class SaveReader
{
    const uint BoolType = 0;
    const uint IntType = 2;
    const uint UIntType = 20;
    const uint String64Type = 16;
    const int String64Length = 0x40;
    const uint SubAreaKey = 0xf74e0e8e;
    const uint SceneKey = 0x0cccc6cd;
    const uint HeartsKey = 0x1e55ecc5;
    const uint MaxHeartsKey = 0x74bd05d0;
    const uint MagicKey = 0x19f26b6a;
    const uint MaxMagicKey = 0x943f8ec3;
    const uint SkulltulasKey = 0xdc941483;
    const uint HeartPiecesKey = 0x0e34c167;
    const uint PlaySecondsKey = 0xe573f564;
    const uint ChildhoodKey = 0xcea220d3;
    static readonly byte[] Magic = { 0x04, 0x03, 0x02, 0x01 };

    public static Location? Read(FileInfo caption)
    {
        var fields = ReadTable(caption.FullName);
        if (fields is null)
            return null;

        var subArea = String(fields.Value, SubAreaKey);
        var scene = String(fields.Value, SceneKey);
        if (subArea.Length == 0 && scene.Length == 0)
            return null;
        var progress = caption.DirectoryName is null ? null : ReadTable(Path.Combine(caption.DirectoryName, SaveFolders.ProgressFile));
        var progressTable = progress?.table ?? new Dictionary<(uint, uint), uint>();
        bool? child = progressTable.TryGetValue((BoolType, ChildhoodKey), out var childhood) ? childhood != 0 : null;
        return new Location(subArea, scene, caption.LastWriteTime, Stats(fields.Value.table, progressTable), child);
    }

    static SaveStats? Stats(Dictionary<(uint, uint), uint> table, Dictionary<(uint, uint), uint> progressTable)
    {
        if (!table.TryGetValue((IntType, MaxHeartsKey), out var maxHearts) || maxHearts == 0)
            return null;

        return new SaveStats(
            (int)table.GetValueOrDefault((IntType, HeartsKey)),
            (int)maxHearts,
            (int)table.GetValueOrDefault((IntType, MagicKey)),
            (int)table.GetValueOrDefault((IntType, MaxMagicKey)),
            (int)table.GetValueOrDefault((IntType, SkulltulasKey)),
            (int)progressTable.GetValueOrDefault((IntType, HeartPiecesKey)),
            progressTable.GetValueOrDefault((UIntType, PlaySecondsKey)));
    }

    static (Dictionary<(uint, uint), uint> table, byte[] data)? ReadTable(string path)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }

        if (data.Length < 0x20 || !data.AsSpan(0, 4).SequenceEqual(Magic))
            return null;

        var end = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(8, 4)), (uint)data.Length);
        var table = new Dictionary<(uint, uint), uint>();
        uint type = 0;
        for (var o = 0x20; o + 8 <= end; o += 8)
        {
            var hash = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o, 4));
            var value = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(o + 4, 4));
            if (hash == 0)
                type = value;
            else
                table[(type, hash)] = value;
        }
        return (table, data);
    }

    static string String((Dictionary<(uint, uint), uint> table, byte[] data) fields, uint key)
    {
        if (!fields.table.TryGetValue((String64Type, key), out var offset) || offset + String64Length > fields.data.Length)
            return "";
        var field = fields.data.AsSpan((int)offset, String64Length);
        var end = field.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? field : field[..end]);
    }
}
