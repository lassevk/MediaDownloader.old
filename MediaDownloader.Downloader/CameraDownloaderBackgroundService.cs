using System;
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
    internal class CameraDownloaderBackgroundService : IBackgroundService
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
        private readonly IConfigurationElementWithDefault<CameraDownloaderConfiguration> _Configuration;

        public CameraDownloaderBackgroundService(
            [NotNull] IConfiguration configuration, [NotNull] ILogger logger, [NotNull] IDriveEjector driveEjector,
            [NotNull] ITrayNotification trayNotification, [NotNull] ITaskProgressReporter taskProgressReporter)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _DriveEjector = driveEjector ?? throw new ArgumentNullException(nameof(driveEjector));
            _TrayNotification = trayNotification ?? throw new ArgumentNullException(nameof(trayNotification));
            _TaskProgressReporter = taskProgressReporter ?? throw new ArgumentNullException(nameof(taskProgressReporter));

            _Configuration = configuration["CameraDownload"]
               .Element<CameraDownloaderConfiguration>()
               .WithDefault(() => new CameraDownloaderConfiguration());
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
            using (_Logger.LogScope(LogLevel.Debug, $"{nameof(CameraDownloaderBackgroundService)}.{nameof(TryDownloadFiles)}"))
            {
                var configuration = _Configuration.Value();

                if (string.IsNullOrWhiteSpace(configuration.Target))
                {
                    _Logger.LogError("CameraDownload configuration is missing Target specification");
                    return;
                }

                var drives = DriveInfo.GetDrives();

                foreach (var kvp in configuration.Cameras)
                {
                    string diskName = kvp.Key;
                    CameraConfiguration cameraConfiguration = kvp.Value.NotNull();

                    DriveInfo disk;
                    try
                    {
                        disk = drives.FirstOrDefault(
                            di => StringComparer.InvariantCultureIgnoreCase.Equals(di.NotNull().VolumeLabel, diskName));

                        if (disk is null)
                            continue;
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    _Logger.LogInformation($"Disk {diskName} located at {disk.RootDirectory.FullName}");

                    string source = cameraConfiguration.Source?.Replace(
                        "{ROOT}", disk.RootDirectory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.PathSeparator));

                    if (source is null)
                    {
                        _Logger.LogError($"CameraDownload camera configuration for {diskName} is missing Source specification");
                        continue;
                    }

                    await DownloadFiles(source, configuration.Target, cameraConfiguration.Operation, cancellationToken);
                    if (cameraConfiguration.Eject)
                    {
                        _DriveEjector.Eject(disk);
                        _TrayNotification.Notify($"Finished downloading files from '{disk.RootDirectory.FullName}', drive ejected");
                    }
                    else
                        _TrayNotification.Notify($"Finished downloading files from '{disk.RootDirectory.FullName}'");
                }
            }
        }

        private async Task DownloadFiles(
            [NotNull] string source, [NotNull] string targetTemplate, CameraDownloadOperation operation,
            CancellationToken cancellationToken)
        {
            var reporter = _TaskProgressReporter.CreateTask($"Download from {source}");
            try
            {
                string[] sourceFilePaths = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
                reporter.Report(sourceFilePaths.Length, 0);
                int count = 0;
                foreach (string sourceFilePath in sourceFilePaths)
                {
                    string targetFilePath = MakeUniqueTargetFilePath(sourceFilePath, CalculateTargetFilePath(targetTemplate, sourceFilePath));
                    if (targetFilePath != null)
                    {

                        Directory.CreateDirectory(Path.GetDirectoryName(targetFilePath).NotNull());

                        await CopyFile(sourceFilePath, targetFilePath, cancellationToken);
                        if (cancellationToken.IsCancellationRequested)
                            break;
                    }

                    if (operation == CameraDownloadOperation.Move)
                        File.Delete(sourceFilePath);

                    reporter.Report(sourceFilePaths.Length, ++count);
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