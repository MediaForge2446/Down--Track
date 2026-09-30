using System.Windows;
using DownTrack.Core.Engines;
using DownTrack.Core.Logging;
using Wpf.Ui.Appearance;

namespace DownTrack;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppLogger.Initialize("DownTrack");
        EnginePaths.EnsureDirectories();

        // התאמה למצב Dark של האפליקציה
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
    }
}
