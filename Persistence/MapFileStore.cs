using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using TheAlchemist.World;

namespace TheAlchemist.Persistence;

public static class MapFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Authoring maps next to the game: <c>Content/maps</c> under <see cref="AppContext.BaseDirectory"/>.</summary>
    public static string MapsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Content", "maps");

    public static string PathForSlot(int slot)
    {
        slot = Math.Clamp(slot, 0, 99);
        return Path.Combine(MapsDirectory, $"map_{slot:D2}.json");
    }

    public static bool SlotOccupied(int slot) => File.Exists(PathForSlot(slot));

    /// <summary>First slot index in [0, <paramref name="maxSlotExclusive"/>) with no file, or last index if all used.</summary>
    public static int FindNextEmptySlot(int maxSlotExclusive = 100)
    {
        maxSlotExclusive = Math.Clamp(maxSlotExclusive, 1, 100);
        for (int i = 0; i < maxSlotExclusive; i++)
        {
            if (!SlotOccupied(i))
                return i;
        }

        return maxSlotExclusive - 1;
    }

    public static string SanitizeMapBaseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "untitled";
        var sb = new StringBuilder();
        foreach (char c in name.Trim())
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '_' or '-')
                sb.Append(c);
            else if (char.IsWhiteSpace(c))
                sb.Append('_');
        }

        string s = sb.ToString().Trim('_');
        if (s.Length == 0)
            return "untitled";
        if (s.Length > 80)
            s = s.Substring(0, 80);
        return s;
    }

    public static bool TrySave(EditorMapData map, int slot)
    {
        map.Schema = EditorMapData.CurrentSchema;
        map.NormalizeInPlace();
        try
        {
            Directory.CreateDirectory(MapsDirectory);
            string path = PathForSlot(slot);
            File.WriteAllText(path, JsonSerializer.Serialize(map, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TrySaveNamed(EditorMapData map, string displayName)
    {
        map.Schema = EditorMapData.CurrentSchema;
        map.NormalizeInPlace();
        try
        {
            Directory.CreateDirectory(MapsDirectory);
            string baseName = SanitizeMapBaseName(displayName);
            string path = Path.Combine(MapsDirectory, baseName + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(map, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryLoad(int slot, out EditorMapData map) => TryLoadFromPath(PathForSlot(slot), out map);

    /// <summary>Loads <paramref name="mapFile"/> from <see cref="MapsDirectory"/> (file name only, e.g. <c>my_map.json</c> or <c>map_00.json</c>).</summary>
    public static bool TryLoadMapFile(string mapFile, out EditorMapData map)
    {
        map = null;
        if (string.IsNullOrWhiteSpace(mapFile))
            return false;

        string name = Path.GetFileName(mapFile.Trim());
        if (name.Length == 0 || name.Contains("..", StringComparison.Ordinal))
            return false;
        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            name += ".json";

        string dir = Path.GetFullPath(MapsDirectory);
        string path = Path.GetFullPath(Path.Combine(dir, name));
        if (!path.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return false;

        return TryLoadFromPath(path, out map);
    }

    private static bool TryLoadFromPath(string path, out EditorMapData map)
    {
        map = null;
        try
        {
            if (!File.Exists(path))
                return false;
            string json = File.ReadAllText(path);
            if (!EditorMapData.TryParseMapJson(json, out var m))
                return false;
            if (!EditorMapData.IsRoomGridSize(m.Width, m.Height))
                return false;
            m.NormalizeInPlace();
            map = m;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<int> ListOccupiedSlots(int maxSlot = 32)
    {
        var list = new List<int>();
        try
        {
            if (!Directory.Exists(MapsDirectory))
                return list;
            for (int i = 0; i < maxSlot; i++)
            {
                if (File.Exists(PathForSlot(i)))
                    list.Add(i);
            }
        }
        catch
        {
            // ignore
        }

        return list;
    }
}
