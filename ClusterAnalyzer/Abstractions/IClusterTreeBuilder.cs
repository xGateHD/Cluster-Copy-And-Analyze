using ClusterAnalyzer.SystemTree;

namespace ClusterAnalyzer;

public interface IClusterTreeBuilder
{
    Task<DirectoryNode> BuildTreeAsync(string fullPath, CancellationToken token);
}
