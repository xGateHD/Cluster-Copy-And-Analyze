using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

static class FileSystemTreeBuilder
{
    public static DirectoryNode BuildTree(string targetPath)
    {
        if (!Directory.Exists(targetPath))
            throw new DirectoryNotFoundException($"Directory {targetPath} not found.");

        var rootDirectory = new DirectoryNode(targetPath, parent: null);

        try
        {
            foreach (var dir in Directory.GetDirectories(targetPath))
            {

                rootDirectory.Childrens.Add(BuildTree(dir));
            }

            // Process files
            foreach (var file in Directory.GetFiles(targetPath))
            {
                FileNode fileNode = new(file, rootDirectory);
                rootDirectory.Childrens.Add(fileNode);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Ошибка доступа к файлу: \n{ex.Message}");
        }

        // Process directories first
        return rootDirectory;
    }
}
