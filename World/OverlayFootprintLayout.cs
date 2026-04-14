using System;
using System.IO;

namespace TheAlchemist.World;

/// <summary>Object/herb sprites anchor at a cell's top-left and span <see cref="ProcTileMap.TileSizePixels"/>-sized tiles like <see cref="MapOverlayDraw"/>.</summary>
public static class OverlayFootprintLayout
{
    public static void GetCoverageTiles(int textureWidthPx, int textureHeightPx, out int tilesW, out int tilesH)
    {
        int ts = ProcTileMap.TileSizePixels;
        tilesW = Math.Max(1, (int)Math.Ceiling(textureWidthPx / (float)ts));
        tilesH = Math.Max(1, (int)Math.Ceiling(textureHeightPx / (float)ts));
    }

    public static bool TryGetCoverageForId(string categoryFolderName, string id, out int tilesW, out int tilesH)
    {
        if (!LooseArtImageDimensions.TryGetPixelSize(categoryFolderName, id, out int pw, out int ph))
        {
            tilesW = tilesH = 1;
            return false;
        }

        GetCoverageTiles(pw, ph, out tilesW, out tilesH);
        return true;
    }

    public static bool CellInsideFootprint(int cellX, int cellY, int anchorX, int anchorY, int footprintTilesW,
        int footprintTilesH) =>
        cellX >= anchorX && cellX < anchorX + footprintTilesW &&
        cellY >= anchorY && cellY < anchorY + footprintTilesH;

    /// <summary>True when <paramref name="playerX"/>,<paramref name="playerY"/> is orthogonally adjacent to any tile in the footprint.</summary>
    public static bool IsOrthoAdjacentToFootprint(int playerX, int playerY, int anchorX, int anchorY, int footprintTilesW,
        int footprintTilesH)
    {
        for (int yy = anchorY; yy < anchorY + footprintTilesH; yy++)
        for (int xx = anchorX; xx < anchorX + footprintTilesW; xx++)
        {
            int md = Math.Abs(playerX - xx) + Math.Abs(playerY - yy);
            if (md == 1)
                return true;
        }

        return false;
    }

    public static void EnsureMaxCoverageIndexed()
    {
        if (_indexed)
            return;
        _indexed = true;
        _maxCoverW = 1;
        _maxCoverH = 1;

        foreach (string category in new[] { "objects", "herbs" })
        {
            string root = Path.Combine(EditorLooseArtLoader.ArtRoot, category);
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file);
                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!LooseArtImageDimensions.TryReadImageDimensions(file, out int pw, out int ph))
                    continue;

                GetCoverageTiles(pw, ph, out int tw, out int th);
                if (tw > _maxCoverW)
                    _maxCoverW = tw;
                if (th > _maxCoverH)
                    _maxCoverH = th;
            }
        }
    }

    public static void GetAnchorSearchBand(out int deltaAnchorsX, out int deltaAnchorsY)
    {
        EnsureMaxCoverageIndexed();
        deltaAnchorsX = _maxCoverW - 1;
        deltaAnchorsY = _maxCoverH - 1;
    }

    private static bool _indexed;
    private static int _maxCoverW = 1;
    private static int _maxCoverH = 1;
}
