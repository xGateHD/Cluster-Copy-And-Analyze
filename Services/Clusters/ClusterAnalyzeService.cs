using ClustersCopyAndAnalyze.Services.Clusters;
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
    private readonly Regex filePathRegex = new(@"(?<=\\)[^\\]+?(?=\.[^\\.]+$|$)");

    public Task<List<ClusterData>> AnalyzeClusterAsync(string fullPath, CancellationToken cancellationToken, IProgress<ТипПрогрессаАнализатора> progress = null)
    {
        var allVolumes = RawDiskLib.Utils.GetAllAvailableVolumes().ToArray();

        if (!allVolumes.Contains(fullPath[0]))
        {
            throw new ArgumentException("Проблема с чтением доступных томов на дисках");
        }

        // List<string> catalogues = GetCataloguesNames(fullPath);
        // List<string> files = GetNamesOfFiles(fullPath);
        using RawDisk disk = new(fullPath[0]);
        int firstDataSector = GetFirstDataSector(disk, out int hui);
        
        byte[] firstDataSectorContains = disk.ReadSectors(firstDataSector, 3);
        var firstClusterInChain = GetFirstsClustersInChain(firstDataSectorContains, files.ToArray());

        byte[] catalogSector = disk.ReadSectors(firstDataSector + (firstClusterInChain[0] - 2) * 8, 1);
        var firstFileClusterInChain = GetFirstsClustersInChain(catalogSector, files.ToArray());

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

    private List<int> GetFirstsClustersInChain(byte[] sector, string[] filesNames)
    {
        var result = new List<int>();
        for (int i = 0; i < sector.Length; i += 32)
        {
            Range fileNameRange = new(i, i + 8);
            var bytesName = sector.Take(fileNameRange).ToArray();
            var utf8 = Encoding.UTF8.GetString(bytesName);
            if (!IsValidAscii(bytesName, out string currentFileName) || string.IsNullOrEmpty(currentFileName)) continue;

            if (filesNames.Any(file => currentFileName.Contains(file)))
            {
                int firstClusterNumber = BitConverter.ToInt16(sector, i + 26);
                result.Add(firstClusterNumber);

            }
        }
        return result;
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

    /// <summary>
    ///
    /// </summary>
    /// <param name="fullPath">Путь к каталогу</param>
    /// <returns>Все имена файлов и каталогов включая родительский</returns>
    private List<string> GetCataloguesNames(string fullPath)
    {
        List<string> files = new() { filePathRegex.Match(fullPath).Value };

        files.AddRange(Directory.GetDirectories(fullPath, "*.*", SearchOption.AllDirectories)
            .Select(file => filePathRegex.Match(file).Value.ToUpper()));
        return files;
    }

    private List<string> GetNamesOfFiles(string fullPath)
    {
        return Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories)
            .Select(file => filePathRegex.Match(file).Value.ToUpper()).ToList();
    }
}