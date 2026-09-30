using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DownTrack.Core.Engines;
using DownTrack.Core.Localization;
using DownTrack.Core.Models;
using Wpf.Ui.Controls;

namespace DownTrack;

public partial class AddMediaWindow : FluentWindow
{
    private readonly string _targetDirectory;
    public ObservableCollection<MediaItem> FoundItems { get; } = new();
    public List<MediaItem> SelectedItems { get; private set; } = new();

    public AddMediaWindow(string targetDirectory)
    {
        InitializeComponent();
        _targetDirectory = targetDirectory;
        ItemsList.ItemsSource = FoundItems;

        FlowDirection = LocalizationService.Instance.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    private async void BtnFetch_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtUrl.Text?.Trim();
        if (string.IsNullOrEmpty(url)) return;

        BtnFetch.IsEnabled = false;
        PnlLoading.Visibility = Visibility.Visible;
        FoundItems.Clear();

        try
        {
            var service = new MediaEngineService();
            var items = await service.ExtractInfoAsync(url);

            foreach (var item in items)
            {
                item.TargetDirectory = _targetDirectory;
                FoundItems.Add(item);
            }

            BtnApplyAll.Visibility = FoundItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnDownloadAll.IsEnabled = FoundItems.Any();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"שגיאה בשליפת המידע: {ex.Message}", "DownTrack", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnFetch.IsEnabled = true;
            PnlLoading.Visibility = Visibility.Collapsed;
        }
    }

    private void TxtUrl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            BtnFetch_Click(sender, e);
        }
    }

    private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is MediaItem item)
        {
            FoundItems.Remove(item);
            BtnDownloadAll.IsEnabled = FoundItems.Any();
            BtnApplyAll.Visibility = FoundItems.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void BtnApplyAll_Click(object sender, RoutedEventArgs e)
    {
        if (!FoundItems.Any()) return;
        var firstFormat = FoundItems.First().SelectedFormat;

        foreach (var item in FoundItems)
        {
            item.SelectedFormat = firstFormat;
        }
    }

    private void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
    {
        SelectedItems = FoundItems.ToList();
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is MediaFormat format ? (int)format : 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int index ? (MediaFormat)index : MediaFormat.AudioMp3;
    }
}