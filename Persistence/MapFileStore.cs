using System;
using System.Collections.Generic;
using System.IO;
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

    public static string MapsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheAlchemist", "Maps");

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

    public static bool TryLoad(int slot, out EditorMapData map)
    {
        map = null;
        try
        {
            string path = PathForSlot(slot);
            if (!File.Exists(path))
                return false;
            string json = File.ReadAllText(path);
            if (!EditorMapData.TryParseMapJson(json, out var m))
                return false;
            if (m.Width != ProcTileMap.WidthTiles || m.Height != ProcTileMap.HeightTiles)
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
