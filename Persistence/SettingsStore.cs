using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using TheAlchemist.Core;

namespace TheAlchemist.Persistence;

public enum KeyBindingSlot
{
    ConfirmPrimary,
    ConfirmAlternate,
    PauseKey,
    MenuShortcutKey,
    NavDownPrimary,
    NavDownAlternate,
    NavUpPrimary,
    NavUpAlternate,
    NextDayKey
}

public sealed class GameSettings
{
    public UiThemeId ThemeId { get; set; } = UiThemeId.Forest;
    public bool Fullscreen { get; set; }
    public Keys ConfirmPrimary { get; set; } = Keys.Enter;
    public Keys ConfirmAlternate { get; set; } = Keys.Space;
    public Keys PauseKey { get; set; } = Keys.Escape;
    public Keys MenuShortcutKey { get; set; } = Keys.M;
    public Keys NavDownPrimary { get; set; } = Keys.Down;
    public Keys NavDownAlternate { get; set; } = Keys.S;
    public Keys NavUpPrimary { get; set; } = Keys.Up;
    public Keys NavUpAlternate { get; set; } = Keys.W;
    public Keys NextDayKey { get; set; } = Keys.N;

    public void SetKey(KeyBindingSlot slot, Keys key)
    {
        switch (slot)
        {
            case KeyBindingSlot.ConfirmPrimary: ConfirmPrimary = key; break;
            case KeyBindingSlot.ConfirmAlternate: ConfirmAlternate = key; break;
            case KeyBindingSlot.PauseKey: PauseKey = key; break;
            case KeyBindingSlot.MenuShortcutKey: MenuShortcutKey = key; break;
            case KeyBindingSlot.NavDownPrimary: NavDownPrimary = key; break;
            case KeyBindingSlot.NavDownAlternate: NavDownAlternate = key; break;
            case KeyBindingSlot.NavUpPrimary: NavUpPrimary = key; break;
            case KeyBindingSlot.NavUpAlternate: NavUpAlternate = key; break;
            case KeyBindingSlot.NextDayKey: NextDayKey = key; break;
            default: throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
        }
    }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string _appFolder = "TheAlchemist";

    public static void SetApplicationFolder(string appFolder)
    {
        if (!string.IsNullOrWhiteSpace(appFolder))
            _appFolder = appFolder;
    }

    private static string SettingsFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), _appFolder, "settings.json");

    public static GameSettings LoadOrDefault()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return new GameSettings();

            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<GameSettings>(json, JsonOptions) ?? new GameSettings();
        }
        catch
        {
            return new GameSettings();
        }
    }

    public static bool TrySave(GameSettings settings)
    {
        try
        {
            string dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
            return true;
        }
        catch
        {
            return false;
        }
    }
}
