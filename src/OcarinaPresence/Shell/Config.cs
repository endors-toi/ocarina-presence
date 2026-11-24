using System.Text;
using System.Text.Json;

namespace OcarinaPresence;

public sealed class Config
{
    public string ApplicationId { get; set; } = "1569075993740714216";
    public string LargeImageKey { get; set; } = "base";
    public string LargeImageText { get; set; } = "";
    public string AdultImageKey { get; set; } = "adultlink";
    public string ChildImageKey { get; set; } = "childlink";
    public string HeartImageKey { get; set; } = "heart";
    public string Details { get; set; } = "";
    public string State { get; set; } = "";
    public bool ShowPresence { get; set; } = true;
    public bool ShowLocation { get; set; } = true;
    public bool ShowDefeats { get; set; } = true;
    public bool ShowAge { get; set; } = true;
    public bool ShowPlayTime { get; set; } = true;
    public bool ShowSkulltulas { get; set; } = true;
    public bool ShowHeartPieces { get; set; } = true;
    public bool ShowHearts { get; set; } = true;
    public bool ShowMagic { get; set; } = true;
    public Dictionary<string, string> LocationNames { get; set; } = new()
    {
        ["TimeTemple"] = "Temple of Time",
        ["LostWood"] = "Lost Woods",
        ["DodongoCavern"] = "Dodongo's Cavern",
        ["Market"] = "Hyrule Castle Town",
        ["ZoraRiver"] = "Zora's River",
        ["ZoraDomain"] = "Zora's Domain",
        ["ZoraFountain"] = "Zora's Fountain",
        ["DreamWorld"] = "Hall of Fallen Foes",
        ["BottomOfTheWell"] = "Bottom of the Well",
        ["InsideTheDekuTree"] = "Inside the Deku Tree",
        ["InsideJabuJabuBelly"] = "Inside Jabu-Jabu's Belly",
        ["GanonCastle"] = "Ganon's Castle",
        ["InsideGanonCastle"] = "Inside Ganon's Castle",
        ["HountedWasteland"] = "Haunted Wasteland",
        ["CrumblingGanonCastle"] = "Escaping Ganon's Castle",
        ["EscapeFromCrumblingGanonCastle"] = "Escaping Ganon's Castle",
        ["FinalBattleField"] = "Fighting the Demon King"
    };
    static readonly string[] DefaultProcessNames = { "fritchii", "lichii", "eden" };

    public List<string> ProcessNames { get; set; } = new(DefaultProcessNames);
    public bool SilencePortStatus { get; set; } = true;
    public string Target { get; set; } = "";
    public string SaveFolder { get; set; } = "";
    public string WindowTitleContains { get; set; } = "Ocarina of Time";
    public string TitleId { get; set; } = "0400486024DEC000";
    public int PollSeconds { get; set; } = 5;
    public bool StartWithWindows { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;

    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static (Config config, bool created) Load(string path)
    {
        if (!File.Exists(path))
        {
            var fresh = new Config();
            fresh.Save(path);
            return (fresh, true);
        }
        var text = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<Config>(text) ?? new Config();
        foreach (var name in DefaultProcessNames)
            if (!config.ProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                config.ProcessNames.Add(name);
        if (JsonSerializer.Serialize(config, Options) != text)
            config.Save(path);
        return (config, false);
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Options));

    public string DisplayName(string internalName)
    {
        if (LocationNames.TryGetValue(internalName, out var name))
            return name;
        var sb = new StringBuilder();
        for (var i = 0; i < internalName.Length; i++)
        {
            var c = internalName[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(internalName[i - 1]))
                sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
