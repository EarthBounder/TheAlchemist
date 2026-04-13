using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheAlchemist.Core;
using TheAlchemist.Persistence;
using TheAlchemist.World;

namespace TheAlchemist.Screens;

public sealed class LevelEditorScreen : IGameScreen
{
    private enum PaletteKind
    {
        Tiles,
        Objects,
        Herbs
    }

    private const int TopChromeHeight = 48;
    private const int PaletteWidth = 224;
    private const int PaletteContentTop = TopChromeHeight + 6;
    private const int GridOriginX = 236;
    private const int GridOriginY = TopChromeHeight + 12;

    private Rectangle _tbPrevSlot;
    private Rectangle _tbNextSlot;
    private Rectangle _tbSave;
    private Rectangle _tbLoad;
    private Rectangle _tbFreeSlot;
    private Rectangle _tbNewMap;
    private Rectangle _tbRun;

    private readonly Game1 _game;
    private readonly EditorMapData _map;
    private readonly Dictionary<string, Texture2D> _terrainTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _objectTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _herbTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _terrainIds = new();
    private readonly List<string> _objectIds = new();
    private readonly List<string> _herbIds = new();

    private Vector2 _viewPan;
    private PaletteKind _paletteKind = PaletteKind.Tiles;
    private int _terrainBrush;
    private int _objectBrush;
    private int _herbBrush;
    private int _slot;
    private string _status = "";
    private float _statusTime;
    private KeyboardState _keysWas;
    private MouseState _mouseWas;

    public LevelEditorScreen(Game1 game)
    {
        _game = game;
        _map = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
        LoadEditorTextures();
        ClampBrushes();
    }

    private void LoadEditorTextures()
    {
        _terrainTex.Clear();
        _objectTex.Clear();
        _herbTex.Clear();
        _terrainIds.Clear();
        _objectIds.Clear();
        _herbIds.Clear();

        GraphicsDevice gd = _game.GraphicsDevice;
        Dictionary<string, Texture2D> terrainDict =
            EditorLooseArtLoader.LoadCategory(gd, "tiles", out List<string> terrainSorted);
        foreach (var kv in terrainDict)
            _terrainTex[kv.Key] = kv.Value;
        _terrainIds.AddRange(terrainSorted);

        Dictionary<string, Texture2D> objectDict =
            EditorLooseArtLoader.LoadCategory(gd, "objects", out List<string> objectSorted);
        foreach (var kv in objectDict)
            _objectTex[kv.Key] = kv.Value;
        _objectIds.AddRange(objectSorted);

        Dictionary<string, Texture2D> herbDict =
            EditorLooseArtLoader.LoadCategory(gd, "herbs", out List<string> herbSorted);
        foreach (var kv in herbDict)
            _herbTex[kv.Key] = kv.Value;
        _herbIds.AddRange(herbSorted);
    }

    private void ClampBrushes()
    {
        if (_terrainIds.Count == 0)
            _terrainBrush = 0;
        else
            _terrainBrush = Math.Clamp(_terrainBrush, 0, _terrainIds.Count - 1);

        if (_objectIds.Count == 0)
            _objectBrush = 0;
        else
            _objectBrush = Math.Clamp(_objectBrush, 0, _objectIds.Count - 1);

        if (_herbIds.Count == 0)
            _herbBrush = 0;
        else
            _herbBrush = Math.Clamp(_herbBrush, 0, _herbIds.Count - 1);
    }

    private void LayoutToolbar()
    {
        int y = 8;
        int h = 32;
        int x = PaletteWidth + 10;
        _tbPrevSlot = new Rectangle(x, y, 30, h);
        x += 34;
        _tbNextSlot = new Rectangle(x, y, 30, h);
        x += 38;
        _tbSave = new Rectangle(x, y, 54, h);
        x += 58;
        _tbLoad = new Rectangle(x, y, 54, h);
        x += 58;
        _tbFreeSlot = new Rectangle(x, y, 72, h);
        x += 76;
        _tbNewMap = new Rectangle(x, y, 56, h);
        x += 60;
        _tbRun = new Rectangle(Math.Min(x, GameConfig.DesignWidth - 62), y, 60, h);
    }

