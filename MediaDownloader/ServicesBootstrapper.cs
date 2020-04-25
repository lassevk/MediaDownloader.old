using DryIoc;

using LVK.DryIoc;

using MediaDownloader.Core;

namespace MediaDownloader
{
    internal class ServicesBootstrapper : IServicesBootstrapper
    {
        public void Bootstrap(IContainer container)
        {
            container.Bootstrap<MediaDownloader.Core.ServicesBootstrapper>();
            container.Bootstrap<Downloader.ServicesBootstrapper>();

            container.Register<ITrayNotification, TrayNotification>();
            container.Register<ITaskProgressReporter, TaskProgressReporter>();
        }
    }
}