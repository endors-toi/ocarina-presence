using System.Text.Json.Nodes;

namespace OcarinaPresence;

sealed class ActivityBuilder
{
    readonly Config _config;

    public ActivityBuilder(Config config) => _config = config;

    public string PlaceName(Location location)
    {
        var name = location.SubArea.Length > 0 ? location.SubArea : location.Scene.Split('/')[0];
        return _config.DisplayName(name);
    }

    public string? AgeImage(Location location)
    {
        var key = location.Child is { } child ? (child ? _config.ChildImageKey : _config.AdultImageKey)
            : location.Scene.EndsWith("_Adult", StringComparison.OrdinalIgnoreCase) ? _config.AdultImageKey
            : location.Scene.EndsWith("_Child", StringComparison.OrdinalIgnoreCase) ? _config.ChildImageKey
            : null;
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    public JsonObject Build(string? action, string? location, Location? save, string? ageImage, DateTime sessionStart)
    {
        var activity = new JsonObject();
        var details = action ?? location ?? _config.Details;
        var state = action is null ? _config.State : location ?? _config.State;
        if (!string.IsNullOrWhiteSpace(details))
            activity["details"] = details;
        if (!string.IsNullOrWhiteSpace(state))
            activity["state"] = state;
        activity["timestamps"] = new JsonObject { ["start"] = new DateTimeOffset(sessionStart).ToUnixTimeSeconds() };

        var stats = save?.Stats;
        var largeImage = (_config.ShowAge ? ageImage : null) ?? _config.LargeImageKey;
        var progress = stats is null ? "" : ProgressText(stats);
        var largeText = progress.Length > 0 ? progress : _config.LargeImageText;
        var assets = new JsonObject();
        if (!string.IsNullOrWhiteSpace(largeImage))
        {
            assets["large_image"] = largeImage;
            if (!string.IsNullOrWhiteSpace(largeText))
                assets["large_text"] = largeText;
        }
        var combat = stats is null ? "" : CombatText(stats);
        if (combat.Length > 0 && !string.IsNullOrWhiteSpace(_config.HeartImageKey))
        {
            assets["small_image"] = _config.HeartImageKey;
            assets["small_text"] = combat;
        }
        if (assets.Count > 0)
            activity["assets"] = assets;
        return activity;
    }

    string ProgressText(SaveStats stats)
    {
        var parts = new List<string>();
        if (_config.ShowPlayTime)
            parts.Add($"🕐 {stats.PlaySeconds / 3600}h{stats.PlaySeconds % 3600 / 60:00}m");
        if (_config.ShowSkulltulas)
            parts.Add($"💀 {stats.Skulltulas}");
        if (_config.ShowHeartPieces)
            parts.Add($"💗 {stats.HeartPieces}");
        return string.Join(" ", parts);
    }

    string CombatText(SaveStats stats)
    {
        var parts = new List<string>();
        if (_config.ShowHearts)
            parts.Add($"❤️ {Quarters(stats.Hearts)}/{Quarters(stats.MaxHearts)}");
        if (_config.ShowMagic && stats.MaxMagic > 0)
            parts.Add($"🟢 {100 * stats.Magic / stats.MaxMagic}%");
        return string.Join(" ", parts);
    }

    static string Quarters(int quarters)
    {
        var whole = quarters / 4;
        var part = (quarters % 4) switch { 1 => "¼", 2 => "½", 3 => "¾", _ => "" };
        return whole == 0 && part.Length > 0 ? part : $"{whole}{part}";
    }
}
