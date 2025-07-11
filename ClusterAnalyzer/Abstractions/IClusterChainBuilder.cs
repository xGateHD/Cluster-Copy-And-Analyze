using ClusterAnalyzer.SystemTree;

namespace ClusterAnalyzer;

public interface IClusterChainBuilder
{
    void BuildChains(DirectoryNode node, byte[] fatTable, uint firstDataSector);
}
