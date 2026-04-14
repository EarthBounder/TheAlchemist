using System;
using System.Text.Json;

namespace TheAlchemist.World;

/// <summary>Authoring-time map: terrain, objects/herbs, per-tile walk flags, optional background path.</summary>
public sealed class EditorMapData
{
    public const int CurrentSchema = 3;

    /// <summary>Room counts per axis when authoring new maps (each room is <see cref="RoomView.WidthTiles"/> x <see cref="RoomView.HeightTiles"/> tiles).</summary>
    public const int MinRoomsPerAxis = 1;
    public const int MaxRoomsPerAxis = 16;

    public const string DefaultTerrainId = "grass";

    public int Schema { get; set; } = CurrentSchema;
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Ground texture id per cell (matches loose filenames under Content/art/tiles).</summary>
    public string[] Terrain { get; set; } = Array.Empty<string>();

    public string[] Objects { get; set; } = Array.Empty<string>();
    public string[] Herbs { get; set; } = Array.Empty<string>();

    /// <summary>When false, hero cannot enter (manual paint). Object/herb sprites also block by footprint from their anchor tile.</summary>
    public bool[] Walkable { get; set; } = Array.Empty<bool>();

    /// <summary>Optional absolute path to a background image shown under the grid in the editor.</summary>
    public string BackgroundImagePath { get; set; }

    public static EditorMapData CreateEmpty(int width, int height)
    {
        int n = width * height;
        var terrain = new string[n];
        var objects = new string[n];
        var herbs = new string[n];
        var walk = new bool[n];
        for (int i = 0; i < n; i++)
        {
            terrain[i] = DefaultTerrainId;
            objects[i] = "";
            herbs[i] = "";
            walk[i] = true;
        }

        return new EditorMapData
        {
            Schema = CurrentSchema,
            Width = width,
            Height = height,
            Terrain = terrain,
            Objects = objects,
            Herbs = herbs,
            Walkable = walk,
            BackgroundImagePath = null
        };
    }

    public static int TileWidthFromRooms(int roomsWide) =>
        Math.Clamp(roomsWide, MinRoomsPerAxis, MaxRoomsPerAxis) * RoomView.WidthTiles;

    public static int TileHeightFromRooms(int roomsHigh) =>
        Math.Clamp(roomsHigh, MinRoomsPerAxis, MaxRoomsPerAxis) * RoomView.HeightTiles;

    public static EditorMapData CreateEmptyRooms(int roomsWide, int roomsHigh) =>
        CreateEmpty(TileWidthFromRooms(roomsWide), TileHeightFromRooms(roomsHigh));

    public static bool IsRoomGridSize(int width, int height) =>
        width > 0 && height > 0 &&
        width % RoomView.WidthTiles == 0 &&
        height % RoomView.HeightTiles == 0 &&
        width / RoomView.WidthTiles <= MaxRoomsPerAxis &&
        height / RoomView.HeightTiles <= MaxRoomsPerAxis;

    public static void GetRoomCounts(int width, int height, out int roomsWide, out int roomsHigh)
    {
        if (IsRoomGridSize(width, height))
        {
            roomsWide = width / RoomView.WidthTiles;
            roomsHigh = height / RoomView.HeightTiles;
        }
        else
        {
            roomsWide = ProcTileMap.RoomsWide;
            roomsHigh = ProcTileMap.RoomsHigh;
        }
    }

    public static EditorMapData Clone(EditorMapData m)
    {
        if (m == null)
            return CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
        m.NormalizeInPlace();
        return new EditorMapData
        {
            Schema = m.Schema,
            Width = m.Width,
            Height = m.Height,
            Terrain = (string[])m.Terrain.Clone(),
            Objects = (string[])m.Objects.Clone(),
            Herbs = (string[])m.Herbs.Clone(),
            Walkable = (bool[])m.Walkable.Clone(),
            BackgroundImagePath = m.BackgroundImagePath
        };
    }

    /// <summary>Hero movement: terrain (e.g. water), walk mask, and full object/herb sprite footprints (anchor = top-left).</summary>
    public static bool IsCellWalkableForPlay(EditorMapData map, int x, int y)
    {
        if (map == null || !map.Contains(x, y))
            return false;
        map.NormalizeInPlace();
        int i = map.Pack(x, y);
        if (!map.Walkable[i])
            return false;
        if (!TerrainRules.IsWalkableTerrainId(map.Terrain[i]))
            return false;
        if (OverlayFootprintCollision.CellOverlapsAnchoredFootprint(map, x, y, map.Objects, "objects"))
            return false;
        if (OverlayFootprintCollision.CellOverlapsAnchoredFootprint(map, x, y, map.Herbs, "herbs"))
            return false;
        return true;
    }

    /// <summary>After removing an object/herb at <paramref name="anchorX"/>,<paramref name="anchorY"/> with id <paramref name="clearedId"/>, sets walk true on cleared footprint cells that are not still under another overlay.</summary>
    public static void RestoreWalkAfterRemovingOverlayAt(EditorMapData map, int anchorX, int anchorY, string categoryFolderName,
        string clearedId)
    {
        if (map == null || string.IsNullOrEmpty(clearedId))
            return;

        OverlayFootprintLayout.TryGetCoverageForId(categoryFolderName, clearedId, out int fw, out int fh);
        for (int oy = 0; oy < fh; oy++)
        for (int ox = 0; ox < fw; ox++)
        {
            int cx = anchorX + ox;
            int cy = anchorY + oy;
            if (!map.Contains(cx, cy))
                continue;
            if (!OverlayFootprintCollision.CellOverlapsAnyObjectOrHerbFootprint(map, cx, cy))
                map.Walkable[map.Pack(cx, cy)] = true;
        }
    }

