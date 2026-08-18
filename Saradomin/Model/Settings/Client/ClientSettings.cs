using System.Text.Json.Serialization;

namespace Saradomin.Model.Settings.Client
{
    public class ClientSettings : SettingsBase
    {
        public const string FileName = "config.json";

        [JsonPropertyName("ip_management")]
        public string ManagementServerAddress { get; set; } = "amilious.xyz";

        [JsonPropertyName("ip_address")]
        public string GameServerAddress { get; set; } = "amilious.xyz";
        
        [JsonPropertyName("world")]
        public ushort World { get; set; } = 1;
        
        [JsonPropertyName("server_port")]
        public ushort GameServerPort { get; set; } = 43594;
        
        [JsonPropertyName("wl_port")]
        public ushort WorldListServerPort { get; set; } = 43595;

        [JsonPropertyName("js5_port")]
        public ushort CacheServerPort { get; set; } = 43595;

        [JsonPropertyName("pluginsFolder")]
        public string PluginsFolder { get; set; } = "plugins";

        [JsonPropertyName("borderlessFullscreen")]
        public bool BorderlessFullscreen { get; set; } = true;

        [JsonPropertyName("startFullscreen")]
        public bool StartFullscreen { get; set; } = false;
        
        [JsonPropertyName("enableAmiliousDebugAtStart")]
        public bool EnableAmiliousDebugAtStart { get; set; } = false;

        [JsonPropertyName("ui_scale")]
        public int UiScale { get; set; } = 1;

        [JsonPropertyName("fps")]
        public int Fps { get; set; } = 0;
        
        
    }
}