    private void HandleToolbarClick(int mx, int my)
    {
        if (_tbPrevSlot.Contains(mx, my))
        {
            _slot = Math.Max(0, _slot - 1);
            SetStatus($"Slot {_slot:D2}");
        }
        else if (_tbNextSlot.Contains(mx, my))
        {
            _slot = Math.Min(99, _slot + 1);
            SetStatus($"Slot {_slot:D2}");
        }
        else if (_tbSave.Contains(mx, my))
        {
            if (MapFileStore.TrySave(_map, _slot))
                SetStatus($"Saved map_{_slot:D2}.json");
            else
                SetStatus("Save failed.");
        }
        else if (_tbLoad.Contains(mx, my))
        {
            if (MapFileStore.TryLoad(_slot, out var loaded))
            {
                CopyMap(loaded, _map);
                SetStatus($"Loaded map_{_slot:D2}.json");
            }
            else
                SetStatus("Load failed (missing or wrong size).");
        }
        else if (_tbFreeSlot.Contains(mx, my))
        {
            _slot = MapFileStore.FindNextEmptySlot();
            SetStatus($"Next empty slot {_slot:D2}");
        }
        else if (_tbNewMap.Contains(mx, my))
        {
            var fresh = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
            CopyMap(fresh, _map);
            SetStatus("New map (cleared).");
        }
        else if (_tbRun.Contains(mx, my))
        {
            _game.PlayTestMap(_map);
        }
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_statusTime > 0f)
            _statusTime -= dt;

        var kb = Keyboard.GetState();
        var ms = Mouse.GetState();

        bool keyPressed(Keys k) => kb.IsKeyDown(k) && !_keysWas.IsKeyDown(k);

        if (input.RawEscapePressed || input.BackPressed)
        {
            _game.ShowMainMenu();
            return;
        }

        if (keyPressed(Keys.D1) || keyPressed(Keys.NumPad1))
            _paletteKind = PaletteKind.Tiles;
        if (keyPressed(Keys.D2) || keyPressed(Keys.NumPad2))
            _paletteKind = PaletteKind.Objects;
        if (keyPressed(Keys.D3) || keyPressed(Keys.NumPad3))
            _paletteKind = PaletteKind.Herbs;

        if (keyPressed(Keys.F4))
        {
            var fresh = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
            CopyMap(fresh, _map);
            SetStatus("New map (cleared).");
        }

        if (keyPressed(Keys.OemOpenBrackets))
        {
            _slot = Math.Max(0, _slot - 1);
            SetStatus($"Slot {_slot:D2}");
        }

        if (keyPressed(Keys.OemCloseBrackets))
        {
            _slot = Math.Min(99, _slot + 1);
            SetStatus($"Slot {_slot:D2}");
        }

        if (keyPressed(Keys.F5))
        {
            if (MapFileStore.TrySave(_map, _slot))
                SetStatus($"Saved map_{_slot:D2}.json");
            else
                SetStatus("Save failed.");
        }

        if (keyPressed(Keys.F6))
        {
            if (MapFileStore.TryLoad(_slot, out var loaded))
            {
                CopyMap(loaded, _map);
                SetStatus($"Loaded map_{_slot:D2}.json");
            }
            else
                SetStatus("Load failed (missing or wrong size).");
        }

        _viewPan += input.FloorPanPixels;

        int mx = (int)input.PointerVirtual.X;
        int my = (int)input.PointerVirtual.Y;

        LayoutToolbar();

        if (input.PointerPressed && my < TopChromeHeight)
        {
            HandleToolbarClick(mx, my);
            _keysWas = kb;
            _mouseWas = ms;
            return;
        }

