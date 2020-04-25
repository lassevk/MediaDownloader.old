using DryIoc;

using LVK.Core.Services;
using LVK.DryIoc;

namespace MediaDownloader.Downloader
{
    public class ServicesBootstrapper : IServicesBootstrapper
    {
        public void Bootstrap(IContainer container)
        {
            container.Bootstrap<LVK.Core.Services.ServicesBootstrapper>();

            container.Register<IBackgroundService, CameraDownloaderBackgroundService>();
            container.Register<IDriveEjector, DriveEjector>();
        }
    }
}