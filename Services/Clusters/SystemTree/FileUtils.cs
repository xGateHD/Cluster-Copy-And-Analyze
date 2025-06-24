using System.Text.RegularExpressions;

namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

static class FileUtils
{
    private static readonly Regex filePathRegex = new(@"(?<=\\)[^\\]+?(?=\.[^\\.]+$|$)");
    public static string GetFileName(string fullPath)
    {
        return filePathRegex.Match(fullPath).Value;
    }
}
