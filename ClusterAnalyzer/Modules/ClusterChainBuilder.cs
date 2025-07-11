using ClusterAnalyzer.SystemTree;

namespace ClusterAnalyzer.Modules;

public class ClusterChainBuilder : IClusterChainBuilder
{
    public void BuildChains(DirectoryNode node, byte[] fatTable, uint firstDataSector)
    {
        foreach (var child in node.Children)
        {
            if (!child.FirstCluster.HasValue)
                continue;

            var chain = new List<Fat32Entry>();
            uint currentCluster = child.FirstCluster.Value;

            while (true)
            {
                uint offset = currentCluster * 4;
                if (offset + 4 > fatTable.Length)
                    break;

                var entry = new Fat32Entry 
                { 
                    Value = BitConverter.ToUInt32(fatTable, (int)offset) 
                };
                chain.Add(entry);

                if (entry.IsEndOfChain || entry.IsBadCluster)
                    break;

                currentCluster = entry.Value & 0x0FFFFFFF;
            }

            child.ClusterChain = chain;

            if (child is DirectoryNode dir)
                BuildChains(dir, fatTable, firstDataSector);
        }
    }
}