    public static void FindWalkableStart(EditorMapData map, out int px, out int py)
    {
        map.NormalizeInPlace();
        int cx = map.Width / 2;
        int cy = map.Height / 2;
        int maxR = Math.Max(map.Width, map.Height);
        for (int r = 0; r < maxR; r++)
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            int x = cx + dx;
            int y = cy + dy;
            if (map.Contains(x, y) && IsCellWalkableForPlay(map, x, y))
            {
                px = x;
                py = y;
                return;
            }
        }

        px = Math.Clamp(cx, 0, map.Width - 1);
        py = Math.Clamp(cy, 0, map.Height - 1);
    }

    public int Pack(int x, int y) => x + y * Width;

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public void NormalizeInPlace()
    {
        int n = Width * Height;
        if (n <= 0)
            return;

        if (Terrain == null || Terrain.Length != n)
        {
            var t = new string[n];
            for (int i = 0; i < n; i++)
                t[i] = DefaultTerrainId;
            Terrain = t;
        }

        if (Objects == null || Objects.Length != n)
        {
            var o = new string[n];
            for (int i = 0; i < n; i++)
                o[i] = "";
            Objects = o;
        }

        if (Herbs == null || Herbs.Length != n)
        {
            var h = new string[n];
            for (int i = 0; i < n; i++)
                h[i] = "";
            Herbs = h;
        }

        if (Walkable == null || Walkable.Length != n)
        {
            var w = new bool[n];
            for (int i = 0; i < n; i++)
                w[i] = true;
            Walkable = w;
        }

        for (int i = 0; i < Terrain.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(Terrain[i]))
                Terrain[i] = DefaultTerrainId;
        }

        for (int i = 0; i < Objects.Length; i++)
        {
            if (Objects[i] == null)
                Objects[i] = "";
        }

        for (int i = 0; i < Herbs.Length; i++)
        {
            if (Herbs[i] == null)
                Herbs[i] = "";
        }
    }

    /// <summary>Parses JSON, including older maps without <c>walkable</c>.</summary>
    public static bool TryParseMapJson(string json, out EditorMapData map)
    {
        map = null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            int w = root.GetProperty("width").GetInt32();
            int h = root.GetProperty("height").GetInt32();
            int n = w * h;
            if (n <= 0)
                return false;

            var terrain = new string[n];
            for (int i = 0; i < n; i++)
                terrain[i] = DefaultTerrainId;

            if (root.TryGetProperty("terrain", out JsonElement te) && te.ValueKind == JsonValueKind.Array)
            {
                int len = Math.Min(te.GetArrayLength(), n);
                for (int i = 0; i < len; i++)
                {
                    JsonElement el = te[i];
                    if (el.ValueKind == JsonValueKind.Number)
                    {
                        int v = el.GetInt32();
                        if (Enum.IsDefined(typeof(TileTerrain), v))
                            terrain[i] = ((TileTerrain)v).ToString().ToLowerInvariant();
                        else
                            terrain[i] = DefaultTerrainId;
                    }
                    else if (el.ValueKind == JsonValueKind.String)
                    {
                        string s = el.GetString();
                        terrain[i] = string.IsNullOrWhiteSpace(s) ? DefaultTerrainId : s.Trim();
                    }
                }
            }

            string[] objects = ParseStringCellArray(root, "objects", n);
            string[] herbs = ParseStringCellArray(root, "herbs", n);

            var walk = new bool[n];
            for (int i = 0; i < n; i++)
                walk[i] = true;
            if (root.TryGetProperty("walkable", out JsonElement we) && we.ValueKind == JsonValueKind.Array)
            {
                int len = Math.Min(we.GetArrayLength(), n);
                for (int i = 0; i < len; i++)
                {
                    JsonElement el = we[i];
                    if (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False)
                        walk[i] = el.GetBoolean();
                }
            }

            string bgPath = null;
            if (root.TryGetProperty("backgroundImagePath", out JsonElement bg) &&
                bg.ValueKind == JsonValueKind.String)
            {
                string s = bg.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    bgPath = s.Trim();
            }

            map = new EditorMapData
            {
                Schema = CurrentSchema,
                Width = w,
                Height = h,
                Terrain = terrain,
                Objects = objects,
                Herbs = herbs,
                Walkable = walk,
                BackgroundImagePath = bgPath
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string[] ParseStringCellArray(JsonElement root, string name, int n)
    {
        var arr = new string[n];
        for (int i = 0; i < n; i++)
            arr[i] = "";

        if (!root.TryGetProperty(name, out JsonElement el) || el.ValueKind != JsonValueKind.Array)
            return arr;

        int len = Math.Min(el.GetArrayLength(), n);
        for (int i = 0; i < len; i++)
        {
            JsonElement item = el[i];
            if (item.ValueKind == JsonValueKind.String)
                arr[i] = item.GetString() ?? "";
        }

        return arr;
    }
}
