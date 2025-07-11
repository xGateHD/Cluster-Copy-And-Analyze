namespace ClusterAnalyzer.SystemTree;

internal record DirectoryNode : FileSystemNode
{
    public uint? FirstSector { get; private set; } = null;
    public List<FileSystemNode> Children { get; init; } = [];

    public DirectoryNode(string fullPath, FileSystemNode? parent) : base(fullPath, parent) { }

    public DirectoryNode(string fullPath, List<FileSystemNode> childrens, FileSystemNode parent) : base(fullPath, parent)
    {
        Children = childrens;
    }

    /// <summary>
    /// Вычисляет номер первого сектора, распределеного каталогу
    /// </summary>
    /// <param name="firstDataSector"></param>
    public uint CalculateSector(uint firstDataSector)
    {
        FirstSector = firstDataSector + (FirstCluster - 2) * 8;
        return FirstSector ?? throw new InvalidOperationException("FirstSector is null."); // Added null check to handle CS8629
    }
}
