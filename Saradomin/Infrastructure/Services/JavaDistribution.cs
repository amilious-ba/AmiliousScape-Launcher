using System;
using System.IO;
using Saradomin.Utilities;
using System.Runtime.InteropServices;

namespace Saradomin.Infrastructure.Services {
    
    /// <summary>
    /// Immutable catalog entry for a bundled Java runtime (URLs, folder name, major version).
    /// </summary>
    public sealed class JavaDistribution {
        
        public static JavaDistribution Temurin11 { get; } = new(
            majorVersion: 11, folderName: "jre11", displayName: "Temurin 11",
            windowsX64Url: "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.20%2B8/OpenJDK11U-jre_x64_windows_hotspot_11.0.20_8.zip",
            linuxX64Url: "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.20%2B8/OpenJDK11U-jre_x64_linux_hotspot_11.0.20_8.tar.gz",
            linuxAarch64Url: "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.20%2B8/OpenJDK11U-jre_aarch64_linux_hotspot_11.0.20_8.tar.gz",
            macX64Url: "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.20%2B8/OpenJDK11U-jre_x64_mac_hotspot_11.0.20_8.tar.gz",
            macAarch64Url: "https://github.com/adoptium/temurin11-binaries/releases/download/jdk-11.0.20%2B8/OpenJDK11U-jre_aarch64_mac_hotspot_11.0.20_8.tar.gz");

        public static JavaDistribution Temurin25 { get; } = new(majorVersion: 25, folderName: "jre25", displayName: "Temurin 25",
            windowsX64Url: "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.4%2B7/OpenJDK25U-jre_x64_windows_hotspot_25.0.4_7.zip",
            linuxX64Url: "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.4%2B7/OpenJDK25U-jre_x64_linux_hotspot_25.0.4_7.tar.gz",
            linuxAarch64Url: "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.4%2B7/OpenJDK25U-jre_aarch64_linux_hotspot_25.0.4_7.tar.gz",
            macX64Url: "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.4%2B7/OpenJDK25U-jre_x64_mac_hotspot_25.0.4_7.tar.gz",
            macAarch64Url: "https://github.com/adoptium/temurin25-binaries/releases/download/jdk-25.0.4%2B7/OpenJDK25U-jre_aarch64_mac_hotspot_25.0.4_7.tar.gz");
        
        public int MajorVersion { get; }
        public string FolderName { get; }
        public string DisplayName { get; }

        private readonly string _windowsX64Url;
        private readonly string _linuxX64Url;
        private readonly string _linuxAarch64Url;
        private readonly string _macX64Url;
        private readonly string _macAarch64Url;

        private JavaDistribution(int majorVersion, string folderName, string displayName, string windowsX64Url,
            string linuxX64Url, string linuxAarch64Url, string macX64Url, string macAarch64Url) {
            MajorVersion = majorVersion;
            FolderName = folderName;
            DisplayName = displayName;
            _windowsX64Url = windowsX64Url;
            _linuxX64Url = linuxX64Url;
            _linuxAarch64Url = linuxAarch64Url;
            _macX64Url = macX64Url;
            _macAarch64Url = macAarch64Url;
        }
        
        public string GetDownloadUrl() {
            var arch = CrossPlatform.GetSystemArchitecture(); // existing helper
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return _windowsX64Url;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) 
                return arch == "x64" ? _linuxX64Url : _linuxAarch64Url;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return arch == "x64" ? _macX64Url : _macAarch64Url;
            throw new NotSupportedException("Your platform is not supported.");
        }

        public string GetInstallDirectory()
        {
            CrossPlatform.MigrateLegacyJreFolder(FolderName);
            return CrossPlatform.GetJreDirectory(FolderName);
        }

        public string GetJavaExecutablePath() {
            var root = GetInstallDirectory();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return Path.Combine(root, "Contents", "Home", "bin", "java");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(root, "bin", "java.exe");
            return Path.Combine(root, "bin", "java");
        }
        
        
    }
}