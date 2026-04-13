using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace TheAlchemist.World;

/// <summary>Loads every image in <c>Content/art/{tiles|objects|herbs|portraits}/</c> from disk (no MGCB entry per file).</summary>
public static class EditorLooseArtLoader
{
    public static string ArtRoot => Path.Combine(AppContext.BaseDirectory, "Content", "art");

    /// <summary>Relative id uses forward slashes, no extension (e.g. <c>ruins/stone</c> for nested files).</summary>
    public static Dictionary<string, Texture2D> LoadCategory(GraphicsDevice device, string categoryFolderName,
        out List<string> sortedIds)
    {
        sortedIds = new List<string>();
        var dict = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        string root = Path.Combine(ArtRoot, categoryFolderName);
        if (!Directory.Exists(root))
            return dict;

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file);
            if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                continue;

            string id = MakeId(root, file);
            if (dict.ContainsKey(id))
                continue;

            try
            {
                using FileStream stream = File.OpenRead(file);
                Texture2D tex = Texture2D.FromStream(device, stream);
                dict[id] = tex;
            }
            catch
            {
                // skip unreadable files
            }
        }

        sortedIds = new List<string>(dict.Keys);
        sortedIds.Sort(StringComparer.OrdinalIgnoreCase);
        return dict;
    }

    private static string MakeId(string categoryRoot, string fullPath)
    {
        string rel = Path.GetRelativePath(categoryRoot, fullPath);
        rel = Path.ChangeExtension(rel, null);
        return rel.Replace('\\', '/');
    }
}
