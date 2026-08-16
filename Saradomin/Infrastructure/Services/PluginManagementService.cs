using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Saradomin.Utilities;

namespace Saradomin.Infrastructure.Services
{
    public class PluginManagementService : IPluginManagementService
    {
        // Client-visible installs only (must contain plugin.class)
        public string PluginRepositoryPath { get; set; }

        // Metadata only (plugin.properties) — client must NOT read this
        public string PluginCatalogPath { get; set; }

        public PluginManagementService(ISettingsService settings)
        {
            var home = CrossPlatform.GetAmiliousScapeHome();

            PluginRepositoryPath = Path.Combine(home, "plugins");
            PluginCatalogPath = Path.Combine(home, "plugins_catalog");

            Directory.CreateDirectory(PluginRepositoryPath);
            Directory.CreateDirectory(PluginCatalogPath);
        }

        public Task<List<string>> EnumerateInstalledPlugins()
        {
            EnsurePluginRepositoryPathSane();

            // Only count plugins that are fully installed
            var installed = Directory
                .GetDirectories(PluginRepositoryPath, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => File.Exists(Path.Combine(dir, "plugin.class")))
                .Select(Path.GetFileName)
                .ToList();

            return Task.FromResult(installed);
        }

        public async Task<bool> IsPluginInstalled(string pluginName)
        {
            EnsurePluginRepositoryPathSane();

            var pluginDir = GetPluginDirectoryPath(pluginName);
            return File.Exists(Path.Combine(pluginDir, "plugin.class"));
        }

        public Task UninstallPlugin(string pluginName)
        {
            EnsurePluginRepositoryPathSane();

            var pluginPath = GetPluginDirectoryPath(pluginName);
            if (Directory.Exists(pluginPath))
            {
                Directory.Delete(pluginPath, true);
            }

            // Optional: leave catalog metadata alone so it still shows in the UI as not installed
            return Task.CompletedTask;
        }

        public Task InstallPlugin(ZipArchive zipArchive, string pluginName)
        {
            throw new NotSupportedException("Feature not supported yet.");
        }

        private string GetPluginDirectoryPath(string pluginName)
            => Path.Combine(PluginRepositoryPath, pluginName);

        private void EnsurePluginRepositoryPathSane()
        {
            if (string.IsNullOrWhiteSpace(PluginRepositoryPath))
            {
                throw new InvalidOperationException("Plugin repository path has not been set.");
            }

            Directory.CreateDirectory(PluginRepositoryPath);
        }

        public void EnsureCatalogPathSane()
        {
            if (string.IsNullOrWhiteSpace(PluginCatalogPath))
            {
                throw new InvalidOperationException("Plugin catalog path has not been set.");
            }

            Directory.CreateDirectory(PluginCatalogPath);
        }
    }
}