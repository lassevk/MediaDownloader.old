namespace MediaDownloader.Core
{
    public interface ITaskProgress
    {
        void Report(long total, long current);
        void Complete();
    }
}