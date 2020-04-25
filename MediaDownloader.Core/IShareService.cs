using JetBrains.Annotations;

namespace MediaDownloader.Core
{
    public interface IShareService
    {
        ulong GetFreeSpace([NotNull] string uncPath);
    }
}