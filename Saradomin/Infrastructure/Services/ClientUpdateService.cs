using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Saradomin.Model.Settings.Launcher;
using Saradomin.Utilities;

namespace Saradomin.Infrastructure.Services
{
    public class ClientUpdateService : IClientUpdateService
    {
        private readonly ISettingsService _settingsService;

        private float CurrentDownloadProgress { get; set; }

        // Just used for display / reference
        public string ClientDownloadURL => "https://github.com/amilious-ba/AmiliousScape-Client/releases/latest";

        private const string GitHubApiLatest = "https://api.github.com/repos/amilious-ba/AmiliousScape-Client/releases/latest";

        public string PreferredTargetFilePath =>
            CrossPlatform.GetAmiliousScapeExecutable();

        public event EventHandler<float> DownloadProgressChanged;

        public ClientUpdateService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        public async Task<string> FetchRemoteClientHashAsync(CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Saradomin-Launcher");

            var releaseJson = await httpClient.GetStringAsync(GitHubApiLatest, cancellationToken);
            using var doc = JsonDocument.Parse(releaseJson);

            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == "AmiliousScape.jar")
                {
                    // GitHub returns "sha256:abcdef123..."
                    var digest = asset.GetProperty("digest").GetString();
                    if (digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    {
                        return digest.Substring(7).ToUpperInvariant(); // remove "sha256:" prefix
                    }
                }
            }

            throw new Exception("Could not find AmiliousScape.jar or its digest in the latest GitHub release.");
        }

        public async Task FetchRemoteClientExecutableAsync(CancellationToken cancellationToken,
            string targetPath = null)
        {
            CurrentDownloadProgress = 0;
            targetPath ??= PreferredTargetFilePath;

            if (File.Exists(targetPath))
                File.Delete(targetPath);

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Saradomin-Launcher");

            // Resolve the real download URL from the latest release
            var releaseJson = await httpClient.GetStringAsync(GitHubApiLatest, cancellationToken);
            using var doc = JsonDocument.Parse(releaseJson);

            string downloadUrl = null;
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == "AmiliousScape.jar")
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
                throw new Exception("Could not find AmiliousScape.jar in the latest GitHub release.");

            var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? 12 * 1024 * 1024f;

            using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var outFileStream = File.OpenWrite(targetPath);

            var data = new byte[8192];
            long totalRead = 0;

            while (true)
            {
                var dataRead = await responseStream.ReadAsync(data, 0, data.Length, cancellationToken);
                if (dataRead <= 0)
                    break;

                await outFileStream.WriteAsync(data.AsMemory(0, dataRead), cancellationToken);
                totalRead += dataRead;

                CurrentDownloadProgress = (float)(totalRead / contentLength);
                DownloadProgressChanged?.Invoke(this, CurrentDownloadProgress);
            }
        }

        public async Task<string> ComputeLocalClientHashAsync(string filePath = null)
        {
            filePath ??= PreferredTargetFilePath;

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Unable to calculate local client hash. File '{filePath}' missing.");

            await using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            stream.Position = 0;
            var hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToUpperInvariant();
        }
    }
}