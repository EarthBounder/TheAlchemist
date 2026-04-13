using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
        Herbs,
        Walk,
        View
    }

    private const int TopChromeHeight = 86;
    private const int PaletteWidth = 224;
    /// <summary>First Y for Tiles/Objs/Herbs/Walk tabs (below palette-column Save As / Load Bg).</summary>
    private const int PaletteToolsTop = TopChromeHeight + 4;
    private const int PaletteToolsRowH = 32;
    private const int PaletteContentTop = PaletteToolsTop + PaletteToolsRowH + 8;
    private const int PaletteViewTabY = PaletteContentTop + 30;
    private const int PaletteListTop = PaletteViewTabY + 28 + 8;
    private const int GridOriginX = 236;
    private const int GridOriginY = TopChromeHeight + 12;
    private const int UndoStackMax = 10;

    private Rectangle _palSaveAs;
    private Rectangle _palLoadBg;

    private Rectangle _tbPrevSlot;
    private Rectangle _tbNextSlot;
    private Rectangle _tbSave;
    private Rectangle _tbLoadMap;
    private Rectangle _tbSaveAs;
    private Rectangle _tbFreeSlot;
    private Rectangle _tbNewMap;
    private Rectangle _tbRun;
    private Rectangle _tbLoadBg;

    private readonly Game1 _game;
    private readonly EditorMapData _map;
    private readonly Dictionary<string, Texture2D> _terrainTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _objectTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _herbTex = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _terrainIds = new();
    private readonly List<string> _objectIds = new();
    private readonly List<string> _herbIds = new();

    private Texture2D _bgTexture;
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
    private bool _saveAsActive;
    private string _saveAsBuffer = "";
    private float _viewZoom = 1f;
    private bool _hasHoverTile;
    private int _hoverTileX;
    private int _hoverTileY;

    /// <summary>Map snapshots before each paint/erase gesture (max <see cref="UndoStackMax"/>).</summary>
    private readonly List<EditorMapData> _undoStack = new();

    private float CellDrawPixels => ProcTileMap.TileSizePixels * _viewZoom;

    public LevelEditorScreen(Game1 game)
    {
        _game = game;
        _map = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
        LoadEditorTextures();
        ClampBrushes();
        ApplyCurrentSlotFromDisk(false);
    }

    public void DisposeGpuResources()
    {
        if (_bgTexture != null)
        {
            _bgTexture.Dispose();
            _bgTexture = null;
        }
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
        int h = 30;
        int y1 = 8;
        int y2 = 44;
        int x = PaletteWidth + 8;
        _tbPrevSlot = new Rectangle(x, y1, 28, h);
        x += 32;
        _tbNextSlot = new Rectangle(x, y1, 28, h);
        x += 34;
        _tbSave = new Rectangle(x, y1, 50, h);
        x += 54;
        _tbLoadMap = new Rectangle(x, y1, 78, h);
        x += 82;
        _tbSaveAs = new Rectangle(x, y1, 58, h);
        x += 62;
        _tbFreeSlot = new Rectangle(x, y1, 72, h);
        x += 76;
        _tbNewMap = new Rectangle(x, y1, 52, h);
        x += 56;
        _tbRun = new Rectangle(Math.Min(x, GameConfig.DesignWidth - 58), y1, 54, h);

        x = PaletteWidth + 8;
        _tbLoadBg = new Rectangle(x, y2, 200, h);

        _palSaveAs = new Rectangle(8, PaletteToolsTop, 108, PaletteToolsRowH);
        _palLoadBg = new Rectangle(120, PaletteToolsTop, PaletteWidth - 128, PaletteToolsRowH);
    }

    private void ApplyCurrentSlotFromDisk(bool showStatus)
    {
        if (MapFileStore.TryLoad(_slot, out EditorMapData loaded))
        {
            CopyMap(loaded, _map);
            if (showStatus)
                SetStatus($"Loaded map_{_slot:D2}.json");
        }
        else
        {
            var fresh = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
            CopyMap(fresh, _map);
            if (showStatus)
                SetStatus($"Slot {_slot:D2} empty (new map).");
        }

        ReloadBackgroundTexture();
        ClearUndoStack();
    }

    private void TryPickBackgroundImage()
    {
        if (!OperatingSystem.IsWindows())
        {
            SetStatus("Load Background is only supported on Windows.");
            return;
        }

        // OpenFileDialog must run on an STA thread. Top-level / non-STA Main caused ShowDialog to fail silently
        // or never appear above the game; a dedicated STA thread matches the common engine integration pattern.
        string picked = null;
        Exception dialogError = null;
        var pickerThread = new Thread(() =>
        {
            try
            {
                System.Windows.Forms.Application.EnableVisualStyles();
                using var dlg = new System.Windows.Forms.OpenFileDialog
                {
                    Title = "Background image (under tile grid)",
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrWhiteSpace(dlg.FileName))
                    picked = dlg.FileName;
            }
            catch (Exception ex)
            {
                dialogError = ex;
            }
        });
        pickerThread.SetApartmentState(ApartmentState.STA);
        pickerThread.IsBackground = false;
        pickerThread.Start();
        pickerThread.Join();

        if (dialogError != null)
        {
            SetStatus($"Load Background failed: {dialogError.Message}");
            return;
        }

        if (string.IsNullOrWhiteSpace(picked))
            return;

        string path = Path.GetFullPath(picked);
        if (!File.Exists(path))
        {
            SetStatus("Background file not found.");
            return;
        }

        _map.BackgroundImagePath = path;
        ReloadBackgroundTexture();
        SetStatus("Background loaded.");
    }

    private void ReloadBackgroundTexture()
    {
        if (_bgTexture != null)
        {
            _bgTexture.Dispose();
            _bgTexture = null;
        }

        if (string.IsNullOrWhiteSpace(_map.BackgroundImagePath))
            return;

        try
        {
            using FileStream fs = File.OpenRead(_map.BackgroundImagePath);
            _bgTexture = Texture2D.FromStream(_game.GraphicsDevice, fs);
        }
        catch
        {
            SetStatus("Background image failed to load.");
        }
    }

    private void HandleToolbarClick(int mx, int my)
    {
        if (_tbPrevSlot.Contains(mx, my))
        {
            _slot = Math.Max(0, _slot - 1);
            ApplyCurrentSlotFromDisk(true);
        }
        else if (_tbNextSlot.Contains(mx, my))
        {
            _slot = Math.Min(99, _slot + 1);
            ApplyCurrentSlotFromDisk(true);
        }
        else if (_tbSave.Contains(mx, my))
        {
            if (MapFileStore.TrySave(_map, _slot))
                SetStatus($"Saved map_{_slot:D2}.json");
            else
                SetStatus("Save failed.");
        }
        else if (_tbLoadMap.Contains(mx, my))
            ApplyCurrentSlotFromDisk(true);
        else if (_tbSaveAs.Contains(mx, my))
            BeginSaveAs();
        else if (_tbFreeSlot.Contains(mx, my))
        {
            _slot = MapFileStore.FindNextEmptySlot();
            ApplyCurrentSlotFromDisk(true);
        }
        else if (_tbNewMap.Contains(mx, my))
        {
            var fresh = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
            CopyMap(fresh, _map);
            ReloadBackgroundTexture();
            ClearUndoStack();
            SetStatus("New map (cleared).");
        }
        else if (_tbRun.Contains(mx, my))
            _game.PlayTestMap(_map);
        else if (_tbLoadBg.Contains(mx, my))
            TryPickBackgroundImage();
    }

    private void BeginSaveAs()
    {
        _saveAsActive = true;
        _saveAsBuffer = "";
        _status = "Save As: type name, Enter to save, Esc to cancel.";
        _statusTime = 8f;
    }

    private void UpdateSaveAsDialog(KeyboardState kb)
    {
        bool keyPressed(Keys k) => kb.IsKeyDown(k) && !_keysWas.IsKeyDown(k);

        if (keyPressed(Keys.Escape))
        {
            _saveAsActive = false;
            SetStatus("Save As cancelled.");
            return;
        }

        if (keyPressed(Keys.Enter))
        {
            if (MapFileStore.TrySaveNamed(_map, _saveAsBuffer))
                SetStatus($"Saved {MapFileStore.SanitizeMapBaseName(_saveAsBuffer)}.json");
            else
                SetStatus("Save As failed.");
            _saveAsActive = false;
            return;
        }

        if (keyPressed(Keys.Back) && _saveAsBuffer.Length > 0)
            _saveAsBuffer = _saveAsBuffer[..^1];

        bool shift = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
        const int maxLen = 80;

        void tryAppend(char c)
        {
            if (_saveAsBuffer.Length < maxLen)
                _saveAsBuffer += c;
        }

        for (int i = 0; i <= 9; i++)
        {
            if (keyPressed(Keys.D0 + i))
            {
                tryAppend((char)('0' + i));
                return;
            }

            if (keyPressed(Keys.NumPad0 + i))
            {
                tryAppend((char)('0' + i));
                return;
            }
        }

        for (Keys k = Keys.A; k <= Keys.Z; k++)
        {
            if (!keyPressed(k))
                continue;
            char c = (char)('a' + (k - Keys.A));
            if (shift)
                c = char.ToUpperInvariant(c);
            tryAppend(c);
            return;
        }

        if (keyPressed(Keys.Space))
        {
            tryAppend(' ');
            return;
        }

        if (keyPressed(Keys.OemMinus))
        {
            tryAppend(shift ? '_' : '-');
            return;
        }

        if (keyPressed(Keys.OemPlus) && shift)
            tryAppend('_');
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_statusTime > 0f)
            _statusTime -= dt;

        var kb = Keyboard.GetState();
        var ms = Mouse.GetState();

        bool keyPressed(Keys k) => kb.IsKeyDown(k) && !_keysWas.IsKeyDown(k);
        bool ctrlHeld = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        bool shiftHeld = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);

        if (_saveAsActive)
        {
            _hasHoverTile = false;
            UpdateSaveAsDialog(kb);
            _keysWas = kb;
            _mouseWas = ms;
            return;
        }

        if (input.RawEscapePressed || input.BackPressed)
        {
            _game.ShowMainMenu();
            return;
        }

        if (ctrlHeld && !shiftHeld && keyPressed(Keys.Z))
            TryUndo();

        if (keyPressed(Keys.D1) || keyPressed(Keys.NumPad1))
            _paletteKind = PaletteKind.Tiles;
        if (keyPressed(Keys.D2) || keyPressed(Keys.NumPad2))
            _paletteKind = PaletteKind.Objects;
        if (keyPressed(Keys.D3) || keyPressed(Keys.NumPad3))
            _paletteKind = PaletteKind.Herbs;
        if (keyPressed(Keys.D4) || keyPressed(Keys.NumPad4))
            _paletteKind = PaletteKind.Walk;
        if (keyPressed(Keys.D5) || keyPressed(Keys.NumPad5))
            _paletteKind = PaletteKind.View;

        if (keyPressed(Keys.F4))
        {
            var fresh = EditorMapData.CreateEmpty(ProcTileMap.WidthTiles, ProcTileMap.HeightTiles);
            CopyMap(fresh, _map);
            ReloadBackgroundTexture();
            ClearUndoStack();
            SetStatus("New map (cleared).");
        }

        if (keyPressed(Keys.OemOpenBrackets))
        {
            _slot = Math.Max(0, _slot - 1);
            ApplyCurrentSlotFromDisk(true);
        }

        if (keyPressed(Keys.OemCloseBrackets))
        {
            _slot = Math.Min(99, _slot + 1);
            ApplyCurrentSlotFromDisk(true);
        }

        if (keyPressed(Keys.F5))
        {
            if (MapFileStore.TrySave(_map, _slot))
                SetStatus($"Saved map_{_slot:D2}.json");
            else
                SetStatus("Save failed.");
        }

        if (keyPressed(Keys.F6))
            ApplyCurrentSlotFromDisk(true);

        if (keyPressed(Keys.F7))
            BeginSaveAs();

        // F-keys often never reach the game when it runs under an IDE (Cursor/VS Code, etc.).
        if (ctrlHeld && shiftHeld && keyPressed(Keys.S))
            BeginSaveAs();

        if (ctrlHeld && shiftHeld && keyPressed(Keys.B))
            TryPickBackgroundImage();

        if (_paletteKind == PaletteKind.View)
            UpdateViewMode(gameTime, in input, kb, shiftHeld);
        else
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
        {
            if (input.PointerPressed)
            {
                if (_palSaveAs.Contains(mx, my))
                {
                    BeginSaveAs();
                    _keysWas = kb;
                    _mouseWas = ms;
                    return;
                }

                if (_palLoadBg.Contains(mx, my))
                {
                    TryPickBackgroundImage();
                    _keysWas = kb;
                    _mouseWas = ms;
                    return;
                }
            }

            HandlePaletteClick(input.PointerPressed, mx, my);
        }
        else if (mx >= PaletteWidth && my >= TopChromeHeight && _paletteKind != PaletteKind.View)
        {
            bool left = ms.LeftButton == ButtonState.Pressed;
            bool right = ms.RightButton == ButtonState.Pressed;
            bool leftEdge = left && _mouseWas.LeftButton == ButtonState.Released;
            bool rightEdge = right && _mouseWas.RightButton == ButtonState.Released;
            if (TryGetTileAtPointer(input.PointerVirtual, out int tx, out int ty))
            {
                if (left && (leftEdge || !input.PointerPressed))
                {
                    if (leftEdge)
                        PushUndoSnapshot();
                    ApplyBrush(tx, ty);
                }

                if (right)
                {
                    if (rightEdge)
                        PushUndoSnapshot();
                    EraseAt(tx, ty);
                }
            }
        }

        _hasHoverTile = _paletteKind != PaletteKind.View &&
                        TryGetTileAtPointer(input.PointerVirtual, out _hoverTileX, out _hoverTileY);

        ClampBrushes();

        _keysWas = kb;
        _mouseWas = ms;
    }

    private void ClearUndoStack() => _undoStack.Clear();

    private void PushUndoSnapshot()
    {
        _undoStack.Add(EditorMapData.Clone(_map));
        while (_undoStack.Count > UndoStackMax)
            _undoStack.RemoveAt(0);
    }

    private void TryUndo()
    {
        if (_undoStack.Count == 0)
        {
            SetStatus("Nothing to undo.");
            return;
        }

        EditorMapData snap = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        CopyMap(snap, _map);
        ReloadBackgroundTexture();
    }

    private static void CopyMap(EditorMapData from, EditorMapData to)
    {
        to.Schema = from.Schema;
        to.Width = from.Width;
        to.Height = from.Height;
        to.Terrain = (string[])from.Terrain.Clone();
        to.Objects = (string[])from.Objects.Clone();
        to.Herbs = (string[])from.Herbs.Clone();
        to.Walkable = (bool[])from.Walkable.Clone();
        to.BackgroundImagePath = from.BackgroundImagePath;
    }

    private void SetStatus(string msg)
    {
        _status = msg;
        _statusTime = 2.6f;
    }

    private void UpdateViewMode(GameTime gameTime, in UiFrameInput input, KeyboardState kb, bool shiftHeld)
    {
        bool kp(Keys k) => kb.IsKeyDown(k) && !_keysWas.IsKeyDown(k);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector2 pan = input.FloorPanPixels;
        int sd = input.ScrollWheelDelta;
        if (sd != 0)
        {
            float scrollPx = sd * (36f / 120f);
            if (shiftHeld)
                pan.X -= scrollPx;
            else
                pan.Y += scrollPx;

            float z0 = _viewZoom;
            float z1 = Math.Clamp(z0 * (sd > 0 ? 1.1f : 1f / 1.1f), 0.25f, 4f);
            ApplyZoomRefocus(z0, z1, input.PointerVirtual.X, input.PointerVirtual.Y);
        }

        _viewPan += pan;

        float speed = 480f * dt;
        if (kb.IsKeyDown(Keys.Left) || kb.IsKeyDown(Keys.A))
            _viewPan.X += speed;
        if (kb.IsKeyDown(Keys.Right) || kb.IsKeyDown(Keys.D))
            _viewPan.X -= speed;
        if (kb.IsKeyDown(Keys.Up) || kb.IsKeyDown(Keys.W))
            _viewPan.Y += speed;
        if (kb.IsKeyDown(Keys.Down) || kb.IsKeyDown(Keys.S))
            _viewPan.Y -= speed;

        float cx = (PaletteWidth + GameConfig.DesignWidth) * 0.5f;
        float cy = (TopChromeHeight + GameConfig.DesignHeight) * 0.5f;
        if (kp(Keys.Add) || kp(Keys.OemPlus))
            ApplyZoomRefocus(_viewZoom, Math.Min(4f, _viewZoom * 1.15f), cx, cy);
        if (kp(Keys.Subtract) || kp(Keys.OemMinus))
            ApplyZoomRefocus(_viewZoom, Math.Max(0.25f, _viewZoom / 1.15f), cx, cy);
    }

    private void ApplyZoomRefocus(float z0, float z1, float screenX, float screenY)
    {
        if (Math.Abs(z1 - z0) < 1e-5f)
            return;
        float ratio = z1 / z0;
        _viewPan.X = screenX - GridOriginX - (screenX - GridOriginX - _viewPan.X) * ratio;
        _viewPan.Y = screenY - GridOriginY - (screenY - GridOriginY - _viewPan.Y) * ratio;
        _viewZoom = z1;
    }

    private void HandlePaletteClick(bool pressed, int mx, int my)
    {
        if (!pressed)
            return;

        int y = PaletteContentTop;
        var catTiles = new Rectangle(8, y, 48, 26);
        var catObjs = new Rectangle(60, y, 48, 26);
        var catHerbs = new Rectangle(112, y, 48, 26);
        var catWalk = new Rectangle(164, y, 52, 26);
        if (catTiles.Contains(mx, my))
            _paletteKind = PaletteKind.Tiles;
        else if (catObjs.Contains(mx, my))
            _paletteKind = PaletteKind.Objects;
        else if (catHerbs.Contains(mx, my))
            _paletteKind = PaletteKind.Herbs;
        else if (catWalk.Contains(mx, my))
            _paletteKind = PaletteKind.Walk;

        var catView = new Rectangle(8, PaletteViewTabY, PaletteWidth - 16, 26);
        if (catView.Contains(mx, my))
        {
            _paletteKind = PaletteKind.View;
            return;
        }

        if (_paletteKind == PaletteKind.View)
            return;

        y = PaletteListTop;
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
        else if (_paletteKind == PaletteKind.Herbs)
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
        float cell = CellDrawPixels;
        if (cell < 1e-4f)
            return false;
        float lx = pointerVirtual.X - GridOriginX - _viewPan.X;
        float ly = pointerVirtual.Y - GridOriginY - _viewPan.Y;
        tx = (int)Math.Floor(lx / cell);
        ty = (int)Math.Floor(ly / cell);
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
            case PaletteKind.Walk:
                _map.Walkable[i] = true;
                break;
            default:
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
            case PaletteKind.Walk:
                _map.Walkable[i] = false;
                break;
            default:
                break;
        }
    }

    private static bool CellShowsWalkLetter(EditorMapData map, int idx)
    {
        if (!string.IsNullOrEmpty(map.Objects[idx]))
            return false;
        return map.Walkable[idx];
    }

    private static bool IsMapCellVisibleInDesign(int sxI, int syI, int cellWidth) =>
        sxI + cellWidth >= PaletteWidth && syI + cellWidth >= TopChromeHeight && sxI <= GameConfig.DesignWidth &&
        syI <= GameConfig.DesignHeight;

    private void GetHoverFootprintSizeTiles(out int wTiles, out int hTiles)
    {
        wTiles = 1;
        hTiles = 1;
        Texture2D tex = null;
        if (_paletteKind == PaletteKind.Objects && _objectIds.Count > 0)
        {
            string id = _objectIds[_objectBrush];
            _objectTex.TryGetValue(id, out tex);
        }
        else if (_paletteKind == PaletteKind.Herbs && _herbIds.Count > 0)
        {
            string id = _herbIds[_herbBrush];
            _herbTex.TryGetValue(id, out tex);
        }

        if (tex == null)
            return;

        wTiles = Math.Max(1, (int)Math.Ceiling(tex.Width / (float)ProcTileMap.TileSizePixels));
        hTiles = Math.Max(1, (int)Math.Ceiling(tex.Height / (float)ProcTileMap.TileSizePixels));
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var font = _game.UiFont;
        var pixel = _game.Pixel;
        var pal = _game.UiPalette;
        float cellPx = CellDrawPixels;
        int cw = Math.Max(1, (int)Math.Round(cellPx));

        LayoutToolbar();

        DrawPalette(spriteBatch, font, pixel, pal);

        int mapPxW = (int)Math.Round(_map.Width * cellPx);
        int mapPxH = (int)Math.Round(_map.Height * cellPx);
        int mapOx = GridOriginX + (int)_viewPan.X;
        int mapOy = GridOriginY + (int)_viewPan.Y;
        bool bgActive = _bgTexture != null;
        if (bgActive)
        {
            var dest = new Rectangle(mapOx, mapOy, mapPxW, mapPxH);
            var mapViewPort = new Rectangle(PaletteWidth, TopChromeHeight,
                GameConfig.DesignWidth - PaletteWidth, GameConfig.DesignHeight - TopChromeHeight);
            if (dest.Intersects(mapViewPort))
                spriteBatch.Draw(_bgTexture, dest, Color.White);
        }

        // With a reference image behind the grid, skip opaque tile fills and let terrain art be slightly
        // transparent so the background remains visible through the tiles.
        Color terrainTint = bgActive ? Color.White * 0.82f : Color.White;

        for (int ty = 0; ty < _map.Height; ty++)
        for (int tx = 0; tx < _map.Width; tx++)
        {
            float sx = GridOriginX + _viewPan.X + tx * cellPx;
            float sy = GridOriginY + _viewPan.Y + ty * cellPx;
            int sxI = (int)Math.Floor(sx);
            int syI = (int)Math.Floor(sy);
            if (!IsMapCellVisibleInDesign(sxI, syI, cw))
                continue;

            int idx = _map.Pack(tx, ty);
            string tid = _map.Terrain[idx];
            var fill = TerrainFallbackColor(tid, pal);
            var cell = new Rectangle(sxI, syI, cw, cw);
            if (!bgActive)
                spriteBatch.Draw(pixel, cell, fill);
            else
                spriteBatch.Draw(pixel, cell, fill * 0.22f);

            spriteBatch.Draw(pixel, new Rectangle(sxI, syI, cw, 1), pal.ButtonBorder * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(sxI, syI + cw - 1, cw, 1), pal.ButtonBorder * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(sxI, syI, 1, cw), pal.ButtonBorder * 0.25f);
            spriteBatch.Draw(pixel, new Rectangle(sxI + cw - 1, syI, 1, cw), pal.ButtonBorder * 0.25f);

            if (_terrainTex.TryGetValue(tid, out Texture2D tt) && tt != null)
                DrawCover(spriteBatch, tt, cell, 1f, terrainTint);
        }

        for (int ty = 0; ty < _map.Height; ty++)
        for (int tx = 0; tx < _map.Width; tx++)
        {
            float sx = GridOriginX + _viewPan.X + tx * cellPx;
            float sy = GridOriginY + _viewPan.Y + ty * cellPx;
            int sxI = (int)Math.Floor(sx);
            int syI = (int)Math.Floor(sy);
            if (!IsMapCellVisibleInDesign(sxI, syI, cw))
                continue;

            int idx = _map.Pack(tx, ty);
            string oid = _map.Objects[idx];
            if (!string.IsNullOrEmpty(oid) && _objectTex.TryGetValue(oid, out Texture2D ot) && ot != null)
                MapOverlayDraw.DrawAnchoredTopLeft(spriteBatch, ot, sxI, syI, cellPx);

            string hid = _map.Herbs[idx];
            if (!string.IsNullOrEmpty(hid) && _herbTex.TryGetValue(hid, out Texture2D ht) && ht != null)
                MapOverlayDraw.DrawAnchoredTopLeft(spriteBatch, ht, sxI, syI, cellPx);
        }

        if (_paletteKind == PaletteKind.Walk)
        {
            float wScale = 0.62f;
            for (int ty = 0; ty < _map.Height; ty++)
            for (int tx = 0; tx < _map.Width; tx++)
            {
                float sx = GridOriginX + _viewPan.X + tx * cellPx;
                float sy = GridOriginY + _viewPan.Y + ty * cellPx;
                int sxI = (int)Math.Floor(sx);
                int syI = (int)Math.Floor(sy);
                if (!IsMapCellVisibleInDesign(sxI, syI, cw))
                    continue;

                int idx = _map.Pack(tx, ty);
                string letter = CellShowsWalkLetter(_map, idx) ? "W" : "N";
                Vector2 letterSize = font.MeasureString(letter) * wScale;
                var col = letter == "W" ? new Color(0.75f, 1f, 0.85f) : new Color(1f, 0.45f, 0.45f);
                spriteBatch.DrawString(font, letter,
                    new Vector2(sx + (cw - letterSize.X) * 0.5f, sy + (cw - letterSize.Y) * 0.5f), col, 0f, Vector2.Zero, wScale,
                    SpriteEffects.None, 0f);
            }
        }

        if (_hasHoverTile && _paletteKind != PaletteKind.View)
        {
            GetHoverFootprintSizeTiles(out int fpw, out int fph);
            var hoverFill = _paletteKind == PaletteKind.Walk ? new Color(0.45f, 1f, 0.55f, 0.30f) : pal.Accent * 0.28f;
            var hoverBorder = _paletteKind == PaletteKind.Walk ? new Color(0.58f, 1f, 0.70f, 0.85f) : pal.Accent;
            int edge = Math.Max(1, Math.Min(2, cw / 6));

            for (int oy = 0; oy < fph; oy++)
            for (int ox = 0; ox < fpw; ox++)
            {
                int tx = _hoverTileX + ox;
                int ty = _hoverTileY + oy;
                if (!_map.Contains(tx, ty))
                    continue;

                int sxI = (int)Math.Floor(GridOriginX + _viewPan.X + tx * cellPx);
                int syI = (int)Math.Floor(GridOriginY + _viewPan.Y + ty * cellPx);
                if (!IsMapCellVisibleInDesign(sxI, syI, cw))
                    continue;

                var cell = new Rectangle(sxI, syI, cw, cw);
                spriteBatch.Draw(pixel, cell, hoverFill);
                spriteBatch.Draw(pixel, new Rectangle(sxI, syI, cw, edge), hoverBorder);
                spriteBatch.Draw(pixel, new Rectangle(sxI, syI + cw - edge, cw, edge), hoverBorder);
                spriteBatch.Draw(pixel, new Rectangle(sxI, syI, edge, cw), hoverBorder);
                spriteBatch.Draw(pixel, new Rectangle(sxI + cw - edge, syI, edge, cw), hoverBorder);
            }
        }

        spriteBatch.Draw(pixel, new Rectangle(PaletteWidth, 0, 2, GameConfig.DesignHeight),
            pal.ButtonBorder * 0.5f);

        spriteBatch.Draw(pixel, new Rectangle(0, 0, GameConfig.DesignWidth, TopChromeHeight), pal.ButtonFill * 0.92f);
        spriteBatch.Draw(pixel, new Rectangle(0, TopChromeHeight - 1, GameConfig.DesignWidth, 1), pal.ButtonBorder);

        DrawToolbar(spriteBatch, font, pixel, pal);

        spriteBatch.DrawString(font, "Editor", new Vector2(12, 12), pal.TitleText, 0f, Vector2.Zero, 0.78f,
            SpriteEffects.None, 0f);

        string hint = _paletteKind == PaletteKind.View
            ? "View: wheel=zoom  Shift+wheel=pan H  mid-drag=pan  arrows/WASD=move  +/-=zoom  |  pick another tab to edit"
            : "Save As: Ctrl+Shift+S or F7  |  Load Bg: Ctrl+Shift+B  |  Ctrl+Z undo (10)  |  1-5 tabs  |  LMB/RMB  |  pan  |  F5/F6/F4  |  Esc menu";
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

        if (_saveAsActive)
        {
            var box = new Rectangle(GameConfig.DesignWidth / 2 - 200, GameConfig.DesignHeight / 2 - 36, 400, 72);
            spriteBatch.Draw(pixel, new Rectangle(0, 0, GameConfig.DesignWidth, GameConfig.DesignHeight),
                Color.Black * 0.45f);
            UiChrome.FillRect(spriteBatch, pixel, box, pal.ButtonFill, pal.Accent);
            string line = "Save As: " + _saveAsBuffer + (_saveAsBuffer.Length < 80 ? "_" : "");
            spriteBatch.DrawString(font, line, new Vector2(box.X + 12, box.Y + 14), pal.PrimaryWhite, 0f, Vector2.Zero,
                0.52f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, "Enter save   Esc cancel", new Vector2(box.X + 12, box.Y + 44),
                pal.PrimaryWhite * 0.8f, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        }
    }

    private void DrawToolbar(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, UiThemePalette pal)
    {
        DrawTb(spriteBatch, font, pixel, _tbPrevSlot, "<", pal);
        DrawTb(spriteBatch, font, pixel, _tbNextSlot, ">", pal);
        DrawTb(spriteBatch, font, pixel, _tbSave, "Save", pal);
        DrawTb(spriteBatch, font, pixel, _tbLoadMap, "Load Map", pal);
        DrawTb(spriteBatch, font, pixel, _tbSaveAs, "Save As", pal);
        DrawTb(spriteBatch, font, pixel, _tbFreeSlot, "Free slot", pal);
        DrawTb(spriteBatch, font, pixel, _tbNewMap, "Clear", pal);
        DrawTb(spriteBatch, font, pixel, _tbRun, "Run", pal);
        DrawTb(spriteBatch, font, pixel, _tbLoadBg, "Load Background", pal);

        string slotTxt = $"{_slot:D2}";
        float sc = 0.55f;
        Vector2 sz = font.MeasureString(slotTxt) * sc;
        int cx = _tbPrevSlot.Right + (_tbNextSlot.Left - _tbPrevSlot.Right) / 2;
        spriteBatch.DrawString(font, slotTxt, new Vector2(cx - sz.X * 0.5f, _tbPrevSlot.Y + 5), pal.PrimaryWhite, 0f,
            Vector2.Zero, sc, SpriteEffects.None, 0f);
    }

    private static void DrawTb(SpriteBatch sb, SpriteFont font, Texture2D pixel, Rectangle r, string label,
        UiThemePalette pal)
    {
        UiChrome.FillRect(sb, pixel, r, pal.ButtonFill, pal.ButtonBorder);
        float scale = 0.42f;
        Vector2 sz = font.MeasureString(label) * scale;
        float maxW = Math.Max(4f, r.Width - 4f);
        if (sz.X > maxW)
            scale *= maxW / sz.X;
        UiChrome.DrawLabel(sb, font, label, r, pal.ButtonLabel, scale);
    }

    private void DrawCover(SpriteBatch spriteBatch, Texture2D tex, Rectangle cell, float cover, Color? tint = null)
    {
        Color c = tint ?? Color.White;
        int inner = Math.Max(1, (int)(Math.Min(cell.Width, cell.Height) * cover));
        float scale = inner / (float)Math.Max(1, Math.Max(tex.Width, tex.Height));
        int dw = Math.Max(1, (int)Math.Round(tex.Width * scale));
        int dh = Math.Max(1, (int)Math.Round(tex.Height * scale));
        int ox = cell.X + (cell.Width - dw) / 2;
        int oy = cell.Y + (cell.Height - dh) / 2;
        spriteBatch.Draw(tex, new Rectangle(ox, oy, dw, dh), c);
    }

    private void DrawPalette(SpriteBatch spriteBatch, SpriteFont font, Texture2D pixel, UiThemePalette pal)
    {
        DrawTb(spriteBatch, font, pixel, _palSaveAs, "Save As", pal);
        DrawTb(spriteBatch, font, pixel, _palLoadBg, "Load Bg", pal);

        int y = PaletteContentTop;
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(8, y, 48, 26), "Tiles", _paletteKind == PaletteKind.Tiles, pal);
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(60, y, 48, 26), "Objs", _paletteKind == PaletteKind.Objects, pal);
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(112, y, 48, 26), "Herbs", _paletteKind == PaletteKind.Herbs, pal);
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(164, y, 52, 26), "Walk", _paletteKind == PaletteKind.Walk, pal);
        DrawCatTab(spriteBatch, font, pixel, new Rectangle(8, PaletteViewTabY, PaletteWidth - 16, 26), "View (pan/zoom)",
            _paletteKind == PaletteKind.View, pal);

        y = PaletteListTop;

        if (_paletteKind == PaletteKind.View)
        {
            spriteBatch.DrawString(font,
                $"Zoom {_viewZoom * 100f:0}% - view only\nno tile edits",
                new Vector2(12, y), pal.PrimaryWhite * 0.9f, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
            return;
        }

        if (_paletteKind == PaletteKind.Walk)
        {
            spriteBatch.DrawString(font, "Walk mask (play)\nLMB walkable\nRMB blocked\nObjects show N",
                new Vector2(12, y), pal.PrimaryWhite * 0.85f, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            return;
        }

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
        UiChrome.DrawLabel(sb, font, label, r, on ? pal.ButtonLabelFocused : pal.ButtonLabel, 0.48f);
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
