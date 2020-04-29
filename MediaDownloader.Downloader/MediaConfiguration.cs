using System.Collections.Generic;

using JetBrains.Annotations;

namespace MediaDownloader.Downloader
{
    internal class MediaConfiguration
    {
        [NotNull]
        public List<string> VolumeLabels { get; } = new List<string>();

        [NotNull]
        public List<MediaOperationConfiguration> Operations { get; } = new List<MediaOperationConfiguration>();
    }
}