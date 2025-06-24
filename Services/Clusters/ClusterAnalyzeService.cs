using ClustersCopyAndAnalyze.Services.Clusters.SystemTree;
using RawDiskLib;
using System.Data;
using System.Text;

namespace ClustersCopyAndAnalyze.Services.Clusters;

public enum ТипПрогрессаАнализатора
{
    ПроверкаТомовДиска = 0,
    СчитываниеДанных = 1,
    ПоискИзначальногоСектораКаталога = 2,
    ПостроениеДревовиднойСтруктурыКаталогов = 3,
    ИщемДескрипторыФайловИКаталоговВДревовиднойСтруктуре = 4,
    ПроверкаПараметров = 5,
    ЧтениеFAT = 6,
    ПоискНачальногоКластера = 7,
    ПостроениеЦепочкиКластеров = 8,
    ВыводЦепочекНаЭкран = 9
}

public interface IClusterAnalyzerService
{
    Task<DataTable> AnalyzeClusterAsync(string fullPath, CancellationToken cancellationToken, IProgress<ТипПрогрессаАнализатора> progress = null);
}

public partial class ClusterAnalyzeService : IClusterAnalyzerService
{
    private uint firstDataSector;

    public async Task<DataTable> AnalyzeClusterAsync(string fullPath, CancellationToken cancellationToken, IProgress<ТипПрогрессаАнализатора> progress = null)
    {
        progress.Report(ТипПрогрессаАнализатора.ПроверкаТомовДиска);
        var allVolumes = RawDiskLib.Utils.GetAllAvailableVolumes().ToArray();
        if (!allVolumes.Contains(fullPath[0]))
        {
            throw new ArgumentException("Проблема с чтением доступных томов на дисках");
        }

        progress.Report(ТипПрогрессаАнализатора.СчитываниеДанных);
        using RawDisk disk = new(fullPath[0]);
        firstDataSector = GetFirstDataSector(disk, out uint fatSector, out uint sectorsPerFAT);
        uint originSector = firstDataSector;
        
        var cataloguePath = SplitPath(fullPath);
        cataloguePath.RemoveAt(0);

        progress.Report(ТипПрогрессаАнализатора.ПоискИзначальногоСектораКаталога);
        foreach (var path in cataloguePath)
        {
            var cataloguesCount = Directory.GetDirectories(path).Length;
            var filesCount = Directory.GetFiles(path).Length;
            var firstCluster = FoundFirstClusterInChain(disk, originSector, FileUtils.GetFileName(path),
                (uint)(cataloguesCount + filesCount));
            originSector = GetFirstCatalogueSector(firstDataSector, firstCluster); //sosi
        }

        progress.Report(ТипПрогрессаАнализатора.ПостроениеДревовиднойСтруктурыКаталогов);
        var targetCatalogue = FileSystemTreeBuilder.BuildTree(fullPath); //sosi2, sosi2/newtext.pdf, NOText.txt
        targetCatalogue.FirstSector = originSector;

        progress.Report(ТипПрогрессаАнализатора.ИщемДескрипторыФайловИКаталоговВДревовиднойСтруктуре);
        FoundClustersInTree(disk, targetCatalogue);

        progress.Report(ТипПрогрессаАнализатора.ЧтениеFAT);
        byte[] loadedFATInMemory = disk.ReadSectors(fatSector, (int)sectorsPerFAT);

        progress.Report(ТипПрогрессаАнализатора.ПостроениеЦепочкиКластеров);
        BuildClusterChain(targetCatalogue, loadedFATInMemory);

        progress.Report(ТипПрогрессаАнализатора.ВыводЦепочекНаЭкран);
        return FormatToDT(targetCatalogue);
    }

    internal static DataTable FormatToDT(FileSystemNode originNode)
    {
        try
        {
            DataTable table = new();
            DataColumn nameOfObject = new("NameOfObject", typeof(string));
            DataColumn currentClusterColumn = new("CurrentCluster", typeof(string));
            DataColumn hexNextClusterInChainColumn = new("HexNextClusterInChain", typeof(string));
            DataColumn nextClusterColumn = new("NextCluster", typeof(string));

            table.Columns.Add(nameOfObject);
            table.Columns.Add(currentClusterColumn);
            table.Columns.Add(hexNextClusterInChainColumn);
            table.Columns.Add(nextClusterColumn);

            // Классическая реализация обхода дерева
            Stack<FileSystemNode> stack = new();
            stack.Push(originNode);

            while (stack.Count > 0)
            {
                FileSystemNode current = stack.Pop();

                if (current.ClusterChain != null && current.ClusterChain.Count > 0)
                {
                    for (int i = 0; i < current.ClusterChain.Count; i++)
                    {
                        Fat32Entry entry = current.ClusterChain[i];
                        table.Rows.Add(
                            current.FullPath,
                            (i == 0) ? current.FirstCluster : current.ClusterChain[i - 1],
                            entry.ToString16(),
                            entry
                        );
                    }
                }
                else
                {
                    // Для случаев, когда нет цепочки кластеров (например, каталог без данных)
                    table.Rows.Add(current.Name, "", "", "");
                }

                if (current is DirectoryNode dirNode && dirNode.Childrens != null)
                {
                    // Классическая реализация обхода дерева:
                    for (int i = dirNode.Childrens.Count - 1; i >= 0; i--) // обратный порядок для удобства
                    {
                        stack.Push(dirNode.Childrens[i]);
                    }
                }
            }

            return table;

        }
        catch (Exception ex)
        {
            throw new Exception($"Ошибка конвертации List<ClusterData> в DataTable. Детали: {ex.Message}");
        }
    }

