using DryIoc;

using LVK.DryIoc;

namespace MediaDownloader.Core
{
    public class ServicesBootstrapper : IServicesBootstrapper
    {
        public void Bootstrap(IContainer container)
        {
            container.Bootstrap<LVK.Core.Services.ServicesBootstrapper>();

            container.Register<IShareService, ShareService>();
            container.Register<IFileCategorizer, FileCategorizer>();
        }
    }
}