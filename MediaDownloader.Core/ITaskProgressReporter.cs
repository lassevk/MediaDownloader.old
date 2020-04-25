using JetBrains.Annotations;

namespace MediaDownloader.Core
{
    public interface ITaskProgressReporter
    {
        [NotNull]
        ITaskProgress CreateTask([NotNull] string title);
    }
}