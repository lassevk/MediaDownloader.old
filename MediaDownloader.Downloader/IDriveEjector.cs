using System.IO;

using JetBrains.Annotations;

namespace MediaDownloader.Downloader
{
    public interface IDriveEjector
    {
        bool Eject([NotNull] DriveInfo driveInfo);
    }
}