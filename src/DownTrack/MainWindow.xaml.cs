using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DownTrack.Core.Localization;
using DownTrack.Core.Models;
using Wpf.Ui.Controls;

namespace DownTrack;

public partial class MainWindow : FluentWindow
{
    private string _currentDirectory;
    public ObservableCollection<FileSystemItem> Items { get; } = new();
    public ObservableCollection<MediaItem> ActiveDownloads { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        // ברירת מחדל: תיקיית המוזיקה של המשתמש
        _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (string.IsNullOrEmpty(_currentDirectory) || !Directory.Exists(_currentDirectory))
        {
            _currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        FilesList.ItemsSource = Items;
        ActiveDownloadsList.ItemsSource = ActiveDownloads;

        ApplyLanguageDirection();
        LoadCurrentDirectory();
    }

    private void ApplyLanguageDirection()
    {
        FlowDirection = LocalizationService.Instance.IsRightToLeft 
            ? FlowDirection.RightToLeft 
            : FlowDirection.LeftToRight;
    }

    public void LoadCurrentDirectory()
    {
        try
        {
            TxtCurrentPath.Text = _currentDirectory;
            Items.Clear();

            var dirInfo = new DirectoryInfo(_currentDirectory);

            // טעינת תיקיות
            foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
            {
                if ((dir.Attributes & FileAttributes.Hidden) != 0) continue;
                Items.Add(new FileSystemItem { Name = dir.Name, FullPath = dir.FullName, IsDirectory = true });
            }

            // טעינת קבצי מדיה (MP3, MP4, FLAC, M4A, MKV)
            var mediaExtensions = new[] { ".mp3", ".mp4", ".flac", ".m4a", ".mkv", ".wav" };
            foreach (var file in dirInfo.GetFiles().Where(f => mediaExtensions.Contains(f.Extension.ToLower())).OrderBy(f => f.Name))
            {
                Items.Add(new FileSystemItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    SizeBytes = file.Length,
                    Extension = file.Extension.ToUpper(),
                    ModifiedDate = file.LastWriteTime,
                    IsDirectory = false
                });
            }

            LblStatus.Text = $"{Items.Count} פריטים בספרייה";
        }
        catch (Exception ex)
        {
            LblStatus.Text = $"שגיאה בטעינת תיקייה: {ex.Message}";
        }
    }

    private void BtnAddMedia_Click(object sender, RoutedEventArgs e)
    {
        // חלון הוספת מדיה מקבל את התיקייה הנוכחית כדי להוריד אליה ישירות
        var addDialog = new AddMediaWindow(_currentDirectory);
        addDialog.Owner = this;
        if (addDialog.ShowDialog() == true && addDialog.SelectedItems.Any())
        {
            foreach (var item in addDialog.SelectedItems)
            {
                ActiveDownloads.Add(item);
                StartDownload(item);
            }
        }
    }

    private async void StartDownload(MediaItem item)
    {
        var service = new Core.Engines.MediaEngineService();
        await service.DownloadItemAsync(item);
        
        // רענון הספרייה עם סיום ההורדה
        if (item.Status == DownloadStatus.Completed && item.TargetDirectory == _currentDirectory)
        {
            LoadCurrentDirectory();
        }
    }

    private void FilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FilesList.SelectedItem is FileSystemItem item && item.IsDirectory)
        {
            _currentDirectory = item.FullPath;
            LoadCurrentDirectory();
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_currentDirectory);
        if (parent != null && parent.Exists)
        {
            _currentDirectory = parent.FullName;
            LoadCurrentDirectory();
        }
    }

    private void BtnForward_Click(object sender, RoutedEventArgs e) { }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtSearch.Text?.Trim().ToLower() ?? "";
        if (string.IsNullOrEmpty(query))
        {
            FilesList.ItemsSource = Items;
        }
        else
        {
            FilesList.ItemsSource = Items.Where(i => i.Name.ToLower().Contains(query)).ToList();
        }
    }
}

public class FileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime ModifiedDate { get; set; }
    public bool IsDirectory { get; set; }

    public string SizeFormatted => IsDirectory ? "--" : $"{SizeBytes / (1024 * 1024):N1} MB";
    public string ModifiedDateFormatted => ModifiedDate.ToString("g");
}