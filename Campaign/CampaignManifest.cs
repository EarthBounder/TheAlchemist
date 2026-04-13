using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheAlchemist.Campaign;

/// <summary>Root campaign list: mission ids in play order (one JSON file per id under missions/).</summary>
public sealed class CampaignManifest
{
    public int Schema { get; set; } = 1;

    /// <summary>Mission definition ids, in order (mission 0, mission 1, ...).</summary>
    public List<string> MissionIds { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; }
}
