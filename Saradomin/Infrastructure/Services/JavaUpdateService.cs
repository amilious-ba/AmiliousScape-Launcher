using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Saradomin.Utilities;

namespace Saradomin.Infrastructure.Services
{
    public class JavaUpdateService : IJavaUpdateService
    {
        public event EventHandler<JavaUpdateInfo> JavaDownloadProgressChanged;

        public async Task DownloadAndSetJava(
            ISettingsService settingsService,
            JavaDistribution distribution)
        {
            var downloadUrl = distribution.GetDownloadUrl();
            var tools = CrossPlatform.GetToolsDirectory();
            Directory.CreateDirectory(tools);

            var extension = downloadUrl.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                ? ".tar.gz"
                : Path.GetExtension(downloadUrl);

            var downloadPath = Path.Combine(tools, distribution.FolderName + extension);
            var extractedPath = distribution.GetInstallDirectory();
            var major = distribution.MajorVersion;

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync(
                    downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength ?? 40 * 1024 * 1024L;
                var totalRead = 0L;
                var buffer = new byte[8192];

                await using (var fileStream = new FileStream(
                    downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                await using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    int bytesRead;
                    do
                    {
                        bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead <= 0)
                            break;

                        totalRead += bytesRead;
                        await fileStream.WriteAsync(buffer, 0, bytesRead);

                        var progress = (float)totalRead / contentLength;
                        JavaDownloadProgressChanged?.Invoke(
                            this,
                            new JavaUpdateInfo(major, downloadUrl, progress, false));
                    } while (bytesRead > 0);
                }
            }

            JavaDownloadProgressChanged?.Invoke(
                this,
                new JavaUpdateInfo(major, downloadUrl, 1f, false));

            if (Directory.Exists(extractedPath))
                Directory.Delete(extractedPath, true);

            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var tempDir = Path.Combine(tools, distribution.FolderName + "_temp");
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                await Task.Run(() => ZipFile.ExtractToDirectory(downloadPath, tempDir));

                var subDirs = Directory.GetDirectories(tempDir);
                if (subDirs.Length == 0)
                    throw new InvalidOperationException(
                        $"Java archive for {distribution.DisplayName} had no root folder.");

                Directory.Move(subDirs[0], extractedPath);
                Directory.Delete(tempDir, true);
            }
            else
            {
                Directory.CreateDirectory(extractedPath);
                await Task.Run(() => CrossPlatform.RunCommandAndGetOutput(
                    $"tar xf \"{downloadPath}\" -C \"{extractedPath}\" --strip-components 1"));
            }

            File.Delete(downloadPath);

            settingsService.Launcher.JavaExecutableLocation = distribution.GetJavaExecutablePath();
            settingsService.SaveAll();

            JavaDownloadProgressChanged?.Invoke(
                this,
                new JavaUpdateInfo(major, downloadUrl, 1f, true));
        }
    }
}