using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Glitonea.Extensions;
using Glitonea.Mvvm.Messaging;
using Saradomin.Infrastructure;
using Saradomin.Utilities;

namespace Saradomin.Infrastructure.Services
{
    public class ClientLaunchService : IClientLaunchService
    {
        private readonly ISettingsService _settingsService;
        private readonly IClientUpdateService _clientUpdateService;

        public ClientLaunchService(
            ISettingsService settingsService,
            IClientUpdateService clientUpdateService)
        {
            _settingsService = settingsService;
            _clientUpdateService = clientUpdateService;
        }

        public async Task LaunchClient()
        {
            var home = CrossPlatform.GetAmiliousScapeHome();
            var jarPath = _clientUpdateService.PreferredTargetFilePath;
            var javaPath = _settingsService.Launcher.JavaExecutableLocation;

            var uiScale = _settingsService.Client.UiScale;
            var fps = _settingsService.Client.Fps;

            var args =
                $"-Dsun.java2d.uiScale={uiScale} " +
                $"-DclientFps={fps} " +
                $"-DclientHomeOverride=\"{home}/\" " +
                $"-jar \"{jarPath}\"";

            // Close launcher immediately: no log streaming
            if (_settingsService.Launcher.ExitAfterLaunchingClient)
            {
                var procExit = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = args,
                        WorkingDirectory = home,
                        UseShellExecute = false
                    }
                };
                procExit.Start();
                Application.Current!.GetDesktopLifetime().Shutdown();
                return;
            }

            new ClientLogMessage("Launching...").Broadcast();
            new ClientLogMessage($"Java: {javaPath}").Broadcast();
            new ClientLogMessage($"Args: {args}").Broadcast();
            new ClientLogMessage($"WorkDir: {home}").Broadcast();
            new ClientLogMessage($"Jar exists: {File.Exists(jarPath)}").Broadcast();
            new ClientLogMessage("----- client output -----").Broadcast();

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = args,
                    WorkingDirectory = home,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    new ClientLogMessage(e.Data).Broadcast();
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    new ClientLogMessage("[ERR] " + e.Data).Broadcast();
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();
            new ClientLogMessage($"----- exit code {proc.ExitCode} -----").Broadcast();
        }
    }
}