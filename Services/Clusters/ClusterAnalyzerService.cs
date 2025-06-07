using ClustersCopyAndAnalyze.Services.Clusters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public enum ТипПрогрессаАнализатора
{
    ПроверкаПравДоступа = 0,
    ПроверкаПути = 1,
    ОткрытиеДескриптораДиска = 2,
    ЧтениеЗагрузочногоСектора = 3,
    ИзвлечениеПараметровДиска = 4,
    ПроверкаПараметров = 5,
    ЧтениеFAT = 6,
    ПоискНачальногоКластера = 7,
    ПостроениеЦепочкиКластеров = 8,
    ЧтениеПоследнегоСектора = 9
}

public interface IClusterAnalyzerService
{
    Task<List<ClusterData>> AnalyzeClusterAsync(string fullPath, CancellationToken cancellationToken, IProgress<ТипПрогрессаАнализатора> progress = null);
}

public class ClusterAnalyzerService : IClusterAnalyzerService
{
    private const int SECTOR_SIZE = 512;
    private const int SECTORS_PER_CLUSTER = 8;
    private const int CLUSTER_SIZE = SECTOR_SIZE * SECTORS_PER_CLUSTER;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BootSector
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] JumpBoot;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] OEMName;
        public ushort BytesPerSector;
        public byte SectorsPerCluster;
        public ushort ReservedSectors;
        public byte NumFATs;
        public ushort RootEntryCount;
        public ushort TotalSectors16;
        public byte Media;
        public ushort SectorsPerFAT16;
        public ushort SectorsPerTrack;
        public ushort NumberOfHeads;
        public uint HiddenSectors;
        public uint TotalSectors32;
        public uint SectorsPerFAT;
        public ushort ExtFlags;
        public ushort FSVersion;
        public uint RootCluster;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct DirEntry
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] Name;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public byte[] Ext;
        public byte Attr;
        public byte NTRes;
        public byte CreationTimeTenth;
        public ushort CreationTime;
        public ushort CreationDate;
        public ushort LastAccessDate;
        public ushort ClusterHigh;
        public ushort LastWriteTime;
        public ushort LastWriteDate;
        public ushort ClusterLow;
        public uint FileSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct LfnEntry
    {
        public byte Ord;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        public byte[] Name1;
        public byte Attr;
        public byte Type;
        public byte Checksum;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        public byte[] Name2;
        public ushort ClusterLow;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Name3;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFilePointerEx(
        IntPtr hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(
        IntPtr hFile,
        byte[] lpBuffer,
        int nNumberOfBytesToRead,
        out int lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_BEGIN = 0;

    /// <summary>
    /// Асинхронно анализирует цепочку кластеров для указанного файла на диске FAT32.
    /// </summary>
    /// <param name="fullPath">Полный путь к файлу (например, "E:\Folder\File.txt")</param>
    /// <param name="progress">Отчет о прогрессе</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список данных о кластерах</returns>
    public async Task<List<ClusterData>> AnalyzeClusterAsync(
        string fullPath,
        CancellationToken cancellationToken,
        IProgress<ТипПрогрессаАнализатора> progress = null)
    {
        progress?.Report(ТипПрогрессаАнализатора.ПроверкаПути);

        if (string.IsNullOrEmpty(fullPath) || !Path.IsPathRooted(fullPath))
            return new List<ClusterData>();

        string driveLetter = Path.GetPathRoot(fullPath).TrimEnd('\\');
        if (string.IsNullOrEmpty(driveLetter) || driveLetter.Length < 2)
            return new List<ClusterData>();

        progress?.Report(ТипПрогрессаАнализатора.ПроверкаПравДоступа);
        string drive = $@"\\.\{driveLetter}";
        IntPtr handle = CreateFile(
            drive,
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_READONLY | FILE_FLAG_NO_BUFFERING,
            IntPtr.Zero);

        if (handle == IntPtr.Zero)
        {
            return new List<ClusterData>();
        }

        try
        {
            progress?.Report(ТипПрогрессаАнализатора.ЧтениеЗагрузочногоСектора);
            byte[] bootBuf = new byte[SECTOR_SIZE];
            if (!ReadDisk(handle, 0, bootBuf))
            {
                return new List<ClusterData>();
            }

            progress?.Report(ТипПрогрессаАнализатора.ИзвлечениеПараметровДиска);
            var bs = ByteArrayToStructure<BootSector>(bootBuf);

            progress?.Report(ТипПрогрессаАнализатора.ПроверкаПараметров);
            var driveInfo = new DriveInfo(driveLetter);
            if (!driveInfo.IsReady || !IsFat32(driveInfo))
            {
                return new List<ClusterData>();
            }

            progress?.Report(ТипПрогрессаАнализатора.ПоискНачальногоКластера);
            uint cluster = bs.RootCluster;
            var parts = Path.GetRelativePath(driveLetter, fullPath).Split('\\');

            foreach (var part in parts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<ClusterData>();
                }
                progress?.Report(ТипПрогрессаАнализатора.ПоискНачальногоКластера);
                if (!FindEntry(handle, bs, cluster, part, out uint newCluster))
                {
                    return new List<ClusterData>();
                }
                cluster = newCluster;
            }

            progress?.Report(ТипПрогрессаАнализатора.ПостроениеЦепочкиКластеров);
            var clusterChain = await GetClusterChainAsync(handle, bs, cluster, progress, cancellationToken);

            progress?.Report(ТипПрогрессаАнализатора.ЧтениеПоследнегоСектора);
            await ReadLastSectorOfChainAsync(handle, bs, clusterChain, progress);

            return clusterChain;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    /// <summary>
    /// Проверяет, является ли диск FAT32.
    /// </summary>
    /// <param name="drive">Информация о диске</param>
    /// <returns>Истина, если диск FAT32</returns>
    private bool IsFat32(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady) return false;
            if (drive.DriveType == DriveType.Removable &&
                drive.DriveFormat.Equals("FAT", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static DataTable FormatToDT(List<ClusterData> list)
    {
        try
        {
            DataTable table = new();
            DataColumn currentClusterCollumn = new("CurrentCluster", typeof(string));
            DataColumn nextClusterCollumn = new("NextCluster", typeof(string));
            DataColumn hexNextClusterInChainCollumn = new("HexNextClusterInChain", typeof(string));

            table.Columns.Add(currentClusterCollumn);
            table.Columns.Add(nextClusterCollumn);
            table.Columns.Add(hexNextClusterInChainCollumn);
            foreach (ClusterData rowData in list)
            {
                table.Rows.Add(rowData.CurrentCluster, rowData.NextCluster, rowData.NextCluster.ToString16());
            }
            return table;

        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка конвертации List<ClusterData> в DataTable. Детали: {ex.Message}");
        }

    }

    /// <summary>
    /// Читает сектор диска в буфер.
    /// </summary>
    /// <param name="handle">Дескриптор диска</param>
    /// <param name="sectorIndex">Индекс сектора</param>
    /// <param name="buffer">Выходной буфер</param>
    /// <returns>Истина, если чтение успешно</returns>
    private bool ReadDisk(IntPtr handle, long sectorIndex, byte[] buffer)
    {
        if (buffer.Length % SECTOR_SIZE != 0)
            return false;

        long offset = sectorIndex * SECTOR_SIZE;
        if (!SetFilePointerEx(handle, offset, out _, FILE_BEGIN))
            return false;

        return ReadFile(handle, buffer, buffer.Length, out int read, IntPtr.Zero) && read == buffer.Length;
    }

    /// <summary>
    /// Читает кластер с диска.
    /// </summary>
    /// <param name="handle">Дескриптор диска</param>
    /// <param name="sectorIndex">Индекс сектора</param>
    /// <param name="buffer">Выходной буфер</param>
    /// <returns>Истина, если чтение успешно</returns>
    private bool ReadCluster(IntPtr handle, long sectorIndex, byte[] buffer)
    {
        return ReadDisk(handle, sectorIndex, buffer);
    }

    /// <summary>
    /// Добавляет символы Unicode из сырых байтов в список.
    /// </summary>
    /// <param name="rawBytes">Массив сырых байтов</param>
    /// <param name="nameChars">Список для добавления символов</param>
    private void AppendPart(byte[] rawBytes, List<ushort> nameChars)
    {
        for (int i = 0; i + 1 < rawBytes.Length; i += 2)
        {
            ushort code = (ushort)(rawBytes[i] | (rawBytes[i + 1] << 8));
            if (code == 0x0000) break;
            if (code == 0xFFFF) continue;
            nameChars.Add(code);
        }
    }

    /// <summary>
    /// Асинхронно создает цепочку кластеров, начиная с заданного кластера.
    /// </summary>
    /// <param name="handle">Дескриптор диска</param>
    /// <param name="bs">Загрузочный сектор</param>
    /// <param name="startCluster">Начальный кластер</param>
    /// <param name="progress">Отчет о прогрессе</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список данных о кластерах</returns>
    private async Task<List<ClusterData>> GetClusterChainAsync(
        IntPtr handle,
        BootSector bs,
        uint startCluster,
        IProgress<ТипПрогрессаАнализатора> progress,
        CancellationToken cancellationToken)
    {
        var chain = new List<ClusterData>();
        uint current = startCluster;

        while (current < 0x0FFFFFF8)
        {
            if (cancellationToken.IsCancellationRequested) break;
            uint next = await Task.Run(() => GetNextCluster(handle, bs, current), cancellationToken);
            chain.Add(new ClusterData
            {
                CurrentCluster = new Fat32Entry(current),
                NextCluster = new Fat32Entry(next)
            });
            progress?.Report(ТипПрогрессаАнализатора.ПостроениеЦепочкиКластеров);
            if (next >= 0x0FFFFFF8) break;
            current = next;
        }

        return chain;
    }

    /// <summary>
    /// Получает следующий кластер в таблице FAT.
    /// </summary>
    /// <param name="handle">Дескриптор диска</param>
    /// <param name="bs">Загрузочный сектор</param>
    /// <param name="current">Текущий кластер</param>
    /// <returns>Индекс следующего кластера</returns>
    private uint GetNextCluster(IntPtr handle, BootSector bs, uint current)
    {
        long fatStart = (long)bs.ReservedSectors * bs.BytesPerSector;
        long fatOffset = fatStart + (long)current * 4;
        uint sectorIndex = (uint)(fatOffset / bs.BytesPerSector);

        byte[] sectorBuf = new byte[bs.BytesPerSector];
        if (!ReadDisk(handle, sectorIndex, sectorBuf))
            return 0xFFFFFFFF;

        int entryOff = (int)(fatOffset % bs.BytesPerSector);
        byte[] entry = new byte[4];
        Array.Copy(sectorBuf, entryOff, entry, 0, 4);

        return BitConverter.ToUInt32(entry, 0) & 0x0FFFFFFF;
    }

    /// <summary>
    /// Асинхронно читает и форматирует последний сектор цепочки кластеров.
    /// </summary>
    /// <param name="handle">Дескриптор диска</param>
    /// <param name="bs">Загрузочный сектор</param>
    /// <param name="chain">Цепочка кластеров</param>
    /// <param name="progress">Отчет о прогрессе</param>
    private async Task ReadLastSectorOfChainAsync(
        IntPtr handle,
        BootSector bs,
        List<ClusterData> chain,
        IProgress<ТипПрогрессаАнализатора> progress)
    {
        if (chain == null || chain.Count == 0) return;

        uint lastCluster = (uint)chain[chain.Count - 1].CurrentCluster.Value;
        long firstDataSector = bs.ReservedSectors + bs.NumFATs * bs.SectorsPerFAT;
        long clusterIndex = lastCluster - 2;
        long firstSectorOfCluster = firstDataSector + clusterIndex * SECTORS_PER_CLUSTER;
        long lastSectorOfCluster = firstSectorOfCluster + (SECTORS_PER_CLUSTER - 1);

        byte[] buf = new byte[bs.BytesPerSector];
        if (!ReadDisk(handle, lastSectorOfCluster, buf)) return;

        var sb = new StringBuilder();
        for (int i = 0; i < buf.Length; i += 16)
        {
            sb.AppendFormat("\n0x{0:X4}: ", i);
            for (int j = 0; j < 16; j++)
            {
                if (i + j < buf.Length)
                    sb.AppendFormat("{0:X2} ", buf[i + j]);
                else
                    sb.Append("   ");
            }
            sb.Append("  ");
            for (int j = 0; j < 16; j++)
            {
                if (i + j < buf.Length)
                {
                    byte b = buf[i + j];
                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
            }
        }
        progress?.Report(ТипПрогрессаАнализатора.ЧтениеПоследнегоСектора);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Находит запись каталога по имени.
    /// </summary>
    /// <param name="handle">Дескриптор диска</param>
    /// <param name="bs">Загрузочный сектор</param>
    /// <param name="startCluster">Начальный кластер</param>
    /// <param name="entryName">Имя записи</param>
    /// <param name="firstCluster">Найденный индекс кластера</param>
    /// <returns>Истина, если запись найдена</returns>
    private bool FindEntry(
        IntPtr handle,
        BootSector bs,
        uint startCluster,
        string entryName,
        out uint firstCluster)
    {
        firstCluster = 0;
        byte[] clusterBuf = new byte[CLUSTER_SIZE];
        uint current = startCluster;
        entryName = entryName.ToUpperInvariant();

        while (current < 0x0FFFFFF8)
        {
            long firstDataSector = bs.ReservedSectors + bs.NumFATs * bs.SectorsPerFAT;
            long sector = firstDataSector + (current - 2) * SECTORS_PER_CLUSTER;
            if (!ReadCluster(handle, sector, clusterBuf)) return false;

            var lfnEntries = new List<LfnEntry>();
            for (int off = 0; off < CLUSTER_SIZE; off += 32)
            {
                byte firstByte = clusterBuf[off];
                byte attr = clusterBuf[off + 11];

                if (firstByte == 0x00 && attr != 0x0F) return false;
                if (firstByte == 0xE5) { lfnEntries.Clear(); continue; }
                if (attr == 0x0F)
                {
                    lfnEntries.Add(ByteArrayToStructure<LfnEntry>(clusterBuf, off));
                    continue;
                }

                var dir = ByteArrayToStructure<DirEntry>(clusterBuf, off);
                string fullName;
                if (lfnEntries.Count > 0)
                {
                    lfnEntries.Sort((a, b) => (a.Ord & 0x1F).CompareTo(b.Ord & 0x1F));
                    var nameChars = new List<ushort>();
                    foreach (var le in lfnEntries)
                    {
                        AppendPart(le.Name1, nameChars);
                        AppendPart(le.Name2, nameChars);
                        AppendPart(le.Name3, nameChars);
                    }
                    fullName = Encoding.Unicode.GetString(
                        nameChars.SelectMany(c => new[] { (byte)(c & 0xFF), (byte)(c >> 8) }).ToArray());
                }
                else
                {
                    string name = Encoding.ASCII.GetString(dir.Name).Trim();
                    string ext = Encoding.ASCII.GetString(dir.Ext).Trim();
                    fullName = ext.Length > 0 ? $"{name}.{ext}" : name;
                }
                lfnEntries.Clear();

                if (fullName.ToUpperInvariant() == entryName)
                {
                    firstCluster = ((uint)dir.ClusterHigh << 16) | dir.ClusterLow;
                    return true;
                }
            }

            current = GetNextCluster(handle, bs, current);
        }
        return false;
    }

    /// <summary>
    /// Преобразует массив байтов в структуру.
    /// </summary>
    /// <param name="buf">Массив байтов</param>
    /// <param name="offset">Смещение в массиве</param>
    /// <returns>Преобразованная структура</returns>
    private T ByteArrayToStructure<T>(byte[] buf, int offset = 0) where T : struct
    {
        var h = GCHandle.Alloc(buf, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(Marshal.UnsafeAddrOfPinnedArrayElement(buf, offset));
        }
        finally
        {
            h.Free();
        }
    }
}