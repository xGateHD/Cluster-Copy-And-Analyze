using System.Data;
using ClusterAnalyzer.Modules;
using RawDiskLib;

namespace ClusterAnalyzer;

public partial class ClusterAnalyzeService(
    IVolumeScanner volumeScanner,
    IClusterTreeBuilder treeBuilder,
    IFatReader fatReader,
    IClusterChainBuilder chainBuilder)
{
    public ClusterAnalyzeService() : this(
        new VolumeScanner(),
        new ClusterTreeBuilder(),
        new FatReader(),
        new ClusterChainBuilder())
    { }

    public async Task<DataTable> AnalyzeFAT32Async(
        string fullPath,
        IProgress<AnalysisPhase>? progress = null,
        CancellationToken token = default)
    {
        progress?.Report(AnalysisPhase.ПроверкаТомовДиска);

        char drive = fullPath[0];
        if (!volumeScanner.IsVolumeAvailable(drive))
            throw new ArgumentException("Том недоступен");

        progress?.Report(AnalysisPhase.СчитываниеДанных);

        using RawDisk disk = volumeScanner.OpenRawDisk(drive);
        byte[] bootSector = disk.ReadSectors(0, 1);
        var (firstDataSector, fatSector, sectorsPerFat) = fatReader.ParseBootSector(bootSector);

        progress?.Report(AnalysisPhase.ПостроениеДревовиднойСтруктурыКаталогов);

        var tree = await treeBuilder.BuildTreeAsync(fullPath, token);

        progress?.Report(AnalysisPhase.ЧтениеFAT);
        byte[] fatTable = await fatReader.ReadFatTableAsync(disk, fatSector, sectorsPerFat, token);

        progress?.Report(AnalysisPhase.ПостроениеЦепочкиКластеров);
        chainBuilder.BuildChains(tree, fatTable, firstDataSector);

        progress?.Report(AnalysisPhase.ВыводЦепочекНаЭкран);
        return tree.ToDataTable();
    }
}
