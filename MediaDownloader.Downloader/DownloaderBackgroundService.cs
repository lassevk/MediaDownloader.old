using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using JetBrains.Annotations;

using LVK.Configuration;
using LVK.Core;
using LVK.Core.Services;
using LVK.Logging;

using MediaDownloader.Core;

namespace MediaDownloader.Downloader
{
    internal class DownloaderBackgroundService : IBackgroundService
    {
        [NotNull]
        private readonly ILogger _Logger;

        [NotNull]
        private readonly IDriveEjector _DriveEjector;

        [NotNull]
        private readonly ITrayNotification _TrayNotification;

        [NotNull]
        private readonly ITaskProgressReporter _TaskProgressReporter;

        [NotNull]
        public IConfiguration Configuration { get; }

        [NotNull]
        private readonly IConfigurationElementWithDefault<Dictionary<string, MediaConfiguration>> _Configuration;

        public DownloaderBackgroundService(
            [NotNull] IConfiguration configuration, [NotNull] ILogger logger, [NotNull] IDriveEjector driveEjector,
            [NotNull] ITrayNotification trayNotification, [NotNull] ITaskProgressReporter taskProgressReporter)
        {
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _DriveEjector = driveEjector;
            _TrayNotification = trayNotification;
            _TaskProgressReporter = taskProgressReporter;
            Configuration = configuration;
            _Configuration = configuration.Element<Dictionary<string, MediaConfiguration>>("Media")
               .WithDefault(() => new Dictionary<string, MediaConfiguration>());
        }

        public async Task Execute(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await TryDownloadFiles(cancellationToken);
                await Task.Delay(5000, cancellationToken);
            }
        }

        private async Task TryDownloadFiles(CancellationToken cancellationToken)
        {
            DriveInfo[] drives = DriveInfo.GetDrives();

            using (_Logger.LogScope(LogLevel.Debug, $"{nameof(DownloaderBackgroundService)}.{nameof(TryDownloadFiles)}"))
            {
                Dictionary<string, MediaConfiguration> configuration = _Configuration.Value();

                foreach (MediaConfiguration mediaConfiguration in configuration.Values)
                {
                    foreach (var volumeLabel in mediaConfiguration.VolumeLabels)
                    {
                        DriveInfo drive = drives.FirstOrDefault(
                            di => StringComparer.InvariantCultureIgnoreCase.Equals(volumeLabel, di.VolumeLabel));

                        if (drive != null)
                            await TryDownloadFilesFromDrive(drive, mediaConfiguration, cancellationToken);
                    }
                }
            }
        }

        private async Task TryDownloadFilesFromDrive(
            DriveInfo drive, MediaConfiguration mediaConfiguration, CancellationToken cancellationToken)
        {
            using (_Logger.LogScope(LogLevel.Debug, $"{nameof(DownloaderBackgroundService)}.{nameof(TryDownloadFilesFromDrive)}"))
            {
                if (mediaConfiguration.Operations.Count == 0)
                {
                    _Logger.LogError($"No operations specified for {drive.VolumeLabel}");
                    return;
                }

                foreach (var operation in mediaConfiguration.Operations)
                {
                    switch (operation.Operation)
                    {
                        case MediaOperation.Unknown:
                            _Logger.LogError($"An invalid operation is specified for {drive.VolumeLabel}");
                            return;

                        case MediaOperation.Copy:
                        case MediaOperation.Move:
                            if (operation.Source is null)
                            {
                                _Logger.LogError($"A {operation.Operation} operation for {drive.VolumeLabel} is missing source");
                                return;
                            }

                            if (operation.Target is null)
                            {
                                _Logger.LogError($"A {operation.Operation} operation for {drive.VolumeLabel} is missing target");
                                return;
                            }

                            string source = operation.Source.Replace(
                                "{ROOT}", drive.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.PathSeparator));

                            await DownloadFiles(
                                source, operation.Target, operation.Operation == MediaOperation.Move, cancellationToken, operation.Masks,
                                operation.Subdirectories);

                            break;

                        case MediaOperation.Delete:
                            if (operation.Source is null)
                            {
                                _Logger.LogError($"A {operation.Operation} operation for {drive.VolumeLabel} is missing source");
                                return;
                            }

                            string deleteSource = operation.Source.Replace(
                                "{ROOT}", drive.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.PathSeparator));

                            await DeleteFiles(deleteSource, cancellationToken, operation.Masks, operation.Subdirectories);
                            break;

                        case MediaOperation.Eject:
                            await Task.Delay(5000, cancellationToken);
                            _DriveEjector.Eject(drive);
                            _TrayNotification.Notify($"Finished downloading files from '{drive.RootDirectory.FullName}', drive ejected");
                            return;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                _TrayNotification.Notify($"Finished downloading files from '{drive.RootDirectory.FullName}'");
            }
        }

