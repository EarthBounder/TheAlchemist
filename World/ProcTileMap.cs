using System;

namespace TheAlchemist.World;

/// <summary>Seed-based overworld grid for herb gathering (placeholder generation).</summary>
public sealed class ProcTileMap
{
    public const int RoomsWide = 3;
    public const int RoomsHigh = 3;

    public const int WidthTiles = RoomsWide * RoomView.WidthTiles;
    public const int HeightTiles = RoomsHigh * RoomView.HeightTiles;
    public const int TileSizePixels = 36;

    private readonly TileTerrain[,] _tiles = new TileTerrain[WidthTiles, HeightTiles];

    public ProcTileMap(int seed)
    {
        var rng = new Random(seed);
        for (int y = 0; y < HeightTiles; y++)
        for (int x = 0; x < WidthTiles; x++)
        {
            double n = SimpleNoise(x, y, seed) + (rng.NextDouble() - 0.5) * 0.15;
            if (n < 0.22)
                _tiles[x, y] = TileTerrain.Water;
            else if (n < 0.32)
                _tiles[x, y] = TileTerrain.Flower;
            else if (n < 0.38)
                _tiles[x, y] = TileTerrain.Dirt;
            else
                _tiles[x, y] = TileTerrain.Grass;
        }
    }

    private static double SimpleNoise(int x, int y, int seed)
    {
        double sx = x * 0.087 + seed * 0.001;
        double sy = y * 0.091 + seed * 0.002;
        return (Math.Sin(sx) * Math.Cos(sy) + 1.0) * 0.5;
    }

    public TileTerrain Get(int x, int y) =>
        (uint)x < (uint)WidthTiles && (uint)y < (uint)HeightTiles ? _tiles[x, y] : TileTerrain.Water;

    public void Set(int x, int y, TileTerrain t)
    {
        if ((uint)x < (uint)WidthTiles && (uint)y < (uint)HeightTiles)
            _tiles[x, y] = t;
    }

    public bool IsWalkable(int x, int y) => Get(x, y) != TileTerrain.Water;

    public static int Pack(int x, int y) => x + y * WidthTiles;

    public static void Unpack(int p, out int x, out int y)
    {
        x = p % WidthTiles;
        y = p / WidthTiles;
    }

    public void ApplyPickedFlowers(System.Collections.Generic.IReadOnlyList<int> packed)
    {
        if (packed == null)
            return;
        for (int i = 0; i < packed.Count; i++)
        {
            Unpack(packed[i], out int x, out int y);
            if ((uint)x < (uint)WidthTiles && (uint)y < (uint)HeightTiles && _tiles[x, y] == TileTerrain.Flower)
                _tiles[x, y] = TileTerrain.Grass;
        }
    }

    public void FindStartPosition(out int px, out int py)
    {
        int cx = WidthTiles / 2;
        int cy = HeightTiles / 2;
        for (int r = 0; r < Math.Max(WidthTiles, HeightTiles); r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            int x = cx + dx;
            int y = cy + dy;
            if (IsWalkable(x, y) && Get(x, y) != TileTerrain.Flower)
            {
                px = x;
                py = y;
                return;
            }
        }

        px = cx;
        py = cy;
    }
}
