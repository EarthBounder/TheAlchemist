using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheAlchemist.Campaign;

/// <summary>One line of authored dialogue (UI should sanitize for font if needed).</summary>
public sealed class DialogueLine
{
    /// <summary>Optional speaker label shown before the line.</summary>
    public string Speaker { get; set; }

    public string Text { get; set; } = "";

    /// <summary>Extra JSON keys preserved for future tools.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; }
}

/// <summary>Static dialogue segment: intro, an interlude, or outro.</summary>
public sealed class DialogueSegment
{
    public int Schema { get; set; } = 1;

    public List<DialogueLine> Lines { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; }
}
