using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheAlchemist.World;

/// <summary>Draws object/herb sprites at their texture pixel size from the anchor tile's top-left (e.g. 72x72 covers 2x2 tiles when <see cref="ProcTileMap.TileSizePixels"/> is 36).</summary>
public static class MapOverlayDraw
{
    public static void DrawAnchoredTopLeft(SpriteBatch spriteBatch, Texture2D texture, int anchorCellTopLeftX,
        int anchorCellTopLeftY, float cellSizePixels = 36f)
    {
        float s = cellSizePixels / ProcTileMap.TileSizePixels;
        int dw = Math.Max(1, (int)Math.Round(texture.Width * s));
        int dh = Math.Max(1, (int)Math.Round(texture.Height * s));
        spriteBatch.Draw(texture, new Rectangle(anchorCellTopLeftX, anchorCellTopLeftY, dw, dh), Color.White);
    }
}
