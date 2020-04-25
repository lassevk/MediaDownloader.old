namespace MediaDownloader.Downloader
{
    internal class CameraConfiguration
    {
        public string Source { get; set; }

        public CameraDownloadOperation Operation { get; set; }

        public bool Eject { get; set; }
    }
}