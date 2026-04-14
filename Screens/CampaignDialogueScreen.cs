using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheAlchemist.Campaign;
using TheAlchemist.Core;

namespace TheAlchemist.Screens;

/// <summary>Plays one intro/interlude/outro beat in order; advances <see cref="GameFlowProgress"/> then returns to <see cref="Game1.PresentCampaignBeat"/>.</summary>
public sealed class CampaignDialogueScreen : IGameScreen
{
    private const float TitleScale = 0.78f;
    private const float MetaScale = 0.58f;
    private const float SpeakerScale = 1.05f;
    private const float BodyScale = 0.95f;
    private const float HintScale = 0.56f;
    private const float NextLabelScale = 0.58f;
    private const float PlaceholderScale = 0.62f;

    private readonly Game1 _game;
    private readonly LoadedCampaign _campaign;
    private readonly GameFlowProgress _progress;
    private int _lineIndex;
    private Texture2D _portrait;
    private readonly Rectangle _nextButton = new Rectangle(400, 600, 480, 56);

    public CampaignDialogueScreen(Game1 game, LoadedCampaign campaign, GameFlowProgress progress)
    {
        _game = game;
        _campaign = campaign;
        _progress = progress;
        _lineIndex = 0;
        SyncPortrait();
    }

    private void ReleasePortrait()
    {
        _portrait?.Dispose();
        _portrait = null;
    }

    private DialogueSegment CurrentSegment()
    {
        if (_campaign == null || _progress == null)
            return null;
        return _campaign.TryGetDialogueForBeat(_progress.CurrentBeatIndex, out DialogueSegment seg) ? seg : null;
    }

    private DialogueLine CurrentLine()
    {
        DialogueSegment seg = CurrentSegment();
        if (seg?.Lines == null || _lineIndex < 0 || _lineIndex >= seg.Lines.Count)
            return null;
        return seg.Lines[_lineIndex];
    }

    private void SyncPortrait()
    {
        ReleasePortrait();
        DialogueSegment seg = CurrentSegment();
        DialogueLine line = CurrentLine();
        if (seg == null || line == null)
            return;
        if (!DialoguePortraitPaths.TryLoadPortraitTexture(_game.GraphicsDevice, seg, line, out _portrait))
            _portrait = null;
    }

    private void AdvanceBeatAndContinue()
    {
        ReleasePortrait();
        if (_progress == null)
        {
            _game.ShowMainMenu();
            return;
        }

        _game.SaveCampaignCheckpoint();
        _progress.TryAdvance();
        if (_progress.IsComplete)
        {
            _game.EndCampaignRun();
            _game.ShowMainMenu();
            return;
        }

        _game.PresentCampaignBeat();
    }

    public void Update(GameTime gameTime, in UiFrameInput input)
    {
        DialogueSegment seg = CurrentSegment();
        if (seg == null)
        {
            AdvanceBeatAndContinue();
            return;
        }

        if (seg.Lines == null || seg.Lines.Count == 0)
        {
            AdvanceBeatAndContinue();
            return;
        }

        if (input.EscapePressed || input.BackPressed)
        {
            ReleasePortrait();
            _game.EndCampaignRun();
            _game.ShowMainMenu();
            return;
        }

        if (!input.ConfirmPressed && !input.PointerPressed)
            return;

        int mx = (int)input.PointerVirtual.X;
        int my = (int)input.PointerVirtual.Y;
        bool clickNext = input.PointerPressed && _nextButton.Contains(mx, my);
        if (!input.ConfirmPressed && !clickNext)
            return;

        _lineIndex++;
        if (_lineIndex >= seg.Lines.Count)
        {
            _lineIndex = 0;
            AdvanceBeatAndContinue();
            return;
        }

        SyncPortrait();
    }

