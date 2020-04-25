using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using JetBrains.Annotations;

using LVK.Logging;

namespace MediaDownloader.Downloader
{
    internal class DriveEjector : IDriveEjector
    {
        [NotNull]
        private readonly ILogger _Logger;

        public DriveEjector([NotNull] ILogger logger)
        {
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Eject(DriveInfo driveInfo)
        {
            using (_Logger.LogScope(LogLevel.Information, $"Attempting to eject drive {driveInfo.RootDirectory.FullName}"))
            {
                string filename = @"\\.\" + driveInfo.RootDirectory.FullName[0] + ":";
                IntPtr handle = CreateFile(
                    filename, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, 0x3, 0, IntPtr.Zero);

                try
                {
                    bool result = false;

                    if (LockVolume(handle) && DismountVolume(handle))
                    {
                        PreventRemovalOfVolume(handle, false);
                        result = AutoEjectVolume(handle);
                    }

                    if (result)
                        _Logger.LogInformation("Ejection attempt succeeded");
                    else
                        _Logger.LogInformation("Ejection attempt failed");
                    
                    return result;
                }
                finally
                {
                    CloseHandle(handle);
                }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr securityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode, byte[] lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        // ReSharper disable InconsistentNaming
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const int FILE_SHARE_READ = 0x1;
        const int FILE_SHARE_WRITE = 0x2;
        const int FSCTL_LOCK_VOLUME = 0x00090018;
        const int FSCTL_DISMOUNT_VOLUME = 0x00090020;
        const int IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;

        const int IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;
        // ReSharper restore InconsistentNaming

        private bool LockVolume(IntPtr handle)
        {
            for (int i = 0; i < 10; i++)
            {
                if (DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero))
                {
                    _Logger.LogInformation("Drive successfully locked");
                    return true;
                }

                Thread.Sleep(500);
            }

            return false;
        }

        private void PreventRemovalOfVolume(IntPtr handle, bool prevent)
        {
            byte[] buf = new byte[1];

            buf[0] = (prevent) ? (byte)1 : (byte)0;
            DeviceIoControl(handle, IOCTL_STORAGE_MEDIA_REMOVAL, buf, 1, IntPtr.Zero, 0, out _, IntPtr.Zero);
        }

        private bool DismountVolume(IntPtr handle)
            => DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);

        private bool AutoEjectVolume(IntPtr handle)
            => DeviceIoControl(handle, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
    }
}