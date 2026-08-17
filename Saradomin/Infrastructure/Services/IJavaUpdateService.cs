using System;
using System.Threading.Tasks;
using Glitonea.Mvvm;

namespace Saradomin.Infrastructure.Services {
    
    public interface IJavaUpdateService : IService {
        
        event EventHandler<JavaUpdateInfo> JavaDownloadProgressChanged;
        Task DownloadAndSetJava(ISettingsService settingsService, JavaDistribution distribution);
        
    }
}