        if (mx < PaletteWidth && my >= TopChromeHeight)
            HandlePaletteClick(input.PointerPressed, mx, my);
        else if (mx >= PaletteWidth && my >= TopChromeHeight)
        {
            bool left = ms.LeftButton == ButtonState.Pressed;
            bool right = ms.RightButton == ButtonState.Pressed;
            bool leftEdge = left && _mouseWas.LeftButton == ButtonState.Released;
            if (TryGetTileAtPointer(input.PointerVirtual, out int tx, out int ty))
            {
                if (left && (leftEdge || !input.PointerPressed))
                    ApplyBrush(tx, ty);
                if (right)
                    EraseAt(tx, ty);
            }
        }

        ClampBrushes();

        _keysWas = kb;
        _mouseWas = ms;
    }

    private static void CopyMap(EditorMapData from, EditorMapData to)
    {
        to.Schema = from.Schema;
        to.Width = from.Width;
        to.Height = from.Height;
        to.Terrain = (string[])from.Terrain.Clone();
        to.Objects = (string[])from.Objects.Clone();
        to.Herbs = (string[])from.Herbs.Clone();
    }

    private void SetStatus(string msg)
    {
        _status = msg;
        _statusTime = 2.6f;
    }

    private void HandlePaletteClick(bool pressed, int mx, int my)
    {
        if (!pressed)
            return;

        int y = PaletteContentTop;
        var catTiles = new Rectangle(8, y, 68, 28);
        var catObjs = new Rectangle(80, y, 68, 28);
        var catHerbs = new Rectangle(152, y, 68, 28);
        if (catTiles.Contains(mx, my))
            _paletteKind = PaletteKind.Tiles;
        else if (catObjs.Contains(mx, my))
            _paletteKind = PaletteKind.Objects;
        else if (catHerbs.Contains(mx, my))
            _paletteKind = PaletteKind.Herbs;

        y += 40;
        if (_paletteKind == PaletteKind.Tiles)
        {
            for (int i = 0; i < _terrainIds.Count; i++)
            {
                var row = new Rectangle(8, y, PaletteWidth - 16, 32);
                if (row.Contains(mx, my))
                    _terrainBrush = i;
                y += 36;
            }
        }
        else if (_paletteKind == PaletteKind.Objects)
        {
            for (int i = 0; i < _objectIds.Count; i++)
            {
                var row = new Rectangle(8, y, PaletteWidth - 16, 36);
                if (row.Contains(mx, my))
                    _objectBrush = i;
                y += 40;
            }
        }
        else
        {
            for (int i = 0; i < _herbIds.Count; i++)
            {
                var row = new Rectangle(8, y, PaletteWidth - 16, 36);
                if (row.Contains(mx, my))
                    _herbBrush = i;
                y += 40;
            }
        }
    }

    private bool TryGetTileAtPointer(Vector2 pointerVirtual, out int tx, out int ty)
    {
        tx = ty = 0;
        int ts = ProcTileMap.TileSizePixels;
        float lx = pointerVirtual.X - GridOriginX - _viewPan.X;
        float ly = pointerVirtual.Y - GridOriginY - _viewPan.Y;
        tx = (int)Math.Floor(lx / ts);
        ty = (int)Math.Floor(ly / ts);
        return _map.Contains(tx, ty);
    }

    private void ApplyBrush(int tx, int ty)
    {
        int i = _map.Pack(tx, ty);
        switch (_paletteKind)
        {
            case PaletteKind.Tiles:
                if (_terrainIds.Count > 0)
                    _map.Terrain[i] = _terrainIds[_terrainBrush];
                break;
            case PaletteKind.Objects:
                if (_objectIds.Count > 0)
                    _map.Objects[i] = _objectIds[_objectBrush];
                break;
            case PaletteKind.Herbs:
                if (_herbIds.Count > 0)
                    _map.Herbs[i] = _herbIds[_herbBrush];
                break;
        }
    }

    private void EraseAt(int tx, int ty)
    {
        int i = _map.Pack(tx, ty);
        switch (_paletteKind)
        {
            case PaletteKind.Tiles:
                _map.Terrain[i] = EditorMapData.DefaultTerrainId;
                break;
            case PaletteKind.Objects:
                _map.Objects[i] = "";
                break;
            case PaletteKind.Herbs:
                _map.Herbs[i] = "";
                break;
        }
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var font = _game.UiFont;
        var pixel = _game.Pixel;
        var pal = _game.UiPalette;
        int ts = ProcTileMap.TileSizePixels;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, GameConfig.DesignWidth, TopChromeHeight), pal.ButtonFill * 0.92f);
        spriteBatch.Draw(pixel, new Rectangle(0, TopChromeHeight - 1, GameConfig.DesignWidth, 1), pal.ButtonBorder);

        LayoutToolbar();
        DrawToolbar(spriteBatch, font, pixel, pal);

        spriteBatch.DrawString(font, "Editor", new Vector2(12, 12), pal.TitleText, 0f, Vector2.Zero, 0.78f,
            SpriteEffects.None, 0f);

        DrawPalette(spriteBatch, font, pixel, pal);

        for (int ty = 0; ty < _map.Height; ty++)
        for (int tx = 0; tx < _map.Width; tx++)
        {
            int sx = GridOriginX + (int)_viewPan.X + tx * ts;
            int sy = GridOriginY + (int)_viewPan.Y + ty * ts;
            if (sx + ts < PaletteWidth || sy + ts < TopChromeHeight || sx > GameConfig.DesignWidth ||
                sy > GameConfig.DesignHeight)
                continue;

            int idx = _map.Pack(tx, ty);
            string tid = _map.Terrain[idx];
            var fill = TerrainFallbackColor(tid, pal);
            var cell = new Rectangle(sx, sy, ts, ts);
            spriteBatch.Draw(pixel, cell, fill);
            spriteBatch.Draw(pixel, new Rectangle(sx, sy, ts, 1), pal.ButtonBorder * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(sx, sy + ts - 1, ts, 1), pal.ButtonBorder * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(sx, sy, 1, ts), pal.ButtonBorder * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(sx + ts - 1, sy, 1, ts), pal.ButtonBorder * 0.25f);

            if (_terrainTex.TryGetValue(tid, out Texture2D tt) && tt != null)
                DrawCover(spriteBatch, tt, cell, 1f);
        }

        for (int ty = 0; ty < _map.Height; ty++)
        for (int tx = 0; tx < _map.Width; tx++)
        {
            int sx = GridOriginX + (int)_viewPan.X + tx * ts;
            int sy = GridOriginY + (int)_viewPan.Y + ty * ts;
            if (sx + ts < PaletteWidth || sy + ts < TopChromeHeight || sx > GameConfig.DesignWidth ||
                sy > GameConfig.DesignHeight)
                continue;

            int idx = _map.Pack(tx, ty);
            string oid = _map.Objects[idx];
            if (!string.IsNullOrEmpty(oid) && _objectTex.TryGetValue(oid, out Texture2D ot) && ot != null)
                MapOverlayDraw.DrawAnchoredTopLeft(spriteBatch, ot, sx, sy);

            string hid = _map.Herbs[idx];
            if (!string.IsNullOrEmpty(hid) && _herbTex.TryGetValue(hid, out Texture2D ht) && ht != null)
                MapOverlayDraw.DrawAnchoredTopLeft(spriteBatch, ht, sx, sy);
        }

        spriteBatch.Draw(pixel, new Rectangle(PaletteWidth, 0, 2, GameConfig.DesignHeight),
            pal.ButtonBorder * 0.5f);

        string hint =
            "1/2/3 palette  |  LMB paint  |  RMB erase  |  Mid-drag pan  |  toolbar: slot/save/load/free/new/run  |  [ ] F5 F6 F4  |  Esc menu";
        spriteBatch.DrawString(font, hint, new Vector2(8, GameConfig.DesignHeight - 52), pal.PrimaryWhite, 0f,
            Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(font, $"Slot {_slot:D2}  ->  {MapFileStore.PathForSlot(_slot)}", new Vector2(8, GameConfig.DesignHeight - 30),
            pal.PrimaryWhite, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

        string artHint = $"Loose art: {EditorLooseArtLoader.ArtRoot}";
        spriteBatch.DrawString(font, artHint, new Vector2(8, GameConfig.DesignHeight - 68), pal.PrimaryWhite * 0.7f, 0f,
            Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        if (_statusTime > 0f && !string.IsNullOrEmpty(_status))
            spriteBatch.DrawString(font, _status, new Vector2(PaletteWidth + 12, TopChromeHeight + 4), pal.Accent, 0f,
                Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawToolbar(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, UiThemePalette pal)
    {
        DrawTb(spriteBatch, font, pixel, _tbPrevSlot, "<", pal);
        DrawTb(spriteBatch, font, pixel, _tbNextSlot, ">", pal);
        DrawTb(spriteBatch, font, pixel, _tbSave, "Save", pal);
        DrawTb(spriteBatch, font, pixel, _tbLoad, "Load", pal);
        DrawTb(spriteBatch, font, pixel, _tbFreeSlot, "Free slot", pal);
        DrawTb(spriteBatch, font, pixel, _tbNewMap, "Clear", pal);
        DrawTb(spriteBatch, font, pixel, _tbRun, "Run", pal);

        string slotTxt = $"{_slot:D2}";
        float sc = 0.55f;
        Vector2 sz = font.MeasureString(slotTxt) * sc;
        int cx = _tbPrevSlot.Right + (_tbNextSlot.Left - _tbPrevSlot.Right) / 2;
        spriteBatch.DrawString(font, slotTxt, new Vector2(cx - sz.X * 0.5f, _tbPrevSlot.Y + 6), pal.PrimaryWhite, 0f,
            Vector2.Zero, sc, SpriteEffects.None, 0f);
    }

    private static void DrawTb(SpriteBatch sb, SpriteFont font, Texture2D pixel, Rectangle r, string label,
        UiThemePalette pal)
    {
        UiChrome.FillRect(sb, pixel, r, pal.ButtonFill, pal.ButtonBorder);
        UiChrome.DrawLabel(sb, font, label, r, pal.ButtonLabel, 0.48f);
    }

    private void DrawCover(SpriteBatch spriteBatch, Texture2D tex, Rectangle cell, float cover)
    {
        int inner = Math.Max(1, (int)(Math.Min(cell.Width, cell.Height) * cover));
        float scale = inner / (float)Math.Max(1, Math.Max(tex.Width, tex.Height));
        int dw = Math.Max(1, (int)Math.Round(tex.Width * scale));
        int dh = Math.Max(1, (int)Math.Round(tex.Height * scale));
        int ox = cell.X + (cell.Width - dw) / 2;
        int oy = cell.Y + (cell.Height - dh) / 2;
        spriteBatch.Draw(tex, new Rectangle(ox, oy, dw, dh), Color.White);
    }

    private void DrawPalette(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, UiThemePalette pal)
    {
        int y = PaletteContentTop;
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(8, y, 68, 28), "Tiles", _paletteKind == PaletteKind.Tiles, pal);
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(80, y, 68, 28), "Objs", _paletteKind == PaletteKind.Objects, pal);
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(152, y, 68, 28), "Herbs", _paletteKind == PaletteKind.Herbs, pal);
        y += 40;

        if (_paletteKind == PaletteKind.Tiles)
        {
            if (_terrainIds.Count == 0)
            {
                spriteBatch.DrawString(font, "(no images in\nart/tiles)", new Vector2(12, y), pal.PrimaryWhite * 0.75f, 0f,
                    Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                return;
            }

            for (int i = 0; i < _terrainIds.Count; i++)
            {
                bool on = _terrainBrush == i;
                string id = _terrainIds[i];
                var row = new Rectangle(8, y, PaletteWidth - 16, 32);
                UiChrome.FillRect(spriteBatch, pixel, row,
                    on ? pal.ButtonFillFocused : pal.ButtonFill,
                    on ? pal.Accent : pal.ButtonBorder);
                if (_terrainTex.TryGetValue(id, out Texture2D tt) && tt != null)
                    DrawCover(spriteBatch, tt, row, 0.85f);
                else
                    spriteBatch.Draw(pixel, new Rectangle(row.X + 4, row.Y + 4, 24, 24), TerrainFallbackColor(id, pal));
                DrawPaletteLabel(spriteBatch, font, id, row, pal);
                y += 36;
            }
        }
        else if (_paletteKind == PaletteKind.Objects)
        {
            if (_objectIds.Count == 0)
            {
                spriteBatch.DrawString(font, "(no images in\nart/objects)", new Vector2(12, y), pal.PrimaryWhite * 0.75f, 0f,
                    Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                return;
            }

            for (int i = 0; i < _objectIds.Count; i++)
            {
                bool on = _objectBrush == i;
                string id = _objectIds[i];
                var row = new Rectangle(8, y, PaletteWidth - 16, 36);
                UiChrome.FillRect(spriteBatch, pixel, row,
                    on ? pal.ButtonFillFocused : pal.ButtonFill,
                    on ? pal.Accent : pal.ButtonBorder);
                if (_objectTex.TryGetValue(id, out Texture2D tex) && tex != null)
                    DrawCover(spriteBatch, tex, row, 0.8f);
                DrawPaletteLabel(spriteBatch, font, id, row, pal);
                y += 40;
            }
        }
        else
        {
            if (_herbIds.Count == 0)
            {
                spriteBatch.DrawString(font, "(no images in\nart/herbs)", new Vector2(12, y), pal.PrimaryWhite * 0.75f, 0f,
                    Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                return;
            }

            for (int i = 0; i < _herbIds.Count; i++)
            {
                bool on = _herbBrush == i;
                string id = _herbIds[i];
                var row = new Rectangle(8, y, PaletteWidth - 16, 36);
                UiChrome.FillRect(spriteBatch, pixel, row,
                    on ? pal.ButtonFillFocused : pal.ButtonFill,
                    on ? pal.Accent : pal.ButtonBorder);
                if (_herbTex.TryGetValue(id, out Texture2D tex) && tex != null)
                    DrawCover(spriteBatch, tex, row, 0.8f);
                DrawPaletteLabel(spriteBatch, font, id, row, pal);
                y += 40;
            }
        }
    }

    private static void DrawPaletteLabel(SpriteBatch spriteBatch, SpriteFont font, string id, Rectangle row,
        UiThemePalette pal)
    {
        float scale = 0.42f;
        string draw = id;
        Vector2 sz = font.MeasureString(draw) * scale;
        if (sz.X > row.Width - 8)
        {
            while (draw.Length > 3 &&
                   (font.MeasureString(draw + "...") * scale).X > row.Width - 8)
                draw = draw.Substring(0, draw.Length - 1);
            draw += "...";
        }

        spriteBatch.DrawString(font, draw, new Vector2(row.X + 4, row.Y + row.Height - 18), pal.PrimaryWhite, 0f,
            Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private static void DrawCatTab(
        SpriteBatch sb,
        SpriteFont font,
        Texture2D pixel,
        Rectangle r,
        string label,
        bool on,
        UiThemePalette pal)
    {
        UiChrome.FillRect(sb, pixel, r, on ? pal.ButtonFillFocused : pal.ButtonFill,
            on ? pal.Accent : pal.ButtonBorder);
        UiChrome.DrawLabel(sb, font, label, r, on ? pal.ButtonLabelFocused : pal.ButtonLabel, 0.55f);
    }

    private static Color TerrainFallbackColor(string id, UiThemePalette pal)
    {
        string s = id.ToLowerInvariant();
        if (s.Contains("water"))
            return new Color(0.18f, 0.32f, 0.55f);
        if (s.Contains("dirt") || s.Contains("mud") || s.Contains("sand"))
            return new Color(0.42f, 0.30f, 0.18f);
        if (s.Contains("flower") || s.Contains("herb") || s.Contains("bloom"))
            return new Color(0.55f, 0.22f, 0.45f);
        return Color.Lerp(new Color(0.25f, 0.48f, 0.28f), pal.ButtonFill, 0.15f);
    }
}
