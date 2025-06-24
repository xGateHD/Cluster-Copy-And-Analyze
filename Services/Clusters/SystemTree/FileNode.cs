using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree
{
    internal record FileNode : FileSystemNode
    {
        public string FileExtension { get; }

        public FileNode(string fullPath, FileSystemNode parent) : base(fullPath, parent)
        {
            FileExtension = Path.GetExtension(fullPath).TrimStart('.');

        }
    }
}
