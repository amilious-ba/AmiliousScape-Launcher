namespace Saradomin.Infrastructure.Services;

public class UpdateInfo {
    public int Version;
    public string DownloadUrl;
    public float ProgressPercentage;
    public bool IsFinished;
        
    public UpdateInfo(int version, string downloadUrl, float progressPercentage, bool isFinished) {
        Version = version;
        DownloadUrl = downloadUrl;
        ProgressPercentage = progressPercentage;
        IsFinished = isFinished;
    }
}