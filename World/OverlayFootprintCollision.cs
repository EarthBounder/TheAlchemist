namespace TheAlchemist.World;

/// <summary>Footprint overlap for object/herb layers (anchor = top-left cell of sprite).</summary>
public static class OverlayFootprintCollision
{
    public static bool CellOverlapsAnchoredFootprint(EditorMapData map, int cellX, int cellY, string[] layer,
        string categoryFolderName)
    {
        if (map == null || layer == null || !map.Contains(cellX, cellY))
            return false;

        map.NormalizeInPlace();
        OverlayFootprintLayout.GetAnchorSearchBand(out int bandAx, out int bandAy);

        int axMin = System.Math.Max(0, cellX - bandAx);
        int axMax = System.Math.Min(map.Width - 1, cellX);
        int ayMin = System.Math.Max(0, cellY - bandAy);
        int ayMax = System.Math.Min(map.Height - 1, cellY);

        for (int ay = ayMin; ay <= ayMax; ay++)
        for (int ax = axMin; ax <= axMax; ax++)
        {
            string id = layer[map.Pack(ax, ay)];
            if (string.IsNullOrEmpty(id))
                continue;

            OverlayFootprintLayout.TryGetCoverageForId(categoryFolderName, id, out int fw, out int fh);
            if (OverlayFootprintLayout.CellInsideFootprint(cellX, cellY, ax, ay, fw, fh))
                return true;
        }

        return false;
    }

    public static bool CellOverlapsAnyObjectOrHerbFootprint(EditorMapData map, int cellX, int cellY) =>
        CellOverlapsAnchoredFootprint(map, cellX, cellY, map.Objects, "objects") ||
        CellOverlapsAnchoredFootprint(map, cellX, cellY, map.Herbs, "herbs");
}
