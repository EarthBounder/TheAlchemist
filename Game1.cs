using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheAlchemist.Campaign;
using TheAlchemist.Core;
using TheAlchemist.Persistence;
using TheAlchemist.Screens;
using TheAlchemist.World;

namespace TheAlchemist;

public sealed class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private KeyboardState _keyboardWas;
    private MouseState _mouseWas;
    private GamePadState _padWas;
    private readonly ThumbstickNavSampler _thumbstickNav = new();
    private bool _middleMousePanActive;
    private Vector2 _middleMousePanLastVirtual;

    private RenderTarget2D _uiScene = null!;
    private readonly VirtualViewport _viewport = new();

    private IGameScreen _screen = null!;
    private GameSettings _settings = null!;
    private EditorMapData _testPlayMap;
    private EditorMapData _campaignPlayMap;
    private WorldState _campaignCheckpointWorld;
    private LoadedCampaign _campaign;
    private GameFlowProgress _campaignProgress;
    private bool _campaignRunActive;

    public SpriteFont UiFont { get; private set; } = null!;
    public SpriteFont TitleFont { get; private set; } = null!;
    public SpriteFont MenuButtonFont { get; private set; } = null!;
    public Texture2D Pixel { get; private set; } = null!;
    public Texture2D HeroSprite { get; private set; } = null!;
    public Texture2D TitleRightSprite { get; private set; } = null!;

    public GameSettings Settings => _settings;
    public UiThemePalette UiPalette => UiThemes.Get(_settings.ThemeId);

    public Game1()
    {
        SettingsStore.SetApplicationFolder("TheAlchemist");
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = GameConfig.Title;

        _settings = SettingsStore.LoadOrDefault();
        ApplyWindowModeFromSettings();
    }

    public void ShowMainMenu()
    {
        _testPlayMap = null;
        EndCampaignRun();
        DisposeLevelEditorIfActive();
        _screen = new MainMenuScreen(this);
    }

    public void ShowLevelEditor()
    {
        EndCampaignRun();
        DisposeLevelEditorIfActive();
        _screen = new LevelEditorScreen(this);
    }

    /// <summary>Intro, interludes, and outro only (missions skipped). For dialogue authoring.</summary>
    public void ShowCampaignDebugStory()
    {
        EndCampaignRun();
        DisposeLevelEditorIfActive();
        if (!CampaignLoader.TryLoad(CampaignLoader.DefaultCampaignRoot, out LoadedCampaign campaign, out string err))
        {
            _screen = new CampaignDebugStoryScreen(this, null, err ?? "Unknown error.");
            return;
        }

        _screen = new CampaignDebugStoryScreen(this, campaign, null);
    }

    /// <summary>Starts a new campaign run: intro, (mission, interlude)*, outro per <see cref="GameFlowSchedule"/>.</summary>
    public void BeginNewCampaignJourney()
    {
        EndCampaignRun();
        DisposeLevelEditorIfActive();
        if (!CampaignLoader.TryLoad(CampaignLoader.DefaultCampaignRoot, out LoadedCampaign campaign, out string err))
        {
            _screen = new CampaignDebugStoryScreen(this, null, err ?? "Unknown error.");
            return;
        }

        _campaign = campaign;
        _campaignProgress = campaign.CreateFlowProgress();
        _campaignRunActive = true;
        PresentCampaignBeat();
    }

    public void PresentCampaignBeat()
    {
        if (!_campaignRunActive || _campaign == null || _campaignProgress == null)
        {
            ShowMainMenu();
            return;
        }

        if (_campaignProgress.IsComplete)
        {
            EndCampaignRun();
            ShowMainMenu();
            return;
        }

        switch (_campaignProgress.CurrentBeat)
        {
            case GameFlowBeat.Intro:
            case GameFlowBeat.Interlude:
            case GameFlowBeat.Outro:
                _screen = new CampaignDialogueScreen(this, _campaign, _campaignProgress);
                break;
            case GameFlowBeat.Mission:
                StartCampaignMissionWorld();
                break;
        }
    }

    public void AdvanceCampaignAfterMission()
    {
        if (!_campaignRunActive || _campaignProgress == null)
            return;
        if (_campaignProgress.CurrentBeat != GameFlowBeat.Mission)
            return;
        SaveCampaignCheckpoint();
        _campaignProgress.TryAdvance();
        _campaignPlayMap = null;
        PresentCampaignBeat();
    }

    public void SaveCampaignCheckpoint()
    {
        if (!_campaignRunActive || _campaignCheckpointWorld == null)
            return;
        SaveStore.TrySave(_campaignCheckpointWorld);
    }

    public void EndCampaignRun()
    {
        _campaignRunActive = false;
        _campaign = null;
        _campaignProgress = null;
        _campaignPlayMap = null;
        _campaignCheckpointWorld = null;
    }

    public bool IsCampaignRunActive => _campaignRunActive;

    private void StartCampaignMissionWorld()
    {
        int mi = GameFlowSchedule.MissionOrdinal(_campaignProgress.CurrentBeatIndex, _campaignProgress.MissionCount);
        MissionDefinition def = _campaign.Missions[mi];
        if (_campaign.TryLoadMapForMission(mi, out EditorMapData map) && map != null)
        {
            _campaignPlayMap = EditorMapData.Clone(map);
            WorldState world = WorldState.NewCampaignMissionPlay(_campaignPlayMap);
            world.CampaignMissionHerbGoal = def.RequiredHerbs;
            world.CampaignMissionHerbsAtStart = world.HerbsCollected;
            world.CampaignMissionCompleted = false;
            _campaignCheckpointWorld = world;
            _screen = new WorldPlayScreen(this, world, _campaignPlayMap);
        }
        else
        {
            int seed = Environment.TickCount;
            var world = WorldState.NewRun(seed);
            world.IsCampaignMissionRun = true;
            world.IsTestMapRun = true;
            world.CampaignMissionHerbGoal = def.RequiredHerbs;
            world.CampaignMissionHerbsAtStart = world.HerbsCollected;
            world.CampaignMissionCompleted = false;
            _campaignCheckpointWorld = world;
            _screen = new WorldPlayScreen(this, world, null);
        }
    }

    public void ShowWorld(WorldState world)
    {
        if (!world.IsCampaignMissionRun)
            EndCampaignRun();
        else
            _campaignCheckpointWorld = world;

        EditorMapData custom = null;
        if (world.IsTestMapRun)
        {
            custom = world.IsCampaignMissionRun ? _campaignPlayMap : _testPlayMap;
            if (custom == null)
                world.IsTestMapRun = false;
        }

        _screen = new WorldPlayScreen(this, world, custom);
    }

    public void ShowPause(WorldState world) => _screen = new PauseMenuScreen(this, world);

    public void PlayTestMap(EditorMapData mapSnapshot)
    {
        EndCampaignRun();
        DisposeLevelEditorIfActive();
        _testPlayMap = EditorMapData.Clone(mapSnapshot);
        _screen = new WorldPlayScreen(this, WorldState.NewTestMapPlay(_testPlayMap), _testPlayMap);
    }

    private void DisposeLevelEditorIfActive()
    {
        if (_screen is LevelEditorScreen ed)
            ed.DisposeGpuResources();
    }

    public void SaveWorld(WorldState world)
    {
        if (world.IsTestMapRun && !world.IsCampaignMissionRun)
            return;
        SaveStore.TrySave(world);
    }

    public void SetTheme(UiThemeId id)
    {
        _settings.ThemeId = id;
        SettingsStore.TrySave(_settings);
    }

    public void SetFullscreen(bool fullscreen)
    {
        _settings.Fullscreen = fullscreen;
        ApplyWindowModeFromSettings();
        SyncBackBufferToWindow();
        SettingsStore.TrySave(_settings);
    }

    public void SetKeyBinding(KeyBindingSlot slot, Keys key)
    {
        _settings.SetKey(slot, key);
        SettingsStore.TrySave(_settings);
    }

    private void ApplyWindowModeFromSettings()
    {
        if (_settings.Fullscreen)
        {
            var dm = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = Math.Max(GameConfig.DesignWidth, dm.Width);
            _graphics.PreferredBackBufferHeight = Math.Max(GameConfig.DesignHeight, dm.Height);
        }
        else
        {
            _graphics.PreferredBackBufferWidth = GameConfig.DesignWidth;
            _graphics.PreferredBackBufferHeight = GameConfig.DesignHeight;
        }

        _graphics.IsFullScreen = _settings.Fullscreen;
        _graphics.HardwareModeSwitch = false;
        _graphics.ApplyChanges();
    }

    protected override void Initialize()
    {
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (_, _) => SyncBackBufferToWindow();

        _screen = new MainMenuScreen(this);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        UiFont = Content.Load<SpriteFont>("Fonts/Roboto");
        TitleFont = Content.Load<SpriteFont>("Fonts/Title");
        MenuButtonFont = Content.Load<SpriteFont>("Fonts/MenuButton");
        HeroSprite = Content.Load<Texture2D>("art/hero/hero");
        TitleRightSprite = Content.Load<Texture2D>("art/title/title_right");

        Pixel = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
        Pixel.SetData(new[] { Color.White });

        _uiScene = new RenderTarget2D(
            GraphicsDevice,
            GameConfig.DesignWidth,
            GameConfig.DesignHeight,
            false,
            SurfaceFormat.Color,
            DepthFormat.None,
            0,
            RenderTargetUsage.DiscardContents);

        SyncBackBufferToWindow();
    }

    protected override void UnloadContent()
    {
        _uiScene.Dispose();
        Pixel.Dispose();
        base.UnloadContent();
    }

    private void SyncBackBufferToWindow()
    {
        int w = Math.Max(320, Window.ClientBounds.Width);
        int h = Math.Max(240, Window.ClientBounds.Height);
        if (_graphics.PreferredBackBufferWidth != w || _graphics.PreferredBackBufferHeight != h)
        {
            _graphics.PreferredBackBufferWidth = w;
            _graphics.PreferredBackBufferHeight = h;
            _graphics.ApplyChanges();
        }

        var pp = GraphicsDevice.PresentationParameters;
        _viewport.Update(pp.BackBufferWidth, pp.BackBufferHeight, GameConfig.DesignWidth, GameConfig.DesignHeight);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            Exit();

        var keyboard = Keyboard.GetState();
        bool altEnter = (keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt)) &&
                        InputHelper.Pressed(keyboard, _keyboardWas, Keys.Enter);
        if (altEnter)
        {
            _settings.Fullscreen = !_settings.Fullscreen;
            ApplyWindowModeFromSettings();
            SyncBackBufferToWindow();
            SettingsStore.TrySave(_settings);
        }

        var mouse = Mouse.GetState();
        var pad = GamePad.GetState(PlayerIndex.One);

        var input = BuildUiInput(gameTime, keyboard, mouse, pad);

        _screen.Update(gameTime, in input);

        _keyboardWas = keyboard;
        _mouseWas = mouse;
        _padWas = pad;

        base.Update(gameTime);
    }

    private static bool KeyPressed(KeyboardState now, KeyboardState was, Keys key) =>
        InputHelper.Pressed(now, was, key);

    private bool IsConfirmKeyPress(KeyboardState kb, KeyboardState was, Keys key)
    {
        if (!KeyPressed(kb, was, key))
            return false;
        if (key == Keys.Enter && (kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt)))
            return false;
        return true;
    }

    private static Keys GetFirstNewNonModifierKey(KeyboardState now, KeyboardState was)
    {
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (key == Keys.None)
                continue;
            if (key is Keys.LeftAlt or Keys.RightAlt or Keys.LeftShift or Keys.RightShift or Keys.LeftControl
                or Keys.RightControl)
                continue;
            if (KeyPressed(now, was, key))
                return key;
        }

        return Keys.None;
    }

    private UiFrameInput BuildUiInput(GameTime gameTime, KeyboardState kb, MouseState mouse, GamePadState pad)
    {
        var pointerScreen = new Vector2(mouse.X, mouse.Y);
        var pointerVirtual = _viewport.ScreenToVirtual(pointerScreen);

        bool pointerDown = mouse.LeftButton == ButtonState.Pressed;
        bool pointerPressed = pointerDown && _mouseWas.LeftButton == ButtonState.Released;

        bool confirm =
            IsConfirmKeyPress(kb, _keyboardWas, _settings.ConfirmPrimary) ||
            IsConfirmKeyPress(kb, _keyboardWas, _settings.ConfirmAlternate) ||
            (pad.IsConnected && pad.IsButtonDown(Buttons.A) && !_padWas.IsButtonDown(Buttons.A));

        bool escape = KeyPressed(kb, _keyboardWas, _settings.PauseKey);
        bool rawEscape = KeyPressed(kb, _keyboardWas, Keys.Escape);
        bool start = pad.IsConnected && pad.IsButtonDown(Buttons.Start) && !_padWas.IsButtonDown(Buttons.Start);
        bool back = pad.IsConnected && pad.IsButtonDown(Buttons.B) && !_padWas.IsButtonDown(Buttons.B);

        bool menuShortcut = KeyPressed(kb, _keyboardWas, _settings.MenuShortcutKey);

        int nav = 0;
        if (KeyPressed(kb, _keyboardWas, _settings.NavDownPrimary) ||
            KeyPressed(kb, _keyboardWas, _settings.NavDownAlternate))
            nav = 1;
        else if (KeyPressed(kb, _keyboardWas, _settings.NavUpPrimary) ||
                 KeyPressed(kb, _keyboardWas, _settings.NavUpAlternate))
            nav = -1;
        else if (pad.IsConnected)
        {
            if (pad.IsButtonDown(Buttons.DPadDown) && !_padWas.IsButtonDown(Buttons.DPadDown))
                nav = 1;
            else if (pad.IsButtonDown(Buttons.DPadUp) && !_padWas.IsButtonDown(Buttons.DPadUp))
                nav = -1;
            else
                nav = _thumbstickNav.Sample(gameTime, pad);
        }

        if (nav == 0 && KeyPressed(kb, _keyboardWas, Keys.Tab))
            nav = kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift) ? -1 : 1;

        int navH = 0;
        if (KeyPressed(kb, _keyboardWas, Keys.Left))
            navH = -1;
        else if (KeyPressed(kb, _keyboardWas, Keys.Right))
            navH = 1;
        else if (pad.IsConnected)
        {
            if (pad.IsButtonDown(Buttons.DPadLeft) && !_padWas.IsButtonDown(Buttons.DPadLeft))
                navH = -1;
            else if (pad.IsButtonDown(Buttons.DPadRight) && !_padWas.IsButtonDown(Buttons.DPadRight))
                navH = 1;
        }

        bool hotNext = KeyPressed(kb, _keyboardWas, _settings.NextDayKey);
        if (pad.IsConnected && pad.IsButtonDown(Buttons.Y) && !_padWas.IsButtonDown(Buttons.Y))
            hotNext = true;

        Keys firstNewKey = GetFirstNewNonModifierKey(kb, _keyboardWas);

        bool timeSlower = KeyPressed(kb, _keyboardWas, Keys.OemOpenBrackets);
        bool timeFaster = KeyPressed(kb, _keyboardWas, Keys.OemCloseBrackets);

        bool followHold = pointerDown || (pad.IsConnected && pad.IsButtonDown(Buttons.A));

        Vector2 floorPan = Vector2.Zero;
        bool middleNow = mouse.MiddleButton == ButtonState.Pressed;
        if (middleNow)
        {
            if (_middleMousePanActive)
                floorPan += pointerVirtual - _middleMousePanLastVirtual;
            _middleMousePanLastVirtual = pointerVirtual;
        }

        _middleMousePanActive = middleNow;

        int scrollDelta = mouse.ScrollWheelValue - _mouseWas.ScrollWheelValue;
        const float wheelPixelsPerStep = 36f / 120f;
        float scrollPx = scrollDelta * wheelPixelsPerStep;
        if (scrollPx != 0f)
        {
            if (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift))
                floorPan.X += scrollPx;
            else
                floorPan.Y -= scrollPx;
        }

        return new UiFrameInput(pointerVirtual, pointerDown, pointerPressed, confirm, escape, start, back, menuShortcut,
            nav, navH, hotNext, firstNewKey, rawEscape, timeSlower, timeFaster, followHold, floorPan, scrollDelta);
    }

    protected override void Draw(GameTime gameTime)
    {
        var letterbox = UiPalette.Letterbox;

        GraphicsDevice.SetRenderTarget(_uiScene);
        GraphicsDevice.Clear(letterbox);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
        _screen.Draw(gameTime, _spriteBatch);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(letterbox);

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.AlphaBlend);
        _spriteBatch.Draw(_uiScene, _viewport.DestinationRectangle, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
