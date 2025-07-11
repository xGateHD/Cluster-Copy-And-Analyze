using System.Data;
using System.Text;
using ClusterAnalyzer.SystemTree;
using RawDiskLib;

namespace ClusterAnalyzer;

public partial class ClusterAnalyzeService
{
    private uint firstDataSector;


    #region Public Methods

    /// <summary>
    /// Выполняет анализ цепочки кластеров для указанного пути.
    /// </summary>
    /// <param name="fullPath">Полный путь к файлу или каталогу.</param>
    /// <param name="cancellationToken">Токен отмены операции.</param>
    /// <param name="progress">Объект для отслеживания прогресса анализа.</param>
    /// <returns>Таблица с результатами анализа.</returns>
    public DataTable AnalyzeCluster(string fullPath, IProgress<AnalysisPhase>? progress = null)
    {
        // Сообщаем о начале проверки томов диска
        progress?.Report(AnalysisPhase.ПроверкаТомовДиска);
        var allVolumes = RawDiskLib.Utils.GetAllAvailableVolumes().ToArray();
        if (!allVolumes.Contains(fullPath[0]))
        {
            throw new ArgumentException("Проблема с чтением доступных томов на дисках");
        }

        // Сообщаем о начале считывания данных
        progress?.Report(AnalysisPhase.СчитываниеДанных);
        using RawDisk disk = new(fullPath[0]);
        firstDataSector = GetFirstDataSector(disk, out uint fatSector, out uint sectorsPerFAT);
        uint originSector = firstDataSector;

        // Разбиваем путь на части и удаляем первый элемент (букву диска)
        var cataloguePath = SplitPath(fullPath);
        cataloguePath.RemoveAt(0);

        // Построение древовидной структуры каталогов
        progress?.Report(AnalysisPhase.ПостроениеДревовиднойСтруктурыКаталогов);
        var targetCatalogue = FileSystemTreeBuilder.BuildTree(fullPath);

        // Поиск изначального сектора каталога
        progress?.Report(AnalysisPhase.ПоискИзначальногоСектораКаталога);
        foreach (var path in cataloguePath)
        {
            var cataloguesCount = Directory.GetDirectories(path).Length;
            var filesCount = Directory.GetFiles(path).Length;
            var firstCluster = FoundFirstClusterInChain(disk, originSector, Path.GetFileNameWithoutExtension(path),
                (uint)(cataloguesCount + filesCount));
            targetCatalogue.FirstCluster = firstCluster;
        }
        targetCatalogue.CalculateSector(firstDataSector);

        // Поиск дескрипторов файлов и каталогов в дереве
        progress?.Report(AnalysisPhase.ИщемДескрипторыФайловИКаталоговВДревовиднойСтруктуре);
        FoundClustersInTree(disk, targetCatalogue);

        // Чтение таблицы FAT
        progress?.Report(AnalysisPhase.ЧтениеFAT);
        byte[] loadedFATInMemory = disk.ReadSectors(fatSector, (int)sectorsPerFAT);

        // Построение цепочки кластеров
        progress?.Report(AnalysisPhase.ПостроениеЦепочкиКластеров);
        BuildClusterChain(targetCatalogue, loadedFATInMemory);

        // Вывод цепочек на экран
        progress?.Report(AnalysisPhase.ВыводЦепочекНаЭкран);
        return targetCatalogue.ToDataTable();
    }

    #endregion


    #region Private Methods

    /// <summary>
    /// Рекурсивно ищет кластеры для всех узлов дерева каталогов.
    /// </summary>
    /// <param name="disk">Объект RawDisk для чтения секторов.</param>
    /// <param name="originNode">Корневой узел дерева каталогов.</param>
    private void FoundClustersInTree(RawDisk disk, DirectoryNode originNode)
    {
        foreach (var children in originNode.Children)
        {
            FoundFirstClusterInChain(disk, children);

            // Если дочерний узел — каталог, продолжаем рекурсию
            if (children is DirectoryNode dir)
            {
                FoundClustersInTree(disk, dir);
            }
        }
    }

