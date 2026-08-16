namespace Saradomin.Infrastructure.Services;

public class LauncherUpdateInfo
{
    public bool UpdateAvailable { get; init; }
    public string TagName { get; init; } = "";
    public string ReleaseUrl { get; init; } = "";
    public string AssetName { get; init; } = "";
    public string AssetDownloadUrl { get; init; } = "";
}