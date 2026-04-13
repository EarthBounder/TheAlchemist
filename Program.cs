using System;

namespace TheAlchemist;

internal static class Program
{
    /// <summary>WinForms file dialogs (e.g. Load Background) require an STA thread.</summary>
    [STAThread]
    private static void Main()
    {
        using var game = new Game1();
        game.Run();
    }
}