    private void FoundClustersInTree(RawDisk disk, DirectoryNode originNode)
    {
        foreach (var children in originNode.Childrens)
        {
            FoundFirstClusterInChain(disk, children);

            if (children is DirectoryNode dir)
            {
                FoundClustersInTree(disk, dir);
            }
        }
    }

    private void BuildClusterChain(DirectoryNode originNode, byte[] loadedFATInMemory)
    {
        foreach (var children in originNode.Childrens)
        {
            children.ClusterChain = [];
            for (uint i = children.FirstCluster.Value * 4; i < loadedFATInMemory.Length; i += 4)
            {
                byte[] clusterBytes =
                [
                    loadedFATInMemory[i],
                    loadedFATInMemory[i + 1],
                    loadedFATInMemory[i + 2],
                    loadedFATInMemory[i + 3]
                ];
                int nextClusterNumber = BitConverter.ToInt16(clusterBytes);
                var entry = new Fat32Entry((uint)nextClusterNumber);
                children.ClusterChain.Add(entry);

                if (entry.IsBadCluster || entry.IsEndOfChain) break;
            }

            if (children is DirectoryNode dir)
            {
                BuildClusterChain(dir, loadedFATInMemory);
            }
        }
    }



    /// <summary>
    /// Находит нужный дескриптор по его имени и считывает номер первого кластера распределенного файлу
    /// </summary>
    /// <param name="firstSector"> Первый сектор, в котором находжится содержимое каталога, т.е. все дескрипторы</param>
    /// <param name="fileName"> Имя файла, который мы будем искать</param>
    /// <param name="maxSectorCount"> Максимальное число секторов, которое будет считываться 
    /// от первого сектора, распределенного файлу</param>
    /// <returns> Возвращает номер первого кластера, распределенного файлу </returns>
    /// <exception cref="NullReferenceException"></exception>
    private uint FoundFirstClusterInChain(RawDisk disk, FileSystemNode node)
    {
        if (node.Parent == null)
        {
            var origin = node as DirectoryNode;
            uint a = origin.FirstSector.Value;
            return origin.FirstSector.Value;
           
        }
        var dir = node.Parent as DirectoryNode;
        uint firstSector = (dir.FirstSector == null) ? dir.CalculateSector(firstDataSector) : dir.FirstSector.Value;

        for (uint sectorPointer = firstSector; sectorPointer < firstSector + dir.Childrens.Count; sectorPointer++)
        {
            byte[] sector = disk.ReadSectors(sectorPointer, 1);
            for (int i = 0; i < sector.Length; i += 32)
            {
                Range fileNameRange = new(i, i + 8);
                var bytesName = sector.Take(fileNameRange).ToArray();

                if (!IsValidAscii(bytesName, out string currentFileName) || string.IsNullOrEmpty(currentFileName)) continue;

                if (node is FileNode fileNode)
                {
                    Range fileExtensionRange = new(i + 8, i + 11);
                    var bytesExtension = sector.Take(fileExtensionRange).ToArray();
                    if (!IsValidAscii(bytesExtension, out string currentFileExtension)) continue;
                    if (!currentFileExtension.Contains(fileNode.FileExtension, StringComparison.CurrentCultureIgnoreCase)) continue;
                }

                if (currentFileName.Contains(node.Name, StringComparison.CurrentCultureIgnoreCase))
                {
                    uint firstClusterNumber = (uint)BitConverter.ToInt16(sector, i + 26);
                    node.FirstCluster = (uint)firstClusterNumber;
                    if (node is DirectoryNode dirNode) dirNode.CalculateSector(firstDataSector);
                    return firstClusterNumber;
                }

            }
        }
        throw new NullReferenceException("Не удалось найти необходимый дескриптор каталога");
    }



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

                if (currentFileName.Contains(fileName.ToUpper()))
                {
                    uint firstClusterNumber = (uint)BitConverter.ToInt16(sector, i + 26);
                    return firstClusterNumber;

                }
            }
        }
        throw new NullReferenceException("Не удалось найти необходимый дескриптор каталога");

    }

    /// <summary>
    /// Вычисляет номер первого сектора, распределеного каталогу
    /// </summary>
    /// <param name="firstDataSector"></param>
    /// <param name="firstClusterNumber"></param>
    /// <returns> Возвращает номер первого сектора, распределенного каталогу</returns>
    private uint GetFirstCatalogueSector(uint firstDataSector, uint firstClusterNumber)
    {
        return firstDataSector + (firstClusterNumber - 2) * 8;
    }

    private uint GetFirstDataSector(RawDisk disk, out uint fatSector, out uint sectorsPerFAT)
    {
        var bootSector = disk.ReadSectors(0, 1);
        int reservedSectors = BitConverter.ToInt16([bootSector[14], bootSector[15]], 0);
        fatSector = (uint)reservedSectors;
        sectorsPerFAT = (uint)BitConverter.ToInt16(bootSector.Skip(36).Take(4).ToArray(), 0);
        uint firstDataSector = (uint)reservedSectors + sectorsPerFAT * 2;
        return firstDataSector;
    }

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

    private static List<string> SplitPath(string path)
    {
        var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        string current = "";

        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            result.Add(current);
        }

        return result;
    }


}