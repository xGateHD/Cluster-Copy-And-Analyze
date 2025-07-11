﻿namespace ClusterAnalyzer.SystemTree;

public record FileNode : FileSystemNode
{
    public string FileExtension { get; }

    public FileNode(string fullPath, FileSystemNode parent) : base(fullPath, parent)
    {
        FileExtension = Path.GetExtension(fullPath).TrimStart('.');
    }
}
