using RawDiskLib;

namespace ClusterAnalyzer;

public interface IVolumeScanner
{
    bool IsVolumeAvailable(char driveLetter);
    RawDisk OpenRawDisk(char driveLetter);
}
