namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

internal record DirectoryNode : FileSystemNode
{
    public uint? FirstSector { get; set; } = null;
    public List<FileSystemNode> Childrens { get; } = [];

    public DirectoryNode(string fullPath, FileSystemNode parent) : base(fullPath, parent) { }

    public DirectoryNode(string fullPath, List<FileSystemNode> childrens, FileSystemNode parent) : base(fullPath, parent)
    {
        Childrens = childrens;
    }

    /// <summary>
    /// Вычисляет номер первого сектора, распределеного каталогу
    /// </summary>
    /// <param name="firstDataSector"></param>
    public uint CalculateSector(uint firstDataSector)
    {
        FirstSector = firstDataSector + (FirstCluster - 2) * 8;
        return FirstSector.Value;
    }
}
