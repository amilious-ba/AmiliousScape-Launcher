using System;
using Glitonea.Mvvm;
using System.Threading;
using System.Threading.Tasks;

namespace Saradomin.Infrastructure.Services;

public interface ILauncherUpdateService : IService {
    
    event EventHandler<float> DownloadProgressChanged;

    Task<LauncherUpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken = default);
    Task DownloadAndApplyUpdateAsync(LauncherUpdateInfo info, CancellationToken cancellationToken = default);
}