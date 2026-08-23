using System;
using Avalonia;
using System.IO;
using Glitonea.Mvvm;
using System.Reflection;
using Glitonea.Extensions;
using Saradomin.Utilities;
using System.Threading.Tasks;
using Glitonea.Mvvm.Messaging;
using Saradomin.Infrastructure;
using Avalonia.Platform.Storage;
using Saradomin.Model.Settings.Client;
using Saradomin.Model.Settings.Launcher;
using Saradomin.Infrastructure.Services;

namespace Saradomin.ViewModel.Controls {
    
    public class SettingsViewModel : ViewModelBase {
        
        private readonly ISettingsService _settingsService;
        
        // Simpler: broadcast a message
        public void CheckLauncherUpdate() => new CheckLauncherUpdateMessage().Broadcast();

        public LauncherSettings Launcher => _settingsService.Launcher;
        public ClientSettings Client => _settingsService.Client;

        public int SwapIntervalIndex {
            get => Client.SwapInterval switch {
                0 => 1,
                -1 => 2,
                _ => 0 // 1 = VSync
            };
            set {
                Client.SwapInterval = value switch {
                    1 => 0,
                    2 => -1,
                    _ => 1
                };
            }
        }
        
        public int VoiceoverSpeakerIndex
        {
            get => (Client.VoiceoverSpeaker ?? "").ToLowerInvariant() switch
            {
                "ostts" => 1,
                "elevenlabs" => 2,
                "openai" => 3,
                _ => 0 // "" or anything else = None
            };
            set
            {
                Client.VoiceoverSpeaker = value switch
                {
                    1 => "ostts",
                    2 => "elevenlabs",
                    3 => "openai",
                    _ => ""
                };

                OnPropertyChanged(nameof(IsElevenLabsSelected));
                OnPropertyChanged(nameof(IsOpenAiSelected));
            }
        }

        public bool IsElevenLabsSelected =>
            string.Equals(Client.VoiceoverSpeaker, "elevenlabs", StringComparison.OrdinalIgnoreCase);

        public bool IsOpenAiSelected =>
            string.Equals(Client.VoiceoverSpeaker, "openai", StringComparison.OrdinalIgnoreCase);
        
        public bool LauncherUpdatePending { get; set; }
        public string LauncherUpdateButtonText =>
            LauncherUpdatePending ? "Update launcher" : "Check launcher";
        
        public string VersionString {
            get {
                var version = Assembly.GetExecutingAssembly().GetName().Version!;
                return $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public SettingsViewModel(ISettingsService settingsService) {
            _settingsService = settingsService;

            Message.Subscribe<MainViewLoadedMessage>(this, OnMainViewLoaded);
        }
        
        public void LaunchScapeWebsite()
            => CrossPlatform.LaunchUrl("https://amilious.xyz");   // change if you want

        public void OpenPluginTutorial()
            => CrossPlatform.LaunchUrl("https://gitlab.com/2009scape/tools/client-plugins");

        public void LaunchProjectWebsite()
            => CrossPlatform.LaunchUrl("https://github.com/amilious-ba/AmiliousScape-Client");  // or your launcher repo later

        public void OpenGameDirectory() {
            var path = CrossPlatform.GetAmiliousScapeHome();
            Directory.CreateDirectory(path);
            CrossPlatform.OpenFolder(path);
        }
        
        public async Task BrowseForJavaExecutable() {
            var window = Application.Current!.GetMainWindow();
            var pickerOptions = new FilePickerOpenOptions {
                Title = "Browse for Java...", AllowMultiple = false,
                SuggestedStartLocation = await window!.StorageProvider.TryGetFolderFromPathAsync(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) };

            var storageFiles = await window.StorageProvider.OpenFilePickerAsync(pickerOptions);
            
            if (storageFiles.Count > 0) {
                Launcher.JavaExecutableLocation = storageFiles[0].Path.AbsolutePath;
            }
        }

        private void OnMainViewLoaded(MainViewLoadedMessage _) {
            Message.Subscribe<SettingsModifiedMessage>(this, OnSettingsModified);
        }

        private void OnSettingsModified(SettingsModifiedMessage _) {
            _settingsService.SaveAll();
        }
    }
}