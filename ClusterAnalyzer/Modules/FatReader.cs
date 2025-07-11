using RawDiskLib;

namespace ClusterAnalyzer.Modules;

public class FatReader : IFatReader
{
    public async Task<byte[]> ReadFatTableAsync(RawDisk disk, uint fatSector, uint sectorsPerFAT, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var result = disk.ReadSectors(fatSector, (int)sectorsPerFAT);
            token.ThrowIfCancellationRequested();
            return result;
        }, token);
    }

    public (uint firstDataSector, uint fatSector, uint sectorsPerFAT) ParseBootSector(byte[] bootSector)
    {
        int reservedSectors = BitConverter.ToInt16(bootSector, 14);
        uint fatSector = (uint)reservedSectors;
        uint sectorsPerFAT = BitConverter.ToUInt32(bootSector, 36);
        uint firstDataSector = fatSector + sectorsPerFAT * 2;
        return (firstDataSector, fatSector, sectorsPerFAT);
    }
}
