using JetBrains.Annotations;

namespace MediaDownloader.Core
{
    public interface ITrayNotification
    {
        void Notify([NotNull] string message);
    }
}