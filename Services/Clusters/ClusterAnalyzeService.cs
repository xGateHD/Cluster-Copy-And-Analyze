using ClustersCopyAndAnalyze.Services.Clusters.SystemTree;
using RawDiskLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ClustersCopyAndAnalyze.Services.Clusters;

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

public partial class ClusterAnalyzeService : IClusterAnalyzerService
{
    

    public async Task<List<ClusterData>> AnalyzeClusterAsync(string fullPath, CancellationToken cancellationToken, IProgress<ТипПрогрессаАнализатора> progress = null)
    {
        var allVolumes = RawDiskLib.Utils.GetAllAvailableVolumes().ToArray();

        if (!allVolumes.Contains(fullPath[0]))
        {
            throw new ArgumentException("Проблема с чтением доступных томов на дисках");
        }

        var cataloguePath = SplitPath(fullPath);
        cataloguePath.RemoveAt(0);

        using RawDisk disk = new(fullPath[0]);
        int firstDataSector = GetFirstDataSector(disk, out int hui);
        var originSector = firstDataSector;

        // Поиск оригинального сектора каталога
        foreach (var path in cataloguePath)
        {
            var filesCount = Directory.GetFiles(path).Length;
            var cataloguesCount = Directory.GetDirectories(path).Length;
            var firstCluster = FoundFirstClusterInChain(disk, originSector, FileUtils.GetFileName(path), filesCount + cataloguesCount);
            originSector = GetFirstCatalogueSector(firstDataSector, firstCluster); //sosi
        }

        var targetCatalogue = FileSystemTreeBuilder.BuildTree(fullPath); //sosi2, sosi2/newtext.pdf, NOText.txt
        targetCatalogue.FirstSector = originSector;

        // var firstClusterInChain = GetFirstsClustersInChain(firstDataSectorContains, files.ToArray());

        // byte[] catalogSector = disk.ReadSectors(firstDataSector + (firstClusterInChain[0] - 2) * 8, 1);
        // var firstFileClusterInChain = GetFirstsClustersInChain(catalogSector, files.ToArray());



        int a = 0;
        return null;
    }

    public static DataTable FormatToDT(List<ClusterData> list)
    {
        try
        {
            DataTable table = new();
            DataColumn currentClusterColumn = new("CurrentCluster", typeof(string));
            DataColumn nextClusterColumn = new("NextCluster", typeof(string));
            DataColumn hexNextClusterInChainColumn = new("HexNextClusterInChain", typeof(string));

            table.Columns.Add(currentClusterColumn);
            table.Columns.Add(nextClusterColumn);
            table.Columns.Add(hexNextClusterInChainColumn);
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
    /// Находит нужный дескриптор по его имени и считывает номер первого кластера распределенного файлу
    /// </summary>
    /// <param name="firstSector"> Первый сектор, в котором находжится содержимое каталога, т.е. все дескрипторы</param>
    /// <param name="fileName"> Имя файла, который мы будем искать</param>
    /// <param name="maxSectorCount"> Максимальное число секторов, которое будет считываться 
    /// от первого сектора, распределенного файлу</param>
    /// <returns> Возвращает номер первого кластера, распределенного файлу </returns>
    /// <exception cref="NullReferenceException"></exception>
    private int FoundFirstClusterInChain(RawDisk disk, int firstSector, string fileName, int maxSectorCount)
    {
        for (int sectorPointer = firstSector; sectorPointer < firstSector + maxSectorCount; sectorPointer++)
        {

            byte[] sector = disk.ReadSectors(sectorPointer, 1);
            for (int i = 0; i < sector.Length; i += 32)
            {
                Range fileNameRange = new(i, i + 8);
                var bytesName = sector.Take(fileNameRange).ToArray();

                if (!IsValidAscii(bytesName, out string currentFileName) || string.IsNullOrEmpty(currentFileName)) continue;

                if (currentFileName.Contains(fileName.ToUpper()))
                {
                    int firstClusterNumber = BitConverter.ToInt16(sector, i + 26);
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
    private int GetFirstCatalogueSector(int firstDataSector, int firstClusterNumber)
    {
        return firstDataSector + (firstClusterNumber - 2) * 8;
    }

    private int GetFirstDataSector(RawDisk disk, out int fatSector)
    {
        var bootSector = disk.ReadSectors(0, 1);
        int reservedSectors = BitConverter.ToInt16([bootSector[14], bootSector[15]], 0);
        fatSector = reservedSectors + 1;
        int sectorsPerFAT = BitConverter.ToInt16(bootSector.Skip(36).Take(4).ToArray(), 0);
        int firstDataSector = reservedSectors + sectorsPerFAT * 2;
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