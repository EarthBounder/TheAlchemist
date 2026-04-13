using System;
using System.Text.Json;

namespace TheAlchemist.World;

/// <summary>Authoring-time map: terrain, objects/herbs, per-tile walk flags, optional background path.</summary>
public sealed class EditorMapData
{
    public const int CurrentSchema = 3;

    public const string DefaultTerrainId = "grass";

    public int Schema { get; set; } = CurrentSchema;
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Ground texture id per cell (matches loose filenames under Content/art/tiles).</summary>
    public string[] Terrain { get; set; } = Array.Empty<string>();

    public string[] Objects { get; set; } = Array.Empty<string>();
    public string[] Herbs { get; set; } = Array.Empty<string>();

    /// <summary>When false, hero cannot enter. Object tiles are forced false in <see cref="NormalizeInPlace"/>.</summary>
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

    /// <summary>Hero movement: terrain (e.g. water), walk mask, and objects block.</summary>
    public static bool IsCellWalkableForPlay(EditorMapData map, int x, int y)
    {
        if (map == null || !map.Contains(x, y))
            return false;
        map.NormalizeInPlace();
        int i = map.Pack(x, y);
        if (!string.IsNullOrEmpty(map.Objects[i]))
            return false;
        if (!map.Walkable[i])
            return false;
        return TerrainRules.IsWalkableTerrainId(map.Terrain[i]);
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

        int ow = Math.Min(Objects.Length, Walkable.Length);
        for (int i = 0; i < ow; i++)
        {
            if (!string.IsNullOrEmpty(Objects[i]))
                Walkable[i] = false;
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
