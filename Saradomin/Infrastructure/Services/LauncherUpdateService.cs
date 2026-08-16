using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Saradomin.Utilities;

namespace Saradomin.Infrastructure.Services
{
    public class LauncherUpdateService : ILauncherUpdateService
    {
        private const string GitHubApiLatest =
            "https://api.github.com/repos/amilious-ba/AmiliousScape-Launcher/releases/latest";

        public event EventHandler<float> DownloadProgressChanged;

        public async Task<LauncherUpdateInfo> CheckForUpdateAsync(
            CancellationToken cancellationToken = default)
        {
            var localVersion = GetLocalVersion();
            

            using var http = CreateHttpClient();
            var json = await http.GetStringAsync(GitHubApiLatest, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagProp)
                ? tagProp.GetString() ?? ""
                : "";
            var releaseUrl = root.TryGetProperty("html_url", out var urlProp)
                ? urlProp.GetString() ?? ""
                : "";

            var assetName = GetAssetNameForCurrentOs();
            string assetUrl = null;

            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.GetProperty("name").GetString() != assetName)
                        continue;

                    assetUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            var remoteVersion = ParseTagVersion(tag);
            var updateAvailable =
                !string.IsNullOrEmpty(assetUrl)
                && remoteVersion != null
                && remoteVersion > localVersion;

            return new LauncherUpdateInfo
            {
                UpdateAvailable = updateAvailable,
                TagName = tag,
                ReleaseUrl = releaseUrl,
                AssetName = assetName,
                AssetDownloadUrl = assetUrl ?? ""
            };
        }

        public async Task DownloadAndApplyUpdateAsync(
            LauncherUpdateInfo info,
            CancellationToken cancellationToken = default)
        {
            if (info == null || string.IsNullOrEmpty(info.AssetDownloadUrl))
                throw new InvalidOperationException("No update asset URL.");

            var home = CrossPlatform.GetAmiliousScapeHome();
            Directory.CreateDirectory(home);

            var runningPath = GetRunningPath();
            var tempName = OperatingSystem.IsWindows()
                ? "AmiliousScape-Launcher-update.exe"
                : "AmiliousScape-Launcher-update";
            var tempPath = Path.Combine(home, tempName);

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (var http = CreateHttpClient())
            using (var response = await http.GetAsync(
                       info.AssetDownloadUrl,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1L;
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = File.Create(tempPath);

                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    readTotal += read;
                    if (total > 0)
                        DownloadProgressChanged?.Invoke(this, (float)readTotal / total);
                }
            }

            DownloadProgressChanged?.Invoke(this, 1f);

            var pid = Environment.ProcessId;
            if (OperatingSystem.IsWindows())
                StartWindowsUpdateScript(pid, tempPath, runningPath);
            else
                StartUnixUpdateScript(pid, tempPath, runningPath);

            Environment.Exit(0);
        }

        private static Version GetLocalVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version
                    ?? new Version(0, 0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }

        private static Version ParseTagVersion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return null;

            var cleaned = tag.Trim().TrimStart('v', 'V');
            // allow "1.7.0" or "1.7.0.0"
            if (!Version.TryParse(cleaned, out var remote))
                return null;

            return new Version(remote.Major, remote.Minor, Math.Max(remote.Build, 0));
        }

        private static string GetRunningPath()
        {
            var path = Environment.ProcessPath
                       ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new Exception("Could not locate the running launcher executable.");
            return path;
        }

        private static string GetAssetNameForCurrentOs()
        {
            if (OperatingSystem.IsWindows())
                return "AmiliousScape-Launcher-win-x64.exe";
            return "AmiliousScape-Launcher-linux-x64";
        }

        private static HttpClient CreateHttpClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AmiliousScape-Launcher");
            return http;
        }

        private static void StartWindowsUpdateScript(int pid, string src, string dst)
        {
            var home = CrossPlatform.GetAmiliousScapeHome();
            var script = Path.Combine(home, "launcher-update.bat");

            File.WriteAllText(script, $@"@echo off
setlocal
set PID={pid}
set SRC={src}
set DST={dst}

:wait
tasklist /FI ""PID eq %PID%"" | find ""%PID%"" >nul
if not errorlevel 1 (
  timeout /t 1 /nobreak >nul
  goto wait
)

copy /Y ""%SRC%"" ""%DST%"" >nul
if errorlevel 1 (
  echo Update failed.
  pause
  exit /b 1
)

del ""%SRC%"" >nul 2>&1
start """" ""%DST%""
del ""%~f0"" >nul 2>&1
");

            Process.Start(new ProcessStartInfo
            {
                FileName = script,
                WorkingDirectory = home,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private static void StartUnixUpdateScript(int pid, string src, string dst)
        {
            var home = CrossPlatform.GetAmiliousScapeHome();
            var script = Path.Combine(home, "launcher-update.sh");

            File.WriteAllText(script, $@"#!/usr/bin/env bash
set -e
PID={pid}
SRC=""{src}""
DST=""{dst}""

while kill -0 ""$PID"" 2>/dev/null; do
  sleep 1
done

chmod +x ""$SRC""
mv -f ""$SRC"" ""$DST""
chmod +x ""$DST""
nohup ""$DST"" >/dev/null 2>&1 &
rm -f ""$0""
");

            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{script}\"",
                WorkingDirectory = home,
                UseShellExecute = false
            });
        }
    }
}