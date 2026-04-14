using System;
using System.IO;

namespace TheAlchemist.World;

/// <summary>Reads pixel width/height from loose art files under <see cref="EditorLooseArtLoader.ArtRoot"/> without loading GPU textures.</summary>
public static class LooseArtImageDimensions
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };

    public static bool TryGetPixelSize(string categoryFolderName, string id, out int width, out int height)
    {
        width = height = 0;
        if (string.IsNullOrWhiteSpace(categoryFolderName) || string.IsNullOrWhiteSpace(id))
            return false;

        string rel = id.Replace('\\', '/').Trim('/');
        string basePath = Path.Combine(EditorLooseArtLoader.ArtRoot, categoryFolderName, rel);
        foreach (string ext in Extensions)
        {
            string path = basePath + ext;
            if (!File.Exists(path))
                continue;
            if (TryReadImageDimensions(path, out width, out height))
                return true;
        }

        return false;
    }

    public static bool TryReadImageDimensions(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            using FileStream fs = File.OpenRead(path);
            Span<byte> sig = stackalloc byte[8];
            if (fs.Read(sig) < 8)
                return false;

            if (sig[0] == 0x89 && sig[1] == 0x50 && sig[2] == 0x4E && sig[3] == 0x47)
            {
                Span<byte> ihdr = stackalloc byte[16];
                if (fs.Read(ihdr) < 16)
                    return false;
                width = ReadBigEndianInt32(ihdr.Slice(8, 4));
                height = ReadBigEndianInt32(ihdr.Slice(12, 4));
                return width > 0 && height > 0;
            }

            if (sig[0] == 0xFF && sig[1] == 0xD8)
            {
                fs.Position = 2;
                return TryReadJpegDimensions(fs, out width, out height);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool TryReadJpegDimensions(FileStream fs, out int width, out int height)
    {
        width = height = 0;
        int b1 = fs.ReadByte();
        while (b1 >= 0)
        {
            int b2 = fs.ReadByte();
            if (b2 < 0)
                return false;
            if (b1 != 0xFF || b2 is 0xFF or 0x00)
            {
                b1 = b2;
                continue;
            }

            int marker = b2;
            if (marker is 0xD9 or 0xDA)
                return false;

            int lenHi = fs.ReadByte();
            int lenLo = fs.ReadByte();
            if (lenHi < 0 || lenLo < 0)
                return false;
            int segLen = (lenHi << 8) | lenLo;
            if (segLen < 2)
                return false;
            int dataLen = segLen - 2;

            if (marker is >= 0xC0 and <= 0xCF && marker is not 0xC4 and not 0xC8 and not 0xCC)
            {
                Span<byte> buf = stackalloc byte[16];
                int n = fs.Read(buf.Slice(0, Math.Min(buf.Length, dataLen)));
                if (n < 5)
                    return false;
                height = (buf[1] << 8) | buf[2];
                width = (buf[3] << 8) | buf[4];
                return width > 0 && height > 0;
            }

            long skip = fs.Position + dataLen;
            if (skip > fs.Length || skip < fs.Position)
                return false;
            fs.Position = skip;
            b1 = fs.ReadByte();
        }

        return false;
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> b) =>
        (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
}
