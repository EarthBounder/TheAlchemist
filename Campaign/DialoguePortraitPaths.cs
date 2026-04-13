using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace TheAlchemist.Campaign;

/// <summary>Resolves portrait filenames under <c>Content/art/portraits</c> from dialogue JSON.</summary>
public static class DialoguePortraitPaths
{
    public static string PortraitsDirectory =>
        Path.Combine(AppContext.BaseDirectory, "Content", "art", "portraits");

    /// <summary>Whether a non-empty speaker has a portrait file or line override defined (does not require file on disk).</summary>
    public static bool HasPortraitDefined(DialogueSegment segment, DialogueLine line)
    {
        if (line == null)
            return false;
        if (!string.IsNullOrWhiteSpace(line.Portrait))
            return true;
        if (string.IsNullOrWhiteSpace(line.Speaker))
            return true;

        if (segment?.SpeakerPortraits == null || segment.SpeakerPortraits.Count == 0)
            return false;

        foreach (var kv in segment.SpeakerPortraits)
        {
            if (string.Equals(kv.Key, line.Speaker, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(kv.Value))
                return true;
        }

        return false;
    }

    /// <summary>Portrait file path under portraits folder: line <see cref="DialogueLine.Portrait"/> wins, else <see cref="DialogueSegment.SpeakerPortraits"/> for <see cref="DialogueLine.Speaker"/>.</summary>
    public static bool TryResolvePortraitFile(DialogueSegment segment, DialogueLine line, out string absolutePath)
    {
        absolutePath = null;
        if (line == null)
            return false;

        string rel = line.Portrait?.Trim();
        if (string.IsNullOrEmpty(rel) && !string.IsNullOrWhiteSpace(line.Speaker) && segment?.SpeakerPortraits != null)
        {
            foreach (var kv in segment.SpeakerPortraits)
            {
                if (!string.Equals(kv.Key, line.Speaker, StringComparison.OrdinalIgnoreCase))
                    continue;
                rel = kv.Value?.Trim();
                break;
            }
        }

        if (string.IsNullOrEmpty(rel))
            return false;

        rel = rel.Replace('\\', '/').TrimStart('/');
        if (rel.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(rel))
            return false;

        foreach (string part in rel.Split('/'))
        {
            if (string.IsNullOrEmpty(part) || part == "." || part == "..")
                return false;
        }

        if (!HasKnownImageExtension(rel))
            rel += ".png";

        string dir = Path.GetFullPath(PortraitsDirectory);
        string relOs = rel.Replace('/', Path.DirectorySeparatorChar);
        string full = Path.GetFullPath(Path.Combine(dir, relOs));
        if (!full.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full, dir, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!File.Exists(full))
            return false;

        absolutePath = full;
        return true;
    }

    private static bool HasKnownImageExtension(string path)
    {
        string e = Path.GetExtension(path);
        return e.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               e.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               e.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Loads the resolved portrait from disk; caller owns disposal of <paramref name="texture"/>.</summary>
    public static bool TryLoadPortraitTexture(GraphicsDevice device, DialogueSegment segment, DialogueLine line,
        out Texture2D texture)
    {
        texture = null;
        if (!TryResolvePortraitFile(segment, line, out string path))
            return false;
        try
        {
            using FileStream stream = File.OpenRead(path);
            texture = Texture2D.FromStream(device, stream);
            return texture != null;
        }
        catch
        {
            texture?.Dispose();
            texture = null;
            return false;
        }
    }
}
