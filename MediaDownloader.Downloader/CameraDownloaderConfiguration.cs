using System.Collections.Generic;

using JetBrains.Annotations;

namespace MediaDownloader.Downloader
{
    internal class CameraDownloaderConfiguration
    {
        public string Target { get; set; }

        [NotNull]
        public Dictionary<string, CameraConfiguration> Cameras { get; } = new Dictionary<string, CameraConfiguration>();
    }
}