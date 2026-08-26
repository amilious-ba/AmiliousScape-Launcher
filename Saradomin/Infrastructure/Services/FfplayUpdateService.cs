using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Saradomin.Utilities;

namespace Saradomin.Infrastructure.Services
{
    public class FfplayUpdateService : IFfplayUpdateService
    {
        // GPL full builds include ffplay (essentials builds often do not)
        private const string WindowsUrl =
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

        private const string LinuxX64Url =
            "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz";

        public event EventHandler<float> DownloadProgressChanged;

        public async Task EnsureFfplayAsync()
        {
            var dest = CrossPlatform.GetFfplayExecutablePath();
            if (File.Exists(dest))
                return;

            Directory.CreateDirectory(CrossPlatform.GetToolsDirectory());
            Directory.CreateDirectory(CrossPlatform.GetFfplayDirectory());

            var url = GetDownloadUrl();
            var ext = url.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase) ? ".tar.xz"
                : url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip"
                : Path.GetExtension(url);

            var archivePath = Path.Combine(CrossPlatform.GetToolsDirectory(), "ffplay-download" + ext);
            var extractDir = Path.Combine(CrossPlatform.GetToolsDirectory(), "ffplay_temp");

            if (File.Exists(archivePath))
                File.Delete(archivePath);
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("AmiliousScape-Launcher");
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? 80 * 1024 * 1024L;
                var readTotal = 0L;
                var buffer = new byte[81920];

                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = File.Create(archivePath);
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, read);
                    readTotal += read;
                    DownloadProgressChanged?.Invoke(this, (float)readTotal / total);
                }
            }

            DownloadProgressChanged?.Invoke(this, 1f);
            Directory.CreateDirectory(extractDir);

            if (ext == ".zip")
            {
                await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, extractDir));
            }
            else
            {
                await Task.Run(() => CrossPlatform.RunCommandAndGetOutput(
                    $"tar xf \"{archivePath}\" -C \"{extractDir}\""));
            }

            var name = OperatingSystem.IsWindows() ? "ffplay.exe" : "ffplay";
            var found = Directory.GetFiles(extractDir, name, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (found == null)
                throw new FileNotFoundException($"{name} was not in the ffmpeg archive.");

            File.Copy(found, dest, overwrite: true);

            if (!OperatingSystem.IsWindows())
            {
                CrossPlatform.RunCommandAndGetOutput($"chmod +x \"{dest}\"");
            }

            Directory.Delete(extractDir, true);
            File.Delete(archivePath);
        }

        private static string GetDownloadUrl()
        {
            if (OperatingSystem.IsWindows())
                return WindowsUrl;

            if (OperatingSystem.IsLinux())
            {
                var arch = CrossPlatform.GetSystemArchitecture();
                if (arch != "x64")
                    throw new NotSupportedException("ffplay auto-download is only set up for Linux x64.");
                return LinuxX64Url;
            }

            throw new NotSupportedException("ffplay auto-download is not set up for this OS.");
        }
    }
}