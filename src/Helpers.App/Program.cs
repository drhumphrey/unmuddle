using Avalonia;
using Avalonia.Media;
using Helpers.App.Services;
using Helpers.Core.Settings;
using Helpers.Core.Platform;

namespace Helpers.App;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    /// <summary>This copy's claim to be the only one running. Null only in the visual designer.</summary>
    public static ISingleInstance? Instance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // One copy at a time: two would fight over the shortcuts. A second copy tells the first and leaves.
        using var instance = AppPlatform.Current.TryClaimSingleInstance("Unmuddle-single-instance");
        if (instance is null)
        {
            return;
        }

        Instance = instance;

        // The first versions were called Helpers. Before anything reads its settings, carry the
        // folders, the saved key and the start-up entry across to the Unmuddle names.
        DataFolderMigration.MoveBoth();
        AppPlatform.Current.MoveFromPreviousName(AssistantService.KeyName);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .With(new FontManagerOptions
            {
                // Lexend, bundled under Assets/Fonts (SIL Open Font Licence), for all app text.
                DefaultFamilyName = "avares://Unmuddle/Assets/Fonts#Lexend",
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily("Segoe UI") }],
            })
            .LogToTrace();
}
