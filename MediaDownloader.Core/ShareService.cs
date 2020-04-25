using System.Runtime.InteropServices;

namespace MediaDownloader.Core
{
    internal class ShareService : IShareService
    {
        public ulong GetFreeSpace(string uncPath)
        {
            if(!GetDiskFreeSpaceEx(uncPath, out var freeBytesAvailable, out _, out _))
                throw new System.ComponentModel.Win32Exception();

            return freeBytesAvailable;
        }

        [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
                                              out ulong lpFreeBytesAvailable,
                                              out ulong lpTotalNumberOfBytes,
                                              out ulong lpTotalNumberOfFreeBytes);

    }
}