using System;
using System.IO;
using System.Linq;
using Glitonea.Mvvm;
using Avalonia.Media;
using System.Net.Http;
using HtmlAgilityPack;
using System.Threading;
using System.Reflection;
using Avalonia.Metadata;
using Saradomin.Utilities;
using Saradomin.View.Windows;
using System.Threading.Tasks;
using Glitonea.Mvvm.Messaging;
using Saradomin.Infrastructure;
using Version = System.Version;
using static System.Environment;
using System.Collections.Generic;
using Avalonia.Controls.Documents;
using Markdig;
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
        private readonly IFfplayUpdateService _ffplayUpdateService;

        private LauncherSettings Launcher { get; }

        public int SelectedTabIndex { get; set; } = 0;
        
        public string Title { get; set; } = "AmiliousScape Launcher";
        
        public InlineCollection AmiliousNewsInlines { get; set; } = new();
        
        public string LaunchLog { get; set; } = "Client log will appear here when you press Play.\n";
        
        public bool CanLaunch { get; private set; } = true;
        public string LaunchText { get; private set; } = "Play AmiliousScape!";

        public bool DimContent { get; private set; }
        public InlineCollection HtmlInlines { get; private set; }

        public MainWindowViewModel(IClientLaunchService launchService, IClientUpdateService updateService,
            ISettingsService settingsService, IRemoteConfigService remoteConfigService,
            IJavaUpdateService javaUpdateService, ILauncherUpdateService launcherUpdateService, 
            IFfplayUpdateService ffplayUpdateService) {
            _launchService = launchService;
            _updateService = updateService;
            _updateService.DownloadProgressChanged += OnClientDownloadProgressUpdated;
            _remoteConfigService = remoteConfigService;
            _javaUpdateService = javaUpdateService;
            _javaUpdateService.JavaDownloadProgressChanged += OnJavaDownloadProgressUpdated;
            _ffplayUpdateService = ffplayUpdateService;
            _ffplayUpdateService.DownloadProgressChanged += OnFfplayDownloadProgressUpdated;
            _launcherUpdateService = launcherUpdateService;
            _launcherUpdateService.DownloadProgressChanged += OnLauncherDownloadProgressUpdated;

            _settingsService = settingsService;
            Launcher = _settingsService.Launcher;

            Message.Subscribe<MainViewLoadedMessage>(this, MainViewLoaded);
            Message.Subscribe<NotificationBoxStateChangedMessage>(this, NotificationBoxStateChanged);
            Message.Subscribe<ClientLaunchRequestedMessage>(this, ClientLaunchRequested);
            Message.Subscribe<ClientLogMessage>(this, msg => AppendLog(msg.Text));
            Message.Subscribe<CheckLauncherUpdateMessage>(this, async _ => {
                // Manual check from Settings — always allow the prompt
                Launcher.SkippedLauncherUpdateTag = "";
                await CheckLauncherUpdateAsync(promptIfAvailable: true);
            });

            _settingsService.Launcher.JavaExecutableLocation ??= CrossPlatform.LocateJavaExecutable();
        }

        private void OnFfplayDownloadProgressUpdated(object sender, float e) {
            LaunchText = $"Updating... (Downloading ffplay - {e * 100:F0}%)";
        }

        private static Version GetLocalVersion() {
            var v = Assembly.GetExecutingAssembly().GetName().Version
                    ?? new Version(0, 0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
        
        private void SetTitle(Version local, Version remoteOrNull) {
            var localText = FormatVersion(local);

            if (remoteOrNull != null && local > remoteOrNull)
                Title = $"AmiliousScape Launcher - v{localText} (unpublished)";
            else
                Title = $"AmiliousScape Launcher - v{localText}";
        }

        private static string FormatVersion(Version v) => $"{v.Major}.{v.Minor}.{v.Build}";

        private void OnLauncherDownloadProgressUpdated(object sender, float e) {
            LaunchText = $"Updating launcher... {e * 100:F0}%";
        }

        public void ExitApplication() { Exit(0); }

        public async void ClientLaunchRequested(ClientLaunchRequestedMessage _) {
            if (CanLaunch) await ExecuteLaunchSequence();
        }
        
        private void AppendLog(string line) {
            var stamp = DateTime.Now.ToString("HH:mm:ss");
            LaunchLog += $"[{stamp}] {line}{NewLine}";
            // Scroll AFTER log text has been updated
            new LogScrollRequestedMessage().Broadcast();
        }

        private HtmlNode ConnectionErrorMessage(HtmlDocument doc, string msg) {
            var failMessage = "<html><body><h3>Not Available<h3><br/>This content is unavailable, likely due to a ";
            doc.LoadHtml(failMessage + msg + "</body></html>");
            return doc.DocumentNode;
        }

        public async void MainViewLoaded(MainViewLoadedMessage _) {
            var local = GetLocalVersion();
            SetTitle(local, null);
            // Load both in parallel
            await Task.WhenAll(
                LoadAmiliousNewsAsync(),
                Load2009ScapeNewsAsync()
            );
            if (Launcher.CheckForLauncherUpdatesOnLaunch)
                await CheckLauncherUpdateAsync(promptIfAvailable: true);
        }

        private async Task CheckLauncherUpdateAsync(bool promptIfAvailable) {
            try {
                var info = await _launcherUpdateService.CheckForUpdateAsync();
                // Update window title from local vs GitHub tag
                var local = GetLocalVersion();
                Version remote = null;
                if (!string.IsNullOrWhiteSpace(info.TagName)) {
                    var cleaned = info.TagName.Trim().TrimStart('v', 'V');
                    if (Version.TryParse(cleaned, out var parsed))
                        remote = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0));
                }
                SetTitle(local, remote);

                if (!info.UpdateAvailable) return;

                // User already said Later for this release tag
                if (!string.IsNullOrEmpty(Launcher.SkippedLauncherUpdateTag)
                    && string.Equals(Launcher.SkippedLauncherUpdateTag, info.TagName, StringComparison.OrdinalIgnoreCase))
                    return;

                if (!promptIfAvailable) return;

                var tag = string.IsNullOrWhiteSpace(info.TagName) ? "latest" : info.TagName;
                var updateNow = await ChoiceBox.ShowAsync("Launcher update available", 
                    $"A newer launcher is available ({tag}).\n\nUpdate now or later?");

                if (updateNow) {
                    LaunchText = "Updating launcher...";
                    CanLaunch = false;
                    try {
                        await _launcherUpdateService.DownloadAndApplyUpdateAsync(info);
                        // process exits inside Apply on success
                    }catch (Exception ex) {
                        CanLaunch = true;
                        LaunchText = "Play AmiliousScape!";
                        NotificationBox.DisplayNotification("Launcher update failed", ex.Message);
                    }
                }else {
                    Launcher.SkippedLauncherUpdateTag = info.TagName;
                    _settingsService.SaveAll();
                }
            }catch {
                // network / non-published build — ignore
                SetTitle(GetLocalVersion(), null);
            }
        }
        
        public Task CheckLauncherUpdateFromSettingsAsync() => CheckLauncherUpdateAsync(promptIfAvailable: true);

        private async Task LoadAmiliousNewsAsync()
{
    try
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("AmiliousScape-Launcher");

        var launcherTask = http.GetStringAsync(
            "https://api.github.com/repos/amilious-ba/AmiliousScape-Launcher/releases");
        var clientTask = http.GetStringAsync(
            "https://api.github.com/repos/amilious-ba/AmiliousScape-Client/releases");

        await Task.WhenAll(launcherTask, clientTask);

        var entries = new List<(DateTime published, string kind, string name, string dateText, string body)>();

        void AddReleases(string json, string kind)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var draft) && draft.GetBoolean())
                    continue;

                var tag = rel.TryGetProperty("tag_name", out var tagEl)
                    ? tagEl.GetString() ?? ""
                    : "";

                var name = rel.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(name))
                    name = tag;

                var body = rel.TryGetProperty("body", out var bodyEl)
                    ? bodyEl.GetString()
                    : "";
                if (string.IsNullOrWhiteSpace(body))
                    body = "(no release notes)";

                var published = DateTime.MinValue;
                if (rel.TryGetProperty("published_at", out var pubEl)
                    && DateTime.TryParse(pubEl.GetString(), out var dt))
                {
                    published = dt.ToUniversalTime();
                }

                var dateText = published == DateTime.MinValue
                    ? "unknown date"
                    : published.ToLocalTime().ToString("yyyy-MM-dd");

                entries.Add((published, kind, name!, dateText, body));
            }
        }

        AddReleases(await launcherTask, "Launcher");
        AddReleases(await clientTask, "Client");

        if (entries.Count == 0)
        {
            AmiliousNewsInlines = new InlineCollection
            {
                new Run("No releases found.")
            };
            return;
        }

        // Create the pipeline once (not inside the loop)
        var pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        var inlines = new InlineCollection();

        foreach (var e in entries.OrderByDescending(x => x.published)) {
            // Title
            inlines.Add(new Run($"{e.kind} {e.name}") {
                FontWeight = FontWeight.Bold,
                FontSize = 16
            });
            inlines.Add(new Run($" — published {e.dateText}") { FontSize = 13 });
            inlines.Add(new LineBreak());
            inlines.Add(new LineBreak());

            // Markdown → HTML → Inlines
            var html = Markdown.ToHtml(e.body, pipeline);

            var doc = new HtmlDocument();
            doc.LoadHtml($"<div>{html}</div>");

            var renderer = new HtmlRenderer(doc.DocumentNode);
            foreach (var inline in renderer.Render())
                inlines.Add(inline);

            inlines.Add(new LineBreak());
            inlines.Add(new LineBreak());
        }

        AmiliousNewsInlines = inlines;
    }
    catch (Exception ex)
    {
        AmiliousNewsInlines = new InlineCollection
        {
            new Run($"Unable to load AmiliousScape release notes.\n\n{ex.Message}")
        };
    }
}

        private async Task Load2009ScapeNewsAsync() {
            
            using var httpClient = new HttpClient();
            HtmlNode node;
            var doc = new HtmlDocument();

            try {
                var response =
                    await httpClient.GetAsync("https://2009scape.org/services/m=news/archives/latest.html");
                doc.Load(await response.Content.ReadAsStreamAsync());
                node = doc.DocumentNode.SelectSingleNode("//div[@class='msgcontents']");
            }
            catch (HttpRequestException) {
                node = ConnectionErrorMessage(doc, "lack of an internet connection.");
            }

            node ??= ConnectionErrorMessage(doc, 
                "blocked internet connection. The stable server should still work.");

            var renderer = new HtmlRenderer(node);
            HtmlInlines = renderer.Render();
        }

        public void NotificationBoxStateChanged(NotificationBoxStateChangedMessage msg) {
            DimContent = msg.WasOpened;
        }

        public void LaunchPage(object parameter) {
            var url = parameter switch {
                "news" => "https://2009scape.org/services/m=news/archives/latest.html",
                "issues" => "https://gitlab.com/2009scape/2009scape/-/issues",
                "forums" => "https://forum.2009scape.org",
                "discord" => "https://discord.gg/BBx8Vrf9Yd",
                _ => throw new ArgumentException($"{parameter} is not a valid page parameter.")
            };

            CrossPlatform.LaunchUrl(url);
        }

        [DependsOn(nameof(CanLaunch))]
        public bool CanExecuteLaunchSequence(object parameter) => CanLaunch;

        //Stub to maintain compatibility with AXAML
        public async Task ExecuteLaunchSequence() {
            await ExecuteLaunchSequence(false);
        }

        private async Task ExecuteLaunchSequence(bool forceWait) {
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
                var distribution = OperatingSystem.IsWindows()
                    ? JavaDistribution.Temurin11
                    : JavaDistribution.Temurin25;

                if (!IsJavaVersion(distribution.MajorVersion.ToString()))
                    await _javaUpdateService.DownloadAndSetJava(_settingsService, distribution);
            }
            catch (Exception e)
            {
                CanLaunch = true;
                LaunchText = $"Failed to download and set Java: {e.Message}";
                return;
            }

            var speaker = (_settingsService.Client.VoiceoverSpeaker ?? "").Trim().ToLowerInvariant();
            var needsFfplay = speaker is "elevenlabs" or "openai";

            if (needsFfplay && !File.Exists(CrossPlatform.GetFfplayExecutablePath()))
            {
                try
                {
                    LaunchText = "Updating... (Downloading ffplay)";
                    await _ffplayUpdateService.EnsureFfplayAsync();
                }
                catch (Exception e)
                {
                    CanLaunch = true;
                    LaunchText = $"Failed to download ffplay: {e.Message}";
                    return;
                }
            }

            if (!File.Exists(CrossPlatform.GetServerProfilePath(CrossPlatform.GetAmiliousScapeHome())) ||
                _settingsService.Launcher.CheckForServerProfilesOnLaunch)
                await AttemptServerProfileUpdate();

            try
            {
                LaunchLog = $"[{DateTime.Now:HH:mm:ss}] Starting launch...\n";
                LaunchText = "Play! (already running)";

                SelectedTabIndex = 4;
                new LogTabActivatedMessage().Broadcast();
                var t = _launchService.LaunchClient();

                if (!_settingsService.Launcher.AllowMultiboxing || forceWait)
                    await t;
            }
            catch (Exception e)
            {
                NotificationBox.DisplayNotification(
                    "Error",
                    $"Unable to launch the AmiliousScape client.\n\n{e.Message}");
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

        /// <summary>
        /// Determines if the installed Java version matches the specified major version.
        /// </summary>
        /// <param name="major">The major version of Java to check for.</param>
        /// <returns>True if the installed Java version matches the specified major version; otherwise, false.</returns>
        private bool IsJavaVersion(string major) {
            if (string.IsNullOrWhiteSpace(Launcher.JavaExecutableLocation) ||
                !File.Exists(Launcher.JavaExecutableLocation))
                return false;
            var javaVersionOutput = CrossPlatform.RunCommandAndGetOutput(
                $"\"{Launcher.JavaExecutableLocation}\" -version"
            );
            return javaVersionOutput.Contains($"version \"{major}");
        }
        
        /// <summary>
        /// This method is called when the client download progress is updated.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The current download progress.</param>
        private void OnClientDownloadProgressUpdated(object sender, float e) {
            LaunchText = $"Updating... (Downloading client - {e * 100:F2}%)";
        }
        
        /// <summary>
        /// This method is called when the Java download progress is updated.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="u">The current update information.</param>
        private void OnJavaDownloadProgressUpdated(object sender, JavaUpdateInfo u) {
            if (u.IsFinished) {
                LaunchText = "Play! (Multiplayer)";
                return;
            }
            if (u.ProgressPercentage >= 0.999f) {
                LaunchText = $"Updating... (Extracting Java {u.Version})";
                return;
            }
            LaunchText = $"Updating... (Downloading Java {u.Version} - {u.ProgressPercentage * 100:F2}%)";
        }
        
    }
}
