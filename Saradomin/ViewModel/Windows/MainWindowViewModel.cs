using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Metadata;
using Glitonea.Mvvm;
using Glitonea.Mvvm.Messaging;
using HtmlAgilityPack;
using Saradomin.Infrastructure;
using Saradomin.Infrastructure.Services;
using Saradomin.Model.Settings.Launcher;
using Saradomin.Utilities;
using Saradomin.View.Windows;

namespace Saradomin.ViewModel.Windows
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly IClientLaunchService _launchService;
        private readonly IClientUpdateService _updateService;
        private readonly IJavaUpdateService _javaUpdateService;
        private readonly IRemoteConfigService _remoteConfigService;
        private readonly ISettingsService _settingsService;

        private LauncherSettings Launcher { get; }

        public string Title { get; set; } = "AmiliousScape Launcher";
        
        public string AmiliousNewsText { get; set; } = "Loading AmiliousScape news...";
        
        public bool CanLaunch { get; private set; } = true;
        public string LaunchText { get; private set; } = "Play AmiliousScape!";

        public bool DimContent { get; private set; }
        public InlineCollection HtmlInlines { get; private set; }

        public MainWindowViewModel(IClientLaunchService launchService,
            IClientUpdateService updateService,
            ISettingsService settingsService,
            IRemoteConfigService remoteConfigService,
            IJavaUpdateService javaUpdateService)
        {
            _launchService = launchService;
            _updateService = updateService;
            _updateService.DownloadProgressChanged += OnClientDownloadProgressUpdated;
            _remoteConfigService = remoteConfigService;
            _javaUpdateService = javaUpdateService;
            _javaUpdateService.JavaDownloadProgressChanged += OnJavaDownloadProgressUpdated;

            _settingsService = settingsService;
            Launcher = _settingsService.Launcher;

            Message.Subscribe<MainViewLoadedMessage>(this, MainViewLoaded);
            Message.Subscribe<NotificationBoxStateChangedMessage>(this, NotificatationBoxStateChanged);
            Message.Subscribe<ClientLaunchRequestedMessage>(this, ClientLaunchRequested);

            _settingsService.Launcher.JavaExecutableLocation ??= CrossPlatform.LocateJavaExecutable();
        }

        public void ExitApplication()
        {
            Environment.Exit(0);
        }

        public async void ClientLaunchRequested(ClientLaunchRequestedMessage _)
        {
            if (CanLaunch)
                await ExecuteLaunchSequence();
        }

        private HtmlNode ConnectionErrorMessage(HtmlDocument doc, string msg)
        {
            var failMessage = "<html><body><h3>Not Available<h3><br/>This content is unavailable, likely due to a ";
            doc.LoadHtml(failMessage + msg + "</body></html>");
            return doc.DocumentNode;
        }

        public async void MainViewLoaded(MainViewLoadedMessage _)
{
    // Load both in parallel
    await Task.WhenAll(
        LoadAmiliousNewsAsync(),
        Load2009ScapeNewsAsync()
    );
}

