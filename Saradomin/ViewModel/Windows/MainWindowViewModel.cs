using System;
using System.IO;
using System.Linq;
using Glitonea.Mvvm;
using System.Net.Http;
using HtmlAgilityPack;
using System.Threading;
using Avalonia.Metadata;
using Saradomin.Utilities;
using Saradomin.View.Windows;
using System.Threading.Tasks;
using Glitonea.Mvvm.Messaging;
using Saradomin.Infrastructure;
using Avalonia.Controls.Documents;
using Saradomin.Infrastructure.Services;
using Saradomin.Model.Settings.Launcher;

namespace Saradomin.ViewModel.Windows {
    
    public class MainWindowViewModel : ViewModelBase {
        
        private readonly ISettingsService _settingsService;
        private readonly IClientLaunchService _launchService;
        private readonly IClientUpdateService _updateService;
        private readonly IJavaUpdateService _javaUpdateService;
        private readonly IRemoteConfigService _remoteConfigService;
        private readonly ILauncherUpdateService _launcherUpdateService;

        private LauncherSettings Launcher { get; }

        public int SelectedTabIndex { get; set; } = 0;
        
        public string Title { get; set; } = "AmiliousScape Launcher";
        
        public string AmiliousNewsText { get; set; } = "Loading AmiliousScape news...";
        
        public string LaunchLog { get; set; } = "Client log will appear here when you press Play.\n";
        
        public bool CanLaunch { get; private set; } = true;
        public string LaunchText { get; private set; } = "Play AmiliousScape!";

        public bool DimContent { get; private set; }
        public InlineCollection HtmlInlines { get; private set; }

        public MainWindowViewModel(IClientLaunchService launchService, IClientUpdateService updateService,
            ISettingsService settingsService, IRemoteConfigService remoteConfigService,
            IJavaUpdateService javaUpdateService, ILauncherUpdateService launcherUpdateService) {
            _launchService = launchService;
            _updateService = updateService;
            _updateService.DownloadProgressChanged += OnClientDownloadProgressUpdated;
            _remoteConfigService = remoteConfigService;
            _javaUpdateService = javaUpdateService;
            _javaUpdateService.JavaDownloadProgressChanged += OnJavaDownloadProgressUpdated;
            _launcherUpdateService = launcherUpdateService;
            _launcherUpdateService.DownloadProgressChanged += OnLauncherDownloadProgressUpdated;

            _settingsService = settingsService;
            Launcher = _settingsService.Launcher;

            Message.Subscribe<MainViewLoadedMessage>(this, MainViewLoaded);
            Message.Subscribe<NotificationBoxStateChangedMessage>(this, NotificatationBoxStateChanged);
            Message.Subscribe<ClientLaunchRequestedMessage>(this, ClientLaunchRequested);
            Message.Subscribe<ClientLogMessage>(this, msg => AppendLog(msg.Text));
            Message.Subscribe<CheckLauncherUpdateMessage>(this, async _ =>
            {
                // Manual check from Settings — always allow the prompt
                Launcher.SkippedLauncherUpdateTag = "";
                await CheckLauncherUpdateAsync(promptIfAvailable: true);
            });

            _settingsService.Launcher.JavaExecutableLocation ??= CrossPlatform.LocateJavaExecutable();
        }

        private void OnLauncherDownloadProgressUpdated(object sender, float e) {
            LaunchText = $"Updating launcher... {e * 100:F0}%";
        }

        public void ExitApplication() {
            Environment.Exit(0);
        }

        public async void ClientLaunchRequested(ClientLaunchRequestedMessage _) {
            if (CanLaunch) await ExecuteLaunchSequence();
        }
        
        private void AppendLog(string line) {
            var stamp = DateTime.Now.ToString("HH:mm:ss");
            LaunchLog += $"[{stamp}] {line}{Environment.NewLine}";
            // Scroll AFTER log text has been updated
            new LogScrollRequestedMessage().Broadcast();
        }

        private HtmlNode ConnectionErrorMessage(HtmlDocument doc, string msg) {
            var failMessage = "<html><body><h3>Not Available<h3><br/>This content is unavailable, likely due to a ";
            doc.LoadHtml(failMessage + msg + "</body></html>");
            return doc.DocumentNode;
        }

        public async void MainViewLoaded(MainViewLoadedMessage _) {
            // Load both in parallel
            await Task.WhenAll(
                LoadAmiliousNewsAsync(),
                Load2009ScapeNewsAsync()
            );
            if (Launcher.CheckForLauncherUpdatesOnLaunch)
                await CheckLauncherUpdateAsync(promptIfAvailable: true);
        }

