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
    private Rectangle _continueStory;
    private Rectangle _mainMenu;
    private Rectangle _quit;
    private int _focus;
    private const float BtnScale = 1.05f;

    public PauseMenuScreen(Game1 game, WorldState world)
    {
        _game = game;
        _world = world;
    }

    private bool CampaignMissionPause => _game.IsCampaignRunActive && _world.IsCampaignMissionRun;

    private int FocusMax => CampaignMissionPause ? 3 : 2;

    private void EnsureLayout()
    {
        var font = _game.UiFont;
        const int padX = 24;
        const int padY = 12;
        const string a = "Resume";
        const string b = "Continue Story";
        const string c = "Main Menu";
        const string d = "Quit to Desktop";
        float mw = Math.Max(Math.Max(font.MeasureString(a).X, font.MeasureString(c).X), font.MeasureString(d).X) *
                   BtnScale;
        if (CampaignMissionPause)
            mw = Math.Max(mw, font.MeasureString(b).X * BtnScale);
        float mh = font.MeasureString(a).Y * BtnScale;
        int w = (int)Math.Ceiling(mw) + padX * 2;
        int h = (int)Math.Ceiling(mh) + padY * 2;
        int x = (GameConfig.DesignWidth - w) / 2;
        int y = 220;
        _resume = new Rectangle(x, y, w, h);
        y = _resume.Bottom + 12;
        if (CampaignMissionPause)
        {
            _continueStory = new Rectangle(x, y, w, h);
            y = _continueStory.Bottom + 12;
        }
        else
            _continueStory = Rectangle.Empty;

        _mainMenu = new Rectangle(x, y, w, h);
        y = _mainMenu.Bottom + 12;
        _quit = new Rectangle(x, y, w, h);
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        EnsureLayout();

        int mx = (int)input.PointerVirtual.X;
        int my = (int)input.PointerVirtual.Y;

        if (_resume.Contains(mx, my))
            _focus = 0;
        else if (CampaignMissionPause && _continueStory.Contains(mx, my))
            _focus = 1;
        else if (_mainMenu.Contains(mx, my))
            _focus = CampaignMissionPause ? 2 : 1;
        else if (_quit.Contains(mx, my))
            _focus = CampaignMissionPause ? 3 : 2;

        if (input.NavigateDelta != 0)
        {
            _focus += input.NavigateDelta;
            if (_focus < 0)
                _focus = FocusMax;
            if (_focus > FocusMax)
                _focus = 0;
        }

        bool hitR = input.PointerPressed && _resume.Contains(mx, my);
        bool hitStory = CampaignMissionPause && input.PointerPressed && _continueStory.Contains(mx, my);
        bool hitM = input.PointerPressed && _mainMenu.Contains(mx, my);
        bool hitQ = input.PointerPressed && _quit.Contains(mx, my);

        if (hitR || (input.ConfirmPressed && _focus == 0) || input.EscapePressed)
        {
            _game.SaveWorld(_world);
            _game.ShowWorld(_world);
            return;
        }

        if (CampaignMissionPause &&
            (hitStory || (input.ConfirmPressed && _focus == 1)))
        {
            _game.AdvanceCampaignAfterMission();
            return;
        }

        int mainFocus = CampaignMissionPause ? 2 : 1;
        int quitFocus = CampaignMissionPause ? 3 : 2;

        if (hitM || (input.ConfirmPressed && _focus == mainFocus))
        {
            _game.SaveWorld(_world);
            _game.ShowMainMenu();
            return;
        }

        if (hitQ || (input.ConfirmPressed && _focus == quitFocus))
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
        if (CampaignMissionPause)
            DrawBtn(spriteBatch, font, pixel, _continueStory, "Continue Story", _focus == 1, pal);
        DrawBtn(spriteBatch, font, pixel, _mainMenu, "Main Menu", _focus == (CampaignMissionPause ? 2 : 1), pal);
        DrawBtn(spriteBatch, font, pixel, _quit, "Quit to Desktop", _focus == (CampaignMissionPause ? 3 : 2), pal);
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
