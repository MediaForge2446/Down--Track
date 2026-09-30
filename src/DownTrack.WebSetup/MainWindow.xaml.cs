using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DownTrack.WebSetup;

public partial class MainWindow : Window
{
    private readonly InstallerEngine _engine = new();
    private CancellationTokenSource? _cts;
    private bool _isHebrew = true;
    private bool _isCompleted = false;

    public MainWindow()
    {
        InitializeComponent();
        MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) DragMove(); };
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await StartInstallation();
    }

    private async Task StartInstallation()
    {
        BtnAction.Visibility = Visibility.Collapsed;
        _isCompleted = false;
        _cts = new CancellationTokenSource();

        var progress = new Progress<double>(val => PrgBar.Value = val);
        var status = new Progress<string>(text => LblStatus.Text = text);

        try
        {
            await _engine.InstallAsync(progress, status, _cts.Token);
            
            _isCompleted = true;
            BtnAction.Content = _isHebrew ? "הפעל את DownTrack" : "Launch DownTrack";
            BtnAction.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            InstallerEngine.Log("Install flow failed", ex);
            LblStatus.Text = _isHebrew ? "ההתקנה נכשלה. עיין ב-installer.log" : "Installation failed. Check installer.log";
            BtnAction.Content = _isHebrew ? "נסה שוב" : "Try again";
            BtnAction.Visibility = Visibility.Visible;
        }
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        _isHebrew = !_isHebrew;
        BtnLang.Content = _isHebrew ? "English" : "עברית";
        FlowDirection = _isHebrew ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        LblSubtitle.Text = _isHebrew ? "מנהל ספריית המדיה שלך" : "Your personal media library manager";

        if (BtnAction.Visibility == Visibility.Visible)
        {
            if (_isCompleted)
                BtnAction.Content = _isHebrew ? "הפעל את DownTrack" : "Launch DownTrack";
            else
                BtnAction.Content = _isHebrew ? "נסה שוב" : "Try again";
        }
    }

    private async void BtnAction_Click(object sender, RoutedEventArgs e)
    {
        if (_isCompleted)
        {
            InstallerEngine.LaunchApp();
            Application.Current.Shutdown();
        }
        else
        {
            await StartInstallation();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Application.Current.Shutdown();
    }
}