        private async Task CheckLauncherUpdateAsync(bool promptIfAvailable)
        {
            try
            {
                var info = await _launcherUpdateService.CheckForUpdateAsync();
                if (!info.UpdateAvailable)
                    return;

                // User already said Later for this release tag
                if (!string.IsNullOrEmpty(Launcher.SkippedLauncherUpdateTag)
                    && string.Equals(Launcher.SkippedLauncherUpdateTag, info.TagName, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!promptIfAvailable)
                    return;

                var tag = string.IsNullOrWhiteSpace(info.TagName) ? "latest" : info.TagName;
                var updateNow = await ChoiceBox.ShowAsync(
                    "Launcher update available",
                    $"A newer launcher is available ({tag}).\n\nUpdate now or later?",
                    "Update now",
                    "Later");

                if (updateNow)
                {
                    LaunchText = "Updating launcher...";
                    CanLaunch = false;
                    try
                    {
                        await _launcherUpdateService.DownloadAndApplyUpdateAsync(info);
                        // process exits inside Apply on success
                    }
                    catch (Exception ex)
                    {
                        CanLaunch = true;
                        LaunchText = "Play AmiliousScape!";
                        NotificationBox.DisplayNotification("Launcher update failed", ex.Message);
                    }
                }
                else
                {
                    Launcher.SkippedLauncherUpdateTag = info.TagName;
                    _settingsService.SaveAll();
                }
            }
            catch
            {
                // network / non-published build — ignore
            }
        }
        
        public Task CheckLauncherUpdateFromSettingsAsync()
            => CheckLauncherUpdateAsync(promptIfAvailable: true);

        private async Task LoadAmiliousNewsAsync() {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("AmiliousScape-Launcher");

                var json = await httpClient.GetStringAsync(
                    "https://api.github.com/repos/amilious-ba/RT4-Client/releases/latest");

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                var name = root.GetProperty("name").GetString() ?? root.GetProperty("tag_name").GetString();
                var body = root.TryGetProperty("body", out var bodyProp)
                    ? bodyProp.GetString() : "(no release notes)";
                var published = root.TryGetProperty("published_at", out var pub)
                    ? pub.GetString() : "";

                AmiliousNewsText = $"{name}\n" + (string.IsNullOrWhiteSpace(published) ? 
                                       "" : $"Published: {published}\n") + "\n" + (body ?? "");
            } catch (Exception ex) {
                AmiliousNewsText = $"Unable to load AmiliousScape release notes.\n\n{ex.Message}";
            }
        }

private async Task Load2009ScapeNewsAsync()
{
    using var httpClient = new HttpClient();
    HtmlNode node;
    var doc = new HtmlDocument();

    try
    {
        var response =
            await httpClient.GetAsync("https://2009scape.org/services/m=news/archives/latest.html");
        doc.Load(await response.Content.ReadAsStreamAsync());
        node = doc.DocumentNode.SelectSingleNode("//div[@class='msgcontents']");
    }
    catch (HttpRequestException)
    {
        node = ConnectionErrorMessage(doc, "lack of an internet connection.");
    }

    if (node == null)
    {
        node = ConnectionErrorMessage(doc, "blocked internet connection. The stable server should still work.");
    }

    var renderer = new HtmlRenderer(node);
    HtmlInlines = renderer.Render();
}

        public void NotificatationBoxStateChanged(NotificationBoxStateChangedMessage msg)
        {
            DimContent = msg.WasOpened;
        }

        public void LaunchPage(object parameter)
        {
            var url = parameter switch
            {
                "news" => "https://2009scape.org/services/m=news/archives/latest.html",
                "issues" => "https://gitlab.com/2009scape/2009scape/-/issues",
                "forums" => "https://forum.2009scape.org",
                "discord" => "https://discord.gg/BBx8Vrf9Yd",
                _ => throw new ArgumentException($"{parameter} is not a valid page parameter.")
            };

            CrossPlatform.LaunchURL(url);
        }

        [DependsOn(nameof(CanLaunch))]
        public bool CanExecuteLaunchSequence(object parameter)
            => CanLaunch;

        //Stub to maintain compatibility with AXAML
        public async Task ExecuteLaunchSequence()
        {
            await ExecuteLaunchSequence(false);
        }

        private async Task ExecuteLaunchSequence(bool forceWait)
        {
            CanLaunch = false;

            try
            {
                if (!File.Exists(_updateService.PreferredTargetFilePath) ||
                    _settingsService.Launcher.CheckForClientUpdatesOnLaunch)
                    await AttemptUpdate();
            }
            catch (Exception e)
            {
                CanLaunch = true;
                LaunchText = $"Failed to update AmiliousScape: {e.Message}";
                return;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    if (!IsJavaVersion("11"))
                        await _javaUpdateService.DownloadAndSetJava11(_settingsService);
                }
                else
                {
                    if (!IsJavaVersion("25"))
                        await _javaUpdateService.DownloadAndSetJava25(_settingsService);
                }
            }
            catch (Exception e)
            {
                CanLaunch = true;
                LaunchText = $"Failed to download and set Java: {e.Message}";
                return;
            }

