using System;
using Mono.Unix;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Saradomin.Utilities {
    
    public static class CrossPlatform {
        
        
        public static string GetToolsDirectory()
            => Path.Combine(GetAmiliousScapeHome(), "tools");

        public static string GetFfplayDirectory()
            => Path.Combine(GetToolsDirectory(), "ffplay");

        public static string GetFfplayExecutablePath()
            => Path.Combine(
                GetFfplayDirectory(),
                OperatingSystem.IsWindows() ? "ffplay.exe" : "ffplay");
        
        public static string GetJreDirectory(string folderName)
            => Path.Combine(GetToolsDirectory(), folderName);
        
        /// <summary>
        /// If the old .../jre11 folder exists and tools/jre11 does not, move it.
        /// </summary>
        public static void MigrateLegacyJreFolder(string folderName)
        {
            var oldPath = Path.Combine(GetAmiliousScapeHome(), folderName);
            var newPath = GetJreDirectory(folderName);

            if (Directory.Exists(newPath) || !Directory.Exists(oldPath))
                return;

            Directory.CreateDirectory(GetToolsDirectory());
            Directory.Move(oldPath, newPath);
        }
        
        public static void LaunchUrl(string url) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Process.Start("xdg-open", url);
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Process.Start("open", url);
            }
        }

        public static void OpenFolder(string path) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Process.Start("xdg-open", path);
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Process.Start("open", path);
            }
        }

        public static string LocateJavaExecutable() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                var envPath = Environment.GetEnvironmentVariable("JAVA_HOME");

                if (!string.IsNullOrEmpty(envPath)) return Path.Combine(envPath, "bin/javaw.exe");
                
                using (var rk = Registry.LocalMachine.OpenSubKey("SOFTWARE\\JavaSoft\\Java Runtime Environment\\")) {
                    if (rk == null) return null;

                    var currentVersion = rk.GetValue("CurrentVersion")?.ToString();

                    if (currentVersion == null) return null;
                    
                    using (var key = rk.OpenSubKey(currentVersion)) {
                        if (key == null) return null;
                        envPath = key.GetValue("JavaHome")?.ToString();
                    }
                }

                return !string.IsNullOrEmpty(envPath) ? Path.Combine(envPath, "bin/javaw.exe") : 
                    throw new FileNotFoundException("Failed to find Java. Make sure it's installed!");
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                     || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) {
                var proc = new Process {
                    StartInfo = new("/bin/which") {
                        Arguments = "java",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };

                proc.Start();
                proc.WaitForExit();
                var data = proc.StandardOutput.ReadToEnd();

                return !string.IsNullOrEmpty(data) ? UnixPath.GetCompleteRealPath(data.Trim()) : 
                    throw new FileNotFoundException("Failed to find Java. Make sure it's installed!");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                var proc = new Process {
                    StartInfo = new("/usr/bin/which") {
                        Arguments = "java",
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };

                proc.Start();
                proc.WaitForExit();
                var data = proc.StandardOutput.ReadToEnd();

                return !string.IsNullOrEmpty(data) ? 
                    Path.Combine(UnixPath.GetCompleteRealPath(data.Trim())) : 
                    throw new FileNotFoundException("Failed to find Java. Make sure it's installed!");
            }
            throw new NotSupportedException("Your platform is not supported.");
        }

        public static string LocateUnixUserHome() {
            return Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        public static string GetAmiliousScapeHome() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) {
                return Path.Combine(
                    LocateUnixUserHome(),
                    "AmiliousScape"
                );
            }
            var userProfile = Path.Combine (
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AmiliousScape"
            );
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AmiliousScape"
            );
            return Directory.Exists(userProfile) ? userProfile : appData;
        }

        public static string GetSaradominHome() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) {
                return Path.Combine(
                    // Get the XDG_DATA_HOME environment variable, or if it doesn't exist, use the default ~/.local/share
                    LocateUnixUserHome(), "AmiliousScape", "saradomin");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AmiliousScape", "saradomin");
        }

        public static string GetSingleplayerBackupsHome() {
            return Path.Combine(GetAmiliousScapeHome(), "singleplayer_backups");
        }

        public static string GetSingleplayerHome() {
            return Path.Combine(GetAmiliousScapeHome(), "singleplayer");
        }

        public static string LocateSingleplayerExecutable() {
            return Path.Combine(GetSingleplayerHome(), RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "launch.bat" : "launch.sh");
        }
        
        public static string GetAmiliousScapeExecutable() {
            return Path.Combine(GetAmiliousScapeHome(), "AmiliousScape.jar");
        }

        public static string GetServerProfilePath(string baseDirectory) {
            baseDirectory ??= GetAmiliousScapeHome();
            return Path.Combine(baseDirectory, "server_profiles.json");
        }

        public static string RunCommandAndGetOutput(string command, Action<string> onOutputReceived = null, 
            Action<string> onErrorReceived = null) {
            Process process = new Process();
            StringBuilder output = new StringBuilder();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                process.StartInfo = new ProcessStartInfo("cmd.exe", "/c " + command) {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                      || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                process.StartInfo = new ProcessStartInfo("bash", "-c \"" + command + "\"") {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            process.OutputDataReceived += (_, e) => {
                if (e.Data == null) return;
                output.AppendLine(e.Data);
                onOutputReceived?.Invoke(e.Data);
            };

            process.ErrorDataReceived += (_, e) => {
                if (e.Data == null) return; 
                output.AppendLine(e.Data);
                onErrorReceived?.Invoke(e.Data);
            };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();

            return output.ToString();
        }

        internal static string GetSystemArchitecture() {
            return RuntimeInformation.OSArchitecture switch {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "aarch64",
                _ => throw new NotSupportedException("Your architecture is not supported.")
            };
        }
        
        public static bool IsDirectoryWritable(string directoryPath) {
            var testFilePath = Path.Combine(directoryPath, "test");

            try {
                File.Create(testFilePath).Dispose();
                File.Delete(testFilePath);

                return true;
            }catch (UnauthorizedAccessException) {
                return false;
            }
        }
        
        public static Process StartJavaProcess(string javaExecutable, string jarPath, string memoryAllocation, 
            Action<string> outputHandler, Action onExit) {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo {
                FileName = javaExecutable,
                Arguments = $"-Xmx{memoryAllocation} -Xms{memoryAllocation} -jar \"{jarPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.Combine(GetSingleplayerHome(), "game")
            };

            process.OutputDataReceived += (_, args) => {
                if (string.IsNullOrEmpty(args.Data)) return;
                outputHandler?.Invoke(args.Data);
            };

            process.ErrorDataReceived += (_, args) => {
                if (string.IsNullOrEmpty(args.Data)) return;
                outputHandler?.Invoke(args.Data);
            };

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => onExit?.Invoke();

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
    }
}