    /// <summary>
    /// Строит цепочку кластеров для всех узлов дерева каталогов.
    /// </summary>
    /// <param name="originNode">Корневой узел дерева каталогов.</param>
    /// <param name="loadedFATInMemory">Массив байт, содержащий таблицу FAT.</param>
    private void BuildClusterChain(DirectoryNode originNode, byte[] loadedFATInMemory)
    {
        foreach (var children in originNode.Children)
        {
            children.ClusterChain = [];
            if (!children.FirstCluster.HasValue) throw new InvalidOperationException("FirstCluster is null.");
            for (uint i = children.FirstCluster.Value * 4; i < loadedFATInMemory.Length; i += 4)
            {
                // Чтение 4 байт для определения следующего кластера
                byte[] clusterBytes =
                [
                    loadedFATInMemory[i],
                    loadedFATInMemory[i + 1],
                    loadedFATInMemory[i + 2],
                    loadedFATInMemory[i + 3]
                ];
                var nextClusterNumber = BitConverter.ToUInt16(clusterBytes);
                var entry = new Fat32Entry { Value = nextClusterNumber };
                children.ClusterChain.Add(entry);

                // Прерываем цепочку, если достигнут конец или плохой кластер
                if (entry.IsBadCluster || entry.IsEndOfChain) break;
            }

            // Рекурсивно строим цепочку для дочерних каталогов
            if (children is DirectoryNode dir)
            {
                BuildClusterChain(dir, loadedFATInMemory);
            }
        }
    }

