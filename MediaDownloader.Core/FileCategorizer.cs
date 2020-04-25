using System;
using System.Collections.Generic;
using System.IO;

namespace MediaDownloader.Core
{
    internal class FileCategorizer : IFileCategorizer
    {
        private static readonly Dictionary<string, FileCategory> _Categories = new Dictionary<string, FileCategory>(StringComparer.InvariantCultureIgnoreCase) {
            [".mp4"] = FileCategory.Movie,
            [".mkv"] = FileCategory.Movie,
            [".wmv"] = FileCategory.Movie,
            [".mov"] = FileCategory.Movie,
            [".flv"] = FileCategory.Movie,
            [".avi"] = FileCategory.Movie,
            
            [".jpg"] = FileCategory.Image,
            [".jpeg"] = FileCategory.Movie,
            [".png"] = FileCategory.Movie,
            
            [".zip"] = FileCategory.Archive,
            [".7z"] = FileCategory.Archive,
            
            [".url"] = FileCategory.Unwanted,
            [".nfo"] = FileCategory.Unwanted,
            [".txt"] = FileCategory.Unwanted,
            [".srr"] = FileCategory.Unwanted,
            [".htm"] = FileCategory.Unwanted,
            [".html"] = FileCategory.Unwanted,
            [".lnk"] = FileCategory.Unwanted,
            [".exe"] = FileCategory.Unwanted,
            [".bat"] = FileCategory.Unwanted,
            [".com"] = FileCategory.Unwanted,
            [".error"] = FileCategory.Unwanted,
            [""] = FileCategory.Unwanted,
            [".gif"] = FileCategory.Unwanted
        };
        
        public FileCategory Categorize(string filename)
        {
            if (String.IsNullOrWhiteSpace(filename))
                return FileCategory.Unknown;

            var extension = Path.GetExtension(filename);
            if (_Categories.TryGetValue(extension, out FileCategory category))
                return category;

            if (extension.ToLower().StartsWith(".zip_ yenc"))
                return FileCategory.Unwanted;

            return FileCategory.Unknown;
        }
    }
}