private async Task LoadAmiliousNewsAsync()
{
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
            ? bodyProp.GetString()
            : "(no release notes)";
        var published = root.TryGetProperty("published_at", out var pub)
            ? pub.GetString()
            : "";

        AmiliousNewsText =
            $"{name}\n" +
            (string.IsNullOrWhiteSpace(published) ? "" : $"Published: {published}\n") +
            "\n" +
            (body ?? "");
    }
    catch (Exception ex)
    {
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
                LaunchText = $"Failed to update 2009scape: {e.Message}";
                return;
            }

            try
            {
                if (!IsJavaVersion11())
                {
                    await _javaUpdateService.DownloadAndSetJava11(_settingsService);
                }
            } catch (Exception e)
            {
                CanLaunch = true;
                LaunchText = $"Failed to download and set Java 11: {e.Message}";
                return;
            }
            

            if (!File.Exists(CrossPlatform.GetServerProfilePath(CrossPlatform.GetAmiliousScapeHome())) ||
                _settingsService.Launcher.CheckForServerProfilesOnLaunch)
                await AttemptServerProfileUpdate();

            try
            {
                // Make sure config.json has the latest settings (fullscreen toggles, etc.)
                _settingsService.SaveAll();
                
                LaunchText = "Play! (already running)";
                {
                    // Will block this task until client process exits.
                    var t = _launchService.LaunchClient();

                    if (!_settingsService.Launcher.AllowMultiboxing || forceWait)
                        await t;
                }
            }
            catch (Exception e)
            {
                NotificationBox.DisplayNotification(
                    "Error",
                    $"Unable to launch the 2009scape client.\n\n{e.Message}"
                );
            }
            finally
            {
                CanLaunch = true;
                LaunchText = "Play!";
                Message.Broadcast<ClientClosedMessage>();
            }
        }

        private async Task AttemptServerProfileUpdate()
        {
            var serverProfilePath = CrossPlatform.GetServerProfilePath(CrossPlatform.GetAmiliousScapeHome());

            try
            {
                await _remoteConfigService.FetchServerProfileConfig(
                    serverProfilePath
                );
            }
            catch
            {
                // Ignore. See next steps.
            }

            try
            {
                await _remoteConfigService.LoadServerProfileConfig(
                    serverProfilePath
                );
            }
            catch
            {
                _remoteConfigService.LoadFailsafeDefaults();
            }

            var relevantServerProfile = _remoteConfigService.AvailableProfiles.FirstOrDefault(
                x => x.GameServerAddress == _settingsService.Client.GameServerAddress
            );

            if (relevantServerProfile == null)
                return;

            _settingsService.Client.GameServerPort = relevantServerProfile.GameServerPort;
            _settingsService.Client.CacheServerPort = relevantServerProfile.CacheServerPort;
            _settingsService.Client.WorldListServerPort = relevantServerProfile.WorldListServerPort;
        }

        private async Task AttemptUpdate()
        {
            LaunchText = "Updating...";

            var localClientHash = string.Empty;
            var remoteClientHash = string.Empty;

            try
            {
                LaunchText = "Updating... (Computing local checksum)";
                localClientHash = await _updateService.ComputeLocalClientHashAsync();
            }
            catch (FileNotFoundException)
            {
                // Ignore. Client hash will stay empty.
            }

            if (!string.IsNullOrEmpty(localClientHash))
            {
                LaunchText = "Updating... (Fetching remote client checksum)";
                remoteClientHash = await _updateService.FetchRemoteClientHashAsync(CancellationToken.None);
            }

            if (string.IsNullOrEmpty(localClientHash)
                || remoteClientHash.Trim().ToLower() != localClientHash!.Trim().ToLower())
            {

                LaunchText = $"Updating... (Downloading client: 0%)";
                Directory.CreateDirectory(CrossPlatform.GetAmiliousScapeHome());

                try
                {
                    await _updateService.FetchRemoteClientExecutableAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    var clientPath = _updateService.PreferredTargetFilePath;

                    if (!File.Exists(clientPath))
                    {
                        LaunchText = "Cannot launch. Missing client executable. Click me again to re-try.";
                        throw;
                    }
                }
            }
        }

        private bool IsJavaVersion11()
        {
            string javaVersionOutput = CrossPlatform.RunCommandAndGetOutput(
                $"\"{Launcher.JavaExecutableLocation}\" -version"
            );
            return javaVersionOutput.Contains("11");
        }
        
        private void OnClientDownloadProgressUpdated(object sender, float e)
        {
            LaunchText = $"Updating... (Downloading client - {e * 100:F2}%)";
        }
        private void OnJavaDownloadProgressUpdated(object sender, Tuple<float, bool> e)
        {
            if (e.Item2)
            {
                LaunchText = "Play! (Multiplayer)";
                return;
            }
            if (e.Item1 >= 0.999f)
            {
                LaunchText = "Updating... (Extracting Java 11)";
                return;
            }
            LaunchText = $"Updating... (Downloading Java 11 - {e.Item1 * 100:F2}%)";
        }
    }
}
