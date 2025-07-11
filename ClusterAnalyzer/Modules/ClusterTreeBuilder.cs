using ClusterAnalyzer.SystemTree;

namespace ClusterAnalyzer.Modules;

public class ClusterTreeBuilder : IClusterTreeBuilder
{
    public async Task<DirectoryNode> BuildTreeAsync(string fullPath, CancellationToken token)
    {
        return await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var root = FileSystemTreeBuilder.BuildTree(fullPath);
            token.ThrowIfCancellationRequested();
            return root;
        }, token);
    }
}
