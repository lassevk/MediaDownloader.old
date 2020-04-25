using System;
using System.Threading.Tasks;

using LVK.AppCore.Windows.Tray;

namespace MediaDownloader
{
    static class Program
    {
        [STAThread]
        public static Task<int> Main() => TrayAppBootstrapper.RunAsync<ServicesBootstrapper>();
    }
}