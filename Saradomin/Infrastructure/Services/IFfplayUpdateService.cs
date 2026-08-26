using System;
using System.Threading.Tasks;
using Glitonea.Mvvm;

namespace Saradomin.Infrastructure.Services
{
    public interface IFfplayUpdateService : IService
    {
        event EventHandler<float> DownloadProgressChanged;

        Task EnsureFfplayAsync();
    }
}