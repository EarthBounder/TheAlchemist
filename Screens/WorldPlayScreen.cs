using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheAlchemist.Core;
using TheAlchemist.World;

namespace TheAlchemist.Screens;

public sealed class WorldPlayScreen : IGameScreen
{
    /// <summary>Hero art is 108×108; occupies 3×3 tiles at 36px tile size. Feet at bottom-center of player tile.</summary>
    private const int HeroFootprintTiles = 3;

    private const float MoveHoldInitialSeconds = 0.2f;
    private const float MoveHoldRepeatSeconds = 0.09f;

    private readonly Game1 _game;
    private readonly WorldState _world;
    private readonly ProcTileMap _procMap;
    private readonly EditorMapData _customMap;
    private readonly Dictionary<string, Texture2D> _terrainPlayTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _objectPlayTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _herbPlayTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Texture2D _customMapBackground;
    private readonly List<(int x, int y)> _clickPath = new();

    private int _heldMoveDx;
    private int _heldMoveDy;
    private float _moveStepCooldown;

    public WorldPlayScreen(Game1 game, WorldState world, EditorMapData customMap = null)
    {
        _game = game;
        _world = world;
        _customMap = customMap;

        if (_customMap != null)
        {
            _procMap = null;
            world.EnsureCompatibleWithCustomMap(_customMap);
            LoadPlayTextures(game.GraphicsDevice);
            _customMapBackground = TryLoadBackgroundTexture(game.GraphicsDevice, _customMap.BackgroundImagePath);
        }
        else
        {
            _procMap = new ProcTileMap(world.Seed);
            world.EnsureCompatibleWithMap(_procMap);
            _procMap.ApplyPickedFlowers(world.PickedFlowerCells);
            _customMapBackground = null;
        }
    }

