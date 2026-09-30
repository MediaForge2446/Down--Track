using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DownTrack.Core.Logging;
using DownTrack.Core.Models;

namespace DownTrack.Core.Engines;

public class MediaEngineService
{
    private static readonly Regex ProgressRegex = new(@"\[download\]\s+(\d+\.?\d*)%", RegexOptions.Compiled);

    public async Task<List<MediaItem>> ExtractInfoAsync(string url, CancellationToken ct = default)
    {
        EnginePaths.EnsureDirectories();
        AppLogger.Info($"Extracting metadata for: {url}");

        var arguments = $"--flat-playlist --dump-single-json --no-warnings \"{url}\"";
        
        var startInfo = new ProcessStartInfo
        {
            FileName = EnginePaths.YtDlpPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(ct);
        var errorTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            AppLogger.Error($"yt-dlp extract error: {error}");
            throw new InvalidOperationException("Failed to extract media information.");
        }

        var items = new List<MediaItem>();
        using var doc = JsonDocument.Parse(output);
        var root = doc.RootElement;

        if (root.TryGetProperty("entries", out var entriesElement) && entriesElement.ValueKind == JsonValueKind.Array)
        {
            // Playlist
            foreach (var entry in entriesElement.EnumerateArray())
            {
                items.Add(ParseEntry(entry));
            }
        }
        else
        {
            // Single video / track
            items.Add(ParseEntry(root));
        }

        AppLogger.Info($"Successfully extracted {items.Count} items.");
        return items;
    }

    private static MediaItem ParseEntry(JsonElement entry)
    {
        var id = entry.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
        var title = entry.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "Unknown Title" : "Unknown Title";
        var durationSec = entry.TryGetProperty("duration", out var durProp) && durProp.TryGetDouble(out var d) ? d : 0;
        
        var thumb = string.Empty;
        if (entry.TryGetProperty("thumbnail", out var thumbProp))
        {
            thumb = thumbProp.GetString() ?? string.Empty;
        }

        var webUrl = entry.TryGetProperty("webpage_url", out var urlProp) 
            ? urlProp.GetString() 
            : (!string.IsNullOrEmpty(id) ? $"https://www.youtube.com/watch?v={id}" : string.Empty);

        return new MediaItem
        {
            Url = webUrl ?? string.Empty,
            Title = SanitizeFilename(title),
            OriginalTitle = title,
            ThumbnailUrl = thumb,
            Duration = TimeSpan.FromSeconds(durationSec),
            SelectedFormat = MediaFormat.AudioMp3,
            Quality = MediaQuality.Audio320k
        };
    }

    public async Task DownloadItemAsync(MediaItem item, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        EnginePaths.EnsureDirectories();
        item.Status = DownloadStatus.Downloading;
        item.ProgressPercent = 0;

        var ffmpegDir = Path.GetDirectoryName(EnginePaths.FfmpegPath);
        var outputTemplate = Path.Combine(item.TargetDirectory, $"{item.Title}.%(ext)s");

        string args;
        if (item.SelectedFormat == MediaFormat.AudioMp3)
        {
            args = $"--extract-audio --audio-format mp3 --audio-quality 0 --ffmpeg-location \"{ffmpegDir}\" -o \"{outputTemplate}\" \"{item.Url}\"";
        }
        else
        {
            args = $"-f \"bv*[ext=mp4]+ba[ext=m4a]/b[ext=mp4]/best\" --ffmpeg-location \"{ffmpegDir}\" -o \"{outputTemplate}\" \"{item.Url}\"";
        }

        AppLogger.Info($"Starting download for '{item.Title}' into '{item.TargetDirectory}'");

        var startInfo = new ProcessStartInfo
        {
            FileName = EnginePaths.YtDlpPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        
        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            var match = ProgressRegex.Match(e.Data);
            if (match.Success && double.TryParse(match.Groups[1].Value, out var pct))
            {
                item.ProgressPercent = pct;
                progress?.Report(pct);
            }
        };

        process.Start();
        process.BeginOutputReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode == 0)
        {
            item.Status = DownloadStatus.Completed;
            item.ProgressPercent = 100;
            AppLogger.Info($"Download completed: {item.Title}");
        }
        else
        {
            item.Status = DownloadStatus.Failed;
            AppLogger.Error($"Download failed with code {process.ExitCode}");
        }
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            name = name.Replace(c, '_');
        }
        return name.Trim();
    }
}
