using System;
using System.IO;

namespace DownTrack.Core.Engines;

public static class EnginePaths
{
    public static string BaseDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DownTrack");

    public static string BinDirectory => Path.Combine(BaseDirectory, "bin");
    public static string TempDirectory => Path.Combine(BaseDirectory, "temp");

    public static string YtDlpPath => Path.Combine(BinDirectory, "yt-dlp.exe");
    public static string FfmpegPath => Path.Combine(BinDirectory, "ffmpeg.exe");
    public static string FfprobePath => Path.Combine(BinDirectory, "ffprobe.exe");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(BinDirectory);
        Directory.CreateDirectory(TempDirectory);
    }

    public static bool AreEnginesPresent()
    {
        return File.Exists(YtDlpPath) && File.Exists(FfmpegPath);
    }
}
