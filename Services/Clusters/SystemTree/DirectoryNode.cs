using System;
using System.Collections.Generic;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

internal record DirectoryNode : FileSystemNode
{
    public int? FirstSector { get; set; } = null;
    public List<FileSystemNode> Childrens { get; } = [];

    public DirectoryNode(string fullPath, FileSystemNode parent) : base(fullPath, parent) { }

    public DirectoryNode(string fullPath, List<FileSystemNode> childrens, FileSystemNode parent) : base(fullPath, parent)
    {
        Childrens = childrens;
    }
}
