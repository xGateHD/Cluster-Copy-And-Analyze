using RawDiskLib;

namespace ClusterAnalyzer;

public interface IFatReader
{
    Task<byte[]> ReadFatTableAsync(RawDisk disk, uint fatSector, uint sectorsPerFAT, CancellationToken token);
    (uint firstDataSector, uint fatSector, uint sectorsPerFAT) ParseBootSector(byte[] bootSector);
}
