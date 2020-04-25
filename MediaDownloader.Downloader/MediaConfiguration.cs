using System.Collections.Generic;

using JetBrains.Annotations;

namespace MediaDownloader.Downloader
{
    internal class MediaConfiguration
    {
        [NotNull]
        public List<string> VolumeLabels { get; } = new List<string>();

        [CanBeNull]
        public string Target { get; set; }

        [CanBeNull]
        public string Source { get; set; }

        [NotNull]
        public List<string> Masks { get; } = new List<string>();

        public DownloadOperation Operation { get; set; }

        public bool Eject { get; set; }
    }
}