namespace MediaDownloader.Core
{
    public interface IFileCategorizer
    {
        FileCategory Categorize(string filename);
    }
}