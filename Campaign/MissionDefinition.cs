using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheAlchemist.Campaign;

/// <summary>Authoring data for one mission; map is resolved via <see cref="MapSlot"/> into <see cref="Persistence.MapFileStore"/>.</summary>
public sealed class MissionDefinition
{
    public int Schema { get; set; } = 1;

    /// <summary>Stable id (matches filename stem under missions/).</summary>
    public string Id { get; set; } = "";

    /// <summary>Which save slot <c>map_XX.json</c> to load for this mission (0-99).</summary>
    public int MapSlot { get; set; }

    public string Title { get; set; } = "";

    /// <summary>Short player-facing blurb.</summary>
    public string Summary { get; set; } = "";

    /// <summary>Additional mission fields without schema churn.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; }
}
