namespace TheAlchemist.World;

/// <summary>One "screen" of the metroidvania map: 36px tiles, room centered in 1280×720 design.</summary>
public static class RoomView
{
    /// <summary>Tiles wide per room (35×36 = 1260px; centered in 1280 design width).</summary>
    public const int WidthTiles = 35;

    /// <summary>Tiles tall per room (20×36 = 720px; full design height).</summary>
    public const int HeightTiles = 20;

    public static void GetRoomIndices(int globalTileX, int globalTileY, out int roomX, out int roomY)
    {
        roomX = globalTileX / WidthTiles;
        roomY = globalTileY / HeightTiles;
    }

    public static void GetRoomOriginTiles(int roomX, int roomY, out int originTileX, out int originTileY)
    {
        originTileX = roomX * WidthTiles;
        originTileY = roomY * HeightTiles;
    }
}
