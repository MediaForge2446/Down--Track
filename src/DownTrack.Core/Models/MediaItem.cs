using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DownTrack.Core.Models;

public enum MediaFormat
{
    AudioMp3,
    VideoMp4
}

public enum MediaQuality
{
    Best,
    High1080p,
    Medium720p,
    Audio320k,
    Audio192k
}

public enum DownloadStatus
{
    Pending,
    Downloading,
    Converting,
    Completed,
    Failed,
    Cancelled
}

public class MediaItem : INotifyPropertyChanged
{
    private string _title = string.Empty;
    private MediaFormat _format = MediaFormat.AudioMp3;
    private MediaQuality _quality = MediaQuality.Audio320k;
    private DownloadStatus _status = DownloadStatus.Pending;
    private double _progressPercent;
    private string _statusMessage = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = string.Empty;

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public string OriginalTitle { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }

    public MediaFormat SelectedFormat
    {
        get => _format;
        set
        {
            if (SetField(ref _format, value))
            {
                // Auto adjust default quality based on format
                Quality = value == MediaFormat.AudioMp3 ? MediaQuality.Audio320k : MediaQuality.High1080p;
            }
        }
    }

    public MediaQuality Quality
    {
        get => _quality;
        set => SetField(ref _quality, value);
    }

    public DownloadStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string TargetDirectory { get; set; } = string.Empty;
    public string? FinalFilePath { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