    private static Texture2D TryLoadBackgroundTexture(GraphicsDevice gd, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            using FileStream fs = File.OpenRead(path);
            return Texture2D.FromStream(gd, fs);
        }
        catch
        {
            return null;
        }
    }

    private void LoadPlayTextures(GraphicsDevice gd)
    {
        foreach (var kv in EditorLooseArtLoader.LoadCategory(gd, "tiles", out _))
            _terrainPlayTex[kv.Key] = kv.Value;
        foreach (var kv in EditorLooseArtLoader.LoadCategory(gd, "objects", out _))
            _objectPlayTex[kv.Key] = kv.Value;
        foreach (var kv in EditorLooseArtLoader.LoadCategory(gd, "herbs", out _))
            _herbPlayTex[kv.Key] = kv.Value;
    }

    private TileTerrain CellTerrain(int tx, int ty)
    {
        if (_procMap != null)
            return _procMap.Get(tx, ty);
        int i = _customMap.Pack(tx, ty);
        return TerrainRules.AsTileTerrain(_customMap.Terrain[i]);
    }

    private bool CellWalkable(int tx, int ty)
    {
        if (_procMap != null)
            return _procMap.IsWalkable(tx, ty);
        return EditorMapData.IsCellWalkableForPlay(_customMap, tx, ty);
    }

    /// <summary>Custom maps: herb sprites block by footprint; pick from orthogonally adjacent to any footprint tile (Confirm / gamepad A).</summary>
    private void TryPickAdjacentHerb(in UiFrameInput input)
    {
        if (_customMap == null || !input.ConfirmPressed)
            return;

        _world.InventoryHerbIds ??= new List<string>();
        int px = _world.PlayerTileX;
        int py = _world.PlayerTileY;

        bool has = false;
        int bestAx = 0, bestAy = 0;
        string bestHid = null;

        for (int ay = 0; ay < _customMap.Height; ay++)
        for (int ax = 0; ax < _customMap.Width; ax++)
        {
            int ai = _customMap.Pack(ax, ay);
            string hid = _customMap.Herbs[ai];
            if (string.IsNullOrEmpty(hid))
                continue;

            OverlayFootprintLayout.TryGetCoverageForId("herbs", hid, out int fw, out int fh);
            if (!OverlayFootprintLayout.IsOrthoAdjacentToFootprint(px, py, ax, ay, fw, fh))
                continue;

            if (!has || ay < bestAy || (ay == bestAy && ax < bestAx))
            {
                has = true;
                bestAx = ax;
                bestAy = ay;
                bestHid = hid;
            }
        }

        if (!has)
            return;

        _world.InventoryHerbIds.Add(bestHid);
        _world.HerbsCollected++;
        _customMap.Herbs[_customMap.Pack(bestAx, bestAy)] = "";
        EditorMapData.RestoreWalkAfterRemovingOverlayAt(_customMap, bestAx, bestAy, "herbs", bestHid);

        _game.SaveWorld(_world);
    }

    private void ClearFlowerAt(int tx, int ty)
    {
        if (_procMap != null)
        {
            _procMap.Set(tx, ty, TileTerrain.Grass);
            _world.PickedFlowerCells.Add(ProcTileMap.Pack(tx, ty));
            return;
        }

        int i = _customMap.Pack(tx, ty);
        _customMap.Terrain[i] = EditorMapData.DefaultTerrainId;
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        if (input.EscapePressed)
        {
            _game.SaveWorld(_world);
            _game.ShowPause(_world);
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _moveStepCooldown -= dt;

        TryPickAdjacentHerb(in input);

        var kb = Keyboard.GetState();

        if (input.PointerPressed)
            TryBeginClickPath(input.PointerVirtual);

        int dx = 0, dy = 0;
        ResolveHeldMoveDirection(kb, out dx, out dy);

        if (dx != 0 || dy != 0)
        {
            bool directionChanged = dx != _heldMoveDx || dy != _heldMoveDy;
            _heldMoveDx = dx;
            _heldMoveDy = dy;

            _clickPath.Clear();

            if (directionChanged)
            {
                TryMove(dx, dy);
                _moveStepCooldown = MoveHoldInitialSeconds;
            }
            else if (_moveStepCooldown <= 0f)
            {
                TryMove(dx, dy);
                _moveStepCooldown = MoveHoldRepeatSeconds;
            }
        }
        else
        {
            _heldMoveDx = 0;
            _heldMoveDy = 0;
            TryAdvanceClickPath();
        }
    }

    private static void ResolveHeldMoveDirection(KeyboardState kb, out int dx, out int dy)
    {
        dx = 0;
        dy = 0;
        if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))
            dy = -1;
        else if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))
            dy = 1;
        else if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))
            dx = -1;
        else if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right))
            dx = 1;
    }

    private void TryBeginClickPath(Vector2 pointerVirtual)
    {
        int px = _world.PlayerTileX;
        int py = _world.PlayerTileY;
        GetRoomViewOrigin(px, py, out int originX, out int originY, out int roomOx, out int roomOy, out int roomEndX,
            out int roomEndY);

        int ts = ProcTileMap.TileSizePixels;
        float mx = pointerVirtual.X - originX;
        float my = pointerVirtual.Y - originY;
        int tx = (int)Math.Floor(mx / ts);
        int ty = (int)Math.Floor(my / ts);

        if (tx < roomOx || tx >= roomEndX || ty < roomOy || ty >= roomEndY)
            return;

        if (tx == px && ty == py)
        {
            _clickPath.Clear();
            return;
        }

        if (!BuildWalkPath(px, py, tx, ty, _clickPath))
            _clickPath.Clear();
        else
            _moveStepCooldown = 0f;
    }

    private void TryAdvanceClickPath()
    {
        if (_clickPath.Count == 0 || _moveStepCooldown > 0f)
            return;

        int px = _world.PlayerTileX;
        int py = _world.PlayerTileY;
        var (nx, ny) = _clickPath[0];
        int mdx = nx - px;
        int mdy = ny - py;
        int manhattan = Math.Abs(mdx) + Math.Abs(mdy);
        if (manhattan != 1)
        {
            _clickPath.Clear();
            return;
        }

        int beforeX = px;
        int beforeY = py;
        TryMove(mdx, mdy);
        if (_world.PlayerTileX == beforeX && _world.PlayerTileY == beforeY)
        {
            _clickPath.Clear();
            return;
        }

        if (_world.PlayerTileX == nx && _world.PlayerTileY == ny)
            _clickPath.RemoveAt(0);

        _moveStepCooldown = MoveHoldRepeatSeconds;
    }

    private bool BuildWalkPath(int sx, int sy, int gx, int gy, List<(int x, int y)> outPath)
    {
        outPath.Clear();
        if (!CellWalkable(gx, gy))
            return false;

        int w = _customMap != null ? _customMap.Width : ProcTileMap.WidthTiles;
        int h = _customMap != null ? _customMap.Height : ProcTileMap.HeightTiles;
        int total = w * h;
        var prev = new int[total];
        for (int i = 0; i < total; i++)
            prev[i] = -1;

        int PackLocal(int x, int y) => x + y * w;
        void UnpackLocal(int p, out int x, out int y)
        {
            x = p % w;
            y = p / w;
        }

        int goal = PackLocal(gx, gy);
        int start = PackLocal(sx, sy);
        if (start == goal)
            return true;

        var q = new Queue<int>();
        q.Enqueue(start);
        prev[start] = start;

        while (q.Count > 0)
        {
            int cur = q.Dequeue();
            UnpackLocal(cur, out int cx, out int cy);

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + CardinalDx(i);
                int ny = cy + CardinalDy(i);
                if ((uint)nx >= (uint)w || (uint)ny >= (uint)h)
                    continue;
                if (!CellWalkable(nx, ny))
                    continue;

                int np = PackLocal(nx, ny);
                if (prev[np] >= 0)
                    continue;

                prev[np] = cur;
                if (np == goal)
                {
                    q.Clear();
                    break;
                }

                q.Enqueue(np);
            }
        }

        if (prev[goal] < 0)
            return false;

        var stack = new Stack<(int x, int y)>();
        int p = goal;
        while (p != start)
        {
            UnpackLocal(p, out int x, out int y);
            stack.Push((x, y));
            p = prev[p];
        }

        while (stack.Count > 0)
            outPath.Add(stack.Pop());

        return true;
    }

    private static int CardinalDx(int i) =>
        i switch { 0 => 0, 1 => 0, 2 => -1, _ => 1 };

    private static int CardinalDy(int i) =>
        i switch { 0 => -1, 1 => 1, 2 => 0, _ => 0 };

    private static void GetRoomViewOrigin(int playerTileX, int playerTileY, out int originX, out int originY,
        out int roomOx, out int roomOy, out int roomEndX, out int roomEndY)
    {
        int ts = ProcTileMap.TileSizePixels;
        RoomView.GetRoomIndices(playerTileX, playerTileY, out int roomIx, out int roomIy);
        RoomView.GetRoomOriginTiles(roomIx, roomIy, out roomOx, out roomOy);

        int roomPixelW = RoomView.WidthTiles * ts;
        int roomPixelH = RoomView.HeightTiles * ts;
        originX = (GameConfig.DesignWidth - roomPixelW) / 2 - roomOx * ts;
        originY = (GameConfig.DesignHeight - roomPixelH) / 2 - roomOy * ts;
        roomEndX = roomOx + RoomView.WidthTiles;
        roomEndY = roomOy + RoomView.HeightTiles;
    }

    private void TryMove(int dx, int dy)
    {
        int nx = _world.PlayerTileX + dx;
        int ny = _world.PlayerTileY + dy;
        if (!CellWalkable(nx, ny))
            return;

        _world.PlayerTileX = nx;
        _world.PlayerTileY = ny;
        _world.InventoryHerbIds ??= new List<string>();

        if (CellTerrain(nx, ny) == TileTerrain.Flower)
        {
            _world.HerbsCollected++;
            _world.InventoryHerbIds.Add("flower");
            ClearFlowerAt(nx, ny);
            _game.SaveWorld(_world);
        }
    }

    private static string FormatInventorySuffix(WorldState world)
    {
        world.InventoryHerbIds ??= new List<string>();
        if (world.InventoryHerbIds.Count == 0)
            return "";

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in world.InventoryHerbIds)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            counts.TryGetValue(id, out int c);
            counts[id] = c + 1;
        }

        var parts = new List<string>();
        foreach (var kv in counts)
            parts.Add($"{kv.Key} x{kv.Value}");

        string s = string.Join(", ", parts);
        if (s.Length > 64)
            s = s.Substring(0, 61) + "...";

        return "  |  Inv: " + s;
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var pal = _game.UiPalette;
        var pixel = _game.Pixel;
        var font = _game.UiFont;
        int ts = ProcTileMap.TileSizePixels;
        int px = _world.PlayerTileX;
        int py = _world.PlayerTileY;

        GetRoomViewOrigin(px, py, out int originX, out int originY, out int roomOx, out int roomOy, out int roomEndX,
            out int roomEndY);
        RoomView.GetRoomIndices(px, py, out int roomIx, out int roomIy);

        bool customBg = _customMap != null && _customMapBackground != null;
        if (customBg)
        {
            int mapPxW = _customMap.Width * ts;
            int mapPxH = _customMap.Height * ts;
            spriteBatch.Draw(_customMapBackground, new Rectangle(originX, originY, mapPxW, mapPxH), Color.White);
        }

        Color terrainTint = customBg ? Color.White * 0.82f : Color.White;

        for (int ty = roomOy; ty < roomEndY; ty++)
        for (int tx = roomOx; tx < roomEndX; tx++)
        {
            int sx = originX + tx * ts;
            int sy = originY + ty * ts;
            int idx = _customMap != null ? _customMap.Pack(tx, ty) : 0;

            if (_customMap != null)
            {
                string tid = _customMap.Terrain[idx];
                var fill = TerrainFallbackColor(tid, pal);
                var cell = new Rectangle(sx, sy, ts, ts);
                if (!customBg)
                    spriteBatch.Draw(pixel, cell, fill);
                else
                    spriteBatch.Draw(pixel, cell, fill * 0.22f);
                if (_terrainPlayTex.TryGetValue(tid, out Texture2D tt) && tt != null)
                    DrawCover(spriteBatch, tt, cell, 1f, terrainTint);
            }
            else
            {
                var t = _procMap.Get(tx, ty);
                var col = TileColor(t, pal);
                spriteBatch.Draw(pixel, new Rectangle(sx, sy, ts, ts), col);
            }

            spriteBatch.Draw(pixel, new Rectangle(sx, sy, ts, 1), pal.ButtonBorder * 0.35f);
            spriteBatch.Draw(pixel, new Rectangle(sx, sy + ts - 1, ts, 1), pal.ButtonBorder * 0.35f);
            spriteBatch.Draw(pixel, new Rectangle(sx, sy, 1, ts), pal.ButtonBorder * 0.35f);
            spriteBatch.Draw(pixel, new Rectangle(sx + ts - 1, sy, 1, ts), pal.ButtonBorder * 0.35f);
        }

        if (_customMap != null)
        {
            for (int ty = roomOy; ty < roomEndY; ty++)
            for (int tx = roomOx; tx < roomEndX; tx++)
            {
                int sx = originX + tx * ts;
                int sy = originY + ty * ts;
                int idx = _customMap.Pack(tx, ty);

                string oid = _customMap.Objects[idx];
                if (!string.IsNullOrEmpty(oid) && _objectPlayTex.TryGetValue(oid, out Texture2D ot) && ot != null)
                    MapOverlayDraw.DrawAnchoredTopLeft(spriteBatch, ot, sx, sy);

                string hid = _customMap.Herbs[idx];
                if (!string.IsNullOrEmpty(hid) && _herbPlayTex.TryGetValue(hid, out Texture2D ht) && ht != null)
                    MapOverlayDraw.DrawAnchoredTopLeft(spriteBatch, ht, sx, sy);
            }
        }

        int psx = originX + px * ts;
        int psy = originY + py * ts;
        var hero = _game.HeroSprite;
        int dest = HeroFootprintTiles * ts;
        float scale = dest / (float)Math.Max(1, Math.Max(hero.Width, hero.Height));
        int dw = Math.Max(1, (int)Math.Round(hero.Width * scale));
        int dh = Math.Max(1, (int)Math.Round(hero.Height * scale));
        int feetX = psx + ts / 2;
        int feetY = psy + ts;
        int ox = feetX - dw / 2;
        int oy = feetY - dh;
        spriteBatch.Draw(hero, new Rectangle(ox, oy, dw, dh), Color.White);

        string inv = FormatInventorySuffix(_world);
        string hud = _customMap != null
            ? $"Herbs {_world.HerbsCollected}{inv}  |  Test map  |  Room {roomIx + 1},{roomIy + 1}/{ProcTileMap.RoomsWide},{ProcTileMap.RoomsHigh}  |  WASD / hold  |  click tile  |  Confirm: pick herb  |  Esc"
            : $"Herbs {_world.HerbsCollected}{inv}  |  Room {roomIx + 1},{roomIy + 1}/{ProcTileMap.RoomsWide},{ProcTileMap.RoomsHigh}  |  Seed {_world.Seed}  |  WASD / hold  |  click tile  |  Esc";
        spriteBatch.DrawString(font, hud, new Vector2(16, GameConfig.DesignHeight - 36), pal.PrimaryWhite, 0f,
            Vector2.Zero, 0.72f, SpriteEffects.None, 0f);
    }

    private static void DrawCover(SpriteBatch spriteBatch, Texture2D tex, Rectangle cell, float cover, Color? tint = null)
    {
        Color c = tint ?? Color.White;
        int inner = Math.Max(1, (int)(Math.Min(cell.Width, cell.Height) * cover));
        float sc = inner / (float)Math.Max(1, Math.Max(tex.Width, tex.Height));
        int dw = Math.Max(1, (int)Math.Round(tex.Width * sc));
        int dh = Math.Max(1, (int)Math.Round(tex.Height * sc));
        int cx = cell.X + (cell.Width - dw) / 2;
        int cy = cell.Y + (cell.Height - dh) / 2;
        spriteBatch.Draw(tex, new Rectangle(cx, cy, dw, dh), c);
    }

    private static Color TerrainFallbackColor(string id, UiThemePalette pal)
    {
        return TileColor(TerrainRules.AsTileTerrain(id), pal);
    }

    private static Color TileColor(TileTerrain t, UiThemePalette pal) =>
        t switch
        {
            TileTerrain.Grass => Color.Lerp(new Color(0.25f, 0.48f, 0.28f), pal.ButtonFill, 0.15f),
            TileTerrain.Dirt => new Color(0.42f, 0.30f, 0.18f),
            TileTerrain.Water => new Color(0.18f, 0.32f, 0.55f),
            TileTerrain.Flower => new Color(0.55f, 0.22f, 0.45f),
            _ => pal.ButtonFill
        };
}
