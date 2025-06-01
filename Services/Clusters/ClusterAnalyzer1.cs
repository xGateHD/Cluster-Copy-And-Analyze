using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters
{
    static class ClusterAnalyzer
    {
        const uint FSCTL_GET_RETRIEVAL_POINTERS = 0x90073;
        const uint GENERIC_READ = 0x80000000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_NO_BUFFERING = 0x20000000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr inBuffer,
            int nInBufferSize,
            IntPtr outBuffer,
            int nOutBufferSize,
            out int pBytesReturned,
            IntPtr overlapped);

        [StructLayout(LayoutKind.Sequential)]
        struct STARTING_VCN_INPUT_BUFFER
        {
            public ulong StartingVcn;
        }

        public static List<long> GetClusters(string filePath)
        {
            var clusters = new List<long>();
            // Open file handle to volume
            IntPtr handle = CreateFile(
                filePath,
                GENERIC_READ,
                FILE_SHARE_READ,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_NO_BUFFERING,
                IntPtr.Zero);
            if (handle == IntPtr.Zero || handle.ToInt64() == -1)
                throw new IOException($"Unable to open file for cluster analysis: {filePath}");

            try
            {
                // Prepare input buffer
                int inSize = Marshal.SizeOf<STARTING_VCN_INPUT_BUFFER>();
                IntPtr inBuffer = Marshal.AllocHGlobal(inSize);
                var input = new STARTING_VCN_INPUT_BUFFER { StartingVcn = 0 };
                Marshal.StructureToPtr(input, inBuffer, false);

                // Allocate output buffer
                int outSize = 1024 * 8;
                IntPtr outBuffer = Marshal.AllocHGlobal(outSize);

                if (!DeviceIoControl(handle,
                    FSCTL_GET_RETRIEVAL_POINTERS,
                    inBuffer,
                    inSize,
                    outBuffer,
                    outSize,
                    out int bytesReturned,
                    IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new IOException($"DeviceIoControl failed. Win32Error={err}");
                }

                // Read extent count
                uint extentCount = (uint)Marshal.ReadInt32(outBuffer);
                // Determine starting offsets
                int offset = 16;
                long prevVcn = Marshal.ReadInt64(outBuffer, 8);

                for (int i = 0; i < extentCount; i++)
                {
                    long nextVcn = Marshal.ReadInt64(outBuffer, offset);
                    long lcn = Marshal.ReadInt64(outBuffer, offset + 8);
                    long count = nextVcn - prevVcn;
                    for (long j = 0; j < count; j++)
                        clusters.Add((long)(lcn + j));
                    prevVcn = nextVcn;
                    offset += 16;
                }

                Marshal.FreeHGlobal(inBuffer);
                Marshal.FreeHGlobal(outBuffer);
            }
            finally
            {
                // Close handle
                CloseHandle(handle);
            }

            return clusters;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);
    }
}
