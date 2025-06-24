namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

internal abstract record FileSystemNode
{
    public string FullPath { get; init; }
    public string Name { get; init; }
    public FileSystemNode? Parent { get; init; }
    public uint? FirstCluster { get; set; } = null;
    public List<Fat32Entry> ClusterChain { get; set; }

    public FileSystemNode(string fullPath, FileSystemNode parent)
    {
        FullPath = fullPath;
        Name = FileUtils.GetFileName(fullPath);
        Parent = parent;
    }


}