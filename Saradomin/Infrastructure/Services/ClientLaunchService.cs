using Avalonia;
using System.IO;
using System.Diagnostics;
using Glitonea.Extensions;
using Saradomin.Utilities;
using System.Threading.Tasks;

namespace Saradomin.Infrastructure.Services {
    
    public class ClientLaunchService : IClientLaunchService{
    
        private readonly ISettingsService _settingsService;
        private readonly IClientUpdateService _clientUpdateService;

        public ClientLaunchService(ISettingsService settingsService, IClientUpdateService clientUpdateService) {
            _settingsService = settingsService;
            _clientUpdateService = clientUpdateService;
        }

        public async Task LaunchClient() {
            
            var home = CrossPlatform.GetAmiliousScapeHome();
            var jarPath = _clientUpdateService.PreferredTargetFilePath;
            var javaPath = _settingsService.Launcher.JavaExecutableLocation;
            var uiScale = _settingsService.Client.UiScale;
            var fps = _settingsService.Client.Fps;
            
            var args =
                $"-Dsun.java2d.uiScale={uiScale} " +
                "-Dsun.java2d.noddraw=true "+
                "-Dsun.java2d.opengl=false "+
                "-Dsun.java2d.d3d=false "+
                $"-DclientFps={fps} " +
                $"-DclientHomeOverride=\"{home}/\" " +
                $"-jar \"{jarPath}\"";

            // Close launcher immediately: no log streaming, hide console window
            if (_settingsService.Launcher.ExitAfterLaunchingClient) {
                var procExit = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = javaPath,
                        Arguments = args,
                        WorkingDirectory = home,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    }
                };
                procExit.Start();
                Application.Current!.GetDesktopLifetime()?.Shutdown();
                return;
            }

            // Keep launcher open: stream logs to the Log tab
            new ClientLogMessage("Launching...").Broadcast();
            new ClientLogMessage($"Java: {javaPath}").Broadcast();
            new ClientLogMessage($"Args: {args}").Broadcast();
            new ClientLogMessage($"WorkDir: {home}").Broadcast();
            new ClientLogMessage($"Jar exists: {File.Exists(jarPath)}").Broadcast();
            new ClientLogMessage("----- client output -----").Broadcast();

            var proc = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = javaPath,
                    Arguments = args,
                    WorkingDirectory = home,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
            };

            proc.OutputDataReceived += OutputDataReceived;
            proc.ErrorDataReceived += ErrorDataReceived;

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();
            new ClientLogMessage($"----- exit code {proc.ExitCode} -----").Broadcast();
        }

        private void ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            if (string.IsNullOrEmpty(e.Data)) return;
            new ClientLogMessage("[ERR] " + e.Data).Broadcast();
        }

        private static void OutputDataReceived(object sender, DataReceivedEventArgs e) {
            if (string.IsNullOrEmpty(e.Data)) return;
            new ClientLogMessage(e.Data).Broadcast();
        }
    }
}