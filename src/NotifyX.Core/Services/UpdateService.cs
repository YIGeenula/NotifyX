using System.Net.Http;
using System.Text.Json;
using Serilog;

namespace CodexBlaze.PaymentReminder.Core.Services;

public record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseTitle,
    string ReleaseNotes,
    string? DownloadUrl,
    string? ReleasePageUrl,
    long FileSizeBytes
);

public interface IUpdateService
{
    string CurrentVersion { get; }
    Task<UpdateCheckResult> CheckForUpdatesAsync();
    Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    void ApplyUpdateAndRestart(string downloadedFilePath);
}

public class UpdateService : IUpdateService
{
    // GitHub public repository details - No API keys or authentication required!
    private const string RepoOwner = "YIGeenula";
    private const string RepoName = "NotifyX";
    private static readonly HttpClient _httpClient;

    static UpdateService()
    {
        _httpClient = new HttpClient();
        // GitHub API requires a User-Agent header
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NotifyX-PaymentReminder-Updater");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public string CurrentVersion
    {
        get
        {
            var version = typeof(UpdateService).Assembly.GetName().Version;
            return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.1";
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check returned status code: {StatusCode}", response.StatusCode);
                return new UpdateCheckResult(false, CurrentVersion, CurrentVersion, string.Empty, string.Empty, null, null, 0);
            }

            var jsonStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(jsonStream);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string releaseTitle = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? "" : "";
            string releaseNotes = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : "";

            // Normalize version strings (e.g., "v1.0.2" -> "1.0.2")
            string cleanLatest = tagName.TrimStart('v', 'V').Trim();
            string cleanCurrent = CurrentVersion.TrimStart('v', 'V').Trim();

            bool isNewer = CompareVersions(cleanLatest, cleanCurrent) > 0;

            string? downloadUrl = null;
            long fileSize = 0;

            if (root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
            {
                // Find matching asset: prefer NotifyX.exe, NotifyX.zip, or first .exe
                foreach (var asset in assetsElem.EnumerateArray())
                {
                    string assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string assetDownload = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                    long size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;

                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = assetDownload;
                        fileSize = size;
                        if (assetName.Equals("NotifyX.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            break; // Highest priority
                        }
                    }
                }
            }

            return new UpdateCheckResult(
                IsUpdateAvailable: isNewer,
                CurrentVersion: cleanCurrent,
                LatestVersion: cleanLatest,
                ReleaseTitle: releaseTitle,
                ReleaseNotes: releaseNotes,
                DownloadUrl: downloadUrl,
                ReleasePageUrl: htmlUrl,
                FileSizeBytes: fileSize
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to check for updates from GitHub Releases.");
            return new UpdateCheckResult(false, CurrentVersion, CurrentVersion, string.Empty, string.Empty, null, null, 0);
        }
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "NotifyX_Update");
        Directory.CreateDirectory(tempDir);

        string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        if (string.IsNullOrEmpty(fileName)) fileName = "NotifyX_Update.exe";

        string destinationPath = Path.Combine(tempDir, fileName);

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double percentage = (double)totalBytesRead / totalBytes.Value * 100.0;
                progress?.Report(percentage);
            }
        }

        return destinationPath;
    }

    public void ApplyUpdateAndRestart(string downloadedFilePath)
    {
        string currentProcessPath = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(currentProcessPath))
        {
            throw new InvalidOperationException("Could not determine current executable path.");
        }

        string tempScriptPath = Path.Combine(Path.GetTempPath(), $"NotifyX_Updater_{Guid.NewGuid():N}.bat");

        // Write a self-contained helper script that waits for current process to exit, swaps executable, and launches new version
        string scriptContent = $@"@echo off
timeout /t 2 /nobreak >nul
copy /y ""{downloadedFilePath}"" ""{currentProcessPath}"" >nul
start """" ""{currentProcessPath}""
del ""{downloadedFilePath}"" >nul 2>&1
(goto) 2>nul & del ""%~f0""
";
        File.WriteAllText(tempScriptPath, scriptContent);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{tempScriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        };

        System.Diagnostics.Process.Start(startInfo);
        Environment.Exit(0);
    }

    private static int CompareVersions(string v1, string v2)
    {
        if (Version.TryParse(v1, out var ver1) && Version.TryParse(v2, out var ver2))
        {
            return ver1.CompareTo(ver2);
        }
        return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
    }
}
