using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheAlchemist.Core;
using TheAlchemist.World;

namespace TheAlchemist.Screens;

public sealed class PauseMenuScreen : IGameScreen
{
    private readonly Game1 _game;
    private readonly WorldState _world;
    private Rectangle _resume;
    private Rectangle _mainMenu;
    private Rectangle _quit;
    private int _focus;
    private const float BtnScale = 1.05f;

    public PauseMenuScreen(Game1 game, WorldState world)
    {
        _game = game;
        _world = world;
    }

    private void EnsureLayout()
    {
        var font = _game.UiFont;
        const int padX = 24;
        const int padY = 12;
        const string a = "Resume";
        const string b = "Main menu";
        const string c = "Quit to desktop";
        float mw = Math.Max(Math.Max(font.MeasureString(a).X, font.MeasureString(b).X), font.MeasureString(c).X) *
                   BtnScale;
        float mh = font.MeasureString(a).Y * BtnScale;
        int w = (int)Math.Ceiling(mw) + padX * 2;
        int h = (int)Math.Ceiling(mh) + padY * 2;
        int x = (GameConfig.DesignWidth - w) / 2;
        int y = 260;
        _resume = new Rectangle(x, y, w, h);
        _mainMenu = new Rectangle(x, y + h + 12, w, h);
        _quit = new Rectangle(x, y + (h + 12) * 2, w, h);
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        EnsureLayout();

        int mx = (int)input.PointerVirtual.X;
        int my = (int)input.PointerVirtual.Y;

        if (_resume.Contains(mx, my))
            _focus = 0;
        else if (_mainMenu.Contains(mx, my))
            _focus = 1;
        else if (_quit.Contains(mx, my))
            _focus = 2;

        if (input.NavigateDelta != 0)
        {
            _focus += input.NavigateDelta;
            if (_focus < 0)
                _focus = 2;
            if (_focus > 2)
                _focus = 0;
        }

        bool hitR = input.PointerPressed && _resume.Contains(mx, my);
        bool hitM = input.PointerPressed && _mainMenu.Contains(mx, my);
        bool hitQ = input.PointerPressed && _quit.Contains(mx, my);

        if (hitR || (input.ConfirmPressed && _focus == 0) || input.EscapePressed)
        {
            _game.SaveWorld(_world);
            _game.ShowWorld(_world);
            return;
        }

        if (hitM || (input.ConfirmPressed && _focus == 1))
        {
            _game.SaveWorld(_world);
            _game.ShowMainMenu();
            return;
        }

        if (hitQ || (input.ConfirmPressed && _focus == 2))
            _game.Exit();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        EnsureLayout();
        var font = _game.UiFont;
        var pixel = _game.Pixel;
        var pal = _game.UiPalette;

        spriteBatch.Draw(pixel, new Rectangle(0, 0, GameConfig.DesignWidth, GameConfig.DesignHeight), pal.PauseDim);

        DrawBtn(spriteBatch, font, pixel, _resume, "Resume", _focus == 0, pal);
        DrawBtn(spriteBatch, font, pixel, _mainMenu, "Main menu", _focus == 1, pal);
        DrawBtn(spriteBatch, font, pixel, _quit, "Quit to desktop", _focus == 2, pal);
    }

    private static void DrawBtn(SpriteBatch sb, SpriteFont font, Texture2D pixel, Rectangle r, string t, bool hot,
        UiThemePalette pal)
    {
        var fill = hot ? pal.ButtonFillFocused : pal.ButtonFill;
        var edge = hot ? pal.Accent : pal.ButtonBorder;
        UiChrome.FillRect(sb, pixel, r, fill, edge);
        UiChrome.DrawLabel(sb, font, t, r, hot ? pal.ButtonLabelFocused : pal.ButtonLabel, BtnScale);
    }
}
