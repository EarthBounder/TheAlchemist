using System;
using System.IO;
using System.Text.Json;
using TheAlchemist.World;

namespace TheAlchemist.Persistence;

public static class SaveStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SaveFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TheAlchemist",
            "world.json");

    public static bool Exists() => File.Exists(SaveFilePath);

    public static bool TrySave(WorldState world)
    {
        try
        {
            string dir = Path.GetDirectoryName(SaveFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(SaveFilePath, JsonSerializer.Serialize(world, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryLoad(out WorldState world)
    {
        world = null;
        try
        {
            if (!File.Exists(SaveFilePath))
                return false;
            string json = File.ReadAllText(SaveFilePath);
            var w = JsonSerializer.Deserialize<WorldState>(json, JsonOptions);
            if (w == null)
                return false;
            if (w.PickedFlowerCells == null)
                w.PickedFlowerCells = new System.Collections.Generic.List<int>();
            if (w.InventoryHerbIds == null)
                w.InventoryHerbIds = new System.Collections.Generic.List<string>();
            world = w;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
