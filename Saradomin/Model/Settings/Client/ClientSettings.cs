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
        public float UiScale { get; set; } = 1f;

        [JsonPropertyName("fps")]
        public int Fps { get; set; } = 0;
        
        [JsonPropertyName("swapInterval")]
        public int SwapInterval { get; set; } = 1; // 1 VSync, 0 uncapped, -1 adaptive
        
        [JsonPropertyName("voiceoverSpeaker")]
        public string VoiceoverSpeaker { get; set; } = "";
        
        [JsonPropertyName("elevenLabsKey")]
        public string ElevenLabsKey { get; set; } = "";
        
        [JsonPropertyName("elevenLabsMale")]
        public string ElevenLabsMale { get; set; } = "pNInz6obpgDQGcFmaJgB";
        
        [JsonPropertyName("elevenLabsFemale")]
        public string ElevenLabsFemale { get; set; } = "21m00Tcm4TlvDq8ikWAM";
        
        [JsonPropertyName("openaiKey")]
        public string OpenaiKey { get; set; } = "";
        
        [JsonPropertyName("openaiModel")]
        public string OpenaiModel { get; set; } = "tts-1";
        
        [JsonPropertyName("openaiVoiceMale")]
        public string OpenaiVoiceMale { get; set; } = "onyx";
        
        [JsonPropertyName("openaiVoiceFemale")]
        public string OpenaiVoiceFemale { get; set; } = "nova";
        
    }
}