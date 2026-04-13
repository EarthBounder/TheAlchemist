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
    public List<int> PickedFlowerCells { get; set; } = new List<int>();

    /// <summary>When true, this session is a level-editor test play (no main save writes).</summary>
    public bool IsTestMapRun { get; set; }

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
            PickedFlowerCells = new List<int>(),
            IsTestMapRun = true
        };
    }

    /// <summary>Clamp player and migrate stale saves after map layout changes.</summary>
    public void EnsureCompatibleWithMap(ProcTileMap map)
    {
        if (Schema != CurrentSchema)
        {
            PickedFlowerCells.Clear();
            HerbsCollected = 0;
            Schema = CurrentSchema;
            map.FindStartPosition(out int px, out int py);
            PlayerTileX = px;
            PlayerTileY = py;
            return;
        }

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
            HerbsCollected = 0;
            Schema = CurrentSchema;
            EditorMapData.FindWalkableStart(map, out int px, out int py);
            PlayerTileX = px;
            PlayerTileY = py;
            return;
        }

        PlayerTileX = Math.Clamp(PlayerTileX, 0, map.Width - 1);
        PlayerTileY = Math.Clamp(PlayerTileY, 0, map.Height - 1);
        int i = map.Pack(PlayerTileX, PlayerTileY);
        if (!TerrainRules.IsWalkableTerrainId(map.Terrain[i]))
        {
            EditorMapData.FindWalkableStart(map, out int sx, out int sy);
            PlayerTileX = sx;
            PlayerTileY = sy;
        }
    }
}
