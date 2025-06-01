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
    #region
    // private const int FIRST_SIGNATURE_OF_BOOT_SECTOR = 0x55;
    // private const int SECOND_SIGNATURE_OF_BOOT_SECTOR = 0xAA;
    // private const int CLUSTER_SIZE = 512;
    public enum ClusterStatus : uint
    {
        Free = 0x00000000,
        Bad = 0x0FFFFFF7,
        LastInChain = 0x0FFFFFF8
    }
    // private const int OFFSET_BYTES_PER_SECTOR = 0x0B;
    // private const int OFFSET_RESERVED_SECTORS = 0x0E;
    // private const int OFFSET_SECTORS_PER_FAT = 0x24;
    // private const int OFFSET_ROOT_CLUSTER = 0x2C; // Смещение для начального кластера корневой директории
    // private const int DIRECTORY_ENTRY_SIZE = 32; // Размер записи директории в FAT32


    public ClusterAnalyzerService() { }

    /// <summary>
    /// Асинхронно анализирует кластеры диска с файловой системой FAT32.
    /// </summary>
    /// <param name="path">Путь к диску (например, "D:\").</param>
    /// <returns>Список данных о кластерах диска.</returns>
    /// <exception cref="ArgumentNullException">Если путь некорректен или null.</exception>
    /// <exception cref="UnauthorizedAccessException">Недостаточно прав для доступа к диску.</exception>
    /// <exception cref="InvalidBootSectorSignatureException">Если сигнатура загрузочного сектора некорректна.</exception>
    public static async Task<List<ClusterData>> DirectoryAnalyzeAsync(string path, IProgress<AnalyzerProgressType> progress = null)
    {
        progress?.Report(AnalyzerProgressType.Проверяем_права_доступа);
        // Всегда использу.tch для обработки ошибок/,AnalyzerProgressType)()trpeR
        // Всегда используйте блок try-catch для обработки ошибок
        // if (!IsAdministrator())
        // throw new UnauthorizedAccessException("Требуются права администратора для анализа диска.");

        // Убедитесь, что используете правильный диск (НЕ системный!)
        progress?.Report(AnalyzerProgressType.Проверяем_корректность_введенного_пути_к_каталогу);
        string drivePath = ValidateDrivePath(path);

        progress?.Report(AnalyzerProgressType.Открытие_потока_для_чтения_файла);
        using FileStream diskStream = new(drivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Читаем загрузочный сектор с проверкой
        progress?.Report(AnalyzerProgressType.Считываем_загрузочный_сектор);
        byte[] bootSector = await ReadBootClusterAsync(diskStream);

        // Извлекаем параметры с проверкой на FAT32
        progress?.Report(AnalyzerProgressType.Извлекаем_параметры_диска);
        int bytesPerSector = BitConverter.ToInt16(bootSector, OFFSET_BYTES_PER_SECTOR);
        int reservedSectors = BitConverter.ToInt16(bootSector, OFFSET_RESERVED_SECTORS);
        int sectorsPerFAT = BitConverter.ToInt32(bootSector, OFFSET_SECTORS_PER_FAT);
        uint rootCluster = BitConverter.ToUInt32(bootSector, OFFSET_ROOT_CLUSTER);

        // Проверка корректности параметров
        progress?.Report(AnalyzerProgressType.Проверяем_корректность_параметров);
        if (bytesPerSector != 512 && bytesPerSector != 1024 && bytesPerSector != 2048 && bytesPerSector != 4096)
            throw new FormatException("Некорректный размер сектора.");


        // Найти начальный кластер каталога
        progress?.Report(AnalyzerProgressType.Ищем_начальный_кластер_каталога);
        uint directoryStartCluster = await FindDirectoryStartClusterAsync(diskStream, path, rootCluster, bytesPerSector, reservedSectors);
        // Прочитать таблицу FAT и получить цепочку кластеров для каталога
        progress?.Report(AnalyzerProgressType.Получаем_цепочку_кластеров_для_каталога);
        return await GetDirectoryClusterChainAsync(diskStream, directoryStartCluster, reservedSectors, bytesPerSector);
    }

    private static bool IsAdministrator()
    {
        return new WindowsPrincipal(WindowsIdentity.GetCurrent())
            .IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string ValidateDrivePath(string path)
    {
        string? drivePath = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(drivePath))
            throw new ArgumentNullException(nameof(drivePath), "Путь некорректен или является null");
        if (!Directory.Exists(drivePath))
            throw new DriveNotFoundException($"Диск {drivePath} не найден или недоступен.");
        // if (!IsAdministrator())
        // throw new UnauthorizedAccessException("Требуются права администратора для анализа диска.");

        return @"\\.\" + drivePath[0] + ":"; // Преобразование пути в логический диск
    }

    private static async Task<byte[]> ReadBootClusterAsync(FileStream diskStream)
    {
        byte[] bootSector = new byte[512]; // Размер загрузочного сектора
        int bytesRead = await diskStream.ReadAsync(bootSector.AsMemory(0, 512));

        // Проверяем, что загрузочный сектор был полностью прочитан
        if (bytesRead != 512)
            throw new IOException($"Не удалось прочитать загрузочный сектор. Прочитано {bytesRead} байтов, ожидалось 512.");

        // Проверка сигнатуры загрузочного сектора
        if (bootSector[510] != 0x55 || bootSector[511] != 0xAA)
            throw new InvalidBootSectorSignatureException($"Неверная сигнатура загрузочного сектора. Ожидалось 0x55AA, обнаружено 0x{bootSector[510]:X2}{bootSector[511]:X2}");

        // Извлечение параметров из загрузочного сектора
        ushort bytesPerSector = BitConverter.ToUInt16(bootSector, 0x0B); // Размер сектора в байтах
        byte sectorsPerCluster = bootSector[0x0D]; // Секторов в одном кластере
        ushort reservedSectors = BitConverter.ToUInt16(bootSector, 0x0E); // Зарезервированные сектора
        byte fatCount = bootSector[0x10]; // Количество таблиц FAT
        uint totalSectors = BitConverter.ToUInt32(bootSector, 0x20); // Общее количество секторов
        uint sectorsPerFAT = BitConverter.ToUInt32(bootSector, 0x24); // Секторов в одной таблице FAT

        // Проверяем, что это FAT32
        uint dataSectors = totalSectors - reservedSectors - (fatCount * sectorsPerFAT);
        uint clusterCount = dataSectors / sectorsPerCluster;

        if (clusterCount < 65525)
            throw new NotSupportedException("Поддерживается только файловая система FAT32.");

        // Проверяем строку FAT32
        string fatType = Encoding.ASCII.GetString(bootSector, 0x52, 8).TrimEnd('\0').Trim();
        if (fatType != "FAT32")
            throw new NotSupportedException($"Неверный тип файловой системы: {fatType}. Поддерживается только FAT32.");

        return bootSector;
    }

    private static async Task<uint> FindDirectoryStartClusterAsync(FileStream diskStream, string directoryPath, uint rootCluster, int bytesPerSector, int reservedSectors)
    {
        // Разделяем путь на части (например, "D:\MyFolder\SubFolder" -> ["MyFolder", "SubFolder"])
        string[] pathParts = directoryPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (pathParts.Length == 0 || !char.IsLetter(pathParts[0][0]) || pathParts[0].Length < 2 || pathParts[0][1] != ':')
            throw new ArgumentException("Некорректный путь к каталогу.", nameof(directoryPath));

        // Начинаем с корневой директории
        uint currentCluster = rootCluster;

        // Проходим по каждому уровню пути (кроме диска, например, "D:")
        for (int i = 1; i < pathParts.Length; i++)
        {
            string dirName = pathParts[i];
            currentCluster = await FindDirectoryEntryClusterAsync(diskStream, currentCluster, dirName, bytesPerSector, reservedSectors);
            if (currentCluster == 0)
                throw new DirectoryNotFoundException($"Каталог {dirName} не найден в {string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Take(i + 1))}.");
        }

        return currentCluster;
    }

    private static async Task<uint> FindDirectoryEntryClusterAsync(FileStream diskStream, uint startCluster, string dirName, int bytesPerSector, int reservedSectors)
    {
        // Рассчитываем начало области данных
        long dataStart = reservedSectors * bytesPerSector;

        // Читаем содержимое каталога
        byte[] dirBuffer = new byte[bytesPerSector];
        uint currentCluster = startCluster;

        while (currentCluster < 0x0FFFFFF8) // Пока не конец цепочки
        {
            long clusterOffset = dataStart + (currentCluster - 2) * bytesPerSector; // Кластеры нумеруются с 2
            diskStream.Seek(clusterOffset, SeekOrigin.Begin);
            int bytesRead = await diskStream.ReadAsync(dirBuffer, 0, bytesPerSector);
            if (bytesRead != bytesPerSector)
                throw new IOException($"Не удалось прочитать кластер {currentCluster}.");

            // Обрабатываем записи директории
            for (int i = 0; i < bytesPerSector; i += DIRECTORY_ENTRY_SIZE)
            {
                if (dirBuffer[i] == 0x00) // Конец записей директории
                    return 0;

                if (dirBuffer[i] == 0xE5) // Удаленная запись
                    continue;

                // Извлекаем имя (8+3 символа)
                string entryName = Encoding.ASCII.GetString(dirBuffer, i, 8).TrimEnd() +
                                  (dirBuffer[i + 8] != 0x20 ? "." + Encoding.ASCII.GetString(dirBuffer, i + 8, 3).TrimEnd() : "");
                if (entryName.ToString() == dirName)
                {
                    // Проверяем, что это каталог
                    byte attributes = dirBuffer[i + 11];
                    if ((attributes & 0x10) != 0) // Каталог
                    {
                        uint highCluster = BitConverter.ToUInt16(dirBuffer, i + 20);
                        uint lowCluster = BitConverter.ToUInt16(dirBuffer, i + 26);
                        return highCluster << 16 | lowCluster;
                    }
                    return 0; // Не каталог
                }
            }

            // Переходим к следующему кластеру
            currentCluster = await GetNextClusterAsync(diskStream, currentCluster, reservedSectors, bytesPerSector);
        }

        return 0; // Каталог не найден
    }

    private static async Task<uint> GetNextClusterAsync(FileStream diskStream, uint currentCluster, int reservedSectors, int bytesPerSector)
    {
        long fatStart = reservedSectors * bytesPerSector;
        long fatOffset = fatStart + currentCluster * 4; // Каждый кластер занимает 4 байта в FAT
        diskStream.Seek(fatOffset, SeekOrigin.Begin);

        byte[] buffer = new byte[4];
        int bytesRead = await diskStream.ReadAsync(buffer, 0, 4);
        if (bytesRead != 4)
            throw new IOException($"Не удалось прочитать запись FAT для кластера {currentCluster}.");

        return BitConverter.ToUInt32(buffer, 0) & 0x0FFFFFFF;
    }

    private static async Task<List<ClusterData>> GetDirectoryClusterChainAsync(FileStream diskStream, uint startCluster, int reservedSectors, int bytesPerSector)
    {
        List<ClusterData> clusterData = [];
        uint current = startCluster;

        while (current < 0x0FFFFFF8)
        {
            Fat32Entry currentCluster = new(current);
            Fat32Entry nextCluster = new(await GetNextClusterAsync(diskStream, current, reservedSectors, bytesPerSector));
            clusterData.Add(new ClusterData
            {
                CurrentCluster = currentCluster,
                NextCluster = nextCluster
            });

            currentCluster = nextCluster;
        }

        return clusterData;
    }
    #endregion
    
    private const int FIRST_SIGNATURE_OF_BOOT_SECTOR = 0x55;
    private const int SECOND_SIGNATURE_OF_BOOT_SECTOR = 0xAA;
    private const int CLUSTER_SIZE = 512;
    private const int OFFSET_BYTES_PER_SECTOR = 0x0B;
    private const int OFFSET_RESERVED_SECTORS = 0x0E;
    private const int OFFSET_SECTORS_PER_FAT = 0x24;
    private const int OFFSET_ROOT_CLUSTER = 0x2C;
    private const int DIRECTORY_ENTRY_SIZE = 32;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode,
                                            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
                                            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint FILE_READ_DATA = 0x0001;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 0x00000003;

    public static async Task<List<ClusterData>> AnalyzeDirectoryClustersAsync(string directoryPath)
    {
        try
        {
            string drive = Path.GetPathRoot(directoryPath)?.TrimEnd('\\');
            if (string.IsNullOrEmpty(drive))
                throw new ArgumentException("Не удалось определить диск для директории");

            DriveInfo driveInfo = new(drive);
            if (driveInfo.DriveFormat != "FAT32")
                throw new Exception($"Файловая система диска {drive} не является FAT32 (найдено: {driveInfo.DriveFormat})");

            IntPtr diskHandle = CreateFile(
                // $@"\\.\{drive}",
                directoryPath,
                FILE_READ_DATA,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (diskHandle == IntPtr.Zero || diskHandle.ToInt64() == -1)
                throw new IOException($"Не удалось открыть диск {drive}. Ошибка: {Marshal.GetLastWin32Error()}");

            try
            {
                return await ReadDirectoryClustersAsync(diskHandle, directoryPath, drive);
            }
            finally
            {
                CloseHandle(diskHandle);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Произошла ошибка: {ex.Message}");
            return null;
        }
    }

    private static async Task<List<ClusterData>> ReadDirectoryClustersAsync(IntPtr diskHandle, string directoryPath, string drive)
    {
        List<ClusterData> clusters = new();
        byte[] bootSector = new byte[512];

        using (FileStream fs = new FileStream(diskHandle, FileAccess.Read))
        {
            fs.Seek(0, SeekOrigin.Begin);
            int bytesRead = await fs.ReadAsync(bootSector, 0, bootSector.Length);
            if (bytesRead != 512)
                throw new IOException("Не удалось прочитать загрузочный сектор");
        }

        ushort bytesPerSector = BitConverter.ToUInt16(bootSector, OFFSET_BYTES_PER_SECTOR);
        byte sectorsPerCluster = bootSector[13];
        ushort reservedSectors = BitConverter.ToUInt16(bootSector, OFFSET_RESERVED_SECTORS);
        uint sectorsPerFat = BitConverter.ToUInt32(bootSector, OFFSET_SECTORS_PER_FAT);
        uint rootCluster = BitConverter.ToUInt32(bootSector, OFFSET_ROOT_CLUSTER);

        long fatOffset = reservedSectors * bytesPerSector;
        long fatSize = sectorsPerFat * bytesPerSector;

        byte[] fatTable = new byte[fatSize];
        using (FileStream fs = new FileStream(diskHandle, FileAccess.Read))
        {
            fs.Seek(fatOffset, SeekOrigin.Begin);
            int bytesReadFat = await fs.ReadAsync(fatTable, 0, (int)fatSize);
            if (bytesReadFat != (int)fatSize)
                throw new IOException("Не удалось прочитать таблицу FAT");
        }

        string relativePath = directoryPath.Substring(drive.Length).TrimStart('\\', '/');
        uint startCluster = await FindDirectoryClusterAsync(diskHandle, rootCluster, relativePath, bytesPerSector, sectorsPerCluster);

        if (startCluster == 0)
            throw new Exception($"Директория '{directoryPath}' не найдена");

        uint currentCluster = startCluster;
        while (currentCluster >= 2 && currentCluster < 0x0FFFFFF8)
        {
            uint nextCluster = BitConverter.ToUInt32(fatTable, (int)(currentCluster * 4)) & 0x0FFFFFFF;
            clusters.Add(new ClusterData
            {
                CurrentCluster = new(currentCluster),
                NextCluster = new(nextCluster)
            });

            if (nextCluster >= 0x0FFFFFF8) break;
            currentCluster = nextCluster;
        }

        return clusters;
    }

    private static async Task<uint> FindDirectoryClusterAsync(IntPtr diskHandle, uint rootCluster, string directoryPath,
                                                              ushort bytesPerSector, byte sectorsPerCluster)
    {
        string[] pathComponents = directoryPath.Trim('/', '\\').Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        uint currentCluster = rootCluster;

        foreach (string dirName in pathComponents)
        {
            long dataOffset = GetDataOffset(diskHandle, bytesPerSector, sectorsPerCluster);
            long clusterOffset = dataOffset + (currentCluster - 2) * bytesPerSector * sectorsPerCluster;

            byte[] clusterData = new byte[bytesPerSector * sectorsPerCluster];
            using (FileStream fs = new FileStream(diskHandle, FileAccess.Read))
            {
                fs.Seek(clusterOffset, SeekOrigin.Begin);
                await fs.ReadAsync(clusterData, 0, clusterData.Length);
            }

            bool found = false;
            for (int i = 0; i < clusterData.Length; i += DIRECTORY_ENTRY_SIZE)
            {
                if (clusterData[i] == 0) break;
                if (clusterData[i] == 0xE5) continue;

                string entryName = Encoding.ASCII.GetString(clusterData, i, 11).Trim();
                if (entryName.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                {
                    uint highCluster = BitConverter.ToUInt16(clusterData, i + 20);
                    uint lowCluster = BitConverter.ToUInt16(clusterData, i + 26);
                    currentCluster = (highCluster << 16) | lowCluster;
                    found = true;
                    break;
                }
            }

            if (!found) return 0;
        }

        return currentCluster;
    }

    private static long GetDataOffset(IntPtr diskHandle, ushort bytesPerSector, byte sectorsPerCluster)
    {
        byte[] buffer = new byte[512];
        using (FileStream fs = new FileStream(diskHandle, FileAccess.Read))
        {
            fs.Seek(0, SeekOrigin.Begin);
            fs.Read(buffer, 0, buffer.Length);
        }

        ushort reservedSectors = BitConverter.ToUInt16(buffer, OFFSET_RESERVED_SECTORS);
        byte numberOfFats = buffer[16];
        uint sectorsPerFat = BitConverter.ToUInt32(buffer, OFFSET_SECTORS_PER_FAT);

        return (reservedSectors + (numberOfFats * sectorsPerFat)) * bytesPerSector;
    }

    public static DataTable FormatToDT(List<ClusterData> list){
        DataTable table = new();
        foreach (ClusterData rowData in list)
        {
            table.Rows.Add(rowData.CurrentCluster, rowData.NextCluster, rowData.NextCluster.ToString16());
        }
        return table;
    }
}