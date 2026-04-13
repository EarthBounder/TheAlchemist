using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TheAlchemist.Core;

public static class InputHelper
{
    public static bool Pressed(KeyboardState now, KeyboardState was, Keys key) =>
        now.IsKeyDown(key) && !was.IsKeyDown(key);
}

public interface IGameScreen
{
    void Update(GameTime gameTime, in UiFrameInput input);
    void Draw(GameTime gameTime, SpriteBatch spriteBatch);
}

public readonly struct UiFrameInput(
    Vector2 pointerVirtual,
    bool pointerDown,
    bool pointerPressed,
    bool confirmPressed,
    bool escapePressed,
    bool startPressed,
    bool backPressed,
    bool menuShortcutPressed,
    int navigateDelta,
    int navigateHorizontalDelta,
    bool hotNextDayPressed,
    Keys firstNewKey,
    bool rawEscapePressed,
    bool timeSlowerPressed,
    bool timeFasterPressed,
    bool followHold,
    Vector2 floorPanPixels,
    int scrollWheelDelta)
{
    public Vector2 PointerVirtual { get; } = pointerVirtual;
    public bool PointerDown { get; } = pointerDown;
    public bool PointerPressed { get; } = pointerPressed;
    public bool ConfirmPressed { get; } = confirmPressed;
    public bool EscapePressed { get; } = escapePressed;
    public bool StartPressed { get; } = startPressed;
    public bool BackPressed { get; } = backPressed;
    public bool MenuShortcutPressed { get; } = menuShortcutPressed;
    public int NavigateDelta { get; } = navigateDelta;
    public int NavigateHorizontalDelta { get; } = navigateHorizontalDelta;
    public bool HotNextDayPressed { get; } = hotNextDayPressed;
    public Keys FirstNewKey { get; } = firstNewKey;
    public bool RawEscapePressed { get; } = rawEscapePressed;
    public bool TimeSlowerPressed { get; } = timeSlowerPressed;
    public bool TimeFasterPressed { get; } = timeFasterPressed;
    public bool FollowHold { get; } = followHold;
    public Vector2 FloorPanPixels { get; } = floorPanPixels;
    /// <summary>Raw SDL/Windows wheel delta since last frame (typically ±120 per notch).</summary>
    public int ScrollWheelDelta { get; } = scrollWheelDelta;
}

public sealed class ThumbstickNavSampler
{
    private float _cooldownSeconds;

    public int Sample(GameTime gameTime, GamePadState pad)
    {
        _cooldownSeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_cooldownSeconds > 0f)
            return 0;

        float y = pad.ThumbSticks.Left.Y;
        if (y >= 0.6f)
        {
            _cooldownSeconds = 0.16f;
            return -1;
        }

        if (y <= -0.6f)
        {
            _cooldownSeconds = 0.16f;
            return 1;
        }

        return 0;
    }
}

public sealed class VirtualViewport
{
    public Rectangle DestinationRectangle { get; private set; }

    private int _virtualW;
    private int _virtualH;

    public void Update(int backBufferW, int backBufferH, int virtualW, int virtualH)
    {
        _virtualW = Math.Max(1, virtualW);
        _virtualH = Math.Max(1, virtualH);

        float scale = Math.Min(backBufferW / (float)_virtualW, backBufferH / (float)_virtualH);
        int w = Math.Max(1, (int)Math.Round(_virtualW * scale));
        int h = Math.Max(1, (int)Math.Round(_virtualH * scale));
        int x = (backBufferW - w) / 2;
        int y = (backBufferH - h) / 2;
        DestinationRectangle = new Rectangle(x, y, w, h);
    }

    public Vector2 ScreenToVirtual(Vector2 screen)
    {
        if (DestinationRectangle.Width <= 0 || DestinationRectangle.Height <= 0)
            return Vector2.Zero;

        float nx = (screen.X - DestinationRectangle.X) / DestinationRectangle.Width;
        float ny = (screen.Y - DestinationRectangle.Y) / DestinationRectangle.Height;
        nx = Math.Clamp(nx, 0f, 1f);
        ny = Math.Clamp(ny, 0f, 1f);
        return new Vector2(nx * _virtualW, ny * _virtualH);
    }
}

public enum UiThemeId
{
    Forest = 0
}

public sealed class UiThemePalette
{
    public required Color Letterbox { get; init; }
    public required Color TitleText { get; init; }
    public required Color ButtonFill { get; init; }
    public required Color ButtonFillFocused { get; init; }
    public required Color Accent { get; init; }
    public required Color ButtonBorder { get; init; }
    public required Color ButtonLabel { get; init; }
    public required Color ButtonLabelFocused { get; init; }
    public required Color PauseDim { get; init; }
    public required Color PrimaryWhite { get; init; }
}

public static class UiThemes
{
    private static readonly UiThemePalette Forest = new()
    {
        Letterbox = new Color(18, 19, 22),
        TitleText = new Color(237, 220, 162),
        ButtonFill = new Color(48, 53, 61),
        ButtonFillFocused = new Color(74, 83, 95),
        Accent = new Color(209, 163, 77),
        ButtonBorder = new Color(20, 22, 26),
        ButtonLabel = new Color(224, 229, 236),
        ButtonLabelFocused = Color.White,
        PauseDim = new Color(0, 0, 0, 135),
        PrimaryWhite = new Color(236, 238, 242)
    };

    public static UiThemePalette Get(UiThemeId _) => Forest;
}

public static class UiChrome
{
    public static void FillRect(SpriteBatch spriteBatch, Texture2D pixel, Rectangle bounds, Color fill, Color border)
    {
        spriteBatch.Draw(pixel, bounds, fill);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
        spriteBatch.Draw(pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
    }

    public static void DrawLabel(
        SpriteBatch spriteBatch,
        SpriteFont font,
        string label,
        Rectangle bounds,
        Color color,
        float scale)
    {
        Vector2 size = font.MeasureString(label) * scale;
        float x = bounds.X + (bounds.Width - size.X) * 0.5f;
        float y = bounds.Y + (bounds.Height - size.Y) * 0.5f;
        spriteBatch.DrawString(font, label, new Vector2(x, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