    public void Draw(GameTime gameTime, SpriteBatch spriteBatch)
    {
        var font = _game.UiFont;
        var pixel = _game.Pixel;
        var pal = _game.UiPalette;

        spriteBatch.DrawString(font, "Campaign", new Vector2(48, 28), pal.TitleText, 0f, Vector2.Zero, TitleScale,
            SpriteEffects.None, 0f);

        DialogueSegment seg = CurrentSegment();
        DialogueLine line = CurrentLine();
        if (seg == null || line == null)
        {
            spriteBatch.DrawString(font, "(loading next beat...)", new Vector2(48, 200), pal.PrimaryWhite * 0.85f, 0f,
                Vector2.Zero, MetaScale, SpriteEffects.None, 0f);
            return;
        }

        var portraitBox = new Rectangle(48, 120, 260, 260);
        UiChrome.FillRect(spriteBatch, pixel, portraitBox, pal.ButtonFill, pal.ButtonBorder);
        if (_portrait != null)
            DrawPortraitFit(spriteBatch, _portrait, portraitBox);
        else
        {
            spriteBatch.DrawString(font, "(no portrait)", new Vector2(portraitBox.X + 16, portraitBox.Y + 100),
                pal.ButtonLabel * 0.7f, 0f, Vector2.Zero, PlaceholderScale, SpriteEffects.None, 0f);
        }

        float ty = 120;
        string beatLabel =
            $"Beat {_progress.CurrentBeatIndex} ({_progress.CurrentBeat})  line {_lineIndex + 1}/{seg.Lines.Count}";
        spriteBatch.DrawString(font, beatLabel, new Vector2(340, ty), pal.PrimaryWhite * 0.75f, 0f, Vector2.Zero,
            MetaScale, SpriteEffects.None, 0f);
        ty += font.LineSpacing * MetaScale + 8;

        if (!string.IsNullOrWhiteSpace(line.Speaker))
        {
            spriteBatch.DrawString(font, line.Speaker + ":", new Vector2(340, ty), pal.Accent, 0f, Vector2.Zero,
                SpeakerScale, SpriteEffects.None, 0f);
            ty += font.LineSpacing * SpeakerScale + 10;
        }

        string body = string.IsNullOrEmpty(line.Text) ? "..." : line.Text;
        DrawWrapped(spriteBatch, font, body, new Rectangle(340, (int)ty, 900, 460), pal.PrimaryWhite, BodyScale);

        UiChrome.FillRect(spriteBatch, pixel, _nextButton, pal.ButtonFill, pal.Accent);
        UiChrome.DrawLabel(spriteBatch, font, "Next (Space / Enter / Click)", _nextButton, pal.ButtonLabel,
            NextLabelScale);
        spriteBatch.DrawString(font, "Esc: abandon campaign", new Vector2(48, 668), pal.PrimaryWhite * 0.65f, 0f,
            Vector2.Zero, HintScale, SpriteEffects.None, 0f);
    }

    private static void DrawPortraitFit(SpriteBatch spriteBatch, Texture2D tex, Rectangle box)
    {
        float s = Math.Min(box.Width / (float)tex.Width, box.Height / (float)tex.Height);
        int w = Math.Max(1, (int)(tex.Width * s));
        int h = Math.Max(1, (int)(tex.Height * s));
        var r = new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
        spriteBatch.Draw(tex, r, Color.White);
    }

    private static void DrawWrapped(SpriteBatch spriteBatch, SpriteFont font, string text, Rectangle area, Color color,
        float scale)
    {
        float lineH = font.LineSpacing * scale;
        float y = area.Y;
        float maxW = area.Width;
        string[] words = text.Split(' ');
        string line = "";
        foreach (string word in words)
        {
            string trial = string.IsNullOrEmpty(line) ? word : line + " " + word;
            bool fits = font.MeasureString(trial).X * scale <= maxW;
            if (fits || string.IsNullOrEmpty(line))
            {
                line = trial;
                continue;
            }

            spriteBatch.DrawString(font, line, new Vector2(area.X, y), color, 0f, Vector2.Zero, scale,
                SpriteEffects.None, 0f);
            y += lineH;
            if (y > area.Bottom - lineH)
                return;
            line = word;
        }

        if (!string.IsNullOrEmpty(line))
            spriteBatch.DrawString(font, line, new Vector2(area.X, y), color, 0f, Vector2.Zero, scale,
                SpriteEffects.None, 0f);
    }
}
