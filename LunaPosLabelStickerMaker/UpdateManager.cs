using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LunaPosLabelStickerMaker;

public class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("releaseDate")]
    public string ReleaseDate { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("installerFileName")]
    public string InstallerFileName { get; set; } = "LunaPOSLabelStickerMakerSetup.exe";

    [JsonPropertyName("changelog")]
    public string Changelog { get; set; } = "";

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; } = false;
}

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = AppConfig.CurrentVersion;
    public string LatestVersion { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Changelog { get; set; } = "";
    public bool IsMandatory { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class UpdateManager
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    static UpdateManager()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LunaPOS-UpdateClient/1.0");
    }

    /// <summary>
    /// Checks for available updates from the cloud update service.
    /// Never exposes URLs, server details, or internal credentials to the UI.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var result = new UpdateCheckResult
        {
            CurrentVersion = AppConfig.CurrentVersion
        };

        if (AppConfig.IsTrial)
        {
            // Trial versions do not auto-update
            return result;
        }

        try
        {
            string json = await _httpClient.GetStringAsync(AppConfig.UpdateManifestUrl);
            if (string.IsNullOrWhiteSpace(json))
            {
                result.ErrorMessage = "Received an empty response from update server.";
                return result;
            }

            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                result.ErrorMessage = "Invalid update manifest format.";
                return result;
            }

            result.LatestVersion = manifest.Version.Trim();
            result.DownloadUrl = manifest.DownloadUrl.Trim();
            result.Changelog = manifest.Changelog.Trim();
            result.IsMandatory = manifest.Mandatory;

            if (IsNewerVersion(result.LatestVersion, result.CurrentVersion))
            {
                result.HasUpdate = true;
            }

            return result;
        }
        catch (HttpRequestException)
        {
            result.ErrorMessage = "Unable to connect to the LunaPOS update service. Please check your internet connection.";
            return result;
        }
        catch (TaskCanceledException)
        {
            result.ErrorMessage = "Connection to update server timed out. Please check your network.";
            return result;
        }
        catch (Exception)
        {
            result.ErrorMessage = "Could not check for updates at this time.";
            return result;
        }
    }

    /// <summary>
    /// Compares two version strings (e.g., "1.0.1" > "1.0.0").
    /// </summary>
    public static bool IsNewerVersion(string remoteVer, string currentVer)
    {
        remoteVer = remoteVer.TrimStart('v', 'V');
        currentVer = currentVer.TrimStart('v', 'V');

        if (Version.TryParse(remoteVer, out var rVer) && Version.TryParse(currentVer, out var cVer))
        {
            return rVer > cVer;
        }

        // Fallback: compare segment by segment
        string[] rParts = remoteVer.Split('.');
        string[] cParts = currentVer.Split('.');
        int maxLen = Math.Max(rParts.Length, cParts.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int r = (i < rParts.Length && int.TryParse(rParts[i], out int rv)) ? rv : 0;
            int c = (i < cParts.Length && int.TryParse(cParts[i], out int cv)) ? cv : 0;
            if (r > c) return true;
            if (r < c) return false;
        }

        return false;
    }

    /// <summary>
    /// Downloads the update installer with live progress reporting.
    /// </summary>
    public static async Task DownloadUpdateAsync(
        string downloadUrl,
        string targetFilePath,
        IProgress<(long bytesReceived, long totalBytes, int percentage)> progress,
        CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        long receivedBytes = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        byte[] buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
            receivedBytes += bytesRead;

            int percentage = totalBytes > 0 ? (int)((receivedBytes * 100) / totalBytes) : 0;
            progress.Report((receivedBytes, totalBytes, percentage));
        }
    }

    /// <summary>
    /// Launches the downloaded installer and terminates current application so it can upgrade seamlessly.
    /// </summary>
    public static void ApplyUpdateAndRestart(string installerPath)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("Update installer not found.", installerPath);

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/CLOSEAPPLICATIONS",
            UseShellExecute = true
        };

        Process.Start(psi);
        Application.Exit();
    }
}
