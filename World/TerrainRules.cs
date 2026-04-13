using System;

namespace TheAlchemist.World;

/// <summary>Maps editor terrain string ids to gameplay <see cref="TileTerrain"/> for walkability and herb pickup.</summary>
public static class TerrainRules
{
    public static TileTerrain AsTileTerrain(string terrainId)
    {
        if (string.IsNullOrWhiteSpace(terrainId))
            return TileTerrain.Grass;

        string s = terrainId.Trim();
        if (Enum.TryParse<TileTerrain>(s, true, out var exact))
            return exact;

        string low = s.ToLowerInvariant();
        if (low.Contains("water") || low.Contains("ocean") || low.Contains("lake"))
            return TileTerrain.Water;
        if (low.Contains("flower") || low.Contains("bloom") || low.Contains("herb_tile"))
            return TileTerrain.Flower;
        if (low.Contains("dirt") || low.Contains("mud") || low.Contains("sand"))
            return TileTerrain.Dirt;

        return TileTerrain.Grass;
    }

    public static bool IsWalkableTerrainId(string terrainId) => AsTileTerrain(terrainId) != TileTerrain.Water;

    public static bool IsFlowerTerrainId(string terrainId) => AsTileTerrain(terrainId) == TileTerrain.Flower;
}