        private async Task DownloadFiles(
            [NotNull] string source, [NotNull] string targetTemplate, bool deleteSourceFiles, CancellationToken cancellationToken,
            IEnumerable<string> masks, bool subdirectories)
        {
            var reporter = _TaskProgressReporter.CreateTask($"Download from {source}");
            try
            {
                var sourceFilePaths = new List<string>();
                foreach (string mask in masks)
                    sourceFilePaths.AddRange(
                        Directory.GetFiles(source, mask, subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));

                if (sourceFilePaths.Count == 0)
                    return;

                reporter.Report(sourceFilePaths.Count, 0);
                int count = 0;
                foreach (string sourceFilePath in sourceFilePaths)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    string targetFilePath = MakeUniqueTargetFilePath(
                        sourceFilePath, CalculateTargetFilePath(targetTemplate, sourceFilePath));

                    if (targetFilePath != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath).NotNull());

                        await CopyFile(sourceFilePath, targetFilePath, cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            break;
                    }

                    if (deleteSourceFiles)
                        File.Delete(sourceFilePath);

                    reporter.Report(sourceFilePaths.Count, ++count);
                }
            }
            finally
            {
                await Task.Delay(1000, cancellationToken);
                reporter.Complete();
            }
        }

        private async Task DeleteFiles(
            [NotNull] string source, CancellationToken cancellationToken, IEnumerable<string> masks, bool subdirectories)
        {
            var reporter = _TaskProgressReporter.CreateTask($"Deleting files from {source}");
            try
            {
                var sourceFilePaths = new List<string>();
                foreach (string mask in masks)
                    sourceFilePaths.AddRange(
                        Directory.GetFiles(source, mask, subdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));

                if (sourceFilePaths.Count == 0)
                    return;

                reporter.Report(sourceFilePaths.Count, 0);
                int count = 0;
                foreach (string sourceFilePath in sourceFilePaths)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    File.Delete(sourceFilePath);
                    reporter.Report(sourceFilePaths.Count, ++count);
                }
            }
            finally
            {
                await Task.Delay(1000, cancellationToken);
                reporter.Complete();
            }
        }

        private async Task CopyFile([NotNull] string sourceFilePath, [NotNull] string targetFilePath, CancellationToken cancellationToken)
        {
            using (_Logger.LogScope(LogLevel.Information, $"Copying file '{sourceFilePath}' to '{targetFilePath}'"))
            {
                try
                {
                    var task = _TaskProgressReporter.CreateTask($"Copy {Path.GetFileName(sourceFilePath)}");
                    try
                    {
                        var buffer = new byte[65536];
                        using (var sourceStream = File.OpenRead(sourceFilePath))
                        using (var targetStream = File.Create(targetFilePath))
                        {
                            task.Report(sourceStream.Length, 0);
                            long copied = 0;
                            while (true)
                            {
                                int inBuffer = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                                if (inBuffer == 0)
                                    break;

                                await targetStream.WriteAsync(buffer, 0, inBuffer, cancellationToken);

                                copied += inBuffer;
                                task.Report(sourceStream.Length, copied);
                            }
                        }

                        File.SetAttributes(targetFilePath, File.GetAttributes(sourceFilePath));
                        File.SetCreationTime(targetFilePath, File.GetCreationTime(sourceFilePath));
                        File.SetLastWriteTime(targetFilePath, File.GetLastWriteTime(sourceFilePath));
                        File.SetLastAccessTime(targetFilePath, File.GetLastAccessTime(sourceFilePath));
                    }
                    finally
                    {
                        task.Complete();
                    }
                }
                catch (Exception ex)
                {
                    _Logger.LogException(ex);
                    if (File.Exists(targetFilePath))
                    {
                        _Logger.LogInformation($"Deleting '{targetFilePath}' due to exception");
                        File.Delete(targetFilePath);
                    }

                    throw;
                }
            }
        }

        [CanBeNull]
        private string MakeUniqueTargetFilePath([NotNull] string sourceFilePath, [NotNull] string targetFilePath)
        {
            if (!File.Exists(targetFilePath))
                return targetFilePath;

            if (IsSameContent(sourceFilePath, targetFilePath))
                return null;

            string directoryFilePath = Path.GetDirectoryName(targetFilePath).NotNull();
            string filename = Path.GetFileNameWithoutExtension(targetFilePath);
            string extension = Path.GetExtension(targetFilePath);

            int counter = 2;
            while (true)
            {
                targetFilePath = Path.Combine(directoryFilePath, $"{filename} ({counter}){extension}");
                if (!File.Exists(targetFilePath))
                    return targetFilePath;

                counter++;
            }
        }

        private bool IsSameContent(string sourceFilePath, string targetFilePath)
        {
            var task = _TaskProgressReporter.CreateTask($"Comparing target {Path.GetFileName(targetFilePath)}");
            try
            {
                var sourceBuffer = new byte[65536];
                var targetBuffer = new byte[65536];

                using (var sourceStream = File.OpenRead(sourceFilePath))
                using (var targetStream = File.OpenRead(targetFilePath))
                {
                    int inSourceBuffer = sourceStream.Read(sourceBuffer, 0, sourceBuffer.Length);
                    int inTargetBuffer = targetStream.Read(targetBuffer, 0, targetBuffer.Length);
                    if (inSourceBuffer != inTargetBuffer)
                        return false;

                    for (int index = 0; index < inSourceBuffer; index++)
                        if (sourceBuffer[index] != targetBuffer[index])
                            return false;
                }

                return true;
            }
            finally
            {
                task.Complete();
            }
        }

        [NotNull]
        private string CalculateTargetFilePath([NotNull] string targetTemplate, [NotNull] string sourceFilePath)
        {
            var creationTime = File.GetCreationTime(sourceFilePath);

            return Path.GetFullPath(
                Regex.Replace(
                    targetTemplate, @"\{(?<format>[^}]+)\}", ma =>
                    {
                        var format = ma.NotNull().Groups["format"].NotNull().Value;
                        switch (format)
                        {
                            case "filename":
                                return Path.GetFileName(sourceFilePath);

                            default:
                                return creationTime.ToString(format, CultureInfo.InvariantCulture);
                        }
                    }));
        }
    }
}