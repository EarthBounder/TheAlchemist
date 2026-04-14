using System;
using System.Collections.Generic;

namespace TheAlchemist.World;

/// <summary>Serializable run state; terrain is regenerated from <see cref="Seed"/>.</summary>
public sealed class WorldState
{
    /// <summary>Bump when map size/packing changes so old saves reset safely.</summary>
    public const int CurrentSchema = 3;

    public int Schema { get; set; } = CurrentSchema;
    public int Seed { get; set; }
    public int PlayerTileX { get; set; }
    public int PlayerTileY { get; set; }
    public int HerbsCollected { get; set; }

    /// <summary>Herb ids picked up this run (editor herb layer or proc flower as <c>flower</c>).</summary>
    public List<string> InventoryHerbIds { get; set; } = new List<string>();

    public List<int> PickedFlowerCells { get; set; } = new List<int>();

    /// <summary>When true, this session is a level-editor test play (no main save writes).</summary>
    public bool IsTestMapRun { get; set; }

    /// <summary>When true, this session is a campaign mission (use pause to continue the story after playing).</summary>
    public bool IsCampaignMissionRun { get; set; }
    public int CampaignMissionHerbGoal { get; set; }
    public int CampaignMissionHerbsAtStart { get; set; }
    public bool CampaignMissionCompleted { get; set; }

    public static WorldState NewRun(int seed)
    {
        var map = new ProcTileMap(seed);
        map.FindStartPosition(out int px, out int py);
        return new WorldState
        {
            Schema = CurrentSchema,
            Seed = seed,
            PlayerTileX = px,
            PlayerTileY = py,
            HerbsCollected = 0,
            InventoryHerbIds = new List<string>(),
            PickedFlowerCells = new List<int>()
        };
    }

    public static WorldState NewTestMapPlay(EditorMapData map)
    {
        EditorMapData.FindWalkableStart(map, out int px, out int py);
        return new WorldState
        {
            Schema = CurrentSchema,
            Seed = 0,
            PlayerTileX = px,
            PlayerTileY = py,
            HerbsCollected = 0,
            InventoryHerbIds = new List<string>(),
            PickedFlowerCells = new List<int>(),
            IsTestMapRun = true
        };
    }

    public static WorldState NewCampaignMissionPlay(EditorMapData map)
    {
        EditorMapData.FindWalkableStart(map, out int px, out int py);
        return new WorldState
        {
            Schema = CurrentSchema,
            Seed = 0,
            PlayerTileX = px,
            PlayerTileY = py,
            HerbsCollected = 0,
            InventoryHerbIds = new List<string>(),
            PickedFlowerCells = new List<int>(),
            IsTestMapRun = true,
            IsCampaignMissionRun = true
        };
    }

    /// <summary>Clamp player and migrate stale saves after map layout changes.</summary>
    public void EnsureCompatibleWithMap(ProcTileMap map)
    {
        if (Schema != CurrentSchema)
        {
            PickedFlowerCells.Clear();
            InventoryHerbIds?.Clear();
            HerbsCollected = 0;
            CampaignMissionCompleted = false;
            Schema = CurrentSchema;
            map.FindStartPosition(out int px, out int py);
            PlayerTileX = px;
            PlayerTileY = py;
            return;
        }

        InventoryHerbIds ??= new List<string>();

        PlayerTileX = Math.Clamp(PlayerTileX, 0, ProcTileMap.WidthTiles - 1);
        PlayerTileY = Math.Clamp(PlayerTileY, 0, ProcTileMap.HeightTiles - 1);
        if (!map.IsWalkable(PlayerTileX, PlayerTileY))
        {
            map.FindStartPosition(out int sx, out int sy);
            PlayerTileX = sx;
            PlayerTileY = sy;
        }
    }

    public void EnsureCompatibleWithCustomMap(EditorMapData map)
    {
        map.NormalizeInPlace();
        if (Schema != CurrentSchema)
        {
            PickedFlowerCells.Clear();
            InventoryHerbIds?.Clear();
            HerbsCollected = 0;
            CampaignMissionCompleted = false;
            Schema = CurrentSchema;
            EditorMapData.FindWalkableStart(map, out int px, out int py);
            PlayerTileX = px;
            PlayerTileY = py;
            return;
        }

        InventoryHerbIds ??= new List<string>();

        PlayerTileX = Math.Clamp(PlayerTileX, 0, map.Width - 1);
        PlayerTileY = Math.Clamp(PlayerTileY, 0, map.Height - 1);
        int i = map.Pack(PlayerTileX, PlayerTileY);
        if (!TerrainRules.IsWalkableTerrainId(map.Terrain[i]) ||
            !EditorMapData.IsCellWalkableForPlay(map, PlayerTileX, PlayerTileY))
        {
            EditorMapData.FindWalkableStart(map, out int sx, out int sy);
            PlayerTileX = sx;
            PlayerTileY = sy;
        }
    }

    public int CampaignMissionHerbsGathered =>
        Math.Max(0, HerbsCollected - CampaignMissionHerbsAtStart);

    public bool IsCampaignMissionObjectiveComplete() =>
        IsCampaignMissionRun && CampaignMissionHerbGoal > 0 && CampaignMissionHerbsGathered >= CampaignMissionHerbGoal;
}
