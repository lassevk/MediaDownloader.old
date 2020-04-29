using System.Collections.Generic;

using JetBrains.Annotations;

namespace MediaDownloader.Downloader
{
    internal class MediaOperationConfiguration
    {
        public MediaOperation Operation { get; set; }

        [NotNull]
        public List<string> Masks { get; } = new List<string>();

        [CanBeNull]
        public string Target { get; set; }

        [CanBeNull]
        public string Source { get; set; }

        public bool Subdirectories { get; set; } = true;
    }
}