            if (!File.Exists(CrossPlatform.GetServerProfilePath(CrossPlatform.GetAmiliousScapeHome())) ||
                _settingsService.Launcher.CheckForServerProfilesOnLaunch)
                await AttemptServerProfileUpdate();

            try
            {
                LaunchLog = $"[{DateTime.Now:HH:mm:ss}] Starting launch...\n";
                LaunchText = "Play! (already running)";

                // 1) switch to log tab
                SelectedTabIndex = 3;

                // 2) then tell the window to scroll
                new LogTabActivatedMessage().Broadcast();

                // 3) then start the client
                var t = _launchService.LaunchClient();

                if (!_settingsService.Launcher.AllowMultiboxing || forceWait)
                    await t;
            }
            catch (Exception e)
            {
                NotificationBox.DisplayNotification(
                    "Error",
                    $"Unable to launch the AmiliousScape client.\n\n{e.Message}"
                );
            }
            finally
            {
                CanLaunch = true;
                LaunchText = "Play AmiliousScape!";
                Message.Broadcast<ClientClosedMessage>();
            }
        }

        private async Task AttemptServerProfileUpdate() {
            
            var serverProfilePath = CrossPlatform.GetServerProfilePath(CrossPlatform.GetAmiliousScapeHome());
            try {
                await _remoteConfigService.FetchServerProfileConfig(serverProfilePath);
            }catch { /* Ignore. See next steps. */ }
            try {
                await _remoteConfigService.LoadServerProfileConfig(serverProfilePath);
            }catch { _remoteConfigService.LoadFailsafeDefaults(); }

            var relevantServerProfile = _remoteConfigService.AvailableProfiles.FirstOrDefault(
                x => x.GameServerAddress == _settingsService.Client.GameServerAddress
            );

            if (relevantServerProfile == null) return;

            _settingsService.Client.GameServerPort = relevantServerProfile.GameServerPort;
            _settingsService.Client.CacheServerPort = relevantServerProfile.CacheServerPort;
            _settingsService.Client.WorldListServerPort = relevantServerProfile.WorldListServerPort;
        }

        private async Task AttemptUpdate() {
            
            LaunchText = "Updating...";

            var localClientHash = string.Empty;
            var remoteClientHash = string.Empty;

            try {
                LaunchText = "Updating... (Computing local checksum)";
                localClientHash = await _updateService.ComputeLocalClientHashAsync();
            }catch (FileNotFoundException) { /* Ignore. Client hash will stay empty.*/}

            if (!string.IsNullOrEmpty(localClientHash)) {
                LaunchText = "Updating... (Fetching remote client checksum)";
                remoteClientHash = await _updateService.FetchRemoteClientHashAsync(CancellationToken.None);
            }

            if (string.IsNullOrEmpty(localClientHash) || 
                remoteClientHash.Trim().ToLower() != localClientHash!.Trim().ToLower()) {

                LaunchText = $"Updating... (Downloading client: 0%)";
                Directory.CreateDirectory(CrossPlatform.GetAmiliousScapeHome());

                try {
                    await _updateService.FetchRemoteClientExecutableAsync(CancellationToken.None);
                }catch (Exception) {
                    var clientPath = _updateService.PreferredTargetFilePath;

                    if (!File.Exists(clientPath)) {
                        LaunchText = "Cannot launch. Missing client executable. Click me again to re-try.";
                        throw;
                    }
                }
            }
        }

        private bool IsJavaVersion(string major) {
            if (string.IsNullOrWhiteSpace(Launcher.JavaExecutableLocation) ||
                !File.Exists(Launcher.JavaExecutableLocation))
                return false;

            string javaVersionOutput = CrossPlatform.RunCommandAndGetOutput(
                $"\"{Launcher.JavaExecutableLocation}\" -version"
            );
            return javaVersionOutput.Contains($"version \"{major}");
        }
        
        private void OnClientDownloadProgressUpdated(object sender, float e) {
            LaunchText = $"Updating... (Downloading client - {e * 100:F2}%)";
        }
        
        private void OnJavaDownloadProgressUpdated(object sender, UpdateInfo updateInfo) {
            if (updateInfo.IsFinished) {
                LaunchText = "Play! (Multiplayer)";
                return;
            }
            if (updateInfo.ProgressPercentage >= 0.999f) {
                LaunchText = $"Updating... (Extracting Java {updateInfo.Version})";
                return;
            }
            LaunchText = $"Updating... (Downloading Java {updateInfo.Version} - {updateInfo.ProgressPercentage * 100:F2}%)";
        }
        
    }
}
