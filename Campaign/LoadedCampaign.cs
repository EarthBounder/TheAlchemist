using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TheAlchemist.Core;
using TheAlchemist.Persistence;
using TheAlchemist.World;

namespace TheAlchemist.Campaign;

/// <summary>Validated campaign content: dialogue segments and mission definitions from disk.</summary>
public sealed class LoadedCampaign
{
    private readonly string[] _missionIdsInOrder;

    internal LoadedCampaign(
        string rootDirectory,
        DialogueSegment intro,
        IReadOnlyList<DialogueSegment> interludes,
        DialogueSegment outro,
        IReadOnlyList<MissionDefinition> missions,
        IReadOnlyList<string> manifestMissionIds)
    {
        RootDirectory = rootDirectory;
        Intro = intro;
        Interludes = interludes;
        Outro = outro;
        Missions = missions;
        _missionIdsInOrder = new string[manifestMissionIds.Count];
        for (int i = 0; i < manifestMissionIds.Count; i++)
            _missionIdsInOrder[i] = manifestMissionIds[i];
    }

    public string RootDirectory { get; }

    public DialogueSegment Intro { get; }

    /// <summary>Length equals mission count; index is the interlude index after each mission.</summary>
    public IReadOnlyList<DialogueSegment> Interludes { get; }

    public DialogueSegment Outro { get; }

    public IReadOnlyList<MissionDefinition> Missions { get; }

    /// <summary>Mission ids in play order (from campaign.json).</summary>
    public IReadOnlyList<string> MissionIdList => _missionIdsInOrder;

    /// <summary>Builds runtime flow state for this campaign order.</summary>
    public GameFlowProgress CreateFlowProgress() => new GameFlowProgress(Missions.Count, MissionIdList);

    /// <summary>Loads <c>map_XX.json</c> for the mission at <paramref name="missionIndex"/> using its <see cref="MissionDefinition.MapSlot"/>.</summary>
    public bool TryLoadMapForMission(int missionIndex, out EditorMapData map)
    {
        map = null;
        if ((uint)missionIndex >= (uint)Missions.Count)
            return false;
        return MapFileStore.TryLoad(Missions[missionIndex].MapSlot, out map);
    }

    /// <summary>Intro, interlude, or outro dialogue for a flow beat index; false on mission beats.</summary>
    public bool TryGetDialogueForBeat(int beatIndex, out DialogueSegment segment)
    {
        segment = null;
        int n = Missions.Count;
        if (n == 0 || beatIndex < 0 || beatIndex >= GameFlowSchedule.TotalBeats(n))
            return false;

        GameFlowBeat kind = GameFlowSchedule.GetBeat(beatIndex, n);
        switch (kind)
        {
            case GameFlowBeat.Intro:
                segment = Intro;
                return true;
            case GameFlowBeat.Outro:
                segment = Outro;
                return true;
            case GameFlowBeat.Interlude:
                segment = Interludes[GameFlowSchedule.InterludeOrdinal(beatIndex, n)];
                return true;
            default:
                return false;
        }
    }
}

public static class CampaignLoader
{
    /// <summary>Default campaign folder next to the executable: <c>Content/data/campaign</c>.</summary>
    public static string DefaultCampaignRoot =>
        Path.Combine(AppContext.BaseDirectory, "Content", "data", "campaign");

    /// <summary>Loads <c>campaign.json</c>, all <c>missions/*.json</c>, and dialogue under <c>dialogue/</c>.</summary>
    public static bool TryLoad(string campaignRootDirectory, out LoadedCampaign campaign, out string error)
    {
        campaign = null;
        error = null;

        if (string.IsNullOrWhiteSpace(campaignRootDirectory))
        {
            error = "Campaign root is empty.";
            return false;
        }

        if (!Directory.Exists(campaignRootDirectory))
        {
            error = $"Campaign folder not found: {campaignRootDirectory}";
            return false;
        }

        string manifestPath = Path.Combine(campaignRootDirectory, "campaign.json");
        if (!File.Exists(manifestPath))
        {
            error = $"Missing campaign.json at {manifestPath}";
            return false;
        }

        CampaignManifest manifest;
        try
        {
            string json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<CampaignManifest>(json, CampaignJson.Options);
            if (manifest?.MissionIds == null || manifest.MissionIds.Count == 0)
            {
                error = "campaign.json must include a non-empty missionIds array.";
                return false;
            }
        }
        catch (Exception ex)
        {
            error = $"campaign.json parse error: {ex.Message}";
            return false;
        }

        int n = manifest.MissionIds.Count;
        string dialogueDir = Path.Combine(campaignRootDirectory, "dialogue");
        string missionsDir = Path.Combine(campaignRootDirectory, "missions");

        if (!TryReadDialogue(Path.Combine(dialogueDir, "intro.json"), out DialogueSegment intro, out error))
            return false;

        var interludes = new DialogueSegment[n];
        for (int i = 0; i < n; i++)
        {
            string path = Path.Combine(dialogueDir, $"interlude_{i}.json");
            if (!TryReadDialogue(path, out interludes[i], out error))
            {
                error = $"Interlude {i}: {error}";
                return false;
            }
        }

        if (!TryReadDialogue(Path.Combine(dialogueDir, "outro.json"), out DialogueSegment outro, out error))
            return false;

        var missions = new MissionDefinition[n];
        for (int i = 0; i < n; i++)
        {
            string id = manifest.MissionIds[i]?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                error = $"campaign.json missionIds[{i}] is empty.";
                return false;
            }

            string missionPath = Path.Combine(missionsDir, $"{id}.json");
            if (!File.Exists(missionPath))
            {
                error = $"Missing mission file: {missionPath}";
                return false;
            }

            try
            {
                string mj = File.ReadAllText(missionPath);
                var def = JsonSerializer.Deserialize<MissionDefinition>(mj, CampaignJson.Options);
                if (def == null || string.IsNullOrWhiteSpace(def.Id))
                {
                    error = $"Mission file invalid (missing id): {missionPath}";
                    return false;
                }

                if (!string.Equals(def.Id.Trim(), id, StringComparison.Ordinal))
                {
                    error = $"Mission id mismatch: file {id}.json has id \"{def.Id}\" (expected \"{id}\").";
                    return false;
                }

                if (def.MapSlot < 0 || def.MapSlot > 99)
                {
                    error = $"Mission {id}: mapSlot must be in [0, 99].";
                    return false;
                }

                missions[i] = def;
            }
            catch (Exception ex)
            {
                error = $"Mission {id}: {ex.Message}";
                return false;
            }
        }

        campaign = new LoadedCampaign(campaignRootDirectory, intro, interludes, outro, missions, manifest.MissionIds);
        return true;
    }

    private static bool TryReadDialogue(string path, out DialogueSegment segment, out string error)
    {
        segment = null;
        if (!File.Exists(path))
        {
            error = $"Missing dialogue file: {path}";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            segment = JsonSerializer.Deserialize<DialogueSegment>(json, CampaignJson.Options);
            if (segment == null)
            {
                error = $"Invalid dialogue: {path}";
                return false;
            }

            segment.Lines ??= new List<DialogueLine>();

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"{path}: {ex.Message}";
            return false;
        }
    }
}
