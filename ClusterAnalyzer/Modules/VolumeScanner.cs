using RawDiskLib;

namespace ClusterAnalyzer.Modules;

public class VolumeScanner : IVolumeScanner
{
    public bool IsVolumeAvailable(char driveLetter)
    {
        var volumes = RawDiskLib.Utils.GetAllAvailableVolumes();
        return volumes.Contains(driveLetter);
    }

    public RawDisk OpenRawDisk(char driveLetter) => new(driveLetter);
}
