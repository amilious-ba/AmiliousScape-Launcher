namespace Saradomin.Infrastructure.Services;

public class JavaUpdateInfo {
    
    #region Properties #################################################################################################
    
    public readonly int Version;
    public readonly string DownloadUrl;
    public readonly float ProgressPercentage;
    public readonly bool IsFinished;
    
    #endregion #########################################################################################################
        
    /// <summary>
    /// This constructor initializes a new instance of the <see cref="JavaUpdateInfo"/> class.
    /// </summary>
    /// <param name="version">The java version that is being downloaded.</param>
    /// <param name="downloadUrl">The url where the java file can be downloaded.</param>
    /// <param name="progressPercentage">The progress percentage of the download.</param>
    /// <param name="isFinished">Indicates whether the download is finished.</param>
    public JavaUpdateInfo(int version, string downloadUrl, float progressPercentage, bool isFinished) {
        Version = version;
        DownloadUrl = downloadUrl;
        ProgressPercentage = progressPercentage;
        IsFinished = isFinished;
    }
    
}