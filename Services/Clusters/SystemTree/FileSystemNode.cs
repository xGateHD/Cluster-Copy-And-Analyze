using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

internal abstract record FileSystemNode
{
    public string FullPath { get; init; }
    public string Name { get; init; }
    public FileSystemNode? Parent { get; init; }

    public FileSystemNode(string fullPath, FileSystemNode parent)
    {
        FullPath = fullPath;
        Name = FileUtils.GetFileName(fullPath);
        Parent = parent;
    }
}