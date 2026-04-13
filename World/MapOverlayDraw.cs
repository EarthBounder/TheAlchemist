using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheAlchemist.World;

/// <summary>Draws object/herb sprites at their texture pixel size from the anchor tile's top-left (e.g. 72x72 covers 2x2 tiles when <see cref="ProcTileMap.TileSizePixels"/> is 36).</summary>
public static class MapOverlayDraw
{
    public static void DrawAnchoredTopLeft(SpriteBatch spriteBatch, Texture2D texture, int anchorCellTopLeftX,
        int anchorCellTopLeftY)
    {
        spriteBatch.Draw(texture,
            new Rectangle(anchorCellTopLeftX, anchorCellTopLeftY, texture.Width, texture.Height), Color.White);
    }
}
