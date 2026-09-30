using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DownTrack.WebSetup;

public class InstallerEngine
{
    // נתיב ההורדה של החבילה המאוחדת מ-GitHub Release שלך
    public const string PackageUrl = "[https://github.com/MediaForge2446/Down-Track/releases/latest/download/DownTrack-FullPackage.zip](https://github.com/MediaForge2446/Down-Track/releases/latest/download/DownTrack-FullPackage.zip)";

    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DownTrack");

    public static string LogFilePath =>
        Path.Combine(InstallDir, "logs", "installer.log");

    public static void Log(string message, Exception? ex = null)
    {
        try
        {
            var logDir = Path.GetDirectoryName(LogFilePath)!;
            Directory.CreateDirectory(logDir);

            var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}";
            if (ex != null) line += $" | EXCEPTION: {ex.Message}
{ex.StackTrace}";

            File.AppendAllText(LogFilePath, line + Environment.NewLine);
            Console.WriteLine(line);
        }
        catch { }
    }

    public async Task InstallAsync(IProgress<double> progress, IProgress<string> statusText, CancellationToken ct = default)
    {
        Log("=== DownTrack WebSetup Started ===");
        Directory.CreateDirectory(InstallDir);

        var zipPath = Path.Combine(InstallDir, "package.zip");

        // שלב 1: הורדת חבילת ה-ZIP המאוחדת
        statusText.Report("מוריד את חבילת התוכנה...");
        Log($"Downloading package from: {PackageUrl}");

        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DownTrack-WebSetup");

            using var response = await client.GetAsync(PackageUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            var bytesRead = 0;
            var totalRead = 0L;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var pct = (double)totalRead / totalBytes * 75.0; // 0% - 75% הורדה
                    progress.Report(pct);
                }
            }
        }

        Log("Download completed successfully.");

        // שלב 2: פריסת הקבצים
        statusText.Report("מתקין רכיבים...");
        progress.Report(80.0);
        Log($"Extracting {zipPath} to {InstallDir}");

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.Combine(InstallDir, entry.FullName);
                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                if (!string.IsNullOrEmpty(entry.Name))
                {
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        }, ct);

        try { File.Delete(zipPath); } catch { }

        progress.Report(90.0);
        Log("Extraction completed.");

        // שלב 3: יצירת קיצור דרך בשולחן העבודה ובתפריט התחל
        statusText.Report("יוצר קיצורי דרך...");
        var exePath = Path.Combine(InstallDir, "DownTrack.exe");
        CreateShortcuts(exePath);

        progress.Report(100.0);
        statusText.Report("ההתקנה הושלמה בהצלחה!");
        Log("Installation finished successfully.");
    }

    private static void CreateShortcuts(string targetExePath)
    {
        try
        {
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

            CreateVbsShortcut(Path.Combine(desktopDir, "DownTrack.lnk"), targetExePath);
            CreateVbsShortcut(Path.Combine(startMenuDir, "DownTrack.lnk"), targetExePath);
            Log("Shortcuts created.");
        }
        catch (Exception ex)
        {
            Log("Failed to create shortcuts", ex);
        }
    }

    private static void CreateVbsShortcut(string shortcutPath, string targetPath)
    {
        var vbsScript = $@"
Set oWS = WScript.CreateObject(""WScript.Shell"")
sLinkFile = ""{shortcutPath}""
Set oLink = oWS.CreateShortcut(sLinkFile)
oLink.TargetPath = ""{targetPath}""
oLink.WorkingDirectory = ""{Path.GetDirectoryName(targetPath)}""
oLink.Description = ""DownTrack - Media Library Manager""
oLink.Save";

        var tempVbs = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.vbs");
        File.WriteAllText(tempVbs, vbsScript);

        var p = Process.Start(new ProcessStartInfo
        {
            FileName = "cscript.exe",
            Arguments = $"//Nologo \"{tempVbs}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        p?.WaitForExit();
        try { File.Delete(tempVbs); } catch { }
    }

    public static void LaunchApp()
    {
        var exe = Path.Combine(InstallDir, "DownTrack.exe");
        if (File.Exists(exe))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = InstallDir,
                UseShellExecute = true
            });
        }
    }
}