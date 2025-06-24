using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClustersCopyAndAnalyze.Services.Clusters.SystemTree;

static class FileUtils
{
    private static readonly Regex filePathRegex = new(@"(?<=\\)[^\\]+?(?=\.[^\\.]+$|$)");
    public static string GetFileName(string fullPath)
    {
        return filePathRegex.Match(fullPath).Value;
    }
}
