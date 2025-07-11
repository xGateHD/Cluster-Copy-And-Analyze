namespace ClusterAnalyzer.SystemTree;

static class FileSystemTreeBuilder
{
    public static DirectoryNode BuildTree(string targetPath) => BuildTree(targetPath, null);

    public static DirectoryNode BuildTree(string targetPath, DirectoryNode? parent)
    {
        if (!Directory.Exists(targetPath))
            throw new DirectoryNotFoundException($"Directory {targetPath} not found.");

        var rootDirectory = new DirectoryNode(targetPath, parent);

        try
        {
            foreach (var dir in Directory.GetDirectories(targetPath))
            {
                rootDirectory.Children.Add(BuildTree(dir, rootDirectory));
            }

            // Process files
            foreach (var file in Directory.GetFiles(targetPath))
            {
                FileNode fileNode = new(file, rootDirectory);
                rootDirectory.Children.Add(fileNode);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"Ошибка доступа к файлу: {targetPath}", ex);
        }

        // Process directories first
        return rootDirectory;
    }
}
