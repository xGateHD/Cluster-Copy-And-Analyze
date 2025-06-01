using System;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.Xml;
using System.Security.Principal;
using System.Text;

namespace ClustersCopyAndAnalyze.Services.Clusters;

sealed class ClusterAnalyzerService
{

    public enum AnalyzerProgressType : int
    {
        Проверяем_права_доступа = 0,
        Проверяем_корректность_введенного_пути_к_каталогу = 1,
        Открытие_потока_для_чтения_файла = 2,
        Считываем_загрузочный_сектор = 3,
        Извлекаем_параметры_диска = 4,
        Проверяем_корректность_параметров = 5,
        Считываем_таблицу_FAT = 6,
        Ищем_начальный_кластер_каталога = 7,
        Получаем_цепочку_кластеров_для_каталога = 8
    }

    public enum ClusterStatus : uint
    {
        Free = 0x00000000,
        Bad = 0x0FFFFFF7,
        LastInChain = 0x0FFFFFF8
    }

    private const int FIRST_SIGNATURE_OF_BOOT_SECTOR = 0x55;
    private const int SECOND_SIGNATURE_OF_BOOT_SECTOR = 0xAA;
    private const int OFFSET_BYTES_PER_SECTOR = 0x0B;
    private const int OFFSET_SECTORS_PER_CLUSTER = 0x0D;
    private const int OFFSET_RESERVED_SECTORS = 0x0E;
    private const int OFFSET_NUMBER_OF_FATS = 0x10;
    private const int OFFSET_TOTAL_SECTORS_32 = 0x20;
    private const int OFFSET_SECTORS_PER_FAT = 0x24;
    private const int OFFSET_ROOT_CLUSTER = 0x2C;
    private const int DIRECTORY_ENTRY_SIZE = 32;
    private const int SECTOR_SIZE = 512;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
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
    private static extern bool ReadFile(
        IntPtr hFile,
        [Out] byte[] lpBuffer,
        int nNumberOfBytesToRead,
        out int lpNumberOfBytesRead,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetFilePointerEx(
        IntPtr hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod);

    private const uint FILE_READ_DATA = 0x0001;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 0x00000003;
    private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_BEGIN = 0;

    public ClusterAnalyzerService() { }

    /// <summary>
    /// Асинхронно анализирует кластеры указанного файла на диске FAT32.
    /// Возвращает список кластеров (текущий → следующий).
    /// </summary>
    /// <param name="fullPath">Полный путь к файлу (например, "D:\Folder\File.txt").</param>
    /// <param name="progress">Прогресс-репорт по этапам.</param>
    /// <returns>Список ClusterData с информацией о цепочке кластеров.</returns>
    public static async Task<List<ClusterData>> AnalyzeFileClustersAsync(
        string fullPath, IProgress<AnalyzerProgressType> progress = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentNullException(nameof(fullPath));

        // Проверяем корректность диска и наличие прав
        progress?.Report(AnalyzerProgressType.Проверяем_корректность_введенного_пути_к_каталогу);
        string driveRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(driveRoot) || !Directory.Exists(driveRoot))
            throw new DriveNotFoundException($"Диск {driveRoot} не найден.");

        DriveInfo driveInfo = new(driveRoot);
        if (!driveInfo.IsReady || !driveInfo.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Диск {driveRoot} не готов или не является FAT32. Текущая FS: {driveInfo.DriveFormat}");

        progress?.Report(AnalyzerProgressType.Открытие_потока_для_чтения_файла);
        string rawPath = $@"\\.\{driveRoot.TrimEnd('\\')}";
        IntPtr handle = CreateFile(
            rawPath,
            FILE_READ_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_READONLY | FILE_FLAG_NO_BUFFERING,
            IntPtr.Zero);

        if (handle == IntPtr.Zero || handle.ToInt64() == -1)
            throw new IOException($"Не удалось открыть диск {driveRoot}. Ошибка: {Marshal.GetLastWin32Error()}");

        try
        {
            // Чтение загрузочного сектора
            progress?.Report(AnalyzerProgressType.Считываем_загрузочный_сектор);
            byte[] bootSector = new byte[SECTOR_SIZE];
            if (!ReadDisk(handle, 0, bootSector))
                throw new IOException("Не удалось прочитать загрузочный сектор.");

            // Разбор полей BPB
            ushort bytesPerSector = BitConverter.ToUInt16(bootSector, OFFSET_BYTES_PER_SECTOR);
            byte sectorsPerCluster = bootSector[OFFSET_SECTORS_PER_CLUSTER];
            ushort reservedSectors = BitConverter.ToUInt16(bootSector, OFFSET_RESERVED_SECTORS);
            byte numberOfFats = bootSector[OFFSET_NUMBER_OF_FATS];
            uint totalSectors = BitConverter.ToUInt32(bootSector, OFFSET_TOTAL_SECTORS_32);
            uint sectorsPerFat = BitConverter.ToUInt32(bootSector, OFFSET_SECTORS_PER_FAT);
            uint rootCluster = BitConverter.ToUInt32(bootSector, OFFSET_ROOT_CLUSTER);

            // Проверка, что это FAT32
            uint dataSectors = totalSectors - reservedSectors - (numberOfFats * sectorsPerFat);
            uint clusterCount = dataSectors / sectorsPerCluster;
            if (clusterCount < 65525)
                throw new NotSupportedException("Поддерживается только файловая система FAT32.");

            string fatType = Encoding.ASCII.GetString(bootSector, 0x52, 8).TrimEnd('\0').Trim();
            if (!fatType.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Неверный тип файловой системы: {fatType}. Поддерживается только FAT32.");

            // Поиск начального кластера файла
            progress?.Report(AnalyzerProgressType.Ищем_начальный_кластер_каталога);
            uint startCluster = await FindEntryClusterAsync(handle, 
                bootSector, fullPath, bytesPerSector, sectorsPerCluster, 
                reservedSectors, numberOfFats, sectorsPerFat, rootCluster);
            if (startCluster == 0)
                throw new FileNotFoundException($"Файл '{fullPath}' не найден.");

            // Получение цепочки кластеров
            progress?.Report(AnalyzerProgressType.Получаем_цепочку_кластеров_для_каталога);
            List<uint> chain = GetClusterChain(handle, bootSector, startCluster, bytesPerSector, reservedSectors, sectorsPerFat, numberOfFats);

            // Формируем выходные данные
            var result = new List<ClusterData>();
            foreach (uint current in chain)
            {
                uint next = GetNextCluster(handle, bootSector, current, bytesPerSector, reservedSectors);
                result.Add(new ClusterData
                {
                    CurrentCluster = new Fat32Entry(current),
                    NextCluster = new Fat32Entry(next)
                });
                if (next >= 0x0FFFFFF8) break;
            }

            return result;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static DataTable FormatToDT(List<ClusterData> list)
    {
        DataTable table = new();
        foreach (ClusterData rowData in list)
        {
            table.Rows.Add(rowData.CurrentCluster, rowData.NextCluster, rowData.NextCluster.ToString16());
        }
        return table;
    }

    #region Основные вспомогательные методы

    private static bool ReadDisk(IntPtr handle, long sectorIndex, byte[] buffer)
    {
        if (buffer.Length % SECTOR_SIZE != 0)
            return false;

        long offset = sectorIndex * SECTOR_SIZE;
        if (!SetFilePointerEx(handle, offset, out _, FILE_BEGIN))
            return false;

        return ReadFile(handle, buffer, buffer.Length, out int read, IntPtr.Zero) && read == buffer.Length;
    }

    private static bool ReadCluster(IntPtr handle, long sectorIndex, byte[] buffer)
    {
        // sectorIndex здесь — индекс первого сектора кластера
        return ReadDisk(handle, sectorIndex, buffer);
    }

    private static void AppendPart(byte[] rawBytes, List<ushort> nameChars)
    {
        for (int i = 0; i + 1 < rawBytes.Length; i += 2)
        {
            ushort code = (ushort)(rawBytes[i] | (rawBytes[i + 1] << 8));
            if (code == 0x0000)    // Terminator
                break;
            if (code == 0xFFFF)    // Padding
                continue;
            nameChars.Add(code);
        }
    }

    private static List<uint> GetClusterChain(
        IntPtr handle,
        byte[] bootSector,
        uint startCluster,
        int bytesPerSector,
        ushort reservedSectors,
        uint sectorsPerFat,
        byte numberOfFats)
    {
        var chain = new List<uint>();
        uint current = startCluster;
        while (current < 0x0FFFFFF8)
        {
            chain.Add(current);
            uint next = GetNextCluster(handle, bootSector, current, bytesPerSector, reservedSectors);
            if (next >= 0x0FFFFFF8) break;
            current = next;
        }
        return chain;
    }

    private static uint GetNextCluster(
        IntPtr handle,
        byte[] bootSector,
        uint currentCluster,
        int bytesPerSector,
        ushort reservedSectors)
    {
        long fatStartSector = reservedSectors;
        long fatOffsetBytes = (long)fatStartSector * bytesPerSector + (long)currentCluster * 4;
        long sectorIndex = fatOffsetBytes / bytesPerSector;
        byte[] sectorBuf = new byte[bytesPerSector];
        if (!ReadDisk(handle, sectorIndex, sectorBuf))
            return 0xFFFFFFFF;

        int entryOffset = (int)(fatOffsetBytes % bytesPerSector);
        byte[] entry = new byte[4];
        Array.Copy(sectorBuf, entryOffset, entry, 0, 4);

        return BitConverter.ToUInt32(entry, 0) & 0x0FFFFFFF;
    }

    private static async Task<uint> FindEntryClusterAsync(
        IntPtr handle,
        byte[] bootSector,
        string fullPath,
        int bytesPerSector,
        uint sectorsPerCluster,
        ushort reservedSectors,
        uint sectorsPerFat,
        uint numberOfFats,
        uint rootCluster)
    {
        // Приводим путь к виду без корня "D:\"
        string driveRoot = Path.GetPathRoot(fullPath);
        string relativePath = fullPath.Substring(driveRoot.Length).TrimStart('\\');
        string[] parts = relativePath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        uint currentCluster = rootCluster;

        foreach (string part in parts)
        {
            // Ищем запись (LFN + 8.3) в каталоге с номером currentCluster
            if (!FindEntry(handle, bootSector, currentCluster, part, bytesPerSector, sectorsPerCluster, reservedSectors, sectorsPerFat, numberOfFats, out uint nextCluster))
                return 0;
            currentCluster = nextCluster;
        }

        return currentCluster;
    }


    private static bool FindEntry(
        IntPtr handle,
        byte[] bootSector,
        uint startCluster,
        string entryName,
        int bytesPerSector,
        uint sectorsPerCluster,
        ushort reservedSectors,
        uint sectorsPerFat,
        uint numberOfFats,
        out uint firstCluster)
    {
        firstCluster = 0;
        entryName = entryName.ToUpperInvariant();

        int clusterSizeBytes = (int)bytesPerSector * (int)sectorsPerCluster;
        byte[] clusterBuf = new byte[clusterSizeBytes];
        uint current = startCluster;

        while (current < 0x0FFFFFF8)
        {
            // Вычисляем индекс первого сектора текущего кластера в области данных
            long firstDataSector = reservedSectors + numberOfFats * sectorsPerFat;
            long sectorIndex = firstDataSector + (current - 2) * sectorsPerCluster;

            if (!ReadCluster(handle, sectorIndex, clusterBuf))
                return false;

            var lfnEntries = new List<LfnEntry>();
            for (int offset = 0; offset < clusterSizeBytes; offset += DIRECTORY_ENTRY_SIZE)
            {
                byte firstByte = clusterBuf[offset];
                byte attr = clusterBuf[offset + 11];

                if (firstByte == 0x00 && attr != 0x0F)
                    return false; // Конец каталога
                if (firstByte == 0xE5)
                {
                    lfnEntries.Clear();
                    continue;
                }

                if (attr == 0x0F)
                {
                    // Это часть LFN
                    LfnEntry le = ByteArrayToStructure<LfnEntry>(clusterBuf, offset);
                    lfnEntries.Add(le);
                    continue;
                }

                // Это обычная 8.3-запись
                DirEntry dir = ByteArrayToStructure<DirEntry>(clusterBuf, offset);
                string fullName;
                if (lfnEntries.Count > 0)
                {
                    // Сортируем LFN-записи по порядку
                    lfnEntries.Sort((a, b) => (a.Ord & 0x1F).CompareTo(b.Ord & 0x1F));
                    var nameChars = new List<ushort>();
                    foreach (var le in lfnEntries)
                    {
                        AppendPart(le.Name1, nameChars);
                        AppendPart(le.Name2, nameChars);
                        AppendPart(le.Name3, nameChars);
                    }

                    byte[] nameBytes = nameChars.SelectMany(c => new[] { (byte)(c & 0xFF), (byte)(c >> 8) }).ToArray();
                    fullName = Encoding.Unicode.GetString(nameBytes);
                }
                else
                {
                    string name = Encoding.ASCII.GetString(dir.Name).Trim();
                    string ext = Encoding.ASCII.GetString(dir.Name.Skip(8).Take(3).ToArray()).Trim();
                    fullName = ext.Length > 0 ? $"{name}.{ext}" : name;
                }

                lfnEntries.Clear();

                if (fullName.ToUpperInvariant() == entryName)
                {
                    firstCluster = ((uint)dir.ClusterHigh << 16) | dir.ClusterLow;
                    return true;
                }
            }

            current = GetNextCluster(handle, bootSector, current, bytesPerSector, reservedSectors);
        }

        return false;
    }

    private static T ByteArrayToStructure<T>(byte[] buffer, int offset = 0) where T : struct
    {
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        {
            handle.Free();
        }
    }

    #endregion
}

struct LfnEntry
{
    public byte Ord;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
    public byte[] Name1;
    public byte Attr;
    public byte Type;
    public byte Checksum;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public byte[] Name2;
    public ushort FirstClusterLow;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] Name3;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct DirEntry
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
    public byte[] Name;
    public byte Attr;
    public byte NTRes;
    public byte CrtTimeTenth;
    public ushort CrtTime;
    public ushort CrtDate;
    public ushort LstAccDate;
    public ushort ClusterHigh;
    public ushort WrtTime;
    public ushort WrtDate;
    public ushort ClusterLow;
    public uint FileSize;
}
