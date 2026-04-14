using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheAlchemist.Core;
using TheAlchemist.Persistence;
using TheAlchemist.World;

namespace TheAlchemist.Screens;

public sealed class MainMenuScreen : IGameScreen
{
    private readonly Game1 _game;
    private Rectangle _continue;
    private Rectangle _newRun;
    private Rectangle _levelEditor;
    private Rectangle _storyDebug;
    private Rectangle _quit;
    private int _focus;
    private const float BtnScale = 1.12f;

    public MainMenuScreen(Game1 game) => _game = game;

    private static bool HasSave => SaveStore.Exists();

    private int FocusMax => HasSave ? 4 : 3;

    private void EnsureLayout()
    {
        var font = _game.UiFont;
        const int padX = 28;
        const int padY = 14;
        const string tContinue = "Continue";
        const string tNew = "New journey";
        const string tEditor = "Level editor";
        const string tStoryDebug = "Story debug";
        const string tQuit = "Quit to desktop";

        float maxTextW = font.MeasureString(tNew).X * BtnScale;
        if (HasSave)
            maxTextW = Math.Max(maxTextW, font.MeasureString(tContinue).X * BtnScale);
        maxTextW = Math.Max(maxTextW, font.MeasureString(tEditor).X * BtnScale);
        maxTextW = Math.Max(maxTextW, font.MeasureString(tStoryDebug).X * BtnScale);
        maxTextW = Math.Max(maxTextW, font.MeasureString(tQuit).X * BtnScale);

        float maxTextH = Math.Max(font.MeasureString(tNew).Y, font.MeasureString(tQuit).Y) * BtnScale;
        if (HasSave)
            maxTextH = Math.Max(maxTextH, font.MeasureString(tContinue).Y * BtnScale);
        maxTextH = Math.Max(maxTextH, font.MeasureString(tEditor).Y * BtnScale);
        maxTextH = Math.Max(maxTextH, font.MeasureString(tStoryDebug).Y * BtnScale);

        int w = (int)Math.Ceiling(maxTextW) + padX * 2;
        int h = (int)Math.Ceiling(maxTextH) + padY * 2;
        const int x = 80;
        const int regionTop = 248;
        const int regionBottom = GameConfig.DesignHeight - 48;
        int btnCount = HasSave ? 5 : 4;
        int stackH = btnCount * h + (btnCount - 1) * 14;
        int y = regionTop + Math.Max(0, (regionBottom - regionTop - stackH) / 2);

        if (HasSave)
        {
            _continue = new Rectangle(x, y, w, h);
            y = _continue.Bottom + 14;
        }
        else
            _continue = Rectangle.Empty;

        _newRun = new Rectangle(x, y, w, h);
        y = _newRun.Bottom + 14;
        _levelEditor = new Rectangle(x, y, w, h);
        y = _levelEditor.Bottom + 14;
        _storyDebug = new Rectangle(x, y, w, h);
        _quit = new Rectangle(x, _storyDebug.Bottom + 14, w, h);
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        EnsureLayout();

        if (input.EscapePressed || input.BackPressed)
        {
            _game.Exit();
            return;
        }

        int mx = (int)input.PointerVirtual.X;
        int my = (int)input.PointerVirtual.Y;

        if (HasSave && _continue.Contains(mx, my))
            _focus = 0;
        else if (_newRun.Contains(mx, my))
            _focus = HasSave ? 1 : 0;
        else if (_levelEditor.Contains(mx, my))
            _focus = HasSave ? 2 : 1;
        else if (_storyDebug.Contains(mx, my))
            _focus = HasSave ? 3 : 2;
        else if (_quit.Contains(mx, my))
            _focus = FocusMax;

        if (input.NavigateDelta != 0)
        {
            _focus += input.NavigateDelta;
            if (_focus < 0)
                _focus = FocusMax;
            if (_focus > FocusMax)
                _focus = 0;
        }

        bool hitContinue = HasSave && input.PointerPressed && _continue.Contains(mx, my);
        bool hitNew = input.PointerPressed && _newRun.Contains(mx, my);
        bool hitEditor = input.PointerPressed && _levelEditor.Contains(mx, my);
        bool hitStoryDebug = input.PointerPressed && _storyDebug.Contains(mx, my);
        bool hitQuit = input.PointerPressed && _quit.Contains(mx, my);

        if (hitContinue || (input.ConfirmPressed && HasSave && _focus == 0))
        {
            if (SaveStore.TryLoad(out var w))
                _game.ShowWorld(w);
            else
                _game.ShowWorld(WorldState.NewRun(Environment.TickCount));
            return;
        }

        if (hitNew || (input.ConfirmPressed && _focus == (HasSave ? 1 : 0)))
            _game.ShowWorld(WorldState.NewRun(Environment.TickCount));
        else if (hitEditor || (input.ConfirmPressed && _focus == (HasSave ? 2 : 1)))
            _game.ShowLevelEditor();
        else if (hitStoryDebug || (input.ConfirmPressed && _focus == (HasSave ? 3 : 2)))
            _game.ShowCampaignDebugStory();
        else if (hitQuit || (input.ConfirmPressed && _focus == FocusMax))
            _game.Exit();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        EnsureLayout();
        var font = _game.UiFont;
        var pixel = _game.Pixel;
        var pal = _game.UiPalette;

        spriteBatch.DrawString(font, GameConfig.Title, new Vector2(96, 120), pal.TitleText, 0f, Vector2.Zero, 1.65f,
            SpriteEffects.None, 0f);

        if (HasSave)
            DrawButton(spriteBatch, font, pixel, _continue, "Continue", _focus == 0, BtnScale, pal);

        DrawButton(spriteBatch, font, pixel, _newRun, "New journey", _focus == (HasSave ? 1 : 0), BtnScale, pal);
        DrawButton(spriteBatch, font, pixel, _levelEditor, "Level editor", _focus == (HasSave ? 2 : 1), BtnScale, pal);
        DrawButton(spriteBatch, font, pixel, _storyDebug, "Story debug", _focus == (HasSave ? 3 : 2), BtnScale, pal);
        DrawButton(spriteBatch, font, pixel, _quit, "Quit to desktop", _focus == FocusMax, BtnScale, pal);
    }

    private static void DrawButton(
        SpriteBatch spriteBatch,
        SpriteFont font,
        Texture2D pixel,
        Rectangle bounds,
        string label,
        bool focused,
        float scale,
        UiThemePalette pal)
    {
        var fill = focused ? pal.ButtonFillFocused : pal.ButtonFill;
        var border = focused ? pal.Accent : pal.ButtonBorder;
        UiChrome.FillRect(spriteBatch, pixel, bounds, fill, border);
        UiChrome.DrawLabel(spriteBatch, font, label, bounds, focused ? pal.ButtonLabelFocused : pal.ButtonLabel, scale);
    }
}