    /// <summary>
    /// Находит нужный дескриптор по его имени и считывает номер первого кластера распределенного файлу.
    /// </summary>
    /// <param name="disk">Объект RawDisk для чтения секторов.</param>
    /// <param name="node">Узел файловой системы (файл или каталог).</param>
    /// <returns>Номер первого кластера, распределенного файлу.</returns>
    /// <exception cref="NullReferenceException">Если дескриптор не найден.</exception>
    private uint FoundFirstClusterInChain(RawDisk disk, FileSystemNode node)
    {
        if (node.Parent == null)
        {
            if (node is not DirectoryNode origin)
                throw new InvalidOperationException("Node is not a DirectoryNode.");
            if (!origin.FirstCluster.HasValue)
                throw new InvalidOperationException("FirstCluster is null.");
#pragma warning disable CS8629 // Тип значения, допускающего NULL, может быть NULL.
            return origin.FirstSector.Value;
#pragma warning restore CS8629 // Тип значения, допускающего NULL, может быть NULL.
        }

        var dir = node.Parent as DirectoryNode ?? throw new InvalidOperationException("Parent node is not a DirectoryNode.");
        uint firstSector = dir.FirstSector ?? dir.CalculateSector(firstDataSector);

        for (uint sectorPointer = firstSector; sectorPointer < firstSector + dir.Children.Count; sectorPointer++)
        {
            byte[] sector = disk.ReadSectors(sectorPointer, 1);
            for (int i = 0; i < sector.Length; i += 32)
            {
                Range fileNameRange = new(i, i + 8);
                var bytesName = sector.Take(fileNameRange).ToArray();

                if (!IsValidAscii(bytesName, out string currentFileName) || string.IsNullOrEmpty(currentFileName))
                {
                    continue;
                }

                if (node is FileNode fileNode)
                {
                    Range fileExtensionRange = new(i + 8, i + 11);
                    var bytesExtension = sector.Take(fileExtensionRange).ToArray();
                    if (!IsValidAscii(bytesExtension, out string currentFileExtension))
                    {
                        continue;
                    }
                    if (!currentFileExtension.Contains(fileNode.FileExtension, StringComparison.CurrentCultureIgnoreCase))
                    {
                        continue;
                    }
                }

                if (currentFileName.Contains(node.Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    uint firstClusterNumber = (uint)BitConverter.ToInt16(sector, i + 26);
                    node.FirstCluster = firstClusterNumber;
                    if (node is DirectoryNode dirNode)
                    {
                        dirNode.CalculateSector(firstDataSector);
                    }
                    return firstClusterNumber;
                }
            }
        }
        throw new NullReferenceException("Не удалось найти необходимый дескриптор каталога");
    }

    /// <summary>
    /// Находит первый кластер по имени файла в указанном диапазоне секторов.
    /// </summary>
    /// <param name="disk">Объект RawDisk для чтения секторов.</param>
    /// <param name="firstSector">Первый сектор для поиска.</param>
    /// <param name="fileName">Имя файла для поиска.</param>
    /// <param name="maxSectorCount">Максимальное количество секторов для поиска.</param>
    /// <returns>Номер первого кластера, распределенного файлу.</returns>
    /// <exception cref="NullReferenceException">Если дескриптор не найден.</exception>
    private uint FoundFirstClusterInChain(RawDisk disk, uint firstSector, string fileName, uint maxSectorCount)
    {
        for (uint sectorPointer = firstSector; sectorPointer < firstSector + maxSectorCount; sectorPointer++)
        {
            byte[] sector = disk.ReadSectors(sectorPointer, 1);
            for (int i = 0; i < sector.Length; i += 32)
            {
                Range fileNameRange = new(i, i + 8);
                var bytesName = sector.Take(fileNameRange).ToArray();

                if (!IsValidAscii(bytesName, out string currentFileName) || string.IsNullOrEmpty(currentFileName)) continue;

                if (currentFileName.Contains(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    uint firstClusterNumber = (uint)BitConverter.ToInt16(sector, i + 26);
                    return firstClusterNumber;
                }
            }
        }
        throw new NullReferenceException("Не удалось найти необходимый дескриптор каталога");
    }

    /// <summary>
    /// Получает номер первого сектора данных, а также параметры FAT.
    /// </summary>
    /// <param name="disk">Объект RawDisk для чтения секторов.</param>
    /// <param name="fatSector">Выходной параметр: сектор FAT.</param>
    /// <param name="sectorsPerFAT">Выходной параметр: количество секторов на одну таблицу FAT.</param>
    /// <returns>Номер первого сектора данных.</returns>
    private uint GetFirstDataSector(RawDisk disk, out uint fatSector, out uint sectorsPerFAT)
    {
        var bootSector = disk.ReadSectors(0, 1);
        int reservedSectors = BitConverter.ToInt16([bootSector[14], bootSector[15]], 0);
        fatSector = (uint)reservedSectors;
        sectorsPerFAT = (uint)BitConverter.ToInt16([.. bootSector.Skip(36).Take(4)], 0);
        uint firstDataSector = (uint)reservedSectors + sectorsPerFAT * 2;
        return firstDataSector;
    }

    /// <summary>
    /// Проверяет, что массив байт содержит только ASCII-символы, и возвращает строку.
    /// </summary>
    /// <param name="data">Массив байт для проверки.</param>
    /// <param name="value">Результирующая строка, если проверка успешна.</param>
    /// <returns>True, если все байты — печатаемые ASCII-символы.</returns>
    private bool IsValidAscii(byte[] data, out string value)
    {
        value = "";
        foreach (var b in data)
        {
            if (b < 0x20 || b > 0x7E)
                return false; // Исключаем непечатаемые символы
        }
        value = Encoding.ASCII.GetString(data);
        return true;
    }

    /// <summary>
    /// Разбивает путь на части, формируя список вложенных путей.
    /// </summary>
    /// <param name="path">Исходный путь.</param>
    /// <returns>Список вложенных путей.</returns>
    private static List<string> SplitPath(string path)
    {
        var parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        string current = "";

        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            result.Add(current);
        }

        return result;
    }

    #endregion
}
